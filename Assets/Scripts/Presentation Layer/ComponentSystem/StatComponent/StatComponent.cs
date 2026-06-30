using System;
using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH
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

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaIncreaseAlpha = 0f;
    public float staminaDecreaseAlpha = 0f;
    public float baseMaxStamina { get; private set; }
    public float maxStaminaBonus { get; private set; } = 0f;

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
    public float weakPointDamageMul = 1f;
    public float helloDamageMul = 1f;
    public bool bMultiAttack = false;
    public float finalAttackHealthPercent = 1f;
    public float attackRythmSpeedMul = 1f;
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
        speedDecreaseWhileAction -= (_amount / 100.0f);
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
        weakPointDamageMul = _amount;
    }

    public void IncreaseHelloDamage(float _amount)
    {
        helloDamageMul = _amount;
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
}
