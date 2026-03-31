using UnityEngine;

public class CanvasEnabler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    public void Initialize()
    {
        if (canvas != null)
        {
            // "UI_Camera"라는 이름을 가진 게임 오브젝트를 찾고, 그 안의 Camera 컴포넌트를 가져옵니다.
            GameObject foundUiCameraObject = GameObject.Find("UI Camera");

            if (foundUiCameraObject != null)
            {
                Camera targetSceneCamera = foundUiCameraObject.GetComponent<Camera>();

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = targetSceneCamera;
                canvas.sortingLayerName = "HUD";
            }
        }
    }
}