using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH
{
    //Move
    public float speed = 1f;
    public float originalSpeed =  1f;

    //Stamina
    public float maxStamina = 100f;
    private float baseMaxStamina;
    private float maxStaminaBonus = 0f;
    public float staminaIncreaseAlpha = 0f;
    public float staminaDecreaseAlpha = 0f;

    //Weapon 범용
    public float weaponChangeCoolTime = 0.5f;
    public bool bCanHunting = false;

    //Axe
    public float axeDamage = 1f; // 기본 데미지를 10으로 수정
    public float speedDecreaseWhileSwing = 0.5f;
    private float baseAxeDamage;
    private float axeDamageMultiplier = 1.0f;
    public float axeDurability = 30f;
    public float axeDurabilityDecAmount = 1f;
    public float axeAttackCoolTime = 1.2f;

    //Rifle
    public float rifleDamage = 10f; // 기본 데미지를 10으로 수정
    private float baseRifleDamage;
    private float rifleDamageMultiplier = 1.0f;
    public float rifleReadyTime = 0;
    public float shotDelay = 1f;
    public int magCap = 2;
    public int ammoCap = 6;
    public float reloadDuration = 3f;
    public float speedDecreaseWhileFire = 0.5f;

    private float baseWeaponChangeCoolTime;
    private float switchSpeedMultiplier = 1.0f;

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

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        baseMaxStamina = maxStamina;
        baseAxeDamage = axeDamage; // 초기값(10)을 base로 캡처
        baseRifleDamage = rifleDamage; // 초기값(10)을 base로 캡처
        baseWeaponChangeCoolTime = weaponChangeCoolTime;
    }

    public void IncreaseAxeDamage(float _amount)
    {
        // _amount가 10.0f이면 10% 증가
        axeDamageMultiplier += (_amount / 100.0f);
        axeDamage = baseAxeDamage * axeDamageMultiplier;

        Debug.Log($"[StatComponent] Axe Damage Increased: {axeDamage} (Multiplier: {axeDamageMultiplier})");
    }

    public void CanHunting()
    {
        bCanHunting = true;
    }

    public void IncreaseSwitchSpeed(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 속도 증가)
        switchSpeedMultiplier += (_amount / 100.0f);

        // 속도가 빨라지면 쿨타임(시간)은 감소함: Time = BaseTime / Multiplier
        weaponChangeCoolTime = baseWeaponChangeCoolTime / switchSpeedMultiplier;

        Debug.Log($"[StatComponent] Switch Speed Increased! New CoolTime: {weaponChangeCoolTime} (Multiplier: {switchSpeedMultiplier})");
    }

    public void IncreaseGunDamage(float _amount)
    {
        // _amount가 10.0f이면 10% 증가
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
}
