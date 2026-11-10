using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 임계치 바(RoguelikeHUD_Polish_Plan.md). 필드 나무 수가 임계치 T를 넘었는지를 112 x 8 캡슐 막대 하나로 보여 준다.
/// 🌲 아이콘(Icon) 왼쪽, "나무 수/T" 숫자(UI_RoguelikeIconNumber, 4px 격자)는 막대 뒤(x 135)에 놓인다. 글자 라벨은 쓰지 않는다.
///
/// [막대] 안쪽 110 x 6을 CPU로 그린 RawImage 하나(RoguelikeBarPainter)가 홈 + 채움 + T 열을 맡고, 윤곽 · 홈 스프라이트(Background)는 프리팹에 있다.
///   채움 = 나무 수 / 스폰 상한을 정수 px로 스냅한다(값이 바뀌면 지수 감속 시정수 0.1초). T 위치부터 오른쪽 끝까지는 초과 구간 홈 색이고,
///   [0, T)는 호박색, T를 넘은 [T, 현재)는 진한 적색 팔레트다. T 위치에는 안쪽 열 전체가 흰색이고 막대 위에 5-3-1 역삼각 표식(Tick)이 선다.
///   웨이브가 시작될 때는 SnapNext를 불러 첫 값을 0에서 차오르지 않고 바로 반영한다.
/// [연출] 24Hz 틱, unscaled 시간, 알파 페이드 없음.
///   값 증가       채움 끝 열을 2틱 동안 밝은 줄 색으로.
///   T를 넘는 순간  T 표식(안쪽 열 + 위 표식)이 3틱 숨고 3틱 보이기를 2회.
///   임계치 피해    채움 전체가 2틱 동안 WHITE. (흔들림은 없다)
/// [숫자] T를 넘은 상태(유예 · 지속 피해)면 글자가 #FF9A80으로 물든다. 값이 바뀔 때만 다시 합성한다.
/// [유예] 유예 시간 표현은 이 바가 아니라 바로 아래 UI_RoguelikeGraceTimer가 맡는다. 스태미너 바 경고와 붉은 화면은 UIView_HUD.SetRoguelikePressureWarning이 맡는다.
/// </summary>
public class UI_RoguelikePressureStrip : MonoBehaviour
{
    private const int InnerWidth = 110;
    private const int InnerHeight = 6;
    private const float FillTau = 0.1f;
    private const int NoTick = -1000000;
    private const int FlashTicks = 2;
    private const int GainEndTicks = 2;
    private const int BlinkStepTicks = 3;
    private const int BlinkTotalTicks = 12;
    // 컨테이너 왼쪽 기준, 안쪽 첫 열에서 표식 폭(5)의 절반을 뺀 x.
    private const int TickOriginX = 11;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("안쪽 110x6 홈 + 채움을 그리는 RawImage.")]
    private RawImage fillSurface;
    [SerializeField, Tooltip("T 위치를 따라가는 위쪽 표식(5x3).")]
    private RectTransform tick;
    [SerializeField] private Image tickImage;
    [SerializeField] private UI_RoguelikeIconNumber iconNumber;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warnColor = new Color(1f, 0.604f, 0.502f, 1f);

    //상태 변수
    private RoguelikeBarPainter painter;
    private float displayFull;
    private int targetFull;
    private int thresholdPixels = InnerWidth;
    private bool bHasThreshold;
    private int paintedFull = -1;
    private int paintedThreshold = -1;
    private int paintedEffects = -1;
    private bool bSnapNext;
    private bool bShownWarn;
    private float clock;
    private int flashStartTick = NoTick;
    private int gainEndStartTick = NoTick;
    private int blinkStartTick = NoTick;
    // 퇴장 연출(숫자가 사라지는 동안)이 끝난 뒤 오브젝트를 끈다.
    private bool bHiding;
    private float hideClock;

    /// <summary>임계치 값. _spawnCap이 막대의 끝(스폰 상한)이다.</summary>
    public void SetValues(int _treeCount, int _threshold, int _spawnCap, EPressureState _state)
    {
        bool bWarn = EPressureState.Safe != _state;

        if (null != iconNumber)
        {
            iconNumber.SetPair(_treeCount, _threshold);

        }

        // T를 넘는 순간(안전 -> 유예)에 T 표식을 깜빡인다.
        if (bWarn != bShownWarn)
        {
            bShownWarn = bWarn;
            if (true == bWarn) blinkStartTick = CurrentTick();
            if (null != iconNumber) iconNumber.SetTint(true == bWarn ? warnColor : normalColor);
        }

        int cap = Mathf.Max(1, _spawnCap);
        int full = Mathf.RoundToInt(Mathf.Clamp01((float)_treeCount / cap) * InnerWidth);
        thresholdPixels = Mathf.RoundToInt(Mathf.Clamp01((float)_threshold / cap) * InnerWidth);
        bHasThreshold = true;

        if (true == bSnapNext)
        {
            bSnapNext = false;
            displayFull = full;
            gainEndStartTick = NoTick;
        }
        else if (full > targetFull)
        {
            gainEndStartTick = CurrentTick();
        }

        targetFull = full;
        Repaint();
    }

    /// <summary>다음 SetValues의 값으로 곧바로 선다(웨이브 시작). 빈 바에서 차오르는 모양을 피한다.</summary>
    public void SnapNext()
    {
        bSnapNext = true;
    }

    /// <summary>임계치 피해 1회: 채움 전체가 2틱 동안 하얗게 번쩍인다.</summary>
    public void PlayDamaged()
    {
        flashStartTick = CurrentTick();
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

        displayFull = Mathf.Abs(targetFull - displayFull) < 0.01f ? targetFull : displayFull + (targetFull - displayFull) * (1f - Mathf.Exp(-_dt / FillTau));

        Repaint();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * RoguelikeBarPainter.TicksPerSecond);
    }

    /// <summary>그릴 내용(채움 px, T 열, 연출 상태)이 바뀌었을 때만 다시 그린다.</summary>
    private void Repaint()
    {
        int tickNow = CurrentTick();
        int fullPixels = Mathf.Clamp(Mathf.RoundToInt(displayFull), 0, InnerWidth);

        bool bFlash = tickNow - flashStartTick < FlashTicks;
        bool bGainEnd = tickNow - gainEndStartTick < GainEndTicks;
        bool bMarkerShown = true == bHasThreshold;

        int blinkElapsed = tickNow - blinkStartTick;
        if (true == bHasThreshold && blinkElapsed < BlinkTotalTicks) bMarkerShown = 1 == (blinkElapsed / BlinkStepTicks) % 2;

        int effects = (true == bFlash ? 1 : 0) | (true == bGainEnd ? 2 : 0) | (true == bMarkerShown ? 4 : 0);
        if (fullPixels == paintedFull && thresholdPixels == paintedThreshold && effects == paintedEffects) return;

        paintedFull = fullPixels;
        paintedThreshold = thresholdPixels;
        paintedEffects = effects;

        if (null != tick) tick.anchoredPosition = new Vector2(TickOriginX + thresholdPixels, tick.anchoredPosition.y);
        if (null != tickImage) tickImage.enabled = bMarkerShown;

        if (null == fillSurface) return;
        if (null == painter) painter = new RoguelikeBarPainter(fillSurface, InnerWidth, InnerHeight, "UI_RoguelikePressureFill");

        painter.Clear();
        painter.Groove(thresholdPixels < InnerWidth ? thresholdPixels : -1);

        int baseEnd = Mathf.Min(fullPixels, thresholdPixels);
        RoguelikeBarPalette basePalette = true == bFlash ? RoguelikeHudColors.Flash : RoguelikeHudColors.Amber;
        RoguelikeBarPalette overPalette = true == bFlash ? RoguelikeHudColors.Flash : RoguelikeHudColors.Red;

        painter.Run(0, baseEnd, basePalette, true);
        if (thresholdPixels < fullPixels) painter.Run(thresholdPixels, fullPixels, overPalette, false);

        if (true == bGainEnd && false == bFlash && 0 < fullPixels)
        {
            painter.EndColumn(fullPixels, thresholdPixels < fullPixels ? overPalette.light : basePalette.light);
        }

        if (true == bMarkerShown && thresholdPixels < InnerWidth) painter.Column(thresholdPixels, RoguelikeHudColors.White);

        painter.Apply();
    }

    private void Awake()
    {
        // 첫 프레임에 텍스처 없는 RawImage(흰 네모)가 보이지 않도록 미리 한 번 그린다.
        Repaint();
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

    private void OnDestroy()
    {
        if (null != painter) painter.Dispose();
    }

    private void OnDisable()
    {
        bHiding = false;
        flashStartTick = NoTick;
        gainEndStartTick = NoTick;
        blinkStartTick = NoTick;
        paintedEffects = -1;
    }
}
