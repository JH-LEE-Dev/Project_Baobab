using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 스태미너 바(RoguelikeHUD_Polish_Plan.md 5-3). 좌측 하단 막대 묶음의 맨 아래 줄이고 112 x 10 캡슐 막대 하나다.
/// 본편 스태미너 바(HUD_Stemina)는 로그라이크에서 화면에서만 숨고(UIView_HUD가 CanvasGroup alpha 0) 동작은 그대로 이어지며,
/// UIView_HUD가 같은 호출을 이 컴포넌트에도 전달한다(RoguelikeHUDLink.StaminaBar). 붉은 화면(BloodScreen)은 본편 쪽에서 계속 나온다.
///
/// 안쪽 110 x 8을 CPU로 그린 RawImage 하나(RoguelikeBarPainter)가 홈 + 채움 + 눈금을 맡고, 윤곽 · 홈 스프라이트(Background)는 프리팹에 있다.
/// 1/4 · 1/2 · 3/4 눈금은 안쪽 맨 아래 행 1px 윤곽색이다. 채움은 정수 px로 스냅하고 값이 바뀌면 지수 감속(시정수 0.12초)으로 따라간다.
/// [연출] 24Hz 틱, unscaled 시간, 알파 페이드 · 화면 흔들림 없음.
///   피해(PlayDamaged)         줄어든 구간이 피해 잔상 팔레트로 0.15초 머문 뒤 0.25초에 걸쳐 현재 값까지 줄어든다.
///   회복(PlayRecovered / PlayRecoverWasted)  채움 끝 열이 2틱 동안 WHITE.
///   위험(SetForcedWarning)    6틱마다 기본 팔레트와 적색(임계치 초과) 팔레트가 번갈아 나온다(2Hz). 무적 팔레트가 켜져 있으면 무적이 먼저다.
///   무적(SetInvincible)       원인(EStaminaInvincibleSource)별 비트로 기억하고 하나라도 켜져 있으면 무적 팔레트.
/// </summary>
public class UI_RoguelikeStaminaBar : MonoBehaviour
{
    private const int InnerWidth = 110;
    private const int InnerHeight = 8;
    private const float FillTau = 0.12f;
    private const int NoTick = -1000000;
    private const int RecoverTicks = 2;
    private const int WarningStepTicks = 6;
    private const float GhostHoldSeconds = 0.15f;
    private const float GhostFallSeconds = 0.25f;
    // 이 비율(최대치 대비)보다 작은 피해는 잔상을 만들지 않는다(본편 HUD_Stemina.minEffectRatio와 같은 값).
    private const float MinDamageRatio = 0.01f;
    // 눈금 위치(안쪽 기준 열). 안쪽 폭 110의 1/4 · 1/2 · 3/4를 반올림한 값이다.
    private static readonly int[] NotchColumns = { 28, 55, 82 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("안쪽 110x8 홈 + 채움을 그리는 RawImage.")]
    private RawImage fillSurface;

    //상태 변수
    private RoguelikeBarPainter painter;
    private float targetRatio;
    private float displayRatio;
    private bool bHasValue;
    private float clock;
    private float pendingLoss;
    private bool bGhost;
    private float ghostRatio;
    private float ghostHold;
    private float ghostFallSpeed;
    private bool bGhostFalling;
    private int recoverStartTick = NoTick;
    private bool bForcedWarning;
    private int invincibleMask;
    private int paintedFill = -1;
    private int paintedGhost = -2;
    private int paintedEffects = -1;

    /// <summary>현재 스태미너 비율(0~1). 값이 바뀔 때마다 매 프레임 들어온다.</summary>
    public void UpdateValue(float _ratio)
    {
        float ratio = Mathf.Clamp01(_ratio);

        if (false == bHasValue)
        {
            bHasValue = true;
            displayRatio = ratio;
            targetRatio = ratio;
        }

        // 피해 이벤트는 같은 호출 안에서 값 변경보다 먼저 도착한다. 이번 갱신 직전의 값이 잔상의 위쪽 끝이다.
        if (0f < pendingLoss)
        {
            pendingLoss = 0f;
            ghostRatio = true == bGhost ? Mathf.Max(ghostRatio, targetRatio) : targetRatio;
            bGhost = true;
            ghostHold = GhostHoldSeconds;
            bGhostFalling = false;
        }

        targetRatio = ratio;
    }

    /// <summary>스태미너가 한 번에 줄어든 순간. _lossRatio는 최대치 대비 줄어든 비율이다.</summary>
    public void PlayDamaged(float _lossRatio)
    {
        if (MinDamageRatio > _lossRatio) return;

        pendingLoss += _lossRatio;
    }

    /// <summary>스태미너가 한 번에 회복된 순간. 채움 끝 열이 2틱 동안 WHITE.</summary>
    public void PlayRecovered(float _gainRatio)
    {
        if (0f >= _gainRatio) return;

        recoverStartTick = CurrentTick();
    }

    /// <summary>최대치에 막혀 회복량이 버려진 회복. 같은 모양으로 채움 끝 열이 2틱 동안 WHITE.</summary>
    public void PlayRecoverWasted()
    {
        recoverStartTick = CurrentTick();
    }

    /// <summary>로그라이크 강제 경고의 바 경고(임계점 초과 · 한 번 맞으면 죽는 상태). 6틱마다 기본 팔레트와 적색 팔레트가 번갈아 나온다.</summary>
    public void SetForcedWarning(bool _bBarWarning)
    {
        bForcedWarning = _bBarWarning;
    }

    /// <summary>무적 구간 시작(true) / 종료(false). 원인별 비트로 기억하고 하나라도 켜져 있으면 무적 팔레트를 쓴다.</summary>
    public void SetInvincible(EStaminaInvincibleSource _source, bool _bInvincible)
    {
        int bit = 1 << (int)_source;

        if (true == _bInvincible) invincibleMask |= bit;
        else invincibleMask &= ~bit;
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;

        displayRatio = Mathf.Abs(targetRatio - displayRatio) < 0.0005f ? targetRatio : displayRatio + (targetRatio - displayRatio) * (1f - Mathf.Exp(-_dt / FillTau));

        StepGhost(_dt);
        Repaint();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * RoguelikeBarPainter.TicksPerSecond);
    }

    /// <summary>잔상: 머무는 동안은 그대로, 그 뒤 현재 값까지 GhostFallSeconds에 걸쳐 줄어든다. 값(목표)이 잔상 위로 올라가면 사라진다.</summary>
    private void StepGhost(float _dt)
    {
        if (false == bGhost) return;

        if (ghostRatio <= targetRatio)
        {
            bGhost = false;
            return;
        }

        if (0f < ghostHold)
        {
            ghostHold -= _dt;
            return;
        }

        if (false == bGhostFalling)
        {
            bGhostFalling = true;
            ghostFallSpeed = (ghostRatio - targetRatio) / GhostFallSeconds;
        }

        ghostRatio = Mathf.MoveTowards(ghostRatio, targetRatio, ghostFallSpeed * _dt);
        if (ghostRatio <= targetRatio) bGhost = false;
    }

    /// <summary>그릴 내용(채움 px, 잔상 px, 연출 상태)이 바뀌었을 때만 다시 그린다.</summary>
    private void Repaint()
    {
        int tickNow = CurrentTick();
        int fillPixels = Mathf.Clamp(Mathf.RoundToInt(displayRatio * InnerWidth), 0, InnerWidth);
        int ghostPixels = true == bGhost ? Mathf.Clamp(Mathf.RoundToInt(ghostRatio * InnerWidth), 0, InnerWidth) : -1;

        bool bInvincible = 0 != invincibleMask;
        bool bWarnPhase = true == bForcedWarning && false == bInvincible && 1 == (tickNow / WarningStepTicks) % 2;
        bool bRecover = tickNow - recoverStartTick < RecoverTicks;

        int effects = (true == bRecover ? 1 : 0) | (true == bWarnPhase ? 2 : 0) | (true == bInvincible ? 4 : 0);
        if (fillPixels == paintedFill && ghostPixels == paintedGhost && effects == paintedEffects) return;

        paintedFill = fillPixels;
        paintedGhost = ghostPixels;
        paintedEffects = effects;

        if (null == fillSurface) return;
        if (null == painter) painter = new RoguelikeBarPainter(fillSurface, InnerWidth, InnerHeight, "UI_RoguelikeStaminaFill");

        painter.Clear();
        painter.Groove(-1);

        if (ghostPixels > fillPixels) painter.Run(fillPixels, ghostPixels, RoguelikeHudColors.Ghost, false);

        RoguelikeBarPalette palette = true == bInvincible ? RoguelikeHudColors.Invincible : (true == bWarnPhase ? RoguelikeHudColors.Red : RoguelikeHudColors.Stamina);
        painter.Run(0, fillPixels, palette, true);

        for (int i = 0; i < NotchColumns.Length; i++) painter.SetPixel(NotchColumns[i], InnerHeight - 1, RoguelikeHudColors.Outline);

        if (true == bRecover && 0 < fillPixels) painter.EndColumn(fillPixels, RoguelikeHudColors.White);

        painter.Apply();
    }

    private void Awake()
    {
        // 첫 프레임에 텍스처 없는 RawImage(흰 네모)가 보이지 않도록 미리 한 번 그린다.
        Repaint();
    }

    private void Update()
    {
        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        recoverStartTick = NoTick;
        paintedEffects = -1;
    }

    private void OnDestroy()
    {
        if (null != painter) painter.Dispose();
    }
}
