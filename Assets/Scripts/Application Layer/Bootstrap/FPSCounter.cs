using UnityEngine;

/// <summary>
/// 현재 프레임레이트를 계측해 화면에 표시합니다.
///
/// 프레임레이트/VSync를 실제로 "설정"하는 책임은 SettingsManager가 단독으로 가집니다.
/// 이 스크립트는 MainMenuScene에 배치되어 있어 Awake에서 값을 건드리면
/// 메인 메뉴로 돌아올 때마다 유저의 FPS 옵션이 덮어써집니다.
/// (모니터 주사율 감지 후 VSync를 켜는 로직도 SettingsManager로 이관되었습니다)
/// </summary>
public class FPSCounter : MonoBehaviour
{
    [SerializeField, Tooltip("화면에 FPS 수치를 표시할지 여부. 릴리즈 빌드에서는 꺼두세요.")]
    private bool showFPS = true;

    private float deltaTime = 0.0f;
    private GUIStyle guiStyle = new GUIStyle();

    void Update()
    {
        // unscaledDeltaTime을 사용해야 타임스케일 영향 없이 정확한 측정이 가능합니다.
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        if (false == showFPS) return;

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
