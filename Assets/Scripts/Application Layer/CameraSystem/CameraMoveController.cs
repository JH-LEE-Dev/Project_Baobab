using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

public class CameraMoveController : MonoBehaviour
{
    // 싱글톤
    private static CameraMoveController instance;
    public static CameraMoveController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CameraMoveController>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CameraMoveController");
                    instance = go.AddComponent<CameraMoveController>();
                }
            }
            return instance;
        }
    }

    // 외부 의존성
    [SerializeField] private CinemachineCamera virtualCamera;

    // 내부 의존성
    private CinemachineBasicMultiChannelPerlin multiChannelPerlin;

    // 상태 변수
    private float shakeTimer;
    private float shakeTimerTotal;
    private float startingIntensity;

    // 줌 상태 변수
    // PixelPerfectCamera.refResolution을 바꾸는 방식은 이 프로젝트에서 효과가 없었다.
    // CinemachineBrain이 매 프레임 Camera.orthographicSize를 CinemachineCamera.Lens.OrthographicSize로
    // 덮어쓰기 때문에(실측 확인됨), 줌은 반드시 Lens.OrthographicSize 자체를 트윈해야 한다.
    // 단, CinemachinePixelPerfect 익스텐션이 그 값을 매 프레임 픽셀 퍼펙트 값으로 다시 보정해버려서
    // (Body 파이프라인 단계에서 PixelPerfectCamera.CorrectCinemachineOrthoSize 호출) 중간값이 전부
    // 스냅되어 "툭" 튀는 것처럼 보인다. 줌 트윈이 도는 동안은 이 익스텐션을 꺼서 보정을 막아야 한다.
    //
    // 트윈이 끝나자마자 다시 켜면, 꺼져있던 동안에도 PixelPerfectCamera 자체는 매 프레임 내부 상태
    // (zoom/orthoSize)를 계속 갱신하고 있어서 재활성화 시점의 보정 기준이 어긋나 눈에 띄는 점프가
    // 생긴다(실측 확인됨). 이 프로젝트는 가상 카메라가 하나뿐이라 이 보정은 화면 해상도가 바뀔 때만
    // 다시 필요하므로, 줌이 끝나도 자동으로 켜지 않고 실제 해상도 변경 이벤트에서만 재활성화한다.
    //
    // 그래서 트윈의 시작·복귀 지점은 "익스텐션이 꺼진 채로도 픽셀 퍼펙트인 값"이어야 한다.
    // 프리팹에 적힌 원본값(5.625)을 그대로 쓰면 화면 세로가 360의 배수가 아닌 해상도에서
    // 배율이 정수가 아니게 되어(2560x1600이면 4.444배) 줌이 끝난 뒤에도 픽셀이 계속 깨진다.
    // 원본값은 authoredOrthographicSize에 한 번만 보관하고, 실제로 쓰는 값은 해상도에 맞춰
    // 계산한다(SettingsData.GetPixelPerfectOrthoSize). 16:9에서는 두 값이 같아 변화가 없다.
    private float baseOrthographicSize;
    private float authoredOrthographicSize;

    // 원본값을 어느 카메라에서 읽었는지 함께 들고 있는다. virtualCamera는 SetupCamera 말고도
    // ZoomCamera·ShakeCamera가 각자 FindAnyObjectByType으로 채우는 경로가 있어서, 플래그 하나로
    // 관리하면 그 경로들에서 교체를 놓친다. 카메라 자체를 기준으로 두면 전부 자동으로 걸린다.
    private CinemachineCamera authoredOrthographicSizeSource;
    private int appliedScreenHeight;
    private Tween zoomTween;
    private CinemachinePixelPerfect pixelPerfectExtension;

    // 퍼블릭 제어 메서드
    public void SetupCamera()
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        InitializePerlin();
    }

    public void SetupCamera(CinemachineCamera _camera)
    {
        // 카메라 교체는 Update가 authoredOrthographicSizeSource 비교로 감지해 다음 프레임에
        // 원본값을 다시 읽는다. 여기서 따로 초기화할 필요가 없다.
        virtualCamera = _camera;
        InitializePerlin();
    }

    public void ShakeCamera(float _intensity, float _time)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (multiChannelPerlin == null && virtualCamera != null)
        {
            multiChannelPerlin = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        }

        if (multiChannelPerlin != null)
        {
            // 호출부는 항상 기준 강도(옵션 100% 기준)를 넘기고, 옵션에서 설정한 비율만큼
            // 여기서 스케일링한다. 기본값이 SLIDER_MAX라 옵션을 건드리지 않은 유저에게는
            // 기존과 동일한 강도가 그대로 적용된다.
            float _scale = SettingsManager.Instance.Current.cameraShake / SettingsData.SLIDER_MAX;
            float _scaledIntensity = _intensity * _scale;

            multiChannelPerlin.AmplitudeGain = _scaledIntensity;
            startingIntensity = _scaledIntensity;
            shakeTimerTotal = _time;
            shakeTimer = _time;
        }
    }

    /// <summary>
    /// CinemachineCamera의 Lens.OrthographicSize를 일시적으로 줄였다가(줌 인) 다시 원래대로
    /// 부드럽게 복구하는 펀치 연출. _zoomMultiplier는 "몇 배 확대"인지를 뜻한다(1.15 = 1.15배 줌 인).
    /// </summary>
    public void ZoomCamera(float _zoomMultiplier, float _inTime, float _holdTime, float _outTime)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null || _zoomMultiplier <= 0f) return;

        if (pixelPerfectExtension == null)
        {
            pixelPerfectExtension = virtualCamera.GetComponent<CinemachinePixelPerfect>();
        }

        if (zoomTween != null && zoomTween.IsActive())
        {
            zoomTween.Kill(true);
        }
        else
        {
            // 트윈이 도는 중에는 Lens가 중간값이라 원본을 읽을 수 없으므로, 트윈이 없을 때만 갱신한다.
            RefreshBaseOrthographicSize();
        }

        if (pixelPerfectExtension != null)
        {
            pixelPerfectExtension.enabled = false;
        }

        float targetSize = baseOrthographicSize / _zoomMultiplier;

        Sequence seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => virtualCamera.Lens.OrthographicSize, SetOrthographicSize, targetSize, _inTime).SetEase(Ease.InOutSine));
        if (_holdTime > 0f)
        {
            seq.AppendInterval(_holdTime);
        }
        seq.Append(DOTween.To(() => virtualCamera.Lens.OrthographicSize, SetOrthographicSize, baseOrthographicSize, _outTime).SetEase(Ease.InOutSine));
        zoomTween = seq;
    }

    // 헬퍼 메서드

    /// <summary>
    /// 현재 화면 해상도에서 픽셀 퍼펙트가 성립하는 직교 크기를 구해 baseOrthographicSize에 넣고,
    /// 줌 트윈이 돌고 있지 않으면 카메라에도 즉시 반영합니다.
    ///
    /// 익스텐션이 켜져 있을 때 보정해 주던 값과 같은 값이라, 반영해도 화면은 그대로입니다.
    /// 대신 익스텐션이 꺼진 뒤(줌 연출 이후)에도 그 값이 유지되어 배율이 정수로 남습니다.
    /// </summary>
    private void RefreshBaseOrthographicSize()
    {
        if (virtualCamera == null) return;

        // 원본값은 카메라마다 한 번만 읽는다. 아래에서 Lens를 덮어쓰기 때문에 같은 카메라에서
        // 다시 읽으면 보정된 값을 원본으로 착각하게 된다.
        if (authoredOrthographicSizeSource != virtualCamera)
        {
            authoredOrthographicSize = virtualCamera.Lens.OrthographicSize;
            authoredOrthographicSizeSource = virtualCamera;
        }

        appliedScreenHeight = Screen.height;
        baseOrthographicSize = SettingsData.GetPixelPerfectOrthoSize(appliedScreenHeight, authoredOrthographicSize);

        // 트윈 중이면 연출이 끊기므로 건드리지 않는다. 트윈은 어차피 baseOrthographicSize로
        // 복귀하도록 만들어져 있어서, 값만 갱신해두면 끝날 때 알아서 맞는 지점에 도착한다.
        if (zoomTween != null && zoomTween.IsActive()) return;

        SetOrthographicSize(baseOrthographicSize);
    }

    private void SetOrthographicSize(float _size)
    {
        LensSettings lens = virtualCamera.Lens;
        lens.OrthographicSize = _size;
        virtualCamera.Lens = lens;
    }

    private void InitializePerlin()
    {
        if (virtualCamera != null)
        {
            multiChannelPerlin = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        }

        if (multiChannelPerlin != null)
        {
            multiChannelPerlin.AmplitudeGain = 0f;
        }
    }

    // 유니티 이벤트 함수
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SettingsManager.Instance.OnScreenTargetResolvedEvent -= HandleScreenTargetResolved;
        SettingsManager.Instance.OnScreenTargetResolvedEvent += HandleScreenTargetResolved;
    }

    private void OnDisable()
    {
        if (SettingsManager.HasInstance)
        {
            SettingsManager.Instance.OnScreenTargetResolvedEvent -= HandleScreenTargetResolved;
        }
    }

    // 실제로 화면 해상도가 바뀌는 드문 순간에만 픽셀 퍼펙트 보정을 재활성화한다.
    // 줌 연출 직후 자동으로 켜지 않는 이유는 클래스 상단 주석 참고.
    private void HandleScreenTargetResolved(int _width, int _height)
    {
        if (pixelPerfectExtension != null)
        {
            pixelPerfectExtension.enabled = true;
        }
    }

    private void OnDestroy()
    {
        if (zoomTween != null && zoomTween.IsActive())
        {
            zoomTween.Kill();
        }

        if (pixelPerfectExtension != null)
        {
            pixelPerfectExtension.enabled = true;
        }
    }

    private void Update()
    {
        // 익스텐션의 보정은 Lens에 들어있는 값을 기준으로 상대적으로 계산되므로, Lens에 남아 있는
        // 값이 지금 해상도의 것이 아니면 엉뚱한 시야가 나온다(줌 이후 해상도를 바꾼 경우).
        // OnScreenTargetResolvedEvent 시점에는 Screen.SetResolution이 아직 반영 전이라
        // Screen 값이 옛날 값이므로, 이벤트가 아니라 실제 화면 크기를 직접 감시한다.
        // 값이 그대로면 비교 두 번으로 끝난다. 카메라가 교체된 경우도 같이 잡는다.
        if (Screen.height != appliedScreenHeight || authoredOrthographicSizeSource != virtualCamera)
        {
            RefreshBaseOrthographicSize();
        }

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
            {
                if (multiChannelPerlin != null)
                {
                    multiChannelPerlin.AmplitudeGain = 0f;
                }
            }
            else
            {
                if (multiChannelPerlin != null)
                {
                    multiChannelPerlin.AmplitudeGain = Mathf.Lerp(startingIntensity, 0f, 1f - (shakeTimer / shakeTimerTotal));
                }
            }
        }
    }
}
