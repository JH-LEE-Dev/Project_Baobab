using UnityEngine;

public class TimeController : MonoBehaviour, ITimeDataProvider
{
    //내부 의존성
    private const float minutesInDay = 1440f;
    private const float timeMultiplier = 5f; // 1초당 5분
    private float currentMinutes;
    private bool isDay;
    private bool bInitialized = false;

    //공개 인터페이스 구현
    public float currentTimePercent => currentMinutes / minutesInDay;


    //퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        // 아침 6시 시작 (6 * 60 = 360분)
        currentMinutes = 360f;
        UpdateDayNightStatus();
        bInitialized = true;
    }

    //내부 로직
    private void UpdateDayNightStatus()
    {
        // 06:00 ~ 18:00 (360분 ~ 1080분) 사이를 '낮'으로 정의
        isDay = (currentMinutes >= 360f && currentMinutes < 1080f);
    }

    private void Update()
    {
        if (bInitialized == false)
        {
            return;
        }

        // 시간 흐름 처리 (1초 = 5분)
        currentMinutes += Time.deltaTime * timeMultiplier;

        // 24시간(1440분)이 지나면 초기화
        if (currentMinutes >= minutesInDay)
        {
            currentMinutes -= minutesInDay;
        }

        UpdateDayNightStatus();
    }

    private void OnGUI()
    {
        if (bInitialized == false)
        {
            return;
        }

        // 현재 시간 계산 (시/분)
        int hours = (int)(currentMinutes / 60f);
        int minutes = (int)(currentMinutes % 60f);
        string stateString = isDay ? "낮 (Day)" : "밤 (Night)";

        // 화면 좌측 상단에 디버그 정보 표시
        GUIStyle style = new GUIStyle();
        style.fontSize = 25;
        style.normal.textColor = Color.white;

        string debugText = $"현재 시간: {hours:D2}:{minutes:D2}\n상태: {stateString}";
        GUI.Label(new Rect(10, 10, 300, 100), debugText, style);
    }
}
