
public interface IInventoryChecker 
{
    public bool bInventoryIsEmpty { get; }
    public bool CanAcquired(LogItem _item);

    /// <summary>
    /// 원석 주머니에 아직 담을 자리가 있는지. 가득 차 있으면 바닥의 원석을 줍지 않는다.
    ///
    /// 원목의 CanAcquired와 달리 "이 알갱이가 통째로 들어가는지"는 보지 않는다. 자리가 조금이라도
    /// 있으면 담을 수 있는 만큼만 담기 때문이다(InventoryManager.GemOreEarned).
    /// 원목과 마찬가지로, 막히는 순간 가득 찼다는 알림도 여기서 함께 띄운다.
    /// </summary>
    public bool CanAcquireGemOre();
}
