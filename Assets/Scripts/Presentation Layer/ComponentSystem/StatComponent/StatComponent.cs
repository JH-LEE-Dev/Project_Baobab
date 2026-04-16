using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH
{
    //Move
    public float speed = 1f;

    //Weapon 범용
    public float weaponChangeCoolTime = 0.5f;

    //Axe
    public float axeDamage = 10f; // 기본 데미지를 10으로 수정
    private float baseAxeDamage;
    private float axeDamageMultiplier = 1.0f;
    public float axeDurability = 30f;
    public float axeDurabilityDecAmount = 1f;
    public float axeAttackCoolTime = 0.5f;

    //Rifle
    public float rifleReadyTime = 0.25f;
    public float afterShotTime = 0.25f;
    public int magCap = 2;
    public int ammoCap = 6;
    public float reloadDuration = 3f;

    float IStatComponent.speed => speed;
    float IStatComponent.weaponChangeCoolTime => weaponChangeCoolTime;

    float IStatComponent.axeDurability => axeDurability;
    float IStatComponent.axeDurabilityDecAmount => axeDurabilityDecAmount;
    float IStatComponent.axeAttackCoolTime => axeAttackCoolTime;

    float IStatComponent.rifleReadyTime => rifleReadyTime;
    float IStatComponent.afterShotTime => afterShotTime;
    int IStatComponent.magCap => magCap;
    int IStatComponent.ammoCap => ammoCap;
    float IStatComponent.reloadDuration => reloadDuration;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        baseAxeDamage = axeDamage; // 초기값(10)을 base로 캡처
        Debug.Log(baseAxeDamage);
        Debug.Log(axeDamage);
    }

    public void IncreaseAxeDamage(float _amount)
    {
        // _amount가 10.0f이면 10% 증가
        axeDamageMultiplier += (_amount / 100.0f);
        axeDamage = baseAxeDamage * axeDamageMultiplier;

        Debug.Log($"[StatComponent] Axe Damage Increased: {axeDamage} (Multiplier: {axeDamageMultiplier})");
    }
}

