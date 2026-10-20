using System;
using UnityEngine;

/// <summary>
/// 빌드 효과 합계(RunModifiers)를 캐릭터 스탯에 반영한다. 기획서 3-3 "구현 시 주의: 런 단위 스킬 부여".
///
/// [변화량 장부 방식]
/// 스탯마다 "지금까지 이 판이 더한 양"을 기록해 두고, 새 합계와의 차이만큼만 Increase를 부른다.
/// 판이 끝나면 RemoveAll이 기록한 양을 그대로 빼서 캐릭터를 시작 상태로 돌린다.
/// StatComponent의 누산기(PercentAccumulator)는 +x 뒤 -x가 정확히 원래 값으로 돌아오므로 오차가 쌓이지 않는다.
///
/// 본편 능력 트리 경로(SkillManager/SkillDispatcher)는 전혀 거치지 않는다. 노드 레벨·툴팁 누적값·스킬 SO를
/// 건드리지 않으므로 세이브와 마을 UI에 흔적이 남지 않는다. (로그라이크 세션은 애초에 저장도 막혀 있다)
///
/// [켜기/끄기 스위치] 회전베기 같은 bool 스위치는 기준 상태가 "꺼짐"이라는 전제로 켰다 끈다.
/// 로그라이크는 본편 능력 트리를 불러오지 않으므로(BootStrap) 이 전제가 항상 성립한다.
/// 도끼는 항상 단일 타격이다(기획서 3-1). 다중 타격 스위치는 켜지 않는다.
///
/// [전투 기준값] 런의 도끼 공격력 10 같은 기준값(부록 A-1)은 본편 캐릭터 프리팹 값 위에 덮어 끼우고,
/// RemoveAll에서 끼우기 전 값으로 되돌린다. 증가 배율 장부와는 별개다.
/// </summary>
public sealed class RunEffectApplier
{
    private enum EStat
    {
        AxeDamage,
        ShockWaveDamage,
        BoomerangDamage,
        DroneDamage,
        AxeAttackSpeed,
        BoomerangAttackSpeed,
        DroneAttackSpeed,
        AxeRange,
        BoomerangRange,
        DroneRange,
        ShockWaveSpeed,
        DroneDuration,
        BoomerangDistance,
        MoveSpeed,
        ShockWaveChance,
        BoomerangCooldownReduction,
        MaxStamina,
        Count,
    }

    private enum ECountStat
    {
        BoomerangCount,
        DroneCount,
        DroneChainCount,
        Count,
    }

    private enum ESwitch
    {
        WhirlWind,
        Count,
    }

    private readonly float[] applied = new float[(int)EStat.Count];
    private readonly int[] appliedCounts = new int[(int)ECountStat.Count];
    private readonly bool[] appliedSwitches = new bool[(int)ESwitch.Count];
    private int appliedWhirlwindInterval;

    private Character character;
    private StatComponent stat;

    // 공통 패시브 "공격속도 증가"가 도끼 공격 속도에도 붙는지. 기획서 6-3은 붙는다.
    private bool bAttackSpeedAffectsAxe = true;
    // 회전베기 기본 "필요 휘두름" 수(I = 3휘두름마다 → 4번째 공격에 발동, 기획서 v2.4 6-1).
    private int spinRequiredSwings = 3;
    private float baseMaxStamina = 100f;

    private float maxMoveSpeedPercent;
    private float shockwaveExtraDelay = 0.12f;
    private float axeExtraStrikeDelay = 0.1f;

    private bool bHasRunBaseStats;
    private StatComponent.CombatBaseStats runBaseStats;
    // 기준값을 끼운 캐릭터와 끼우기 전 값. RemoveAll이 이 캐릭터에 되돌린다.
    private StatComponent injectedStat;
    private StatComponent.CombatBaseStats originalBaseStats;

    public void Initialize(bool _bAttackSpeedAffectsAxe, int _spinRequiredSwings)
    {
        bAttackSpeedAffectsAxe = _bAttackSpeedAffectsAxe;
        spinRequiredSwings = Mathf.Max(0, _spinRequiredSwings);
    }

    /// <summary>충격파 추가 발생·도끼 연속 타격 사이의 간격(초). 튜닝 SO 값.</summary>
    public void SetRepeatDelays(float _shockwaveExtraDelay, float _axeExtraStrikeDelay)
    {
        shockwaveExtraDelay = Mathf.Max(0f, _shockwaveExtraDelay);
        axeExtraStrikeDelay = Mathf.Max(0f, _axeExtraStrikeDelay);
    }

    /// <summary>런 동안 쓸 전투 기준값(도끼 공격력 10 등). 다음 Apply에서 캐릭터에 끼운다.</summary>
    public void SetRunBaseCombatStats(in StatComponent.CombatBaseStats _stats)
    {
        runBaseStats = _stats;
        bHasRunBaseStats = true;
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
        stat = null != _character ? _character.statComponent : null;
    }

    /// <summary>밸런스의 최대 스태미너. 캐릭터 기본 최대치와의 차이를 "기준 보정"으로 장부에 함께 올린다.</summary>
    public void SetBaseMaxStamina(float _maxStamina)
    {
        baseMaxStamina = Mathf.Max(1f, _maxStamina);
    }

    /// <summary>현재 합계로 스탯을 맞춘다. 카드를 살 때마다, 그리고 런 시작 시 부른다.</summary>
    public void Apply(RunModifiers _mods)
    {
        if (null == stat || null == _mods) return;

        // 전투 기준값은 증가 배율보다 먼저 끼운다. SetCombatBaseStats가 쌓인 배율을 다시 곱해 현재 값을 맞춘다.
        if (bHasRunBaseStats && injectedStat != stat)
        {
            RestoreBaseStats();
            originalBaseStats = stat.GetCombatBaseStats();
            stat.SetCombatBaseStats(in runBaseStats);
            injectedStat = stat;
        }

        ApplyDamageAndSpeed(_mods);

        // // 범위. 충격파는 범위 스탯이 따로 없고 확장 속도 × 수명이 곧 범위다(본편 "충격파 범위" 노드와 같은 처리).
        // 도끼 범위는 공통 패시브(전역)만. 회전베기 카드의 범위 증가는 회전베기 타격에만 붙는다(아래 회전베기 배율).
        float globalRange = _mods.Get(ERunEffect.RangePercent);
        SetValue(EStat.AxeRange, globalRange);
        SetValue(EStat.BoomerangRange, _mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Boomerang));
        SetValue(EStat.DroneRange, _mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Drone));
        SetValue(EStat.ShockWaveSpeed, _mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Shockwave));

        // // 지속 시간
        SetValue(EStat.DroneDuration, _mods.GetForSkill(ERunEffect.DurationPercent, ERoguelikeSkill.Drone));
        // 부메랑의 "유지 시간"은 한 번 왕복하며 날아 있는 시간이고, 본편에서는 사거리(타원 장축 비율)가 그 길이를 정한다.
        // 부메랑 전용 값만 쓴다 - 공통 패시브 "유지 시간 증가"의 대상(오염·별 표식·별 생존)에는 부메랑이 없다(기획서 6-3).
        SetValue(EStat.BoomerangDistance, _mods.Get(ERunEffect.DurationPercent, ERoguelikeSkill.Boomerang));

        // // 이동 속도. 계약 등과 겹쳐도 합산 상한을 넘지 않는다(기획서 6-3 "합산 상한", 부록 A-5 +60%).
        float moveSpeed = _mods.Get(ERunEffect.MoveSpeedPercent);
        if (maxMoveSpeedPercent > 0f) moveSpeed = Mathf.Min(moveSpeed, maxMoveSpeedPercent);
        SetValue(EStat.MoveSpeed, moveSpeed);

        // // 재사용 대기 감소(부메랑만 본편 스탯이 있다)
        SetValue(EStat.BoomerangCooldownReduction, _mods.GetForSkill(ERunEffect.CooldownReductionPercent, ERoguelikeSkill.Boomerang));

        // // 최대 스태미너. 밸런스 최대치와 캐릭터 기본값의 차이 + 카드 가산 + 카드 비율.
        float maxStaminaPercent = _mods.Get(ERunEffect.MaxStaminaPercent);
        float targetMax = Mathf.Max(1f, baseMaxStamina * (1f + maxStaminaPercent / 100f) + _mods.Get(ERunEffect.MaxStaminaAdd));
        SetValue(EStat.MaxStamina, targetMax - stat.baseMaxStamina);
        // 체력 컴포넌트의 최대치는 본편에선 던전 입장 때만 맞춘다. 런 도중 카드로 바뀌면 여기서 바로 반영한다(HUD 바도 이 값을 읽는다).
        character?.SyncMaxStaminaForRun();

        // // 충격파: 본편과 같은 발동 조건(휘두를 때 확률). 확률은 카드 데이터의 ChancePercent로만 정해진다.
        bool bShockwave = _mods.IsSkillActive(ERoguelikeSkill.Shockwave);
        SetValue(EStat.ShockWaveChance, bShockwave ? _mods.GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Shockwave) : 0f);

        // // 개수 계열. 스킬이 꺼져 있으면 0개 = 미해금.
        int globalCount = Mathf.RoundToInt(_mods.Get(ERunEffect.CountAdd));

        bool bBoomerang = _mods.IsSkillActive(ERoguelikeSkill.Boomerang);
        SetCount(ECountStat.BoomerangCount, bBoomerang ? 1 + globalCount + Mathf.RoundToInt(_mods.Get(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang)) : 0);

        bool bDrone = _mods.IsSkillActive(ERoguelikeSkill.Drone);
        SetCount(ECountStat.DroneCount, bDrone ? 1 + globalCount + Mathf.RoundToInt(_mods.Get(ERunEffect.CountAdd, ERoguelikeSkill.Drone)) : 0);
        SetCount(ECountStat.DroneChainCount, bDrone ? Mathf.RoundToInt(_mods.GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)) : 0);

        // // 스위치
        SetSwitch(ESwitch.WhirlWind, _mods.IsSkillActive(ERoguelikeSkill.Spin));

        // // 회전베기 빈도 사다리(기획서 v2.4 6-1): 필요 휘두름 3 → 2 → 1 → 0(상시). n휘두름 뒤 n+1번째 공격에 발동하므로
        // 본편 공격 코드의 발동 주기는 n + 1이다(1 = 공격할 때마다). 데이터의 TriggerHitCountAdd가 n을 줄인다.
        int requiredSwings = Mathf.Max(0, spinRequiredSwings + Mathf.RoundToInt(_mods.GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin)));
        int interval = requiredSwings + 1;
        if (interval != appliedWhirlwindInterval)
        {
            appliedWhirlwindInterval = interval;
            RunCombatOverrides.SetWhirlwindSwingInterval(interval);
        }

        // 회전베기 카드의 회전 피해·범위 증가는 회전베기 타격에만 곱한다(일반 도끼 공격에는 붙지 않는다).
        RunCombatOverrides.SetWhirlwindMultipliers(
            Mathf.Max(0f, 1f + _mods.Get(ERunEffect.DamagePercent, ERoguelikeSkill.Spin) / 100f),
            Mathf.Max(0.1f, 1f + _mods.Get(ERunEffect.RangePercent, ERoguelikeSkill.Spin) / 100f));

        // // 레벨 효과의 반복 수(부록 A-4). 본편 공격 코드는 덮어쓰기 값이 0/false일 때 예전 그대로다(RunCombatOverrides).
        // 충격파 MAX 2중 발생, 스텔라 III 다른 액티브의 별 타격.

        int extraShockwaves = _mods.IsSkillActive(ERoguelikeSkill.Shockwave) ? Mathf.RoundToInt(_mods.GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave)) : 0;
        RunCombatOverrides.SetShockWaveExtraCount(extraShockwaves, shockwaveExtraDelay);

        RunCombatOverrides.SetSkillsHitRunTargets(IsStarHitByActives(_mods));

        // 계약 다다익선: 도끼 공격 연속 2회 타격.
        RunCombatOverrides.SetAxeExtraStrikes(Mathf.RoundToInt(_mods.Get(ERunEffect.AxeExtraStrikeAdd)), axeExtraStrikeDelay);

        character?.RefreshDronesForRun();
    }

    /// <summary>이 판이 더한 것을 전부 뺀다. 캐릭터가 런 시작 전 상태로 돌아간다.</summary>
    public void RemoveAll()
    {
        if (null != stat)
        {
            for (int i = 0; i < applied.Length; i++) SetValue((EStat)i, 0f);
            for (int i = 0; i < appliedCounts.Length; i++) SetCount((ECountStat)i, 0);
            for (int i = 0; i < appliedSwitches.Length; i++) SetSwitch((ESwitch)i, false);
        }
        else
        {
            Array.Clear(applied, 0, applied.Length);
            Array.Clear(appliedCounts, 0, appliedCounts.Length);
            Array.Clear(appliedSwitches, 0, appliedSwitches.Length);
        }

        RestoreBaseStats();

        appliedWhirlwindInterval = 0;
        RunCombatOverrides.ResetAll();

        character?.SyncMaxStaminaForRun();
        character?.RefreshDronesForRun();
    }

    /// <summary>
    /// 피해량과 공격 속도만 다시 맞춘다. 전투 중 조건부 보너스(임계치 초과·이동 중)가 바뀔 때 RoguelikeSystem이 부른다.
    /// 전체 Apply는 드론 재배치·최대 스태미너 동기화까지 하므로 매 프레임 부르기에는 무겁다.
    /// </summary>
    public void ApplyDamageAndSpeed(RunModifiers _mods)
    {
        if (null == stat || null == _mods) return;

        // // 피해량: 전역(+ 조건부) + 각 스킬 전용. 도끼는 전역만(회전베기 전용 피해는 회전베기 배율로 따로 곱한다).
        float globalDamage = _mods.GlobalDamagePercent;
        SetValue(EStat.AxeDamage, globalDamage);
        SetValue(EStat.ShockWaveDamage, globalDamage + _mods.Get(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave));
        SetValue(EStat.BoomerangDamage, globalDamage + _mods.Get(ERunEffect.DamagePercent, ERoguelikeSkill.Boomerang));
        SetValue(EStat.DroneDamage, globalDamage + _mods.Get(ERunEffect.DamagePercent, ERoguelikeSkill.Drone));

        // // 공격 속도: 전역(+ 조건부) + 각 스킬 전용
        float globalSpeed = _mods.GlobalAttackSpeedPercent;
        SetValue(EStat.AxeAttackSpeed, bAttackSpeedAffectsAxe ? globalSpeed : 0f);
        SetValue(EStat.BoomerangAttackSpeed, globalSpeed + _mods.Get(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Boomerang));
        SetValue(EStat.DroneAttackSpeed, globalSpeed + _mods.Get(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Drone));
    }

    /// <summary>이동속도 합산 상한(%). 0이면 상한 없음.</summary>
    public void SetMoveSpeedCap(float _maxPercent)
    {
        maxMoveSpeedPercent = Mathf.Max(0f, _maxPercent);
    }

    /// <summary>스텔라 III "다른 액티브 스킬도 별을 공격". 스텔라 기본 능력이 켜져 있을 때만(스텔라+드론처럼 별을 공격하지 않는 조합에서는 꺼짐).</summary>
    public static bool IsStarHitByActives(RunModifiers _mods)
    {
        return null != _mods && _mods.IsSkillActive(ERoguelikeSkill.Stella) && _mods.GetForSkill(ERunEffect.StarHitByActives, ERoguelikeSkill.Stella) > 0f;
    }

    /// <summary>끼워 둔 전투 기준값을 끼우기 전 값으로 되돌린다.</summary>
    private void RestoreBaseStats()
    {
        if (null == injectedStat) return;

        injectedStat.SetCombatBaseStats(in originalBaseStats);
        injectedStat = null;
    }

    private void SetValue(EStat _stat, float _target)
    {
        int idx = (int)_stat;
        float delta = _target - applied[idx];
        if (Mathf.Approximately(delta, 0f)) return;

        switch (_stat)
        {
            case EStat.AxeDamage: stat.IncreaseAxeDamage(delta); break;
            case EStat.ShockWaveDamage: stat.IncreaseShockWaveDamage(delta); break;
            case EStat.BoomerangDamage: stat.IncreaseBoomerangDamage(delta); break;
            case EStat.DroneDamage: stat.IncreaseDroneDamage(delta); break;
            case EStat.AxeAttackSpeed: stat.IncreaseAxeAttackSpeed(delta); break;
            case EStat.BoomerangAttackSpeed: stat.IncreaseBoomerangAttackSpeed(delta); break;
            case EStat.DroneAttackSpeed: stat.IncreaseDroneAttackSpeed(delta); break;
            case EStat.AxeRange: stat.IncreaseAxeRangeMultiplier(delta); break;
            case EStat.BoomerangRange: stat.IncreaseBoomerangRange(delta); break;
            case EStat.DroneRange: stat.IncreaseDroneRange(delta); break;
            case EStat.ShockWaveSpeed: stat.IncreaseShockWaveSpeed(delta); break;
            case EStat.DroneDuration: stat.IncreaseDroneDuration(delta); break;
            case EStat.BoomerangDistance: stat.IncreaseBoomerangDistance(delta); break;
            case EStat.MoveSpeed: stat.IncreaseMovementSpeed(delta); break;
            case EStat.ShockWaveChance: stat.IncreaseShockWaveChance(delta); break;
            case EStat.BoomerangCooldownReduction: stat.IncreaseBoomerangCooldownReduction(delta); break;
            case EStat.MaxStamina: stat.IncreaseMaxStamina(delta); break;
        }

        applied[idx] = _target;
    }

    private void SetCount(ECountStat _stat, int _target)
    {
        int idx = (int)_stat;
        int delta = _target - appliedCounts[idx];
        if (0 == delta) return;

        switch (_stat)
        {
            case ECountStat.BoomerangCount: stat.IncreaseBoomerangCount(delta); break;
            case ECountStat.DroneCount: stat.IncreaseDroneCount(delta); break;
            case ECountStat.DroneChainCount: stat.IncreaseDroneChainCount(delta); break;
        }

        appliedCounts[idx] = _target;
    }

    private void SetSwitch(ESwitch _switch, bool _bOn)
    {
        int idx = (int)_switch;
        if (appliedSwitches[idx] == _bOn) return;

        switch (_switch)
        {
            case ESwitch.WhirlWind: stat.ActivateWhirlWind(_bOn); break;
        }

        appliedSwitches[idx] = _bOn;
    }
}
