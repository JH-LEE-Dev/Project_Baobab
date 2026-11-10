using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 목표치 바. 현재 웨이브의 "현재 처치 수 / 목표 처치 수"(예: 12/40)를 채움과 숫자로 보여 준다(RoguelikeHUD_Polish_Plan.md).
/// 112 x 8 캡슐 막대 하나이고 채움은 분홍적색이다(임계치 바의 호박색과 구분). 왼쪽에 도끼 아이콘, 막대 뒤(x 135)에 "현재/목표" 숫자(UI_RoguelikeIconNumber, 4px 격자)가 놓인다. 글자 라벨은 쓰지 않는다.
///   - 안쪽 110 x 6을 CPU로 그린 RawImage 하나(RoguelikeBarPainter)가 홈 + 채움을 맡고, 윤곽 · 홈 스프라이트(Background)는 프리팹에 있다.
///   - 채움 폭은 정수 px로 스냅하고 값이 바뀌면 지수 감속(시정수 0.12초)으로 따라간다. 목표를 채우기 전에도 막대 전체를 쓴다(끝 여백 없음).
///   - 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다(24Hz 틱, 알파 페이드 없음).
/// [연출] 값 증가: 채움 끝 열을 2틱 동안 밝은 줄 색으로.
///        목표 달성: 숫자가 #FFD060으로 바뀌고 2px 흰 열이 채움을 왼쪽에서 오른쪽으로 7틱에 걸쳐 한 번 훑는다. 새 웨이브(목표 미달)가 되면 숫자는 원래 색으로 돌아온다.
/// [숫자] 목표를 넘으면 41/40처럼 그대로 보여 준다(최대 999). 값이 바뀔 때만 다시 합성하고, 등장 · 변경 · 퇴장 연출은 숫자 컴포넌트가 맡는다.
/// [경고] 임계점 경고는 스태미너 바와 붉은 화면(BloodScreen)이 맡는다(UIView_HUD.SetRoguelikePressureWarning).
/// [피해] 임계치 피해의 번쩍임은 임계치 바(UI_RoguelikePressureStrip)와 유예 타이머가 맡는다.
/// </summary>
public class UI_RoguelikeThresholdBar : MonoBehaviour
{
    private const int InnerWidth = 110;
    private const int InnerHeight = 6;
    private const float FillTau = 0.12f;
    private const float MaxStepSeconds = 1f / 20f;
    private const int NoTick = -1000000;
    private const int GainEndTicks = 2;
    private const int SweepTicks = 7;
    private const int SweepWidth = 2;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("안쪽 110x6 홈 + 채움을 그리는 RawImage.")]
    private RawImage fillSurface;
    [SerializeField] private UI_RoguelikeIconNumber iconNumber;

    //상태 변수
    private RoguelikeBarPainter painter;
    private int targetPixels;
    private float displayPixels;
    private int shownKilled = -1;
    private int shownTarget = -1;
    private bool bGoalReached;
    private float clock;
    private int gainEndStartTick = NoTick;
    private int sweepStartTick = NoTick;
    private int paintedPixels = -1;
    private int paintedEffects = -1;
    // 퇴장 연출(숫자가 사라지는 동안)이 끝난 뒤 오브젝트를 끈다.
    private bool bHiding;
    private float hideClock;

    /// <summary>처치 진행도. 채움 = _killed / _target, 숫자 = "_killed/_target".</summary>
    public void SetProgress(int _killed, int _target)
    {
        ApplyValueText(_killed, _target);

        float ratio = 0 < _target ? Mathf.Clamp01((float)_killed / _target) : 0f;
        int pixels = 1f <= ratio ? InnerWidth : Mathf.RoundToInt(ratio * InnerWidth);
        if (pixels > targetPixels) gainEndStartTick = CurrentTick();
        targetPixels = pixels;

        bool bDone = 0 < _target && _killed >= _target;
        if (bDone != bGoalReached)
        {
            bGoalReached = bDone;
            if (true == bDone) sweepStartTick = CurrentTick();
            if (null != iconNumber) iconNumber.SetTint(true == bDone ? RoguelikeHudColors.NumberDone : Color.white);
        }
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

    /// <summary>처음 모습으로 되돌린다(런이 시작·종료될 때).</summary>
    public void ResetBar()
    {
        targetPixels = 0;
        displayPixels = 0f;
        shownKilled = -1;
        shownTarget = -1;
        bGoalReached = false;
        gainEndStartTick = NoTick;
        sweepStartTick = NoTick;
        if (null != iconNumber)
        {
            iconNumber.HideImmediate();
            iconNumber.SetTint(Color.white);
        }

        Repaint();
    }

    /// <summary>
    /// _dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 실제 시간 없이 이 메서드로 시간대별 모습을 만들 수 있다.
    /// </summary>
    public void Step(float _dt)
    {
        clock += _dt;

        displayPixels = Mathf.Abs(targetPixels - displayPixels) < 0.01f ? targetPixels : displayPixels + (targetPixels - displayPixels) * (1f - Mathf.Exp(-_dt / FillTau));

        Repaint();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * RoguelikeBarPainter.TicksPerSecond);
    }

    /// <summary>"처치 수/목표 처치 수" 숫자를 갱신한다. 값이 그대로면 아무것도 하지 않는다.</summary>
    private void ApplyValueText(int _killed, int _target)
    {
        if (_killed == shownKilled && _target == shownTarget) return;

        shownKilled = _killed;
        shownTarget = _target;

        if (null != iconNumber) iconNumber.SetPair(_killed, _target);
    }

    /// <summary>채움 px와 연출 상태가 바뀌었을 때만 다시 그린다.</summary>
    private void Repaint()
    {
        int tickNow = CurrentTick();
        int pixels = Mathf.Clamp(Mathf.RoundToInt(displayPixels), 0, InnerWidth);
        bool bGainEnd = tickNow - gainEndStartTick < GainEndTicks;
        int sweepElapsed = tickNow - sweepStartTick;
        int sweepState = sweepElapsed < SweepTicks ? sweepElapsed + 1 : 0;

        int effects = (true == bGainEnd ? 1 : 0) | (sweepState << 1);
        if (pixels == paintedPixels && effects == paintedEffects) return;

        paintedPixels = pixels;
        paintedEffects = effects;

        if (null == fillSurface) return;
        if (null == painter) painter = new RoguelikeBarPainter(fillSurface, InnerWidth, InnerHeight, "UI_RoguelikeKillFill");

        painter.Clear();
        painter.Groove(-1);
        painter.Run(0, pixels, RoguelikeHudColors.Kill, true);

        if (true == bGainEnd && 0 < pixels) painter.EndColumn(pixels, RoguelikeHudColors.Kill.light);

        if (0 < sweepState && 0 < pixels)
        {
            int x = Mathf.RoundToInt(sweepElapsed * (pixels - SweepWidth) / (float)(SweepTicks - 1));
            painter.Fill(Mathf.Max(0, x), 0, Mathf.Min(pixels, x + SweepWidth), InnerHeight, RoguelikeHudColors.White);
        }

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
            hideClock += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
            if (UI_RoguelikeIconNumber.ExitSeconds + 0.02f <= hideClock)
            {
                bHiding = false;
                gameObject.SetActive(false);
            }
        }

        Step(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds));
    }

    private void OnDisable()
    {
        bHiding = false;
    }

    private void OnDestroy()
    {
        if (null != painter) painter.Dispose();
    }
}
