using System;
using UnityEngine;

/// <summary>
/// 월드 HUD 값 · 연출 상태기계(WorldHUD_Plan.md 10 · 11장, 사양 원본 world_hud_v8_motion.py). 순수 C# 클래스라 에디터 검증기(RoguelikeWorldHudVerifier)가 같은 시간표를 그대로 재생해
/// 파이썬 기준 레이어와 픽셀 단위로 비교한다. RoguelikeWorldHud(MonoBehaviour)는 시계를 먹이고 이 클래스가 그린 버퍼를 텍스처로 올리기만 한다.
///   등장(10-1) / 퇴장(10-2): 띠 그리기 범위 · 숫자 이동 · 칸 켜기 · 스태미너 차오름. 값 변화 반응(10-3): 새 칸 번쩍임, 숫자 튐, 불꽃 / 물방울, 돌파 훑기, 손실 펄스, 목표 달성.
/// 시간은 호출자가 준 절대 시계(_now, 초, unscaled)다. 연출 시작 시각을 저장하고 (_now - 시작)으로 계산하므로 시뮬과 같은 식이 된다.
///   시뮬의 프레임 카운터(30fps)는 프레임 수 / 30초로 바꿔 쓴다(ActiveFrames). 이동은 정수 px이고 반투명 페이드는 없다.
/// 값 변화(나무 수 · 처치 수)는 Tick에서 이전 값과 비교해 연출을 건다(시뮬 frame()의 감지 순서와 같다). 손실 펄스 · 스태미너 잔상 · 회복은 호출 시각이 아니라
/// 다음 Tick 시각으로 시작을 찍는다(시뮬이 프레임 시각에 건 것과 같다).
/// 요소 8개: 0 임계치 띠 / 1 스태미너 띠 / 2 웨이브 숫자 / 3 임계치 숫자 / 4 스태미너 숫자(+대쉬 칸) / 5 남은 벌목 수 숫자(위 가운데) / 6 띠 둘레 입자 / 7 남은 수 둘레 입자. 웨이브 숫자는 좌상단(11장).
/// 모든 버퍼 · 풀은 만들 때 한 번만 만들고 Tick / PaintChanged는 할당이 없다.
/// </summary>
public sealed class RoguelikeWorldHudPresenter
{
    public const int ElementCount = 8;
    public const int ThresholdBandIndex = 0;
    public const int StaminaBandIndex = 1;
    public const int WaveLabelIndex = 2;
    public const int ThresholdLabelIndex = 3;
    public const int StaminaLabelIndex = 4;
    public const int KillLabelIndex = 5;
    public const int FxBandIndex = 6;
    public const int FxKillIndex = 7;
    public const int AllMask = (1 << ElementCount) - 1;

    private const int DashMaxCells = 2;
    // 이 비율(최대치 대비)보다 작은 피해는 잔상을 만들지 않는다(본편 HUD_Stemina.minEffectRatio와 같은 값).
    private const float MinDamageRatio = 0.01f;
    private const int CellMax = 64;

    // 시간 처리: 시작 시각 없음 / 프레임 수 -> 초 / 경계 부동소수 오차 흡수
    private const double NoTime = -1e9d;
    private const double NoTimeLimit = -1e8d;
    private const double FramesPerSecond = 30d;
    private const double FrameEpsilon = 1e-6d;

    // 10-1 등장(등장 시작 기준 초)
    private const double EntranceGateSeconds = 1.0d;
    private const double BandDrawSeconds = 0.45d;
    private const double CellLightStart = 0.30d;
    private const double CellLightStep = 0.012d;
    private const double StaminaFillStart = 0.35d;
    private const double StaminaFillEnd = 0.75d;
    private const double StaminaFillHeadSeconds = 0.78d;
    private const double SideStart = 0.20d;
    private const double SideEnd = 0.55d;
    private const double VertStart = 0.25d;
    private const double VertEnd = 0.62d;
    private const double SideDistance = 70d;
    private const double VertDistance = 30d;
    private const int FillHeadWidthFilling = 4;
    private const int FillHeadWidthNormal = 2;

    // 10-2 퇴장(퇴장 시작 기준 초)
    private const double ExitLabelSeconds = 0.30d;
    private const double ExitBandStart = 0.05d;
    private const double ExitBandEnd = 0.45d;
    private const double ExitSideDistance = 90d;
    private const double ExitVertDistance = 34d;

    // 10-3 값 변화 반응(프레임 수는 30fps 기준 시뮬 값)
    private const int CellFlashFrames = 3;
    private const int TreeBumpIncreaseFrames = 4;
    private const int TreeBumpDecreaseFrames = 3;
    private const int KillBumpFrames = 4;
    private const int PulseRimFrames = 4;
    private const double SweepSeconds = 0.35d;
    private const double SweepOvershoot = 30d;
    private const double TrailDelaySeconds = 0.08d;
    private const double TrailEndSeconds = 0.45d;
    private const double RecoverSeconds = 0.15d;
    private const double WarningHalfPeriod = 0.5d;
    private const double GoalBounceSeconds = 0.30d;
    private const double GoalBounceHeight = 6d;
    private const int GoalSparkleCount = 10;
    private const int SparkSpawnCount = 5;
    private const int DropSpawnCount = 2;

    // 흔들림 [+1, -1, +1, -1, +1] px, 첫 프레임은 0(시뮬 st_shake 표)
    private static readonly int[] ShakeByFrame = { 0, 1, -1, 1, -1, 1 };

    //내부 의존성
    private readonly RoguelikeWorldHudPainter painter;
    private readonly RoguelikeWorldHudParticles particles = new RoguelikeWorldHudParticles();
    private readonly Color32[] fxBandBuffer = new Color32[RoguelikeWorldHudGeometry.FxBandRect.width * RoguelikeWorldHudGeometry.FxBandRect.height];
    private readonly Color32[] fxKillBuffer = new Color32[RoguelikeWorldHudGeometry.FxKillRect.width * RoguelikeWorldHudGeometry.FxKillRect.height];
    private readonly System.Random random = new System.Random();

    //상태 변수 (값)
    private int wave;
    private bool bUnlimit;
    private int unlimitIndex;
    private int killed;
    private int killTarget;
    private int trees;
    private int threshold = 1;
    private double staminaRatio;
    private double staminaCurrent;
    private int staminaMax = 1;
    private bool bForcedWarning;
    private bool bInvincible;
    private bool bDashUnlocked;
    private int dashMax = DashMaxCells;
    private readonly float[] dashRatios = { 1f, 1f };

    //상태 변수 (연출)
    private bool bVisible;
    private bool bExiting;
    private bool bPrimed;
    private double now;
    private double showStart;
    private double exitStart;
    private int prevShownTrees;
    private int prevKilled;
    private bool bPrevGoal;
    private readonly double[] cellFlashStart = new double[CellMax];
    private double treeBumpStart = NoTime;
    private int treeBumpFrames;
    private double killBumpStart = NoTime;
    private double crossStart = NoTime;
    private double goalStart = NoTime;
    private bool bPulseQueued;
    private double pulseStart = NoTime;
    private float pendingLoss;
    private bool bTrail;
    private bool bTrailQueued;
    private double trailFrom;
    private double trailStart = NoTime;
    private bool bRecoverQueued;
    private double recoverStart = NoTime;
    private double recoverGain;
    private bool bWarningQueued;
    private double warningStart = NoTime;

    //상태 변수 (이번 Tick에 계산한 화면 값)
    private int shownTrees;
    private int sideOffset;
    private int vertOffset;
    private int revealHi;
    private ulong flashMask;
    private bool bSweep;
    private double sweepA;
    private bool bRimFlash;
    private bool bPulseRim;
    private int shakeOffset;
    private int treeBumpOffset;
    private int killBumpOffset;
    private RoguelikeWorldHudDigitStyle treeStyle;
    private RoguelikeWorldHudDigitStyle killStyle;
    private int staminaFill;
    private int staminaTrail;
    private int staminaFlashFrom;
    private int staminaFlashTo;
    private int staminaHeadWidth;
    private int staminaNumber;
    private bool bAlternateRim;
    private bool bOver;
    private double shownTrailRatio;

    // 마지막으로 그린 요소별 상태 해시(같으면 다시 그리지 않는다)
    private readonly ulong[] paintedHashes = new ulong[ElementCount];
    private readonly bool[] bPainted = new bool[ElementCount];
    private bool bBandFxPainted;
    private bool bKillFxPainted;

    // 요소 위치 보정(정수 px, 화면 기준 +x 오른쪽 / +y 아래)
    private readonly int[] offsetsX = new int[ElementCount];
    private readonly int[] offsetsY = new int[ElementCount];

    public bool IsVisible => bVisible;
    public bool IsDashUnlocked => bDashUnlocked;
    public bool IsExiting => bExiting;

    public RoguelikeWorldHudPresenter(RoguelikeWorldHudSheets _sheets)
    {
        painter = new RoguelikeWorldHudPainter(_sheets);
        ClearEffects();
    }

    /// <summary>요소 _index의 그린 버퍼(Unity 텍스처 순서 = 아래에서 위 행). 크기는 Geometry 사각형이다.</summary>
    public Color32[] GetBuffer(int _index)
    {
        switch (_index)
        {
            case ThresholdBandIndex: return painter.ThresholdBandBuffer;
            case StaminaBandIndex: return painter.StaminaBandBuffer;
            case WaveLabelIndex: return painter.WaveLabelBuffer;
            case ThresholdLabelIndex: return painter.ThresholdLabelBuffer;
            case StaminaLabelIndex: return painter.StaminaLabelBuffer;
            case KillLabelIndex: return painter.KillLabelBuffer;
            case FxBandIndex: return fxBandBuffer;
            default: return fxKillBuffer;
        }
    }

    /// <summary>요소 _index의 위치 보정(px). 이동 · 튐 · 흔들림은 렌더러를 정수 px 옮겨서 처리한다(텍스처를 다시 그리지 않는다).</summary>
    public int GetOffsetX(int _index)
    {
        return offsetsX[_index];
    }

    public int GetOffsetY(int _index)
    {
        return offsetsY[_index];
    }

    // ------------------------------------------------------------------ 표시 제어

    /// <summary>
    /// 등장 시작(10-1). 이미 보이는 중(퇴장 아님)이면 연출을 다시 시작하지 않고 다시 그리기만 한다. 퇴장 중이면 퇴장을 버리고 처음부터 등장한다.
    /// </summary>
    public void Show(double _now)
    {
        now = _now;
        ForceRepaint();

        if (true == bVisible && false == bExiting) return;

        bVisible = true;
        bExiting = false;
        bPrimed = false;
        showStart = _now;
        ClearEffects();
        bWarningQueued = true == bForcedWarning;
    }

    /// <summary>퇴장 시작(10-2). 보이지 않거나 이미 퇴장 중이면 무시한다.</summary>
    public void Hide(double _now)
    {
        if (false == bVisible || true == bExiting) return;

        now = _now;
        bExiting = true;
        exitStart = _now;
    }

    /// <summary>즉시 숨긴다(퇴장 연출 없음).</summary>
    public void HideImmediate()
    {
        bVisible = false;
        bExiting = false;
        particles.Clear();
        ForceRepaint();
    }

    /// <summary>런이 시작 · 종료될 때 값과 연출을 처음으로 되돌린다.</summary>
    public void ResetState()
    {
        HideImmediate();

        wave = 0;
        bUnlimit = false;
        unlimitIndex = 0;
        killed = 0;
        killTarget = 0;
        trees = 0;
        ClearEffects();
    }

    // ------------------------------------------------------------------ 값 입력

    public void SetWave(int _wave, bool _bUnlimit, int _unlimitIndex)
    {
        wave = _wave;
        bUnlimit = _bUnlimit;
        unlimitIndex = _unlimitIndex;
    }

    public void SetKills(int _killed, int _target)
    {
        killed = _killed;
        killTarget = _target;
    }

    public void SetThreshold(int _trees, int _threshold)
    {
        trees = _trees;
        threshold = _threshold;
    }

    /// <summary>손실 1초 펄스(10-3): 다음 Tick 시각부터 0.133초 동안 초과 토막 테 + 스태미너 띠 테 흰색, 스태미너 숫자 흔들림 0.2초.</summary>
    public void PlayPulse()
    {
        bPulseQueued = true;
    }

    /// <summary>스태미너 비율(0~1). 값이 줄어든 순간 PlayStaminaDamaged가 먼저 불렸으면 직전 채움에서 지금 채움까지 잔상(0.08 ~ 0.45초, ease-in-cubic)을 만든다.</summary>
    public void SetStaminaRatio(float _ratio)
    {
        double ratio = Mathf.Clamp01(_ratio);

        // 피해 이벤트는 같은 호출 안에서 값 변경보다 먼저 도착한다. 이번 갱신 직전의 값이 잔상의 위쪽 끝이다.
        if (0f < pendingLoss)
        {
            pendingLoss = 0f;
            trailFrom = true == bTrail ? Math.Max(shownTrailRatio, staminaRatio) : staminaRatio;
            bTrailQueued = true;
        }

        staminaRatio = ratio;
    }

    /// <summary>스태미너 숫자 현재 / 최대. 표시는 올림 정수(기획서 3-2)다.</summary>
    public void SetStaminaNumbers(float _current, float _max)
    {
        staminaCurrent = Math.Max(0d, _current);
        staminaMax = Mathf.Max(1, Mathf.CeilToInt(_max));
    }

    public void PlayStaminaDamaged(float _lossRatio)
    {
        if (MinDamageRatio > _lossRatio) return;

        pendingLoss += _lossRatio;
    }

    /// <summary>스태미너가 한 번에 회복된 순간(최대치 대비 비율). 늘어난 구간을 0.15초 동안 흰색으로 덮는다.</summary>
    public void PlayStaminaRecovered(float _gainRatio)
    {
        if (0f >= _gainRatio) return;

        bRecoverQueued = true;
        recoverGain = _gainRatio;
    }

    public void SetForcedWarning(bool _bWarning)
    {
        if (bForcedWarning == _bWarning) return;

        bForcedWarning = _bWarning;
        bWarningQueued = true == _bWarning;
        if (false == _bWarning) warningStart = NoTime;
    }

    public void SetInvincible(bool _bInvincible)
    {
        bInvincible = _bInvincible;
    }

    public void SetDashUnlocked(bool _bUnlocked)
    {
        bDashUnlocked = _bUnlocked;
    }

    public void SetDashMax(int _count)
    {
        dashMax = Mathf.Clamp(_count, 0, DashMaxCells);
    }

    public void SetDashCharge(int _index, float _ratio)
    {
        if (0 > _index || DashMaxCells <= _index) return;

        dashRatios[_index] = Mathf.Clamp01(_ratio);
    }

    // ------------------------------------------------------------------ 진행

    /// <summary>
    /// 시계를 _now로 옮기고 이번 프레임의 화면 값(범위 · 오프셋 · 색)을 계산한다. 값 변화 감지 -> 연출 트리거 -> 화면 값 순서는 시뮬 frame()과 같다.
    /// 퇴장이 끝나면 보이지 않는 상태가 된다(IsVisible == false).
    /// </summary>
    public void Tick(double _now)
    {
        if (false == bVisible) return;

        now = _now;
        StampQueued();

        double e = now - showStart;
        int enterCells = e >= CellLightStart ? (int)((e - CellLightStart) / CellLightStep) : -1;
        shownTrees = e < EntranceGateSeconds ? Math.Min(trees, Math.Max(0, (enterCells + 1) * 2)) : trees;
        bOver = threshold <= trees;

        // 이동량(등장은 바깥 -> 제자리, 퇴장은 제자리 -> 바깥). 정수 px, 반올림은 파이썬 round와 같은 은행가 반올림.
        double sideEntrance = 1d - RoguelikeWorldHudEase.OutBack(RoguelikeWorldHudEase.Phase(e, SideStart, SideEnd));
        double vertEntrance = 1d - RoguelikeWorldHudEase.OutBack(RoguelikeWorldHudEase.Phase(e, VertStart, VertEnd));
        double exitPhase = true == bExiting ? RoguelikeWorldHudEase.Phase(now, exitStart, exitStart + ExitLabelSeconds) : 0d;
        sideOffset = (int)Math.Round(sideEntrance * SideDistance + RoguelikeWorldHudEase.InCubic(exitPhase) * ExitSideDistance);
        vertOffset = (int)Math.Round(vertEntrance * VertDistance + RoguelikeWorldHudEase.InCubic(exitPhase) * ExitVertDistance);

        DetectChanges(e);
        ComputeBand(e);
        ComputeLabels(e);

        particles.Step(now);

        if (true == bExiting && now >= exitStart + ExitBandEnd) HideImmediate();
    }

    /// <summary>
    /// 이번 Tick 결과 중 바뀐 요소만 다시 그린다(요소마다 상태 해시 비교). 다시 그린 요소의 비트가 켜진 마스크를 돌려준다(호출자가 텍스처로 올린다).
    /// 보이지 않으면 0.
    /// </summary>
    public int PaintChanged()
    {
        if (false == bVisible) return 0;

        int mask = 0;

        ulong hash = Mix(Mix(Mix(Mix(Mix(Mix(Mix(Mix(0UL, shownTrees), threshold), RoguelikeWorldHudGeometry.A0), revealHi), bOver ? 1 : 0), (long)flashMask), bSweep ? 1 : 0), (long)(sweepA * 1024d));
        hash = Mix(hash, bRimFlash ? 1 : 0);
        if (true == IsChanged(ThresholdBandIndex, hash))
        {
            RoguelikeWorldHudThresholdFx fx = new RoguelikeWorldHudThresholdFx(RoguelikeWorldHudGeometry.A0, revealHi, flashMask, bSweep, sweepA, bRimFlash, false);
            painter.PaintThresholdBand(shownTrees, threshold, bOver, in fx);
            mask |= 1 << ThresholdBandIndex;
        }

        hash = Mix(Mix(Mix(Mix(Mix(Mix(Mix(Mix(0UL, staminaFill), staminaTrail), staminaFlashFrom), staminaFlashTo), staminaHeadWidth), revealHi), bOver ? 1 : 0), bInvincible ? 1 : 0);
        hash = Mix(Mix(hash, bAlternateRim ? 1 : 0), bPulseRim ? 1 : 0);
        if (true == IsChanged(StaminaBandIndex, hash))
        {
            RoguelikeWorldHudTone tone = true == bInvincible ? RoguelikeWorldHudPalette.Invincible : (true == bOver ? RoguelikeWorldHudPalette.Hot : RoguelikeWorldHudPalette.Stamina);
            RoguelikeWorldHudTone rim = true == bPulseRim ? RoguelikeWorldHudPalette.RimPulse : (true == bAlternateRim ? RoguelikeWorldHudPalette.RimFlash : RoguelikeWorldHudPalette.Rim);
            painter.PaintStaminaBand(staminaFill, staminaTrail, in tone, staminaFlashFrom, staminaFlashTo, in rim, staminaHeadWidth, RoguelikeWorldHudGeometry.A0, revealHi);
            mask |= 1 << StaminaBandIndex;
        }

        hash = Mix(Mix(Mix(0UL, wave), bUnlimit ? 1 : 0), unlimitIndex);
        if (true == IsChanged(WaveLabelIndex, hash))
        {
            if (true == bUnlimit) painter.PaintUnlimitLabel(unlimitIndex);
            else painter.PaintWaveLabel(wave);
            mask |= 1 << WaveLabelIndex;
        }

        hash = Mix(Mix(Mix(0UL, shownTrees), threshold), (int)treeStyle);
        if (true == IsChanged(ThresholdLabelIndex, hash))
        {
            painter.PaintThresholdLabel(shownTrees, threshold, treeStyle);
            mask |= 1 << ThresholdLabelIndex;
        }

        int dashCount = true == bDashUnlocked ? dashMax : 0;
        hash = Mix(Mix(Mix(Mix(0UL, staminaNumber), staminaMax), bOver ? 1 : 0), dashCount);
        hash = Mix(Mix(hash, (long)(dashRatios[0] * 4096f)), (long)(dashRatios[1] * 4096f));
        if (true == IsChanged(StaminaLabelIndex, hash))
        {
            RoguelikeWorldHudDashCell cell0 = new RoguelikeWorldHudDashCell(dashRatios[0], 1f <= dashRatios[0]);
            RoguelikeWorldHudDashCell cell1 = new RoguelikeWorldHudDashCell(dashRatios[1], 1f <= dashRatios[1]);
            painter.PaintStaminaLabel(staminaNumber, staminaMax, true == bOver ? RoguelikeWorldHudDigitStyle.Warn : RoguelikeWorldHudDigitStyle.White, dashCount, in cell0, in cell1);
            mask |= 1 << StaminaLabelIndex;
        }

        int remaining = Mathf.Max(0, killTarget - killed);
        hash = Mix(Mix(0UL, remaining), (int)killStyle);
        if (true == IsChanged(KillLabelIndex, hash))
        {
            painter.PaintKillLabel(remaining, killStyle);
            mask |= 1 << KillLabelIndex;
        }

        // 입자: 살아 있는 동안만 매 프레임 다시 그리고, 다 사라진 프레임에 한 번 비운다.
        int bandAlive = particles.AliveCount(RoguelikeWorldHudParticles.BandLayer);
        if (0 < bandAlive || true == bBandFxPainted)
        {
            particles.Paint(RoguelikeWorldHudParticles.BandLayer, now, fxBandBuffer, in RoguelikeWorldHudGeometry.FxBandRect);
            bBandFxPainted = 0 < bandAlive;
            mask |= 1 << FxBandIndex;
        }

        int killAlive = particles.AliveCount(RoguelikeWorldHudParticles.KillLayer);
        if (0 < killAlive || true == bKillFxPainted)
        {
            particles.Paint(RoguelikeWorldHudParticles.KillLayer, now, fxKillBuffer, in RoguelikeWorldHudGeometry.FxKillRect);
            bKillFxPainted = 0 < killAlive;
            mask |= 1 << FxKillIndex;
        }

        // 위치 보정(정수 px)
        offsetsX[ThresholdLabelIndex] = -sideOffset;
        offsetsY[ThresholdLabelIndex] = treeBumpOffset;
        offsetsX[StaminaLabelIndex] = sideOffset + shakeOffset;
        offsetsY[StaminaLabelIndex] = 0;
        offsetsX[WaveLabelIndex] = 0;
        offsetsY[WaveLabelIndex] = -vertOffset;
        offsetsX[KillLabelIndex] = 0;
        offsetsY[KillLabelIndex] = -vertOffset + killBumpOffset;

        return mask;
    }

    // // 내부

    private void ClearEffects()
    {
        for (int i = 0; CellMax > i; i++) cellFlashStart[i] = NoTime;

        treeBumpStart = NoTime;
        treeBumpFrames = 0;
        killBumpStart = NoTime;
        crossStart = NoTime;
        goalStart = NoTime;
        bPulseQueued = false;
        pulseStart = NoTime;
        pendingLoss = 0f;
        bTrail = false;
        bTrailQueued = false;
        trailStart = NoTime;
        shownTrailRatio = 0d;
        bRecoverQueued = false;
        recoverStart = NoTime;
        bWarningQueued = false;
        warningStart = NoTime;
        prevShownTrees = 0;
        prevKilled = 0;
        bPrevGoal = false;
        particles.Clear();
        ForceRepaint();
    }

    private void ForceRepaint()
    {
        for (int i = 0; ElementCount > i; i++) bPainted[i] = false;

        bBandFxPainted = true;
        bKillFxPainted = true;
    }

    /// <summary>호출 시각이 아니라 이번 Tick 시각으로 연출 시작을 찍는다.</summary>
    private void StampQueued()
    {
        if (true == bPulseQueued)
        {
            bPulseQueued = false;
            pulseStart = now;
        }

        if (true == bTrailQueued)
        {
            bTrailQueued = false;
            bTrail = true;
            trailStart = now;
        }

        if (true == bRecoverQueued)
        {
            bRecoverQueued = false;
            recoverStart = now;
        }

        if (true == bWarningQueued)
        {
            bWarningQueued = false;
            warningStart = now;
        }
    }

    /// <summary>시작 시각 _start에서 _length 프레임(30fps) 중 남은 프레임 수. 시작 직후 첫 프레임이 _length, 끝나면 0.</summary>
    private int ActiveFrames(double _start, int _length)
    {
        if (NoTimeLimit >= _start) return 0;

        int frame = (int)Math.Floor((now - _start) * FramesPerSecond + FrameEpsilon);
        int remain = _length - frame;
        return 0 < remain ? remain : 0;
    }

    /// <summary>
    /// 값 변화 감지 -> 연출 트리거(시뮬 frame()의 감지 블록). 나무 수는 "보이는 나무 수"(등장 중에는 켜진 칸만큼)로 비교한다.
    /// 등장 중(1.0초 전)에는 칸 번쩍임만 걸고 숫자 튐 · 불꽃은 걸지 않는다. 처음 한 번은 비교 없이 값만 맞춘다.
    /// </summary>
    private void DetectChanges(double _e)
    {
        bool bGoal = 0 < killTarget && killTarget <= killed;

        if (false == bPrimed)
        {
            bPrimed = true;
            prevShownTrees = shownTrees;
            prevKilled = killed;
            bPrevGoal = bGoal;
            return;
        }

        bool bEntered = _e >= EntranceGateSeconds;

        if (shownTrees > prevShownTrees)
        {
            int cellEnd = (shownTrees + 1) >> 1;
            for (int c = (prevShownTrees + 1) >> 1; cellEnd > c; c++)
            {
                if (CellMax > c) cellFlashStart[c] = now;
            }

            if (true == bEntered)
            {
                treeBumpStart = now;
                treeBumpFrames = TreeBumpIncreaseFrames;
            }
        }
        else if (shownTrees < prevShownTrees && true == bEntered)
        {
            // 웨이브 클리어(처치 >= 목표) 뒤 감소 = 묘목 복귀(물방울, 숫자 튐 없음). 그 전 감소 = 불꽃 + 숫자 튐.
            bool bSapling = true == bGoal;
            int cellEnd = (prevShownTrees + 1) >> 1;

            for (int c = (shownTrees + 1) >> 1; cellEnd > c; c++)
            {
                GetCellCenter(c, out int cx, out int cy);

                if (false == bSapling)
                {
                    particles.SpawnBurst(RoguelikeWorldHudParticles.BandLayer, cx, cy, now, SparkSpawnCount, in RoguelikeWorldHudPalette.Amber.light, in RoguelikeWorldHudPalette.Amber.main, in RoguelikeWorldHudPalette.White, 3, 40d, 50d);
                }
                else if (0 == c % 2)
                {
                    particles.SpawnBurst(RoguelikeWorldHudParticles.BandLayer, cx, cy, now, DropSpawnCount, in RoguelikeWorldHudPalette.Foam, in RoguelikeWorldHudPalette.White, in RoguelikeWorldHudPalette.White, 2, 25d, 50d);
                }
            }

            if (false == bSapling)
            {
                treeBumpStart = now;
                treeBumpFrames = TreeBumpDecreaseFrames;
            }
        }

        if (prevShownTrees < threshold && threshold <= shownTrees) crossStart = now;

        prevShownTrees = shownTrees;

        if (killed > prevKilled) killBumpStart = now;
        prevKilled = killed;

        if (true == bGoal && false == bPrevGoal)
        {
            goalStart = now;
            SpawnGoalSparkles();
        }

        bPrevGoal = bGoal;
    }

    /// <summary>목표 달성 반짝이: 남은 수 숫자 위에서 위로 10개(금색 2톤 + 흰색, 속도 60, 위 보정 80).</summary>
    private void SpawnGoalSparkles()
    {
        int width = painter.MeasureKillLabelWidth(Mathf.Max(0, killTarget - killed));
        int left = RoguelikeWorldHudGeometry.ScreenWidth / 2 - width / 2;
        int top = RoguelikeWorldHudGeometry.WaveY - vertOffset;

        for (int i = 0; GoalSparkleCount > i; i++)
        {
            particles.SpawnBurst(RoguelikeWorldHudParticles.KillLayer, left + random.Next(8, width - 4 + 1), top + 6, now, 1, in RoguelikeWorldHudPalette.GoldLight, in RoguelikeWorldHudPalette.GoldMain, in RoguelikeWorldHudPalette.White, 3, 60d, 80d);
        }
    }

    /// <summary>칸 가운데의 화면 좌표(cell_center). 임계치 띠 칸 c의 가운데 A, 띠 가운데 B에서 x = (a + b) / 2, y = (b - a) / 4.</summary>
    private void GetCellCenter(int _cell, out int _x, out int _y)
    {
        int cellsFirst = Mathf.Max(0, threshold >> 1);
        int firstEnd = RoguelikeWorldHudGeometry.A0 + cellsFirst * RoguelikeWorldHudGeometry.Pitch - 2 + 4 * RoguelikeWorldHudGeometry.RimThickness;
        int secondStart = firstEnd + RoguelikeWorldHudGeometry.Split;
        int rim = 2 * RoguelikeWorldHudGeometry.RimThickness;
        int a = cellsFirst > _cell ? RoguelikeWorldHudGeometry.A0 + rim + _cell * RoguelikeWorldHudGeometry.Pitch + rim : secondStart + rim + (_cell - cellsFirst) * RoguelikeWorldHudGeometry.Pitch + rim;
        int b = (RoguelikeWorldHudGeometry.B0 + RoguelikeWorldHudGeometry.B1) >> 1;

        _x = (a + b) >> 1;
        _y = (b - a) >> 2;
    }

    /// <summary>띠 보이는 범위 · 새 칸 · 돌파 훑기 · 펄스 테 · 스태미너 채움 · 잔상을 계산한다.</summary>
    private void ComputeBand(double _e)
    {
        // 띠 그리기 범위: 등장 0 ~ 0.45초 ease-out-cubic(마구리 -> 끝), 퇴장 0.05 ~ 0.45초 ease-in-cubic(위 꼭짓점 쪽부터 지움)
        double hi = RoguelikeWorldHudGeometry.A0 + (RoguelikeWorldHudGeometry.A1 - RoguelikeWorldHudGeometry.A0) * RoguelikeWorldHudEase.OutCubic(RoguelikeWorldHudEase.Phase(_e, 0d, BandDrawSeconds));
        if (true == bExiting && now >= exitStart + ExitBandStart)
        {
            hi = RoguelikeWorldHudGeometry.A1 - (RoguelikeWorldHudGeometry.A1 - RoguelikeWorldHudGeometry.A0) * RoguelikeWorldHudEase.InCubic(RoguelikeWorldHudEase.Phase(now, exitStart + ExitBandStart, exitStart + ExitBandEnd));
        }

        revealHi = (int)hi;

        flashMask = 0UL;
        for (int c = 0; CellMax > c; c++)
        {
            if (0 < ActiveFrames(cellFlashStart[c], CellFlashFrames)) flashMask |= 1UL << c;
        }

        // 돌파 훑기: 0.35초 동안 흰 빛 띠가 초과 토막을 틈 -> 끝으로 훑는다(ease-out-cubic). 그동안 초과 토막 테 흰 테.
        int firstEnd = RoguelikeWorldHudGeometry.A0 + Mathf.Max(0, threshold >> 1) * RoguelikeWorldHudGeometry.Pitch - 2 + 4 * RoguelikeWorldHudGeometry.RimThickness;
        int secondStart = firstEnd + RoguelikeWorldHudGeometry.Split;
        bSweep = NoTimeLimit < crossStart && SweepSeconds > now - crossStart;
        sweepA = true == bSweep ? secondStart + (RoguelikeWorldHudGeometry.A1 - secondStart + SweepOvershoot) * RoguelikeWorldHudEase.OutCubic((now - crossStart) / SweepSeconds) : 0d;

        bPulseRim = 0 < ActiveFrames(pulseStart, PulseRimFrames);
        bRimFlash = true == bPulseRim || true == bSweep;

        // 스태미너: 등장 0.35 ~ 0.75초 ease-out-cubic으로 0 -> 현재값. 차오르는 동안 머리 4px 흰색.
        double fillFactor = _e < EntranceGateSeconds ? RoguelikeWorldHudEase.OutCubic(RoguelikeWorldHudEase.Phase(_e, StaminaFillStart, StaminaFillEnd)) : 1d;
        staminaFill = RoguelikeWorldHudPainter.ToStaminaPixels(staminaRatio * fillFactor);
        staminaHeadWidth = _e < StaminaFillHeadSeconds ? FillHeadWidthFilling : FillHeadWidthNormal;
        staminaNumber = Mathf.Max(0, (int)Math.Ceiling(staminaCurrent * fillFactor));

        staminaTrail = 0;
        if (true == bTrail)
        {
            double k = RoguelikeWorldHudEase.Phase(now, trailStart + TrailDelaySeconds, trailStart + TrailEndSeconds);

            if (1d > k)
            {
                shownTrailRatio = trailFrom + (staminaRatio - trailFrom) * RoguelikeWorldHudEase.InCubic(k);
                staminaTrail = RoguelikeWorldHudPainter.ToStaminaPixels(shownTrailRatio);
            }
            else
            {
                bTrail = false;
            }
        }

        staminaFlashFrom = 0;
        staminaFlashTo = 0;
        if (NoTimeLimit < recoverStart && RecoverSeconds > now - recoverStart)
        {
            staminaFlashTo = staminaFill;
            staminaFlashFrom = Mathf.Max(0, staminaFill - RoguelikeWorldHudPainter.ToStaminaPixels(recoverGain));
        }

        bAlternateRim = false;
        if (true == bForcedWarning && false == bInvincible && NoTimeLimit < warningStart)
        {
            bAlternateRim = 1 == (int)((now - warningStart) / WarningHalfPeriod) % 2;
        }
    }

    /// <summary>숫자 색 · 튐 · 흔들림 · 목표 달성 튀어오름을 계산한다.</summary>
    private void ComputeLabels(double _e)
    {
        // 나무 숫자: 증가(4프레임)는 위 2px(앞 2프레임) -> 1px(뒤 2프레임), 감소(3프레임)는 2px 1프레임 -> 1px 2프레임. 호박색은 남은 프레임이 2 이상일 때.
        int treeRemain = ActiveFrames(treeBumpStart, treeBumpFrames);
        treeBumpOffset = 3 <= treeRemain ? -2 : (1 <= treeRemain ? -1 : 0);
        if (true == bOver) treeStyle = RoguelikeWorldHudDigitStyle.Warn;
        else treeStyle = 2 <= treeRemain ? RoguelikeWorldHudDigitStyle.Amber : RoguelikeWorldHudDigitStyle.White;

        // 남은 벌목 수 숫자(v8): 처치(남은 수 감소) 4프레임 아래로 튐(2px 2프레임 -> 1px 2프레임) + 호박색. 목표 달성(남은 수 0)이면 금색 유지 + 0.30초 sin 곡선으로 아래로 6px 튀었다 돌아옴.
        bool bGoal = 0 < killTarget && killTarget <= killed;
        if (true == bGoal)
        {
            killStyle = RoguelikeWorldHudDigitStyle.Gold;
            double ct = now - goalStart;
            killBumpOffset = GoalBounceSeconds > ct ? (int)Math.Round(GoalBounceHeight * Math.Sin(Math.Min(1d, ct / GoalBounceSeconds) * Math.PI)) : 0;
        }
        else
        {
            int killRemain = ActiveFrames(killBumpStart, KillBumpFrames);
            killBumpOffset = 3 <= killRemain ? 2 : (1 <= killRemain ? 1 : 0);
            killStyle = 2 <= killRemain ? RoguelikeWorldHudDigitStyle.Amber : RoguelikeWorldHudDigitStyle.White;
        }

        // 스태미너 숫자 흔들림: 펄스 시작 프레임 0, 이후 [+1, -1, +1, -1, +1] (0.2초)
        shakeOffset = 0;
        if (NoTimeLimit < pulseStart)
        {
            int frame = (int)Math.Floor((now - pulseStart) * FramesPerSecond + FrameEpsilon);
            if (0 <= frame && ShakeByFrame.Length > frame) shakeOffset = ShakeByFrame[frame];
        }
    }

    private bool IsChanged(int _index, ulong _hash)
    {
        if (true == bPainted[_index] && paintedHashes[_index] == _hash) return false;

        bPainted[_index] = true;
        paintedHashes[_index] = _hash;
        return true;
    }

    private static ulong Mix(ulong _hash, long _value)
    {
        unchecked
        {
            ulong hash = (_hash ^ (ulong)_value) * 1099511628211UL;
            return hash ^ (hash >> 29);
        }
    }
}
