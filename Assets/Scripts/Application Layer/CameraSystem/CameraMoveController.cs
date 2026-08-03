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
    private float baseOrthographicSize;
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
            multiChannelPerlin.AmplitudeGain = _intensity;
            startingIntensity = _intensity;
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
            baseOrthographicSize = virtualCamera.Lens.OrthographicSize;
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
