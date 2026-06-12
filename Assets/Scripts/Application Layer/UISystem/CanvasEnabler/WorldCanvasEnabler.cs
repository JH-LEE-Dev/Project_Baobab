using UnityEngine;

public class WorldCanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    public void Initialize()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = CameraFinder.Instance.PPMainCamera;
            canvas.sortingLayerName = "WorldUI";
        }

        CameraFinder.Instance.CameraFindEvent -= ResetCanvas;
        CameraFinder.Instance.CameraFindEvent += ResetCanvas;
    }

    private void ResetCanvas()
    {
        if (canvas != null)
        {
            canvas.worldCamera = CameraFinder.Instance.PPMainCamera;
            canvas.sortingLayerName = "WorldUI";
        }
    }
}
