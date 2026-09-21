/// <summary>
/// 교체 시스템이 "어느 쪽 슬롯을 버릴지" 가리키는 값입니다.
/// </summary>
public enum ELogSwapTarget
{
    /// <summary>교체할 것이 없는 상태.</summary>
    None = 0,

    /// <summary>캐릭터 인벤토리의 슬롯을 버린다(바닥의 원목을 먹기 위해).</summary>
    Inventory,

    /// <summary>이동식 운반 상자(OffroadContainer)의 슬롯을 버린다(인벤토리의 원목을 넣기 위해).</summary>
    OffroadContainer,
}

/// <summary>
/// "지금 교체하면 버려질 슬롯" 한 건의 정보입니다. UI가 그 슬롯을 표시(강조/안내)하는 데 필요한
/// 것만 담습니다.
///
/// <b>slotIndex는 그 보관함의 슬롯 배열 인덱스</b>입니다(인벤토리면 IInventory.inventorySlots의,
/// 운반 상자면 운반 상자의). UI는 이미 같은 배열로 슬롯을 그리고 있으므로 인덱스만으로 어떤 칸인지
/// 찾을 수 있습니다. treeType/logState/count는 그 시점의 사본이라, 교체가 끝나 슬롯이 비워진 뒤에도
/// (LogSwapExecutedSignal) "무엇이 몇 개 버려졌는지" 그대로 읽을 수 있습니다.
/// </summary>
public struct LogSwapSlotInfo
{
    /// <summary>어느 보관함의 슬롯인지. None이면 버릴 슬롯이 없다는 뜻이다.</summary>
    public ELogSwapTarget target;

    /// <summary>버려질 슬롯의 인덱스. 없으면 -1.</summary>
    public int slotIndex;

    public TreeType treeType;
    public LogState logState;

    /// <summary>그 슬롯에 쌓여 있는 개수(= 교체하면 버려지는 원목 수).</summary>
    public int count;

    /// <summary>버릴 슬롯이 정해져 있는지. false면 지금은 교체가 되지 않는 상태다.</summary>
    public bool bHasSlot => target != ELogSwapTarget.None && slotIndex >= 0;

    public static LogSwapSlotInfo None => new LogSwapSlotInfo
    {
        target = ELogSwapTarget.None,
        slotIndex = -1,
        treeType = TreeType.None,
        logState = LogState.Normal,
        count = 0,
    };

    public static bool IsSame(in LogSwapSlotInfo _a, in LogSwapSlotInfo _b)
    {
        return _a.target == _b.target
            && _a.slotIndex == _b.slotIndex
            && _a.treeType == _b.treeType
            && _a.logState == _b.logState
            && _a.count == _b.count;
    }
}

/// <summary>
/// 원목 한 칸의 "가치 순위". 클수록 비싼 원목이다.
///
/// 가치 순서는 TreeType enum 인덱스와 같다 - LogItemValueDataBase.asset의 기본 가치가 enum 순서대로
/// 단조 증가한다(OakTree 4 → ObsidianTree 3천만). 같은 수종끼리는 LogState로 가른다. 이쪽도 평가
/// 배율이 enum 순서대로 단조 증가한다(Normal x1 → Perfect x20).
/// (이 전제가 깨지면, 즉 enum 순서와 가치 순서가 어긋나게 되면 이 비교식도 함께 고쳐야 한다.
///  DensityManager.CalculateMostValuableTreeType / InventoryManager.BuildRescueTargets와 같은 전제다)
///
/// 흡입 순서(ItemDetector.SortByPickupPriority), 운반 상자 전송 순서(OffroadContainer.TryTransferOneSlot),
/// 교체 대상 선정(InventoryManager / OffroadContainer의 교체 시스템)이 모두 이 한 곳의 기준을 쓴다.
/// 세 곳의 기준이 갈라지면 "비싼 원목을 먼저 담기로 해놓고 비싼 슬롯을 버리는" 모순이 생긴다.
/// </summary>
public static class LogSlotPriority
{
    /// <summary>원목이 아니거나 비어 있어 순위를 매길 수 없는 슬롯.</summary>
    public const int NONE = -1;

    // LogState는 6종(0~5)이라 3비트면 충분하다.
    private const int LOG_STATE_BITS = 3;

    /// <summary>수종이 1순위, 같은 수종 안에서는 등급이 2순위.</summary>
    public static int GetOrder(TreeType _treeType, LogState _logState)
    {
        return ((int)_treeType << LOG_STATE_BITS) | (int)_logState;
    }
}
