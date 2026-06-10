using UnityEngine;

public class WorldCanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    public void Initialize()
    {
        if(canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = CameraFinder.Instance.PPMainCamera;
            canvas.sortingLayerName = "WorldUI";
        }
    }
}
