using Unity.Profiling;

/// <summary>
/// 원목 흡입·획득·교체 구간을 프로파일러에서 구분해 보기 위한 측정 마커.
/// 흡입 반경이 화면 전체로 커졌을 때의 프레임 드랍을 실측하려고 넣었다.
///
/// 마커는 측정만 한다(게임 로직에 영향 없음). ProfilerMarker의 Begin/End는 프로파일러가 켜진 빌드
/// (에디터·Development Build)에서만 기록되고, 릴리스 빌드에서는 빈 호출이 된다.
/// </summary>
public static class PickupProfilerMarkers
{
    /// <summary>캐릭터 아이템 감지 틱 한 번(정렬 + 반경 안 아이템 전부에 흡입 판정). 0.2초마다.</summary>
    public static readonly ProfilerMarker DetectTick = new ProfilerMarker("LumberBoy.Pickup.DetectTick");

    /// <summary>감지 틱의 충돌 격자 질의(흡입 반경 + 화면 개수 세기).</summary>
    public static readonly ProfilerMarker DetectScan = new ProfilerMarker("LumberBoy.Pickup.DetectScan");

    /// <summary>LogItemController의 원목 매 프레임 업데이트 루프 전체(비행·흡입·착지, 그 안에서 일어나는 획득 포함).</summary>
    public static readonly ProfilerMarker LogItemsUpdate = new ProfilerMarker("LumberBoy.Pickup.LogItemsUpdate");

    /// <summary>원목 한 개가 캐릭터에 도착해 습득되는 처리 전체(인벤토리·UI·효과음·풀 반환).</summary>
    public static readonly ProfilerMarker LogAcquired = new ProfilerMarker("LumberBoy.Pickup.LogAcquired");

    /// <summary>습득 신호를 받은 UnitSystem 처리(인벤토리 반영 + 효과음·바운스·흔들림).</summary>
    public static readonly ProfilerMarker UnitItemAcquired = new ProfilerMarker("LumberBoy.Pickup.UnitItemAcquired");

    /// <summary>인벤토리 UI의 획득 갱신(슬롯 재바인딩 + 용량바).</summary>
    public static readonly ProfilerMarker InventoryUIItemAdded = new ProfilerMarker("LumberBoy.Pickup.InventoryUIItemAdded");

    /// <summary>습득된 원목의 풀 반환(비활성화 포함).</summary>
    public static readonly ProfilerMarker LogPoolRelease = new ProfilerMarker("LumberBoy.Pickup.LogPoolRelease");

    /// <summary>인벤토리의 매 프레임 교체 제안 갱신.</summary>
    public static readonly ProfilerMarker LogSwapState = new ProfilerMarker("LumberBoy.Pickup.LogSwapState");

    /// <summary>교체 키 처리(슬롯 비우기 + 즉시 흡입).</summary>
    public static readonly ProfilerMarker LogSwapRequested = new ProfilerMarker("LumberBoy.Pickup.LogSwapRequested");
}
