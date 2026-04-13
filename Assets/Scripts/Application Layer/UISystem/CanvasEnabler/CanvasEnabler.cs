using UnityEngine;

public class CanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    public void Initialize()
    {
        if(canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
        }
    }
}
