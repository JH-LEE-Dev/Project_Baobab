using UnityEngine;

public class CanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    public bool bPPUI = false;

    public void Initialize()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            if (bPPUI == true)
            {
                canvas.worldCamera = CameraFinder.Instance.PPUiCamera;
            }

            canvas.sortingLayerName = "HUD";
        }

        CameraFinder.Instance.CameraFindEvent -= ResetCanvas;
        CameraFinder.Instance.CameraFindEvent += ResetCanvas;
    }

    private void ResetCanvas()
    {
        if (bPPUI == true)
        {
            canvas.worldCamera = CameraFinder.Instance.PPUiCamera;
        }

        canvas.sortingLayerName = "HUD";
    }
}
