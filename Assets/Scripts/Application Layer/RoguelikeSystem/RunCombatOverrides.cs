using UnityEngine;

/// <summary>
/// 본편 전투 코드에 하드코딩된 상수 중 로그라이크 카드가 바꿔야 하는 값의 덮어쓰기 창구.
///
/// [본편 영향 없음] 모든 값의 기본값은 "덮어쓰지 않음"이고, 읽는 쪽은 기본값일 때 원래 상수를 그대로 쓴다.
/// 로그라이크 세션만 값을 넣고, 세션이 끝나면(RoguelikeSystem.Release) ResetAll로 되돌린다.
/// 메인메뉴를 거치지 않으면 본편에 들어갈 수 없으므로 본편 플레이 중에는 항상 기본값이다.
///
/// 충격파 각도처럼 인스턴스 필드로 이미 열려 있는 값은 여기 두지 않고 생성 직후 그 인스턴스에 직접 넣는다.
/// </summary>
public static class RunCombatOverrides
{
    /// <summary>
    /// 회전베기 발동 주기(이 휘두름마다 한 번 회전베기). 0 이하면 AttackComponent의 기본 상수(3)를 쓴다.
    /// 1이면 휘두를 때마다 회전베기다(로그라이크 회전베기 MAX "상시 회전베기").
    /// </summary>
    public static int WhirlwindSwingInterval { get; private set; }

    /// <summary>회전베기 타격에만 곱하는 피해 배율(회전베기 카드의 "회전 피해 증가"). 1이면 본편처럼 도끼 피해 그대로.</summary>
    public static float WhirlwindDamageMultiplier { get; private set; } = 1f;

    /// <summary>회전베기 타격에만 곱하는 범위 배율(회전베기 카드의 "범위 증가"). 1이면 본편처럼 도끼 범위 그대로.</summary>
    public static float WhirlwindRangeMultiplier { get; private set; } = 1f;

    /// <summary>충격파가 나갈 때 이어서 더 나가는 수(충격파 MAX 2중 발생 = 1). 0이면 본편처럼 하나.</summary>
    public static int ShockWaveExtraCount { get; private set; }

    /// <summary>추가 충격파 사이의 간격(초).</summary>
    public static float ShockWaveExtraDelay { get; private set; } = 0.12f;

    /// <summary>추가 충격파의 피해 배율(충격파 MAX 두 번째 = 0.6). 추가 충격파가 없는 본편에서는 읽히지 않는다.</summary>
    public static float ShockWaveExtraDamageMultiplier { get; private set; } = 1f;

    /// <summary>
    /// 모든 충격파의 피해에 곱하는 배율(충격파 카드의 레벨별 "충격파 피해 증가", 기획서 v2.8 A-4 기준 곡선 10 → 115). 1이면 본편처럼 그대로.
    /// 공격력 증가 같은 피해% 합산과 섞이지 않게 따로 곱한다.
    /// </summary>
    public static float ShockWaveDamageScale { get; private set; } = 1f;

    /// <summary>true면 충격파 그림 색에 ShockWaveTint를 곱한다(포자 충격파의 초록 충격파). false(본편)면 원래 색 그대로.</summary>
    public static bool bShockWaveTinted { get; private set; }

    /// <summary>충격파 그림에 곱하는 색. bShockWaveTinted일 때만 쓴다.</summary>
    public static Color ShockWaveTint { get; private set; } = Color.white;

    /// <summary>
    /// true면 도끼 이외의 공격(회전베기 360도·충격파·부메랑·드론 레이저)도 IRunSkillHitTarget(로그라이크 별)을 친다(스텔라 III).
    /// false(본편)면 그런 대상은 예전처럼 건너뛴다.
    /// </summary>
    public static bool bSkillsHitRunTargets { get; private set; }

    /// <summary>도끼 공격이 맞힌 대상을 이어서 더 치는 횟수(계약 다다익선 "연속 2회 타격" = 1). 0이면 본편처럼 한 번.</summary>
    public static int AxeExtraStrikes { get; private set; }

    /// <summary>추가 타격 사이의 간격(초).</summary>
    public static float AxeExtraStrikeDelay { get; private set; } = 0.1f;

    /// <summary>
    /// 도끼 한 번에 맞히는 나무 수(범위 증가 패시브의 "도끼 동시 타격 수", 기획서 v2.6 부록 A-5). 휘두름 범위 안 가까운 순으로 이만큼 친다.
    /// 1 이하면 본편처럼 가장 가까운 한 그루만 친다.
    /// </summary>
    public static int AxeMaxTargets { get; private set; }

    /// <summary>true면 부메랑을 입력 방향이 아니라 가장 가까운 나무 방향으로 던진다(기획서 v2.7 6-1). false(본편)면 예전 그대로.</summary>
    public static bool bBoomerangAimAtTrees { get; private set; }

    /// <summary>
    /// 드론 레이저의 1차 타겟을 나무보다 먼저 고르는 창구(스텔라 III의 별, 기획서 v2.8 6-1). _origin에서 _range 안에 그런 대상이 있으면
    /// 그 대상을 맞힌 것으로 처리하고 true와 함께 바닥 위치(체인이 이어지는 자리)와 레이저가 꽂히는 지점을 돌려준다.
    /// </summary>
    public delegate bool DronePriorityTargetFinder(Vector3 _origin, float _range, out Vector3 _groundPosition, out Vector3 _aimPosition);

    /// <summary>null(본편)이면 드론은 예전처럼 노린 나무를 친다.</summary>
    public static DronePriorityTargetFinder DronePriorityTarget { get; private set; }

    public static void SetDronePriorityTarget(DronePriorityTargetFinder _finder)
    {
        DronePriorityTarget = _finder;
    }

    public static void SetAxeMaxTargets(int _count)
    {
        AxeMaxTargets = _count > 1 ? _count : 0;
    }

    public static void SetBoomerangAimAtTrees(bool _bOn)
    {
        bBoomerangAimAtTrees = _bOn;
    }

    public static void SetAxeExtraStrikes(int _count, float _delay)
    {
        AxeExtraStrikes = _count > 0 ? _count : 0;
        AxeExtraStrikeDelay = _delay > 0f ? _delay : 0f;
    }

    public static void SetWhirlwindSwingInterval(int _interval)
    {
        WhirlwindSwingInterval = _interval;
    }

    public static void SetWhirlwindMultipliers(float _damage, float _range)
    {
        WhirlwindDamageMultiplier = _damage > 0f ? _damage : 1f;
        WhirlwindRangeMultiplier = _range > 0f ? _range : 1f;
    }

    public static void SetShockWaveExtraCount(int _count, float _delay, float _damageMultiplier)
    {
        ShockWaveExtraCount = _count > 0 ? _count : 0;
        ShockWaveExtraDelay = _delay > 0f ? _delay : 0f;
        ShockWaveExtraDamageMultiplier = _damageMultiplier > 0f ? _damageMultiplier : 0f;
    }

    public static void SetShockWaveDamageScale(float _scale)
    {
        ShockWaveDamageScale = _scale > 0f ? _scale : 1f;
    }

    public static void SetShockWaveTint(bool _bTinted, Color _tint)
    {
        bShockWaveTinted = _bTinted;
        ShockWaveTint = _bTinted ? _tint : Color.white;
    }

    public static void SetSkillsHitRunTargets(bool _bOn)
    {
        bSkillsHitRunTargets = _bOn;
    }

    public static void ResetAll()
    {
        WhirlwindSwingInterval = 0;
        WhirlwindDamageMultiplier = 1f;
        WhirlwindRangeMultiplier = 1f;
        ShockWaveExtraCount = 0;
        ShockWaveExtraDelay = 0.12f;
        ShockWaveExtraDamageMultiplier = 1f;
        ShockWaveDamageScale = 1f;
        bShockWaveTinted = false;
        ShockWaveTint = Color.white;
        bSkillsHitRunTargets = false;
        AxeExtraStrikes = 0;
        AxeExtraStrikeDelay = 0.1f;
        AxeMaxTargets = 0;
        bBoomerangAimAtTrees = false;
        DronePriorityTarget = null;
    }
}
