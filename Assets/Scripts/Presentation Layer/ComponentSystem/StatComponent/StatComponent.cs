

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

    float IStatComponent.weaponChangeCoolTime => weaponChangeCoolTime;
}
