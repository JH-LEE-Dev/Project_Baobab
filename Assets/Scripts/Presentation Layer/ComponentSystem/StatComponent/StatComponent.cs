using System;
using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH, ICharacterStatForNPC
{
    public event Action CanHuntEvent;

    [Header("For Debugging")]
    public int money = 0;

    [Header("Character Stat")]
    public float pickupRangeMultiplier = 1f;

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
    public float sourceOfSpeedAmount = 0f;

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

    [Header("Axe Settings")]
    public float axeDamage = 1f;
    public float speedDecreaseWhileAction = 0.5f;
    public float axeDurability = 30f;
    public float axeDurabilityDecAmount = 1f;
    public float axeAttackCoolTime = 1.2f;
    public float axeAttackRangeMultiplier = 1f;
    public float axeDurabilityDecIgnoreChance = 0f;
    public float baseAxeDamage { get; private set; }
    public float axeDamageMultiplier { get; private set; } = 1.0f;
    public float baseAxeAttackCoolTime { get; private set; }
    public float axeAttackSpeedMultiplier { get; private set; } = 1.0f;

    [Header("Axe - Shockwave")]
    public float shockWaveChance = 0f;
    public float shockWaveDamage = 1f;
    public float shockWaveSpeed = 2f;
    public float shockWaveDuration = 0.2f;
    public float shockWaveCreateDelay = 0f;
    public float baseShockWaveDamage { get; private set; }
    public float shockWaveDamageMultiplier { get; private set; } = 1.0f;
    public float baseShockWaveSpeed { get; private set; }
    public float shockWaveSpeedMultiplier { get; private set; } = 1.0f;
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
    public float baseBoomerangHitRadius { get; private set; }
    public float boomerangRangeMultiplier { get; private set; } = 1.0f;
    public float baseBoomerangMajorAxisRatio { get; private set; }
    public float boomerangDistanceMultiplier { get; private set; } = 1.0f;
    public float baseBoomerangCooldown { get; private set; }
    public float boomerangCooldownReductionAlpha = 0f;
    public float baseBoomerangDamageInterval { get; private set; }
    public float boomerangAttackSpeedMultiplier { get; private set; } = 1.0f;

    [Header("Axe - Drone")]
    public int droneCount = 0; // "드론" 스킬 레벨 = 던전 입장 시 캐릭터를 따라다니는 드론 개수 (0이면 미해금 상태로 소환되지 않음)
    public float droneDamage = 5f;
    public float droneAttackRange = 3f; // "범위" - 드론이 나무를 탐지/공격하는 반경
    public float droneActiveDuration = 3f; // "지속시간" - 공격 키를 누르면 활성화되는 시간
    public float droneDamageInterval = 1f; // "공격 속도"가 반영되는 판정 주기
    public float baseDroneDamage { get; private set; }
    public float droneDamageMultiplier { get; private set; } = 1.0f;
    public float baseDroneAttackRange { get; private set; }
    public float droneRangeMultiplier { get; private set; } = 1.0f;
    public float baseDroneActiveDuration { get; private set; }
    public float droneDurationMultiplier { get; private set; } = 1.0f;
    public float baseDroneDamageInterval { get; private set; }
    public float droneAttackSpeedMultiplier { get; private set; } = 1.0f;
    public int droneChainCount = 0; // "연쇄공격" - 드론의 공격이 주변 나무로 전이되는 횟수 (0이면 전이 없음)
    public float droneChainRange = 1.5f; // "연쇄공격 범위" - 전이 대상을 찾는 반경
    public float baseDroneChainRange { get; private set; }
    public float droneChainRangeMultiplier { get; private set; } = 1.0f;
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
    public float baseShotDelay { get; private set; }
    public float rifleAttackSpeedMultiplier { get; private set; } = 1.0f;
    public float baseReloadDuration { get; private set; }
    public float reloadSpeedMultiplier { get; private set; } = 1.0f;

    [Header("Rifle - Ricochet")]
    public int ricochetCnt = 0;
    public float ricochetAngle = 90f;
    public float ricochetDist = 0.5f;
    public float ricochetDamage = 1f;

    [Header("Attack")]
    public float weakPointDamageMul = 0f;
    public float helloDamageMul = 0f;
    public bool bMultiAttack = false;
    public float finalAttackHealthPercent = 0f;
    public float attackRythmSpeedMul = 0f;
    public bool bWhirlWind = false;

    [Header("Critical")]
    public float criticalChance = 0f;
    public float ciriticalDamageMul = 2f;

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
        axeDamageMultiplier += (_amount / 100.0f);
        axeDamage = baseAxeDamage * axeDamageMultiplier;
    }

    public void CanHunting()
    {
        bCanHunting = true;
        CanHuntEvent?.Invoke();
    }

    public void IncreaseSwitchSpeed(float _amount)
    {
        switchSpeedMultiplier += (_amount / 100.0f);
        weaponChangeCoolTime = baseWeaponChangeCoolTime / switchSpeedMultiplier;
    }

    public void IncreaseGunDamage(float _amount)
    {
        rifleDamageMultiplier += (_amount / 100.0f);
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
        speedDecreaseWhileAction += (_amount / 100.0f);
    }

    public void IncreaseShockWaveChance(float _amount)
    {
        shockWaveChance += _amount;
    }

    public void IncreaseShockWaveDamage(float _amount)
    {
        shockWaveDamageMultiplier += (_amount / 100.0f);
        shockWaveDamage = baseShockWaveDamage * shockWaveDamageMultiplier;
    }

    public void IncreaseShockWaveSpeed(float _amount)
    {
        shockWaveSpeedMultiplier += (_amount / 100.0f);
        shockWaveSpeed = baseShockWaveSpeed * shockWaveSpeedMultiplier;
    }

    public void IncreaseAxeRangeMultiplier(float _amount)
    {
        axeAttackRangeMultiplier += (_amount / 100.0f);
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
        pickupRangeMultiplier += (_amount / 100.0f);
    }

    public void IncreaseRicochetRange(float _amount)
    {
        ricochetAngle += ricochetAngle * (_amount / 100.0f);
        ricochetDist += ricochetDist * (_amount / 100.0f);
    }

    public void IncreaseRicochetDamage(float _amount)
    {
        ricochetDamage += _amount;
    }

    public void IncreaseReloadSpeed(float _amount)
    {
        reloadSpeedMultiplier += (_amount / 100.0f);
        reloadDuration = baseReloadDuration / reloadSpeedMultiplier;
    }

    public void IncreaseRifleAttackSpeed(float _amount)
    {
        rifleAttackSpeedMultiplier += (_amount / 100.0f);
        shotDelay = baseShotDelay / rifleAttackSpeedMultiplier;
    }

    public void IncreaseMovementSpeed(float _amount)
    {
        speedMultiplier += (_amount / 100.0f);
        originalSpeed = baseSpeed * speedMultiplier;
    }

    public void IncreaseAxeAttackSpeed(float _amount)
    {
        axeAttackSpeedMultiplier += (_amount / 100.0f);
        axeAttackCoolTime = baseAxeAttackCoolTime / axeAttackSpeedMultiplier;
    }

    public void IncreaseWeakPointDamageMul(float _amount)
    {
        weakPointDamageMul += _amount;
    }

    public void IncreaseHelloDamage(float _amount)
    {
        helloDamageMul += _amount;
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
        criticalChance += (_amount / 100.0f);
    }

    public void IncreaseCriticalDamage(float _amount)
    {
        ciriticalDamageMul += (_amount / 100.0f);
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
        sourceOfSpeedAmount += (_amount / 100.0f);
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
    private float starPathSpeedTimer = 0f;
    private float currentStarPathSpeedBonus = 0f;
    private Coroutine starPathSpeedCoroutine;

    public void IncreaseStarPathSpeedBoost(float _amount)
    {
        starPathSpeedBoostAmount += (_amount / 100.0f);
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
        boomerangDamageMultiplier += (_amount / 100.0f);
        boomerangDamage = baseBoomerangDamage * boomerangDamageMultiplier;
    }

    public void IncreaseBoomerangRange(float _amount)
    {
        boomerangRangeMultiplier += (_amount / 100.0f);
        boomerangHitRadius = baseBoomerangHitRadius * boomerangRangeMultiplier;
    }

    public void IncreaseBoomerangDistance(float _amount)
    {
        boomerangDistanceMultiplier += (_amount / 100.0f);
        boomerangMajorAxisRatio = baseBoomerangMajorAxisRatio * boomerangDistanceMultiplier;
    }

    public void IncreaseBoomerangCooldownReduction(float _amount)
    {
        boomerangCooldownReductionAlpha += _amount;
        boomerangCooldown = baseBoomerangCooldown * Mathf.Max(0f, 1f - (boomerangCooldownReductionAlpha / 100.0f));
    }

    public void IncreaseBoomerangAttackSpeed(float _amount)
    {
        boomerangAttackSpeedMultiplier += (_amount / 100.0f);
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
        droneDamageMultiplier += (_amount / 100.0f);
        droneDamage = baseDroneDamage * droneDamageMultiplier;
    }

    public void IncreaseDroneRange(float _amount)
    {
        droneRangeMultiplier += (_amount / 100.0f);
        droneAttackRange = baseDroneAttackRange * droneRangeMultiplier;
    }

    public void IncreaseDroneDuration(float _amount)
    {
        droneDurationMultiplier += (_amount / 100.0f);
        droneActiveDuration = baseDroneActiveDuration * droneDurationMultiplier;
    }

    public void IncreaseDroneAttackSpeed(float _amount)
    {
        droneAttackSpeedMultiplier += (_amount / 100.0f);
        droneDamageInterval = baseDroneDamageInterval / droneAttackSpeedMultiplier;
    }

    public void IncreaseDroneChainCount(int _amount)
    {
        droneChainCount += _amount;
    }

    public void IncreaseDroneChainRange(float _amount)
    {
        droneChainRangeMultiplier += (_amount / 100.0f);
        droneChainRange = baseDroneChainRange * droneChainRangeMultiplier;
    }

    public void ActivateDroneOverheatBoost(bool _boolean)
    {
        bDroneOverheatBoost = _boolean;
    }
}
