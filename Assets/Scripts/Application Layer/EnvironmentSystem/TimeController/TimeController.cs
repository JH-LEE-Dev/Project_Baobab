// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  시간 흐름(낮/밤) 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: Update()가 전체 주석 처리되어 있어 시간이 흐르지 않습니다.
//
// 참고: 이 기능을 전제로 적혀 있던 "나무 그림자 각도 래치" 검토 항목도
//       이 결정으로 함께 빠졌습니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: EnvironmentSystem이 아직 이 타입을 참조합니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
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

