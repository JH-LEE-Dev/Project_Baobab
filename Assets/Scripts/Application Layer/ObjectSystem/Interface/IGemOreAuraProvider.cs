/// <summary>
/// 원석에 붙는 종류별 아우라를 빌려주는 쪽(GemOreItemController).
/// 원목의 ILogItemAuraProvider와 같은 구조이고, 열쇠만 LogState 대신 GemOreType이다.
/// </summary>
public interface IGemOreAuraProvider
{
    /// <summary>해당 종류의 아우라를 하나 꺼낸다. 등록된 프리팹이 없으면 null.</summary>
    ItemAuraEffectController GetAura(GemOreType _gemOreType);

    /// <summary>다 쓴 아우라를 돌려준다. 꺼낼 때와 같은 종류를 넘겨야 올바른 풀로 반환된다.</summary>
    void ReleaseAura(GemOreType _gemOreType, ItemAuraEffectController _aura);
}
