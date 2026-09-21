using System;
using UnityEngine;

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
/// "지금 교체하면 버려질 슬롯" 한 건과, 그 자리에 들어올 원목의 정보입니다. UI가 교체를
/// <b>환율</b>로 보여주는 데 필요한 것만 담습니다.
///
///   "소나무 3개 ↔ 자작나무 5개 (환율 3.3 : 1)"
///     └ 버릴 쪽: treeType / logState / count      └ 들어올 쪽: incoming* 세 필드
///
/// <b>slotIndex는 그 보관함의 슬롯 배열 인덱스</b>입니다(인벤토리면 IInventory.inventorySlots의,
/// 운반 상자면 운반 상자의). UI는 이미 같은 배열로 슬롯을 그리고 있으므로 인덱스만으로 어떤 칸인지
/// 찾을 수 있습니다. 나머지는 그 시점의 사본이라, 교체가 끝나 슬롯이 비워진 뒤에도
/// (LogSwapExecutedSignal) "무엇이 몇 개 버려졌는지" 그대로 읽을 수 있습니다.
/// </summary>
public struct LogSwapSlotInfo
{
    /// <summary>어느 보관함의 슬롯인지. None이면 버릴 슬롯이 없다는 뜻이다.</summary>
    public ELogSwapTarget target;

    /// <summary>버려질 슬롯의 인덱스. 없으면 -1.</summary>
    public int slotIndex;

    /// <summary>버려질 원목의 수종/등급.</summary>
    public TreeType treeType;
    public LogState logState;

    /// <summary>그 슬롯에 쌓여 있는 개수(= 교체하면 버려지는 원목 수).</summary>
    public int count;

    /// <summary>버려질 원목의 개당 가치(기본 가치 × 등급 배율). 코인 환산 표시용.</summary>
    public long unitValue;

    /// <summary>비운 자리에 들어올 원목의 수종/등급.</summary>
    public TreeType incomingTreeType;
    public LogState incomingLogState;

    /// <summary>
    /// 들어올 원목이 지금 몇 개 대기 중인지. 인벤토리 교체면 "바닥에서 못 먹고 있는 개수",
    /// 운반 상자 교체면 "가방에 들어 있는 개수"다. 슬롯 최대 중첩을 넘지 않게 잘라서 준다.
    /// </summary>
    public int incomingCount;

    /// <summary>들어올 원목의 개당 가치.</summary>
    public long incomingUnitValue;

    /// <summary>
    /// 들어올 원목이 <b>지금 어느 슬롯에 있는지</b>. 운반 상자 교체면 "교체 뒤 상자로 전송될 가방 슬롯"의
    /// 인덱스(IInventory.inventorySlots 기준)라, 가방 UI가 그 칸에 "이게 넘어갑니다" 표시를 붙일 수 있다.
    /// 인벤토리 교체면 들어올 원목이 바닥에 있으므로 -1.
    /// </summary>
    public int incomingSlotIndex;

    /// <summary>버릴 슬롯이 정해져 있는지. false면 지금은 교체가 되지 않는 상태다.</summary>
    public bool bHasSlot => target != ELogSwapTarget.None && slotIndex >= 0;

    /// <summary>
    /// 환율 - 들어올 원목 1개가 버릴 원목 몇 개 값인지. "환율 3.3 : 1"처럼 표시한다.
    /// 교체는 항상 더 비싼 원목을 위해서만 제안되므로 1보다 크다.
    /// </summary>
    public float exchangeRate => unitValue > 0 ? (float)incomingUnitValue / unitValue : 0f;

    public static LogSwapSlotInfo None => new LogSwapSlotInfo
    {
        target = ELogSwapTarget.None,
        slotIndex = -1,
        treeType = TreeType.None,
        logState = LogState.Normal,
        count = 0,
        unitValue = 0,
        incomingTreeType = TreeType.None,
        incomingLogState = LogState.Normal,
        incomingCount = 0,
        incomingUnitValue = 0,
        incomingSlotIndex = -1,
    };

    public static bool IsSame(in LogSwapSlotInfo _a, in LogSwapSlotInfo _b)
    {
        return _a.target == _b.target
            && _a.slotIndex == _b.slotIndex
            && _a.treeType == _b.treeType
            && _a.logState == _b.logState
            && _a.count == _b.count
            && _a.incomingTreeType == _b.incomingTreeType
            && _a.incomingLogState == _b.incomingLogState
            && _a.incomingCount == _b.incomingCount
            && _a.incomingSlotIndex == _b.incomingSlotIndex;
    }
}

/// <summary>
/// 원목의 <b>실제 가치</b>를 한 곳에서 답하는 정적 창구입니다.
///
/// 흡입 순서(ItemDetector.SortByPickupPriority), 운반 상자 전송 순서(OffroadContainer.TryTransferOneSlot),
/// 교체 판정(InventoryManager / OffroadContainer의 교체 시스템)이 모두 여기서 같은 값을 읽습니다.
/// 세 곳의 기준이 갈라지면 "비싼 원목을 먼저 담기로 해놓고 비싼 슬롯을 버리는" 모순이 생깁니다.
///
/// <b>왜 수종 순서(enum 인덱스)로 비교하지 않는가</b> - 같은 수종 안에서는 등급이, 같은 등급 안에서는
/// 수종이 가치를 정하지만 둘을 섞으면 뒤집힙니다. 인접 수종의 가치비는 2.3~9배인데 등급 배율은
/// 황금 ×5 / 다이아 ×10 / 프리즘 ×20이라, 황금 소나무(12×5=60)가 일반 자작나무(40)보다 비쌉니다.
/// 수종을 먼저 보는 사전순으로는 이걸 거꾸로 판정합니다. 그래서 반드시 기본 가치 × 등급 배율의
/// 실제 값을 씁니다.
///
/// 값의 출처는 LogItemValueDataBase(기본 가치 + 등급 배율) 하나이고, 제재소 평가(LogEvaluator)도
/// 같은 표를 보므로 유저가 마을에서 본 가격과 던전에서 본 환율이 어긋나지 않습니다.
///
/// 정적인 이유 - 흡입 정렬은 Presentation 계층의 정적 메서드에서 돌고, 그쪽에 인벤토리 참조를
/// 흘려보낼 통로가 없습니다. Rumble(진동 창구)과 같은 방식으로 InventoryManager.Initialize가 한 번
/// 등록합니다. 등록 전에는 예전 사전순으로 동작해 아무것도 깨지지 않습니다.
/// </summary>
public static class LogValue
{
    /// <summary>원목이 아니거나 비어 있어 가치를 매길 수 없는 슬롯.</summary>
    public const long NONE = -1;

    private const int LOG_STATE_COUNT = 6;   // LogState: Destoyed ~ Perfect

    private static LogItemValueDataBase dataBase;

    // (수종, 등급) → 개당 가치. 13 × 6 = 78칸. Find(람다)로 매번 뒤지면 감지 틱마다 델리게이트가
    // 할당되므로, 등록 시점에 한 번 표로 펼쳐 둔다.
    private static readonly long[] unitValueCache = new long[(int)TreeType.Max * LOG_STATE_COUNT];
    private static bool bCacheBuilt = false;

    // 인벤토리 슬롯 하나에 쌓을 수 있는 최대 개수. "보이는 만큼"을 이 값으로 잘라, 바닥에 50개가
    // 보여도 비운 한 칸에 들어갈 20개까지만 이번 교체/선점의 이득으로 친다.
    private static int slotCapacity = 5;

    public static int SlotCapacity => slotCapacity;

    public static bool HasDataBase => dataBase != null;

    public static void SetDataBase(LogItemValueDataBase _dataBase)
    {
        dataBase = _dataBase;
        bCacheBuilt = false;

        if (dataBase != null)
        {
            BuildCache();
        }
    }

    public static void SetSlotCapacity(int _capacity)
    {
        slotCapacity = Mathf.Max(1, _capacity);
    }

    /// <summary>
    /// 원목 1개의 가치 = 기본 가치 × 등급 배율. 스킬 배율(logValueMultiplier)은 모든 원목에 똑같이
    /// 곱해지므로 비교에서 빠져도 순서가 같다 - 여기서는 곱하지 않는다.
    /// </summary>
    public static long GetUnitValue(TreeType _treeType, LogState _logState)
    {
        if (bCacheBuilt)
        {
            int index = CacheIndex(_treeType, _logState);
            if (index >= 0 && index < unitValueCache.Length) return unitValueCache[index];
        }

        // 등록 전 폴백: 수종 우선 사전순. 순서만 유지되면 되는 곳(에디터 검증 등)에서 깨지지 않게 한다.
        return ((long)_treeType << 3) | (long)_logState;
    }

    /// <summary>
    /// "지금 보이는 만큼"의 가치 = 개당 가치 × min(보이는 개수, 슬롯 최대 중첩).
    ///
    /// 흡입 선점과 교체 상한이 같은 식을 쓴다. 칸 하나의 가치는 그 칸을 채울 수 있는 만큼이지
    /// 원목 하나의 가치가 아니다 - 그래야 보석 <b>한 개</b>가 마지막 칸을 잠그고 뒤따르는 일반 원목
    /// 홍수(15개 × 40 = 600)를 막는 일이 없다(황금 소나무 1개 = 60).
    /// </summary>
    public static long GetGroupValue(TreeType _treeType, LogState _logState, int _visibleCount)
    {
        int effectiveCount = Mathf.Clamp(_visibleCount, 1, slotCapacity);
        return GetUnitValue(_treeType, _logState) * effectiveCount;
    }

    /// <summary>등급이 일반보다 높은(황금/다이아/프리즘) 원목인지. 교체에서 절대 버리지 않는다.</summary>
    public static bool IsGemGrade(LogState _logState)
    {
        return _logState > LogState.Normal;
    }

    private static int CacheIndex(TreeType _treeType, LogState _logState)
    {
        return (int)_treeType * LOG_STATE_COUNT + (int)_logState;
    }

    private static void BuildCache()
    {
        Array.Clear(unitValueCache, 0, unitValueCache.Length);

        for (int tree = 0; tree < (int)TreeType.Max; tree++)
        {
            LogItemValueData valueData = dataBase.Get((TreeType)tree);
            long baseValue = valueData != null ? valueData.value : 0;

            for (int state = 0; state < LOG_STATE_COUNT; state++)
            {
                float multiplier = dataBase.GetStateMultiplier((LogState)state);
                unitValueCache[tree * LOG_STATE_COUNT + state] = (long)Math.Round(baseValue * (double)multiplier);
            }
        }

        bCacheBuilt = true;
    }
}
