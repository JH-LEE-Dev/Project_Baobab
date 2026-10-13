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
/// [켜기/끄기 스위치] 회전베기·다중 타격 같은 bool 스위치는 기준 상태가 "꺼짐"이라는 전제로 켰다 끈다.
/// 로그라이크는 본편 능력 트리를 불러오지 않으므로(BootStrap) 이 전제가 항상 성립한다.
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
        MultiAttack,
        Count,
    }

    private readonly float[] applied = new float[(int)EStat.Count];
    private readonly int[] appliedCounts = new int[(int)ECountStat.Count];
    private readonly bool[] appliedSwitches = new bool[(int)ESwitch.Count];
    private int appliedWhirlwindInterval;

    private Character character;
    private StatComponent stat;

    // 기획서 3-3 미정 항목. 공통 패시브 "공격속도 증가"가 도끼 공격 속도에도 붙는지. 기본은 붙는다(임시 카드 세트 기준).
    private bool bAttackSpeedAffectsAxe = true;
    private int baseWhirlwindInterval = 3;
    private float baseMaxStamina = 100f;

    public void Initialize(bool _bAttackSpeedAffectsAxe, int _baseWhirlwindInterval)
    {
        bAttackSpeedAffectsAxe = _bAttackSpeedAffectsAxe;
        baseWhirlwindInterval = Mathf.Max(1, _baseWhirlwindInterval);
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

        // // 피해량: 전역 + 각 스킬 전용
        float globalDamage = _mods.Get(ERunEffect.DamagePercent);
        SetValue(EStat.AxeDamage, globalDamage + _mods.Get(ERunEffect.DamagePercent, ERoguelikeSkill.Spin));
        SetValue(EStat.ShockWaveDamage, _mods.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave));
        SetValue(EStat.BoomerangDamage, _mods.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Boomerang));
        SetValue(EStat.DroneDamage, _mods.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Drone));

        // // 공격 속도
        float globalSpeed = _mods.Get(ERunEffect.AttackSpeedPercent);
        SetValue(EStat.AxeAttackSpeed, bAttackSpeedAffectsAxe ? globalSpeed : 0f);
        SetValue(EStat.BoomerangAttackSpeed, _mods.GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Boomerang));
        SetValue(EStat.DroneAttackSpeed, _mods.GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Drone));

        // // 범위. 충격파는 범위 스탯이 따로 없고 확장 속도 × 수명이 곧 범위다(본편 "충격파 범위" 노드와 같은 처리).
        float globalRange = _mods.Get(ERunEffect.RangePercent);
        SetValue(EStat.AxeRange, globalRange + _mods.Get(ERunEffect.RangePercent, ERoguelikeSkill.Spin));
        SetValue(EStat.BoomerangRange, _mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Boomerang));
        SetValue(EStat.DroneRange, _mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Drone));
        SetValue(EStat.ShockWaveSpeed, _mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Shockwave));

        // // 지속 시간
        SetValue(EStat.DroneDuration, _mods.GetForSkill(ERunEffect.DurationPercent, ERoguelikeSkill.Drone));

        // // 이동 속도
        SetValue(EStat.MoveSpeed, _mods.Get(ERunEffect.MoveSpeedPercent));

        // // 재사용 대기 감소(부메랑만 본편 스탯이 있다)
        SetValue(EStat.BoomerangCooldownReduction, _mods.GetForSkill(ERunEffect.CooldownReductionPercent, ERoguelikeSkill.Boomerang));

        // // 최대 스태미너. 밸런스 최대치와 캐릭터 기본값의 차이 + 카드 가산 + 카드 비율.
        float maxStaminaPercent = _mods.Get(ERunEffect.MaxStaminaPercent);
        float targetMax = Mathf.Max(1f, baseMaxStamina * (1f + maxStaminaPercent / 100f) + _mods.Get(ERunEffect.MaxStaminaAdd));
        SetValue(EStat.MaxStamina, targetMax - stat.baseMaxStamina);

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
        SetSwitch(ESwitch.MultiAttack, _mods.IsOn(ERunEffect.MultiHit));

        // // 회전베기 발동 간격(필요 휘두름 수). 데이터의 TriggerHitCountAdd로 줄이거나 늘린다.
        int interval = Mathf.Max(1, baseWhirlwindInterval + Mathf.RoundToInt(_mods.GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin)));
        if (interval != appliedWhirlwindInterval)
        {
            appliedWhirlwindInterval = interval;
            RunCombatOverrides.SetWhirlwindSwingInterval(interval);
        }

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

        appliedWhirlwindInterval = 0;
        RunCombatOverrides.ResetAll();

        character?.RefreshDronesForRun();
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
            case ESwitch.MultiAttack: stat.SetMultiAttack(_bOn); break;
        }

        appliedSwitches[idx] = _bOn;
    }
}
