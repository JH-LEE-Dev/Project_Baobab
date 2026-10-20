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

    /// <summary>
    /// true면 도끼 이외의 공격(회전베기 360도·충격파·부메랑·드론 레이저)도 IRunSkillHitTarget(로그라이크 별)을 친다(스텔라 III).
    /// false(본편)면 그런 대상은 예전처럼 건너뛴다.
    /// </summary>
    public static bool bSkillsHitRunTargets { get; private set; }

    /// <summary>도끼 공격이 맞힌 대상을 이어서 더 치는 횟수(계약 다다익선 "연속 2회 타격" = 1). 0이면 본편처럼 한 번.</summary>
    public static int AxeExtraStrikes { get; private set; }

    /// <summary>추가 타격 사이의 간격(초).</summary>
    public static float AxeExtraStrikeDelay { get; private set; } = 0.1f;

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

    public static void SetShockWaveExtraCount(int _count, float _delay)
    {
        ShockWaveExtraCount = _count > 0 ? _count : 0;
        ShockWaveExtraDelay = _delay > 0f ? _delay : 0f;
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
        bSkillsHitRunTargets = false;
        AxeExtraStrikes = 0;
        AxeExtraStrikeDelay = 0.1f;
    }
}
