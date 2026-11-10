
public interface ISporeShieldStatProvider
{
    public float ShieldDamageMultiplier { get; }
    public float ShieldPenetrationPercent { get; }
    public float ShieldRegenReductionMul { get; }
    public float StarMarkDamageMultiplier { get; }
    // 3스테이지 배리어 데미지 감소율(1 = 100% 막음). 배리어 관통력 특성이 낮춘다.
    public float BarrierDamageReduction { get; }
}
