using UnityEngine;

public class CanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    public bool bPPUI = false;
    //public bool bScreenSpace_NoPP = false;

    public void Initialize()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            if (bPPUI == true)
            {
                canvas.worldCamera = CameraFinder.Instance.PPUiCamera;
            }
            else
                canvas.worldCamera = Camera.main;

            canvas.sortingLayerName = "HUD";
        }

        CameraFinder.Instance.CameraFindEvent -= ResetCanvas;
        CameraFinder.Instance.CameraFindEvent += ResetCanvas;
    }

    private void ResetCanvas()
    {
        if (bPPUI == true)
        {
            if (canvas != null)
            {
                canvas.worldCamera = CameraFinder.Instance.PPUiCamera;
                canvas.sortingLayerName = "HUD";
            }
        }
        else
        {
            if (canvas != null)
            {
                canvas.worldCamera = Camera.main;
                canvas.sortingLayerName = "HUD";
            }
        }
    }
}
