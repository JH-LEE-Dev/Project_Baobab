using System;
using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH
{
    public event Action CanHuntEvent;

    [Header("Movement")]
    public float speed = 1f;
    public float originalSpeed = 1f;

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

    [Header("Axe - Shockwave")]
    public float shockWaveChance = 0f;
    public float shockWaveDamage = 1f;
    public float shockWaveDuration = 0f;
    public float shockWaveCreateDelay = 0.3f;

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

    [Header("Rifle - Ricochet")]
    public int ricochetCnt = 0;
    public float ricochetAngle = 90f;
    public float ricochetDist = 0.5f;
    public float ricochetDamage = 1f;

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
        baseAxeDamage = axeDamage;
        baseRifleDamage = rifleDamage;
        baseWeaponChangeCoolTime = weaponChangeCoolTime;
    }

    public void IncreaseAxeDamage(float _amount)
    {
        axeDamageMultiplier += (_amount / 100.0f);
        axeDamage = baseAxeDamage * axeDamageMultiplier;

        Debug.Log($"[StatComponent] Axe Damage Increased: {axeDamage} (Multiplier: {axeDamageMultiplier})");
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

        Debug.Log($"[StatComponent] Switch Speed Increased! New CoolTime: {weaponChangeCoolTime} (Multiplier: {switchSpeedMultiplier})");
    }

    public void IncreaseGunDamage(float _amount)
    {
        rifleDamageMultiplier += (_amount / 100.0f);
        rifleDamage = baseRifleDamage * rifleDamageMultiplier;

        Debug.Log($"[StatComponent] Rifle Damage Increased: {rifleDamage} (Multiplier: {rifleDamageMultiplier})");
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

        Debug.Log($"[StatComponent] Max Stamina Increased: {maxStamina} (Bonus: {maxStaminaBonus})");
    }

    public void ResetSpeed()
    {
        speed = originalSpeed;
    }

    public void LoadSaveData(CharacterStatSaveData _data)
    {
        originalSpeed = _data.originalSpeed;
        speed = originalSpeed; // 현재 속도를 원본 속도로 초기화

        maxStamina = _data.maxStamina;
        maxStaminaBonus = _data.maxStaminaBonus;
        staminaIncreaseAlpha = _data.staminaIncreaseAlpha;
        staminaDecreaseAlpha = _data.staminaDecreaseAlpha;

        axeDamage = _data.axeDamage;
        axeDamageMultiplier = _data.axeDamageMultiplier;
        axeDurability = _data.axeDurability;
        speedDecreaseWhileAction = _data.speedDecreaseWhileAction;
        axeAttackRangeMultiplier = _data.axeAttackRangeMultiplier;
        axeDurabilityDecIgnoreChance = _data.axeDurabilityDecIgnoreChance;

        rifleDamage = _data.rifleDamage;
        rifleDamageMultiplier = _data.rifleDamageMultiplier;
        gunPenetrationChance = _data.gunPenetrationChance;

        ricochetCnt = _data.ricochetCnt;
        ricochetAngle = _data.ricochetAngle;
        ricochetDist = _data.ricochetDist;
        ricochetDamage = _data.ricochetDamage;

        weaponChangeCoolTime = _data.weaponChangeCoolTime;
        switchSpeedMultiplier = _data.switchSpeedMultiplier;

        bCanHunting = _data.bCanHunting;

        shockWaveChance = _data.shockWaveChance;
        shockWaveDamage = _data.shockWaveDamage;
        shockWaveDuration = _data.shockWaveDuration;
        shockWaveCreateDelay = _data.shockWaveCreateDelay;

        Debug.Log("[StatComponent] Save Data Loaded and Applied.");
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

        Debug.Log($"[StatComponent] Gun Penetration Chance Increased: {gunPenetrationChance * 100.0f}% (+{_amount}%)");
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
        shockWaveDamage += _amount;
    }

    public void IncreaseShockWaveDuration(float _amount)
    {
        shockWaveDuration += _amount;
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
}
