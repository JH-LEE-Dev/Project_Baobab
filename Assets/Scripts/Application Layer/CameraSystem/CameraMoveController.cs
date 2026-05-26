using UnityEngine;
using Unity.Cinemachine;

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

    // 헬퍼 메서드
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
