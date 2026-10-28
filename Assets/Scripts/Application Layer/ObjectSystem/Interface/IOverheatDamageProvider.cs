/// <summary>
/// 4스테이지 과열(불씨) 피해 배율을 나무에 알려 주는 창구. TreeObj가 화상 틱을 뺀 모든 피해의 마지막에 곱한다.
/// 던전 나무는 InDungeonObjectManager가 넘겨주고, 넘겨받지 못한 나무(마을 나무 등)는 배율 1로 맞는다.
/// </summary>
public interface IOverheatDamageProvider
{
    public float OverheatDamageMultiplier { get; }
}
