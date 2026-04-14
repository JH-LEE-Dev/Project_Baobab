

public class StatComponent : PComponent, IStatComponent
{
    public float speed = 1f;
    public float weaponChangeCoolTime = 0.5f;
    public float axeDurabilityDecAmount = 1f;
    public float axeAttackCoolTime = 0.5f;
    float IStatComponent.weaponChangeCoolTime => weaponChangeCoolTime;
}
