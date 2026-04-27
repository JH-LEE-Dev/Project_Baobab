public interface IStatComponent
{
    public float speed { get; }
    public float weaponChangeCoolTime { get; }
    public bool bCanHunting { get; }

    public float axeDamage { get; }
    public float axeDurability { get; }
    public float axeDurabilityDecAmount { get; }
    public float axeAttackCoolTime { get; }

    public float rifleDamage { get; }
    public float rifleReadyTime { get; }
    public float afterShotTime { get; }
    public int magCap { get; } //현재 탄창 최대 용량
    public int ammoCap { get; } //현재 탄약 최대 보유량
    public float reloadDuration { get; }
}
