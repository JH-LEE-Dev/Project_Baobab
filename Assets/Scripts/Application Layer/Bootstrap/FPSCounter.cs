using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private float deltaTime = 0.0f;
    private GUIStyle guiStyle = new GUIStyle();

    void Awake()
    {
        // 빌드 시 프레임 제한 해제 (VSync 끄기 및 목표 프레임 무제한 설정)
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    void Update()
    {
        // unscaledDeltaTime을 사용해야 타임스케일 영향 없이 정확한 측정이 가능합니다.
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        guiStyle.alignment = TextAnchor.UpperCenter;
        guiStyle.fontSize = h * 2 / 50;
        guiStyle.normal.textColor = Color.white;

        float fps = 1.0f / deltaTime;
        string text = string.Format("{0:0.} FPS", fps);

        // 중앙 상단 배치를 위한 Rect (x, y, width, height)
        Rect rect = new Rect(0, 10, w, h * 2 / 50);
        
        // 가독성을 위한 검은색 외곽선 효과
        guiStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, guiStyle);
        
        guiStyle.normal.textColor = Color.green;
        GUI.Label(rect, text, guiStyle);
    }
}
