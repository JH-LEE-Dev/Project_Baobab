using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 웨이브 표시. 막대 묶음(하단 중앙)의 왼쪽 윗 모서리에 놓이는 배지다(기획서 v2.6 10-1).
/// 깃발 아이콘(Flag) + 작은 숫자(UI_RoguelikeIconNumber)로 "12/30"을 그린다. 30웨이브 이후(언리밋)는 깃발이 금색으로 바뀌고 무한대 아이콘(Infinity) + 언리밋 번호(Unlimit-3이면 "3")를 그린다.
/// 글자 라벨은 쓰지 않는다. 배지 프레임·아이콘은 프리팹에 있고 이 컴포넌트는 숫자와 색, 튐 연출만 맡는다.
///
/// [웨이브가 바뀔 때] 24Hz 틱으로 위로 { +2, +3, +1, 0 }px 튀어 오르고(스텝당 3틱), 처음 6틱은 깃발이 흰색과 원래 색으로 번갈아 번쩍인다.
///   정수 픽셀 이동이고, 시간은 unscaled라 ESC로 멈춰도 이어진다. 처음 표시될 때(HUD가 켜질 때)는 튀지 않는다.
/// </summary>
public class UI_RoguelikeWaveLabel : MonoBehaviour
{
    private const int TotalWaves = 30;
    private const float TicksPerSecond = 24f;
    private const int StepTicks = 3;
    // 숫자 시작 x(라벨 왼쪽 기준). 깃발만 있을 때와 깃발 + 무한대 아이콘이 있을 때.
    private const int NumberX = 13;
    private const int NumberXUnlimit = 23;
    private const int FlashTicks = 6;
    private static readonly int[] HopPattern = { 2, 3, 1, 0 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform labelRoot;
    [SerializeField] private UI_RoguelikeIconNumber iconNumber;
    [SerializeField, Tooltip("숫자 앵커. 언리밋이면 무한대 아이콘 뒤로 옮긴다.")]
    private RectTransform numberAnchor;
    [SerializeField] private Image flagImage;
    [SerializeField] private Image infinityImage;
    [SerializeField] private Color flagColor = Color.white;
    [SerializeField] private Color unlimitFlagColor = new Color(1f, 0.82f, 0.35f, 1f);

    //상태 변수
    private int wave;
    private bool bUnlimit;
    private int unlimitIndex;
    private bool bHasWave;
    private float clock;
    private float hopStartClock = -1f;
    private Vector2 basePosition;
    private bool bBasePositionCaptured;
    // 퇴장 연출(숫자가 사라지는 동안)이 끝난 뒤 오브젝트를 끈다.
    private bool bHiding;
    private float hideClock;

    /// <summary>웨이브 표기. 언리밋이면 _unlimitIndex를 무한대 아이콘 옆에 그린다. 같은 웨이브가 다시 오면 아무것도 하지 않는다.</summary>
    public void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex)
    {
        bool bChanged = false == bHasWave || wave != _waveNumber || bUnlimit != _bUnlimit || unlimitIndex != _unlimitIndex;
        if (false == bChanged) return;

        bool bFirst = false == bHasWave;
        wave = _waveNumber;
        bUnlimit = _bUnlimit;
        unlimitIndex = _unlimitIndex;
        bHasWave = true;

        Refresh();
        if (false == bFirst) PlayChanged();
    }

    /// <summary>켜면 숫자가 등장 연출로 다시 나타난다. 끄면 숫자가 퇴장 연출을 재생한 뒤 오브젝트가 꺼진다.</summary>
    public void SetVisible(bool _bVisible)
    {
        if (true == _bVisible)
        {
            bHiding = false;
            gameObject.SetActive(true);
            if (null != iconNumber) iconNumber.Restore();
            return;
        }

        if (false == gameObject.activeInHierarchy || null == iconNumber)
        {
            gameObject.SetActive(false);
            return;
        }

        iconNumber.PlayExit();
        bHiding = true;
        hideClock = 0f;
    }

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;

        if (hopStartClock < 0f) return;

        int tick = (int)((clock - hopStartClock) * TicksPerSecond);
        int step = tick / StepTicks;

        if (HopPattern.Length <= step)
        {
            hopStartClock = -1f;
            ApplyHop(0, false);
            return;
        }

        ApplyHop(HopPattern[step], tick < FlashTicks && 0 == tick % 2);
    }

    // // 내부

    private void Refresh()
    {
        if (null != infinityImage) infinityImage.enabled = bUnlimit;

        if (null != numberAnchor) numberAnchor.anchoredPosition = new Vector2(true == bUnlimit ? NumberXUnlimit : NumberX, numberAnchor.anchoredPosition.y);

        if (null != iconNumber)
        {
            if (true == bUnlimit) iconNumber.SetSingle(unlimitIndex);
            else iconNumber.SetPair(wave, TotalWaves);
        }

        if (null != flagImage) flagImage.color = true == bUnlimit ? unlimitFlagColor : flagColor;
    }

    private void PlayChanged()
    {
        hopStartClock = clock;
        Step(0f);
    }

    private void ApplyHop(int _offset, bool _bFlash)
    {
        if (null != labelRoot)
        {
            if (false == bBasePositionCaptured)
            {
                basePosition = labelRoot.anchoredPosition;
                bBasePositionCaptured = true;
            }

            labelRoot.anchoredPosition = new Vector2(basePosition.x, basePosition.y + _offset);
        }

        if (null != flagImage) flagImage.color = true == _bFlash ? Color.white : (true == bUnlimit ? unlimitFlagColor : flagColor);
    }

    private void Update()
    {
        if (true == bHiding)
        {
            hideClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (UI_RoguelikeIconNumber.ExitSeconds + 0.02f <= hideClock)
            {
                bHiding = false;
                gameObject.SetActive(false);
            }
        }

        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        bHiding = false;
        hopStartClock = -1f;
        ApplyHop(0, false);
    }
}
