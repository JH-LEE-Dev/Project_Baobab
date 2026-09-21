
public interface IInventoryChecker 
{
    public bool bInventoryIsEmpty { get; }
    public bool CanAcquired(LogItem _item);

    /// <summary>
    /// 원석이 빨려오기 전에 주머니 자리를 미리 잡는다. 실제로 잡힌 양을 돌려주며, 0이면
    /// 자리가 없다는 뜻이므로 흡입을 시작하지 말아야 한다(바닥에 그대로 남는다).
    ///
    /// 요청한 양보다 적게 잡힐 수 있다. 그때는 그만큼만 담기고 나머지는 버려진다 -
    /// 자투리 용량이 영영 안 쓰이는 것을 막기 위함이다.
    ///
    /// <b>자리를 미리 잡아야 하는 이유:</b> 알갱이는 한 번에 여러 개가 동시에 빨려온다.
    /// 흡입 시작 때 남은 자리만 보고 보내면 먼저 도착한 것이 자리를 다 채워, 뒤따라온 것들이
    /// 한 톨도 못 받고 사라진다. 플레이어 눈에는 원석이 그냥 증발한 것으로 보인다.
    /// (용광로가 pendingOre로 같은 문제를 막는 것과 같은 장치)
    ///
    /// 잡은 자리는 반드시 CancelGemOreReservation으로 돌려줘야 한다. 도착했을 때도,
    /// 도착하지 못하고 사라질 때도 마찬가지다.
    /// 막히는 순간 가득 찼다는 알림도 여기서 함께 띄운다(원목의 CanAcquired와 같은 처리).
    /// </summary>
    public long ReserveGemOre(long _amount);

    /// <summary>잡아둔 자리를 돌려준다. 도착해서 담기 직전, 또는 도착하지 못하고 사라질 때 부른다.</summary>
    public void CancelGemOreReservation(long _amount);
}
