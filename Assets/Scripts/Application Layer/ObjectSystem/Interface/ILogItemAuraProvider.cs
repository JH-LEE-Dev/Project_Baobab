/// <summary>
/// 보석 등급 원목이 드랍될 때 붙는 아우라 이펙트를 공급/회수한다.
/// LogItem이 직접 Instantiate 하지 않고 이 인터페이스로 요청해, 풀 관리를 소유자(LogItemController)에 맡긴다.
/// 공급자가 없는 경로(예: 마을 LogItemPoolingManager)에서는 아우라 없이 동작한다.
/// </summary>
public interface ILogItemAuraProvider
{
    /// <summary>해당 상태의 아우라를 하나 꺼낸다. 등록된 프리팹이 없으면 null.</summary>
    ItemAuraEffectController GetAura(LogState _logState);

    /// <summary>다 쓴 아우라를 돌려준다. 꺼낼 때와 같은 상태를 넘겨야 올바른 풀로 반환된다.</summary>
    void ReleaseAura(LogState _logState, ItemAuraEffectController _aura);
}
