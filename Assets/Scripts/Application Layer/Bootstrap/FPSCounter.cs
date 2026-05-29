using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private float deltaTime = 0.0f;
    private GUIStyle guiStyle = new GUIStyle();

    public bool blimitFrame = false;

    void Awake()
    {
        if (blimitFrame == true)
        {
            double refreshRate = 60.0;
#if UNITY_2022_2_OR_NEWER
            var ratio = Screen.currentResolution.refreshRateRatio;
            if (ratio.denominator > 0)
            {
                refreshRate = (double)ratio.numerator / ratio.denominator;
            }
#else
            refreshRate = Screen.currentResolution.refreshRate;
#endif

            if (refreshRate >= 58.0 && refreshRate <= 62.0)
            {
                // 60Hz 모니터인 경우: VSync를 활성화하여 화면 찢어짐 및 지터 방지
                QualitySettings.vSyncCount = 1;
            }
            else
            {
                // 고주사율 모니터인 경우: VSync를 끄고 프레임만 60으로 제한
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1; //프레임 제한 끄기.
        }
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
