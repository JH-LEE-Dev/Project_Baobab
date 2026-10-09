/// <summary>
/// 캐릭터의 아이템 감지 틱 한 번(반경 안 원목 전부에 SetSuckTarget을 거는 루프)을 하나의 묶음으로 표시한다.
///
/// 묶음 안에서는 InventoryManager.CanAcquired가 가상 슬롯 스냅샷을 원목마다 새로 만들지 않고, 묶음 첫 호출에서
/// 한 번 만든 것을 이어 쓴다. 흡입 반경이 화면 전체로 커지면 한 틱에 수백 개가 들어오는데, 원목마다
/// "예약 정리 → 슬롯 복사 → 예약분 재배치"를 처음부터 되풀이하던 비용을 없애기 위한 것이다.
///
/// 결과는 원목마다 새로 계산할 때와 같다. 묶음이 도는 동안에는 슬롯 내용이 바뀌지 않고(습득은 원목이 도착하는
/// Update에서 일어난다), 예약 목록은 이 루프가 추가하는 것 말고는 바뀌지 않기 때문이다. 자세한 근거는
/// InventoryManager.CanAcquiredInBatch에 있다.
///
/// Presentation 계층(Character)에는 인벤토리 참조를 흘려보낼 통로가 없어 LogValue·Rumble과 같은 정적 창구를 쓴다.
/// Begin/End는 반드시 짝을 맞춰야 하므로 호출부는 try/finally로 감싼다.
/// </summary>
public static class LogAcquireBatch
{
    /// <summary>지금 감지 틱 묶음 안인지.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>묶음마다 바뀌는 번호. 인벤토리가 "이번 묶음의 스냅샷을 이미 만들었는지"를 이 값으로 가린다.</summary>
    public static int Id { get; private set; }

    public static void Begin()
    {
        Id++;
        IsActive = true;
    }

    public static void End()
    {
        IsActive = false;
    }
}
