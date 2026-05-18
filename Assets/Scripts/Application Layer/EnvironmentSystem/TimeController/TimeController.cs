using UnityEngine;

public class TimeController : MonoBehaviour, ITimeDataProvider
{
    //내부 의존성
    private const float minutesInDay = 1440f;
    private const float timeMultiplier = 5;
    private float currentMinutes;
    public bool isDay { get; private set; }
    //private bool bInitialized = false;

    //공개 인터페이스 구현    
    public float currentTimePercent => currentMinutes / minutesInDay;


    //퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        // 아침 6시 시작 (6 * 60 = 360분)
        currentMinutes = 360f;
        UpdateDayNightStatus();
        //bInitialized = true;
    }

    //내부 로직
    private void UpdateDayNightStatus()
    {
        // 06:00 ~ 18:00 (360분 ~ 1080분) 사이를 '낮'으로 정의
        isDay = (currentMinutes >= 360f && currentMinutes < 1080f);
    }

    private void Update()
    {
        // if (bInitialized == false)
        // {
        //     return;
        // }

        // // 시간 흐름 처리 (1초 = 5분)
        // currentMinutes += Time.deltaTime * timeMultiplier;

        // // 24시간(1440분)이 지나면 초기화
        // if (currentMinutes >= minutesInDay)
        // {
        //     currentMinutes -= minutesInDay;
        // }

        // UpdateDayNightStatus();
    }
}

