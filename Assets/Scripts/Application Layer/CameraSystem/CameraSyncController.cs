using UnityEngine;
using Unity.Cinemachine;

public class CameraSyncController : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] 
    private Camera sourceCamera;
    [SerializeField] 
    private Camera targetCamera;

    // 내부 의존성
    private Transform sourceTransform;
    private Transform targetTransform;

    public void Initialize(Camera _sourceCamera, Camera _targetCamera)
    {
        sourceCamera = _sourceCamera;
        targetCamera = _targetCamera;

        UpdateTransforms();
    }

    private void UpdateTransforms()
    {
        if (sourceCamera != null)
        {
            sourceTransform = sourceCamera.transform;
        }
        else
        {
            sourceTransform = null;
        }

        if (targetCamera != null)
        {
            targetTransform = targetCamera.transform;
        }
        else
        {
            targetTransform = null;
        }
    }

    private void SetupCamerasFromFinder()
    {
        if (CameraFinder.Instance != null)
        {
            sourceCamera = CameraFinder.Instance.PPMainCamera;
            targetCamera = CameraFinder.Instance.OverlayCamera;
        }

        UpdateTransforms();
    }

    private void Awake()
    {
        UpdateTransforms();
    }

    private void Start()
    {
        if (CameraFinder.Instance != null)
        {
            SetupCamerasFromFinder();
            CameraFinder.Instance.CameraFindEvent -= SetupCamerasFromFinder;
            CameraFinder.Instance.CameraFindEvent += SetupCamerasFromFinder;
        }
    }

    private void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        if (CameraFinder.Instance != null)
        {
            CameraFinder.Instance.CameraFindEvent -= SetupCamerasFromFinder;
        }
    }

    private void OnCameraUpdated(CinemachineBrain _brain)
    {
        if (sourceCamera == null || targetCamera == null)
        {
            return;
        }

        if (_brain.OutputCamera == sourceCamera)
        {
            if (sourceTransform != null && targetTransform != null)
            {
                // 동일 부모 계층 구조 하에서의 지터를 방지하기 위해 local로 동기화
                targetTransform.localPosition = sourceTransform.localPosition;
                targetTransform.localRotation = sourceTransform.localRotation;
            }

            targetCamera.orthographicSize = sourceCamera.orthographicSize;
            targetCamera.fieldOfView = sourceCamera.fieldOfView;
        }
    }
}
