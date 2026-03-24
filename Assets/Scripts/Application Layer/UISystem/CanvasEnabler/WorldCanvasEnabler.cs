using UnityEngine;

public class WorldCanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    public void Initialize()
    {
        if(canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "HUD";
        }
    }
}
