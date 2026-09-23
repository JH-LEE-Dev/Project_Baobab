using System;
using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH, ICharacterStatForNPC
{
    public event Action CanHuntEvent;

    [Header("For Debugging")]
    // 에디터에서 플레이를 누르기 전에 캐릭터 소지금을 원하는 값으로 맞춰두기 위한 치트 값이다.
    // bOverrideStartMoney를 켰을 때만 적용되며, UnitSystem이 에디터에서만 반영하므로 빌드에는 영향이 없다.
    [Tooltip("체크하면 게임 시작 시 캐릭터 소지금을 아래 값으로 강제한다. (에디터 전용)")]
    public bool bOverrideStartMoney = false;
    [Tooltip("bOverrideStartMoney가 켜져 있을 때 적용할 소지금. 이어하기로 들어가도 세이브 값 대신 이 값이 들어간다.")]
    public long startMoney = 0;

    [Header("Character Stat")]
    public float pickupRangeMultiplier = 1f;
    private PercentAccumulator pickupRangeAccum;

    [Header("Movement")]
    public float originalSpeed = 1f;
    public float speed => (activeActionCount > 0) ? originalSpeed * speedDecreaseWhileAction : originalSpeed;

    private int activeActionCount = 0;

    public void AddActionState()
    {
        activeActionCount++;
    }

    public void RemoveActionState()
    {
        activeActionCount--;
        if (activeActionCount < 0) activeActionCount = 0;
    }
    public float baseSpeed { get; private set; }
    public float speedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator speedAccum;
    public float sourceOfSpeedAmount = 0f;
    private PercentAccumulator sourceOfSpeedAmountAccum;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaIncreaseAlpha = 0f;
    public float staminaDecreaseAlpha = 0f;
    public float baseMaxStamina { get; private set; }
    public float maxStaminaBonus { get; private set; } = 0f;
    public float sourceOfStaminaRecoverAmount = 0f;
    private float currentSourceOfSpeedBonus = 0f;
    private Coroutine sourceOfSpeedCoroutine;
    public float staminaRecoverAmount = 0f;

    [Header("General Weapon Settings")]
    public float weaponChangeCoolTime = 0.5f;
    public bool bCanHunting = false;
    public float baseWeaponChangeCoolTime { get; private set; }
    public float switchSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator switchSpeedAccum;

    [Header("Axe Settings")]
    public float axeDamage = 1f;
    public float speedDecreaseWhileAction = 0.5f;
    private PercentAccumulator speedDecreaseWhileActionAccum;
    public float axeDurability = 30f;
    public float axeDurabilityDecAmount = 1f;
    public float axeAttackCoolTime = 1.2f;
    public float axeAttackRangeMultiplier = 1f;
    private PercentAccumulator axeAttackRangeAccum;
    public float axeDurabilityDecIgnoreChance = 0f;
    public float baseAxeDamage { get; private set; }
    public float axeDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator axeDamageAccum;
    public float baseAxeAttackCoolTime { get; private set; }
    public float axeAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator axeAttackSpeedAccum;

    [Header("Axe - Shockwave")]
    public float shockWaveChance = 0f;
    public float shockWaveDamage = 1f;
    public float shockWaveSpeed = 2f;
    public float shockWaveDuration = 0.2f;
    public float shockWaveCreateDelay = 0f;
    public float baseShockWaveDamage { get; private set; }
    public float shockWaveDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator shockWaveDamageAccum;
    public float baseShockWaveSpeed { get; private set; }
    public float shockWaveSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator shockWaveSpeedAccum;
    public bool bShockWaveCritical = false;
    public bool bShockWaveEnforcement = false;
    public bool bShockWaveMastery = false;
    public bool bOverheat = false;
    public bool bShockWaveOverheatBoost = false; // "화염 참격" 특성 - 과열 상태에서 충격파 폭발 효과 적용 여부

    [Header("Axe - Boomerang")]
    public int boomerangCount = 0; // "부메랑" 스킬 레벨 = 동시에 존재 가능한 부메랑 개수 (0이면 미해금 상태로 발사되지 않음)
    public float boomerangDamage = 1f;
    public float boomerangHitRadius = 0.5f; // "범위"
    public float boomerangMajorAxisRatio = 1f; // "사정거리" (CameraBoundsUtil 타원 장축 비율)
    public float boomerangCooldown = 2.5f; // "쿨타임"
    public float boomerangDamageInterval = 0.3f; // "공격 속도"가 반영되는 판정 주기
    public bool bBoomerangCritical = false;
    public bool bBoomerangOverheatBoost = false; // "화염 부메랑" 특성 - 과열 상태에서 부메랑 강화 적용 여부
    public float baseBoomerangDamage { get; private set; }
    public float boomerangDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangDamageAccum;
    public float baseBoomerangHitRadius { get; private set; }
    public float boomerangRangeMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangRangeAccum;
    public float baseBoomerangMajorAxisRatio { get; private set; }
    public float boomerangDistanceMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangDistanceAccum;
    public float baseBoomerangCooldown { get; private set; }
    public float boomerangCooldownReductionAlpha = 0f;
    public float baseBoomerangDamageInterval { get; private set; }
    public float boomerangAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangAttackSpeedAccum;

    [Header("Axe - Drone")]
    public int droneCount = 0; // "드론" 스킬 레벨 = 던전 입장 시 캐릭터를 따라다니는 드론 개수 (0이면 미해금 상태로 소환되지 않음)
    public float droneDamage = 5f;
    public float droneAttackRange = 3f; // "범위" - 드론이 나무를 탐지/공격하는 반경
    public float droneActiveDuration = 3f; // "지속시간" - 공격 키를 누르면 활성화되는 시간
    public float droneDamageInterval = 1f; // "공격 속도"가 반영되는 판정 주기
    public float baseDroneDamage { get; private set; }
    public float droneDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneDamageAccum;
    public float baseDroneAttackRange { get; private set; }
    public float droneRangeMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneRangeAccum;
    public float baseDroneActiveDuration { get; private set; }
    public float droneDurationMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneDurationAccum;
    public float baseDroneDamageInterval { get; private set; }
    public float droneAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneAttackSpeedAccum;
    public int droneChainCount = 0; // "연쇄공격" - 드론의 공격이 주변 나무로 전이되는 횟수 (0이면 전이 없음)
    public float droneChainRange = 1.5f; // "연쇄공격 범위" - 전이 대상을 찾는 반경
    public float baseDroneChainRange { get; private set; }
    public float droneChainRangeMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneChainRangeAccum;
    public bool bDroneOverheatBoost = false; // "드론 과부하" 특성 - 과열 상태에서 드론 강화 적용 여부

    [Header("Overheat")]
    public float overheatEfficiencyBonus = 0f; // "과열 강화" - 과열 버프(이동속도/공격속도/공격력) 효율 증가율(%). 100이면 기본 20%가 40%가 된다.
    public float overheatConsumptionReductionAlpha = 0f; // "과열 유지" - 과열 지속시간 소모 속도 감소율(%)
    public float overheatGainBonusAlpha = 0f; // "열기 포집" - 열기 접촉으로 얻는 과열 획득량 증가율(%)
    public float heatRecoveryAmount = 0f; // "열기 회수" - 과열 상태에서 나무 벌목 시 회복되는 과열 지속시간(초). 0이면 미해금
    public bool bOverheatPermanent = false; // "화신" - 항상 과열 상태를 유지

    [Header("Stamina Recovery")]
    public float recoveryPowerBonus = 0f; // "회복력" - 모든 피로도 회복 효과(전리품 포션, 체력의 원천, 휴식) 증가율(%)

    [Header("Rifle Settings")]
    public float rifleDamage = 10f;
    public float rifleReadyTime = 0;
    public float shotDelay = 1f;
    public int magCap = 2;
    public int ammoCap = 6;
    public float reloadDuration = 3f;
    public float gunPenetrationChance = 0f;
    public float baseRifleDamage { get; private set; }
    public float rifleDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator rifleDamageAccum;
    public float baseShotDelay { get; private set; }
    public float rifleAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator rifleAttackSpeedAccum;
    public float baseReloadDuration { get; private set; }
    public float reloadSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator reloadSpeedAccum;

    [Header("Rifle - Ricochet")]
    public int ricochetCnt = 0;
    public float ricochetAngle = 90f;
    public float ricochetDist = 0.5f;
    public float ricochetDamage = 1f;
    private ValueAccumulator ricochetDamageAccum;

    [Header("Attack")]
    public float weakPointDamageMul = 0f;
    private ValueAccumulator weakPointDamageAccum;
    public float helloDamageMul = 0f;
    private ValueAccumulator helloDamageAccum;
    public bool bMultiAttack = false;
    public float finalAttackHealthPercent = 0f;
    public float attackRythmSpeedMul = 0f;
    public bool bWhirlWind = false;

    [Header("Critical")]
    public float criticalChance = 0f;
    private PercentAccumulator criticalChanceAccum;
    public float ciriticalDamageMul = 2f;
    private PercentAccumulator ciriticalDamageMulAccum;

    // 인터페이스 구현 프로퍼티들
    float IStatComponent.speed => speed;
    float IStatComponent.weaponChangeCoolTime => weaponChangeCoolTime;

    float IStatComponent.axeDamage => axeDamage;
    float IStatComponent.axeDurability => axeDurability;
    float IStatComponent.axeDurabilityDecAmount => axeDurabilityDecAmount;
    float IStatComponent.axeAttackCoolTime => axeAttackCoolTime;

    float IStatComponent.rifleDamage => rifleDamage;
    float IStatComponent.rifleReadyTime => rifleReadyTime;
    float IStatComponent.afterShotTime => shotDelay;
    int IStatComponent.magCap => magCap;
    int IStatComponent.ammoCap => ammoCap;
    float IStatComponent.reloadDuration => reloadDuration;

    bool IStatComponent.bCanHunting => bCanHunting;

    // ICharacterStatForNPC 구현 - NPC(럼버잭 등)가 캐릭터와 동일한 셰이크웨이브 스탯을 그대로 참조할 때 사용
    float ICharacterStatForNPC.shockWaveChance => shockWaveChance;
    float ICharacterStatForNPC.shockWaveDamage => shockWaveDamage;
    float ICharacterStatForNPC.shockWaveSpeed => shockWaveSpeed;
    float ICharacterStatForNPC.shockWaveDuration => shockWaveDuration;
    float ICharacterStatForNPC.shockWaveCreateDelay => shockWaveCreateDelay;
    bool ICharacterStatForNPC.bShockWaveMastery => bShockWaveMastery;
    bool ICharacterStatForNPC.bShockWaveCritical => bShockWaveCritical;
    bool ICharacterStatForNPC.bShockWaveEnforcement => bShockWaveEnforcement;
    bool ICharacterStatForNPC.bShockWaveOverheatBoost => bShockWaveOverheatBoost;
    float ICharacterStatForNPC.criticalChance => criticalChance;
    float ICharacterStatForNPC.ciriticalDamageMul => ciriticalDamageMul;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        baseMaxStamina = maxStamina;
        baseSpeed = originalSpeed;
        baseAxeDamage = axeDamage;
        baseAxeAttackCoolTime = axeAttackCoolTime;
        baseRifleDamage = rifleDamage;
        baseShotDelay = shotDelay;
        baseWeaponChangeCoolTime = weaponChangeCoolTime;
        baseReloadDuration = reloadDuration;
        baseShockWaveDamage = shockWaveDamage;
        baseShockWaveSpeed = shockWaveSpeed;
        baseBoomerangDamage = boomerangDamage;
        baseBoomerangHitRadius = boomerangHitRadius;
        baseBoomerangMajorAxisRatio = boomerangMajorAxisRatio;
        baseBoomerangCooldown = boomerangCooldown;
        baseBoomerangDamageInterval = boomerangDamageInterval;
        baseDroneDamage = droneDamage;
        baseDroneAttackRange = droneAttackRange;
        baseDroneActiveDuration = droneActiveDuration;
        baseDroneDamageInterval = droneDamageInterval;
        baseDroneChainRange = droneChainRange;
    }

    public void IncreaseAxeDamage(float _amount)
    {
        axeDamageMultiplier = axeDamageAccum.Add(axeDamageMultiplier, _amount);
        axeDamage = baseAxeDamage * axeDamageMultiplier;
    }

    public void CanHunting()
    {
        bCanHunting = true;
        CanHuntEvent?.Invoke();
    }

    public void IncreaseSwitchSpeed(float _amount)
    {
        switchSpeedMultiplier = switchSpeedAccum.Add(switchSpeedMultiplier, _amount);
        weaponChangeCoolTime = baseWeaponChangeCoolTime / switchSpeedMultiplier;
    }

    public void IncreaseGunDamage(float _amount)
    {
        rifleDamageMultiplier = rifleDamageAccum.Add(rifleDamageMultiplier, _amount);
        rifleDamage = baseRifleDamage * rifleDamageMultiplier;
    }

    public void StaminaDecreaseAlpha(float _amount)
    {
        staminaDecreaseAlpha += _amount;
    }

    public void StaminaIncreaseAlpha(float _amount)
    {
        staminaIncreaseAlpha += _amount;
    }

    public void IncreaseMaxStamina(float _amount)
    {
        maxStaminaBonus += _amount;
        maxStamina = baseMaxStamina + maxStaminaBonus;
    }

    public void ResetSpeed()
    {
        activeActionCount = 0;
    }

    public void IncreaseAmmoCap(int _amount)
    {
        ammoCap += _amount;
    }

    public void IncreaseMagCap(int _amount)
    {
        magCap += _amount;
    }

    public void IncreaseGunPenetration(float _amount)
    {
        gunPenetrationChance += _amount;
    }

    public void IncreaseRicochetCnt(int _amount)
    {
        ricochetCnt += _amount;
    }

    public void IncreaseSpeedWhileAction(float _amount)
    {
        speedDecreaseWhileAction = speedDecreaseWhileActionAccum.Add(speedDecreaseWhileAction, _amount);
    }

    public void IncreaseShockWaveChance(float _amount)
    {
        shockWaveChance += _amount;
    }

    public void IncreaseShockWaveDamage(float _amount)
    {
        shockWaveDamageMultiplier = shockWaveDamageAccum.Add(shockWaveDamageMultiplier, _amount);
        shockWaveDamage = baseShockWaveDamage * shockWaveDamageMultiplier;
    }

    public void IncreaseShockWaveSpeed(float _amount)
    {
        shockWaveSpeedMultiplier = shockWaveSpeedAccum.Add(shockWaveSpeedMultiplier, _amount);
        shockWaveSpeed = baseShockWaveSpeed * shockWaveSpeedMultiplier;
    }

    public void IncreaseAxeRangeMultiplier(float _amount)
    {
        axeAttackRangeMultiplier = axeAttackRangeAccum.Add(axeAttackRangeMultiplier, _amount);
    }

    public void IncreaseAxeDurability(float _amount)
    {
        axeDurability += _amount;
    }

    public void IncreaseAxeDurabilityDecIgnoreChance(float _amount)
    {
        axeDurabilityDecIgnoreChance += _amount;
    }

    public void IncreasePickupRange(float _amount)
    {
        pickupRangeMultiplier = pickupRangeAccum.Add(pickupRangeMultiplier, _amount);
    }

    public void IncreaseRicochetRange(float _amount)
    {
        ricochetAngle += ricochetAngle * (_amount / 100.0f);
        ricochetDist += ricochetDist * (_amount / 100.0f);
    }

    public void IncreaseRicochetDamage(float _amount)
    {
        ricochetDamage = ricochetDamageAccum.Add(ricochetDamage, _amount);
    }

    public void IncreaseReloadSpeed(float _amount)
    {
        reloadSpeedMultiplier = reloadSpeedAccum.Add(reloadSpeedMultiplier, _amount);
        reloadDuration = baseReloadDuration / reloadSpeedMultiplier;
    }

    public void IncreaseRifleAttackSpeed(float _amount)
    {
        rifleAttackSpeedMultiplier = rifleAttackSpeedAccum.Add(rifleAttackSpeedMultiplier, _amount);
        shotDelay = baseShotDelay / rifleAttackSpeedMultiplier;
    }

    public void IncreaseMovementSpeed(float _amount)
    {
        speedMultiplier = speedAccum.Add(speedMultiplier, _amount);
        originalSpeed = baseSpeed * speedMultiplier;
    }

    public void IncreaseAxeAttackSpeed(float _amount)
    {
        axeAttackSpeedMultiplier = axeAttackSpeedAccum.Add(axeAttackSpeedMultiplier, _amount);
        axeAttackCoolTime = baseAxeAttackCoolTime / axeAttackSpeedMultiplier;
    }

    public void IncreaseWeakPointDamageMul(float _amount)
    {
        weakPointDamageMul = weakPointDamageAccum.Add(weakPointDamageMul, _amount);
    }

    public void IncreaseHelloDamage(float _amount)
    {
        helloDamageMul = helloDamageAccum.Add(helloDamageMul, _amount);
    }

    public void SetMultiAttack(bool _boolean)
    {
        bMultiAttack = _boolean;
    }

    public void SetFinalAttackHealthPercent(float _percent)
    {
        finalAttackHealthPercent = (_percent / 100.0f);
    }

    public void SetAttackRythmSpeedAmount(float _percent)
    {
        attackRythmSpeedMul = _percent;
    }

    public void ActivateWhirlWind(bool _boolean)
    {
        bWhirlWind = _boolean;
    }

    public void IncreaseCriticalChance(float _amount)
    {
        criticalChance = criticalChanceAccum.Add(criticalChance, _amount);
    }

    public void IncreaseCriticalDamage(float _amount)
    {
        ciriticalDamageMul = ciriticalDamageMulAccum.Add(ciriticalDamageMul, _amount);
    }

    public void ActivateShockWaveCritical(bool _boolean)
    {
        bShockWaveCritical = _boolean;
    }

    public void ActivateShockWaveEnforcement(bool _boolean)
    {
        bShockWaveEnforcement = _boolean;
    }

    public void ShockWaveMastery(bool _boolean)
    {
        bShockWaveMastery = _boolean;
    }

    public void ActivateOverheat(bool _boolean)
    {
        bOverheat = _boolean;
    }

    public void ActivateShockWaveOverheatBoost(bool _boolean)
    {
        bShockWaveOverheatBoost = _boolean;
    }

    public void IncreaseOverheatEfficiency(float _amount)
    {
        overheatEfficiencyBonus += _amount;
    }

    public void IncreaseOverheatConsumptionReduction(float _amount)
    {
        overheatConsumptionReductionAlpha += _amount;
    }

    public void IncreaseOverheatGainBonus(float _amount)
    {
        overheatGainBonusAlpha += _amount;
    }

    public void IncreaseHeatRecoveryAmount(float _amount)
    {
        heatRecoveryAmount += _amount;
    }

    public void ActivateOverheatPermanent(bool _boolean)
    {
        bOverheatPermanent = _boolean;
    }

    public void IncreaseRecoveryPower(float _amount)
    {
        recoveryPowerBonus += _amount;
    }

    public void IncreaseSourceOfStaminaRecoverAmount(float _amount)
    {
        sourceOfStaminaRecoverAmount += _amount;
    }

    public void IncreaseSourceOfSpeedAmount(float _amount)
    {
        sourceOfSpeedAmount = sourceOfSpeedAmountAccum.Add(sourceOfSpeedAmount, _amount);
    }

    private float sourceOfSpeedTimer = 0f;

    public void ActivateSourceOfSpeed()
    {
        if (sourceOfSpeedAmount <= 0) return;

        sourceOfSpeedTimer = 3f;

        if (sourceOfSpeedCoroutine == null)
        {
            sourceOfSpeedCoroutine = StartCoroutine(SourceOfSpeedRoutine());
        }
    }

    private System.Collections.IEnumerator SourceOfSpeedRoutine()
    {
        currentSourceOfSpeedBonus = sourceOfSpeedAmount;
        IncreaseMovementSpeed(currentSourceOfSpeedBonus * 100.0f);

        while (sourceOfSpeedTimer > 0f)
        {
            sourceOfSpeedTimer -= Time.deltaTime;
            yield return null;
        }

        IncreaseMovementSpeed(-currentSourceOfSpeedBonus * 100.0f);
        currentSourceOfSpeedBonus = 0f;
        sourceOfSpeedCoroutine = null;
    }

    public void Reset()
    {
        if (sourceOfSpeedCoroutine != null)
        {
            StopCoroutine(sourceOfSpeedCoroutine);
            IncreaseMovementSpeed(-currentSourceOfSpeedBonus * 100.0f);
            currentSourceOfSpeedBonus = 0f;
            sourceOfSpeedCoroutine = null;
        }

        if (starPathSpeedCoroutine != null)
        {
            StopCoroutine(starPathSpeedCoroutine);
            IncreaseMovementSpeed(-currentStarPathSpeedBonus * 100.0f);
            currentStarPathSpeedBonus = 0f;
            starPathSpeedCoroutine = null;
        }
    }

    // 별길 걸음 - 별 표식 나무 벌목 시 일정 시간 이동속도 증가 (SourceOfSpeed와 별개의 타이머로 관리)
    public float starPathSpeedBoostAmount = 0f;
    private PercentAccumulator starPathSpeedBoostAmountAccum;
    private float starPathSpeedTimer = 0f;
    private float currentStarPathSpeedBonus = 0f;
    private Coroutine starPathSpeedCoroutine;

    public void IncreaseStarPathSpeedBoost(float _amount)
    {
        starPathSpeedBoostAmount = starPathSpeedBoostAmountAccum.Add(starPathSpeedBoostAmount, _amount);
    }

    public void ActivateStarPathSpeedBoost()
    {
        if (starPathSpeedBoostAmount <= 0f) return;

        starPathSpeedTimer = 5f;

        if (starPathSpeedCoroutine == null)
        {
            starPathSpeedCoroutine = StartCoroutine(StarPathSpeedRoutine());
        }
    }

    private System.Collections.IEnumerator StarPathSpeedRoutine()
    {
        // 버프가 지속되는 동안 스킬 레벨업으로 amount가 바뀌어도 더한 만큼만 정확히 되돌리도록 스냅샷을 사용한다.
        currentStarPathSpeedBonus = starPathSpeedBoostAmount;
        IncreaseMovementSpeed(currentStarPathSpeedBonus * 100.0f);

        while (starPathSpeedTimer > 0f)
        {
            starPathSpeedTimer -= Time.deltaTime;
            yield return null;
        }

        IncreaseMovementSpeed(-currentStarPathSpeedBonus * 100.0f);
        currentStarPathSpeedBonus = 0f;
        starPathSpeedCoroutine = null;
    }

    public void IncreaseStaminaRecoverAmount(float _amount)
    {
        staminaRecoverAmount = _amount;
    }

    public void IncreaseBoomerangCount(int _amount)
    {
        boomerangCount += _amount;
    }

    public void IncreaseBoomerangDamage(float _amount)
    {
        boomerangDamageMultiplier = boomerangDamageAccum.Add(boomerangDamageMultiplier, _amount);
        boomerangDamage = baseBoomerangDamage * boomerangDamageMultiplier;
    }

    public void IncreaseBoomerangRange(float _amount)
    {
        boomerangRangeMultiplier = boomerangRangeAccum.Add(boomerangRangeMultiplier, _amount);
        boomerangHitRadius = baseBoomerangHitRadius * boomerangRangeMultiplier;
    }

    public void IncreaseBoomerangDistance(float _amount)
    {
        boomerangDistanceMultiplier = boomerangDistanceAccum.Add(boomerangDistanceMultiplier, _amount);
        boomerangMajorAxisRatio = baseBoomerangMajorAxisRatio * boomerangDistanceMultiplier;
    }

    public void IncreaseBoomerangCooldownReduction(float _amount)
    {
        boomerangCooldownReductionAlpha += _amount;
        boomerangCooldown = baseBoomerangCooldown * Mathf.Max(0f, 1f - (boomerangCooldownReductionAlpha / 100.0f));
    }

    public void IncreaseBoomerangAttackSpeed(float _amount)
    {
        boomerangAttackSpeedMultiplier = boomerangAttackSpeedAccum.Add(boomerangAttackSpeedMultiplier, _amount);
        boomerangDamageInterval = baseBoomerangDamageInterval / boomerangAttackSpeedMultiplier;
    }

    public void ActivateBoomerangCritical(bool _boolean)
    {
        bBoomerangCritical = _boolean;
    }

    public void ActivateBoomerangOverheatBoost(bool _boolean)
    {
        bBoomerangOverheatBoost = _boolean;
    }

    public void IncreaseDroneCount(int _amount)
    {
        droneCount += _amount;
    }

    public void IncreaseDroneDamage(float _amount)
    {
        droneDamageMultiplier = droneDamageAccum.Add(droneDamageMultiplier, _amount);
        droneDamage = baseDroneDamage * droneDamageMultiplier;
    }

    public void IncreaseDroneRange(float _amount)
    {
        droneRangeMultiplier = droneRangeAccum.Add(droneRangeMultiplier, _amount);
        droneAttackRange = baseDroneAttackRange * droneRangeMultiplier;
    }

    public void IncreaseDroneDuration(float _amount)
    {
        droneDurationMultiplier = droneDurationAccum.Add(droneDurationMultiplier, _amount);
        droneActiveDuration = baseDroneActiveDuration * droneDurationMultiplier;
    }

    public void IncreaseDroneAttackSpeed(float _amount)
    {
        droneAttackSpeedMultiplier = droneAttackSpeedAccum.Add(droneAttackSpeedMultiplier, _amount);
        droneDamageInterval = baseDroneDamageInterval / droneAttackSpeedMultiplier;
    }

    public void IncreaseDroneChainCount(int _amount)
    {
        droneChainCount += _amount;
    }

    public void IncreaseDroneChainRange(float _amount)
    {
        droneChainRangeMultiplier = droneChainRangeAccum.Add(droneChainRangeMultiplier, _amount);
        droneChainRange = baseDroneChainRange * droneChainRangeMultiplier;
    }

    public void ActivateDroneOverheatBoost(bool _boolean)
    {
        bDroneOverheatBoost = _boolean;
    }
}
