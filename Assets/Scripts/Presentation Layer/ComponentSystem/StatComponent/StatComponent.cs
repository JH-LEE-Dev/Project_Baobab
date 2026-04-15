public class StatComponent : PComponent, IStatComponent
{
    //Move
    public float speed = 1f;

    //Weapon 범용
    public float weaponChangeCoolTime = 0.5f;

    //Axe
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
}
