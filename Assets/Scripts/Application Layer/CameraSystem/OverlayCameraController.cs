using UnityEngine;

public class OverlayCameraController : MonoBehaviour
{
    //외부 의존성
    [SerializeField] private Camera mainCamera;

    //내부 의존성
    private Camera myOverlayCamera;

    public void Initialize(Camera _mainCamera)
    {
        mainCamera = _mainCamera;
    }

    private void Awake()
    {
        myOverlayCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            if (CameraFinder.Instance != null)
            {
                mainCamera = CameraFinder.Instance.PPMainCamera;
            }

            if (mainCamera == null)
            {
                return;
            }
        }

        if (myOverlayCamera == null)
        {
            return;
        }

        transform.position = mainCamera.transform.position;
        transform.rotation = mainCamera.transform.rotation;
        myOverlayCamera.orthographicSize = mainCamera.orthographicSize;
    }
}
