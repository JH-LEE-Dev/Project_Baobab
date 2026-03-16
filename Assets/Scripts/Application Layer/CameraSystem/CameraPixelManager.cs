using UnityEngine;
using Unity.Cinemachine;

[ExecuteAlways]
public class CameraPixelManager : MonoBehaviour
{
    // 내부 의존성
    [Header("해상도 및 크기 설정")]
    public float referenceHeight = 1080f;   // 기준이 되는 해상도 세로 길이
    public float defaultOrthoSize = 5.625f; // 기준 해상도에서의 카메라 크기
    
    [SerializeField] private CinemachineCamera virtualCamera;

    public void Setup(CinemachineCamera _virtualCamera)
    {
        virtualCamera = _virtualCamera;
        UpdateCameraSize();
    }

    private void UpdateCameraSize()
    {
        // 에디터에서 참조가 끊긴 경우를 대비해 자동 검색
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            if (virtualCamera == null)
            {
                virtualCamera = Object.FindAnyObjectByType<CinemachineCamera>();
            }
        }

        if (virtualCamera == null) return;

        // 계산 공식: (현재 세로 해상도 / 기준 세로 해상도) * 기준 Ortho Size
        // 이 공식을 통해 해상도가 커지면 Size도 커져서 캐릭터의 물리적 크기를 일정하게 유지합니다.
        float orthoSize = (Screen.height / referenceHeight) * defaultOrthoSize;

        // 현재 값과 다를 때만 갱신 (에디터 성능 최적화)
        if (!Mathf.Approximately(virtualCamera.Lens.OrthographicSize, orthoSize))
        {
            LensSettings lens = virtualCamera.Lens;
            lens.OrthographicSize = orthoSize;
            virtualCamera.Lens = lens;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(virtualCamera);
            }
#endif
        }
    }

    private void Awake()
    {
        UpdateCameraSize();
    }

    private void Update()
    {
        UpdateCameraSize();
    }

    private void OnValidate()
    {
        UpdateCameraSize();
    }
}