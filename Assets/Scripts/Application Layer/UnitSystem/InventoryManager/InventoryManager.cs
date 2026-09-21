using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour, IInventory, IInventoryForSkill, IInventoryChecker, IInventoryCH, IMoneyData
{
    public event Action ItemCantAcquiedEvent;
    public event Action ItemAddedEvent;
    public event Action ItemRemovedEvent;
    public event Action SpendMoneyEvent;
    public event Action InventorySpecChangedEvent;
    public event Action LoosAllInventoryItemEvent;
    public event Action InventoryIsFullEvent;
    // 원석 재화가 늘거나 줄었을 때 발생. HUD가 이 이벤트만 구독하면 어느 재화가 바뀌었는지 알 수 있다.
    public event Action<MoneyType> GemOreChangedEvent;

    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    private long money = 0;
    private long carrot = 0;

    // 보석 나무 원석 재화. money/carrot과 동일하게 세이브에 영구 저장된다.
    private long goldOre = 0;
    private long diamondOre = 0;
    private long prismOre = 0;

    // ── [죽은 코드] 여기부터 아래 원석/주머니 관련 멤버는 전부 동작하지 않는다 ──────────────
    //
    // SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라 원석 아이템이 생성되지 않으므로
    // GemOreEarned / ReserveGemOre / CancelGemOreReservation 이 호출되지 않고, 보유량과
    // 획득 이력은 0/false 로 고정된다. 주머니 한도(30)도 걸릴 일이 없다.
    // 버그 / 회귀 검토 대상에서 제외한다.
    //
    // 세이브 필드는 그대로 두므로 스위치를 다시 켜면 저장해 둔 값이 이어진다.
    // ───────────────────────────────────────────────────────────────────────────────

    // 각 원석을 한 번이라도 얻은 적이 있는지. 보유량이 0이 되어도 유지되며 세이브에 저장된다.
    // HUD가 "발견한 재화만 표시"를 판정하는 데 쓴다(IMoneyData.HasEverAcquired).
    private bool bHasEverAcquiredGoldOre = false;
    private bool bHasEverAcquiredDiamondOre = false;
    private bool bHasEverAcquiredPrismOre = false;

    // 원석 주머니 한도. 세 종류를 합친 총량이 이 값을 넘지 못한다.
    // 인스펙터 값은 특성을 하나도 찍지 않았을 때의 시작 한도다.
    //
    // <b>세이브에 담지 않는다.</b> 한도를 올리는 것은 특성뿐이고 특성 트리는 이미 저장되므로,
    // 로드할 때 스킬 트리 복원이 이 시작 한도 위에 특성 몫을 그대로 다시 얹어 같은 값이 나온다.
    // currentSlotCount/maxItemsPerSlot을 저장하지 않는 것과 같은 이유다.
    //
    // 한 번 더 적으면 값이 겹칠 뿐 아니라 복원 순서에 매이게 된다. 인벤토리를 스킬 트리보다
    // 먼저 복원하도록 바뀌는 순간 특성 몫이 매 로드마다 누적되는데, 저장하지 않으면 그 함정이
    // 아예 없다. 나중에 특성 말고 다른 것(아이템/퀘스트 등)이 주머니를 키우게 되면 그때는
    // 저장해야 하므로, 그 시점에 이 주석과 함께 다시 판단할 것.
    [Tooltip("원석 주머니의 시작 한도. 황금/다이아/프리즘을 합친 총량이 이 값을 넘지 못한다. 특성으로 늘어난다.")]
    [SerializeField] private long gemOrePouchCapacity = 30;

    // 빨려오는 중이라 아직 도착하지 않은 원석이 잡아둔 자리.
    // 알갱이는 한 번에 여러 개가 동시에 날아오므로, 자리를 미리 잡아두지 않으면 먼저 도착한 것이
    // 주머니를 다 채워 뒤따라온 것들이 한 톨도 못 받고 사라진다.
    // (용광로의 pendingOre와 같은 장치)
    private long pendingGemOre = 0;
    [SerializeField] private long sunEssence;
    [SerializeField] private long moonEssence;
    [SerializeField] private long lightningEssence;

    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private ItemDataPool itemDataPool;

    // "원목 보험 증서" 전리품 효과 보유 상태. 영구 획득 여부(InDungeonObjectManager.bHasAcquiredLostAndFoundBox)를
    // 매 던전 진입마다 SetupForForestType()이 여기에 다시 밀어넣어주고, 탈진 1회로 소모된다.
    private bool hasLostAndFoundBoxEffect;

    // "원목 보험 증서"가 탈진 시 구제하는 비율. 전체 합이 아니라 나무 종류별로 각각 이 비율만큼 구제한다.
    private const float LostAndFoundRescueRatio = 0.3f;
    // 나무 종류별 유실 예정 수량 / 구제 목표 집계용 버퍼(탈진마다 새로 할당하지 않도록 재사용한다)
    private readonly int[] rescueAtRiskCountsByTreeType = new int[Enum.GetValues(typeof(TreeType)).Length];
    private readonly int[] rescueTargetsByTreeType = new int[Enum.GetValues(typeof(TreeType)).Length];

    public bool bInventoryIsEmpty { get; private set; }

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;
    long IInventory.money => money;
    long IInventory.carrot => carrot;
    int IInventory.maxCapacity => currentSlotCount * maxItemsPerSlot;
    int IInventory.currentItemCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < currentSlotCount; i++)
            {
                if (inventorySlots[i].itemData != null)
                {
                    total += inventorySlots[i].totalCount;
                }
            }
            return total;
        }
    }

    public int currentSlotCnt => currentSlotCount;

    public int GetMaxItemsPerSlot()
    {
        return maxItemsPerSlot;
    }

    long IMoneyData.money => money;

    long IMoneyData.carrot => carrot;

    long IMoneyData.sunEssence => sunEssence;

    long IMoneyData.moonEssence => moonEssence;

    long IMoneyData.lightningEssence => lightningEssence;

    long IMoneyData.goldOre => goldOre;

    long IMoneyData.diamondOre => diamondOre;

    long IMoneyData.prismOre => prismOre;

    long IMoneyData.GetMoney(MoneyType _moneyType)
    {
        return GetMoney(_moneyType);
    }

    bool IMoneyData.HasEverAcquired(MoneyType _moneyType)
    {
        return HasEverAcquired(_moneyType);
    }

    long IMoneyData.GemOrePouchCapacity => gemOrePouchCapacity;

    long IMoneyData.TotalGemOre => TotalGemOre;

    public int maxItemCntPerSlot => maxItemsPerSlot;

    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // 원목의 실제 가치(기본 가치 × 등급 배율)의 단일 출처. 흡입 정렬·교체 판정·전송 순서가 전부
    // LogValue(정적 창구)를 통해 이 표를 읽는다. Initialize에서 한 번 등록한다.
    [SerializeField] private LogItemValueDataBase logItemValueDataBase;

    private LogItemPoolingManager logItemPoolingManager;
    private List<LogItem> activeDroppedItems = new List<LogItem>(64);

    // DropAllItem 시 화면에 날아가는 연출 개수 상한(아이템 데이터 자체는 상한과 무관하게 전량 버려진다)
    private const int MaxDropVisualCount = 15;
    // 연출이 한꺼번에 터지지 않고 하나씩 연달아 발사되도록 하는 간격
    [SerializeField] private float dropVisualInterval = 0.08f;
    private Coroutine dropVisualsCoroutine;

    // 교체(PlayLogDropVisuals)가 쓰는 연출 순서 버퍼. 새 연출을 시작하기 전에 진행 중인 코루틴을
    // 먼저 멈추므로, 코루틴이 읽는 도중에 이 리스트가 바뀌는 일은 없다. 교체마다 새로 잡지 않는다.
    private readonly List<(TreeType treeType, LogState logState)> swapDropVisualPlan =
        new List<(TreeType, LogState)>(MaxDropVisualCount);

    private VFXComponent vfxComponent;

    public void Initialize()
    {
        if (itemDataPool == null) itemDataPool = new ItemDataPool(CreateItemData);

        vfxComponent = GetComponent<VFXComponent>();
        vfxComponent.Initialize();

        logItemPoolingManager = GetComponent<LogItemPoolingManager>();
        logItemPoolingManager.Initialize(false);

        activeDroppedItems.Clear();

        // 1. 슬롯 리스트 최대 개수(SYSTEM_VAR.MAX_INVENTORY_CNT)만큼 미리 생성
        if (inventorySlots.Count < SYSTEM_VAR.MAX_INVENTORY_CNT)
        {
            int needCount = SYSTEM_VAR.MAX_INVENTORY_CNT - inventorySlots.Count;
            for (int i = 0; i < needCount; i++)
            {
                inventorySlots.Add(new InventorySlot());
            }
        }

        // 2. 모든 슬롯(최대 개수)의 데이터들을 풀로 반환하고 슬롯 초기화
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].itemData is ItemData data)
            {
                itemDataPool.Release(data);
            }
            inventorySlots[i].Setup(null, 0);
        }

        // 3. 모든 아이템 타입에 대해 풀 미리 생성
        itemDataPool.WarmAll();

        // 흡입 정렬(ItemDetector)과 교체 판정이 같은 가치 표를 보도록 정적 창구에 등록한다.
        // Presentation 계층의 정적 정렬에는 인벤토리 참조를 흘려보낼 통로가 없어 Rumble과 같은 방식을 쓴다.
        LogValue.SetDataBase(logItemValueDataBase);
        LogValue.SetSlotCapacity(maxItemsPerSlot);

        ClearLogSwapRequest();
        lastNotifiedSwapInfo = LogSwapSlotInfo.None;

        UpdateInventoryEmptyState();
    }

    private void Update()
    {
        UpdateLogSwapState();

        if (activeDroppedItems.Count > 0)
        {
            float deltaTime = Time.deltaTime;
            for (int i = activeDroppedItems.Count - 1; i >= 0; i--)
            {
                activeDroppedItems[i].ManualUpdate(deltaTime);
            }
        }
    }

    /// <summary>
    /// 인벤토리 슬롯을 확장합니다.
    /// </summary>
    /// <param name="_amount">추가할 슬롯 개수</param>
    public void ExpandInventory(int _amount)
    {
        currentSlotCount = Mathf.Min(currentSlotCount + _amount, SYSTEM_VAR.MAX_INVENTORY_CNT);
    }

    public void ItemAcquired(Item _item)
    {
        if (_item == null) return;

        bool itemAdded = false;

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null &&
                inventorySlots[i].totalCount < maxItemsPerSlot &&
                IsSameItem(_item, (ItemData)inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCount(_item);
                itemAdded = true;
                break;
            }
        }

        if (!itemAdded)
        {
            // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
            for (int i = 0; i < currentSlotCount; i++)
            {
                if (inventorySlots[i].itemData == null)
                {
                    ItemData newData = itemDataPool.Get(_item.itemType);
                    if (newData != null)
                    {
                        newData.CopyFrom(_item);
                        inventorySlots[i].Setup(newData, 1);
                        itemAdded = true;
                        break;
                    }
                }
            }
        }

        if (itemAdded)
        {
            CheckInventoryFull();
            UpdateInventoryEmptyState();
            ItemAdded();
        }
        else
        {
            bool hasSpaceRemaining = false;
            for (int i = 0; i < currentSlotCount; i++)
            {
                if (inventorySlots[i].totalCount < maxItemsPerSlot)
                {
                    hasSpaceRemaining = true;
                    break;
                }
            }

            if (hasSpaceRemaining)
            {
                ItemCantAcquiedEvent?.Invoke();
            }
            else
            {
                InventoryIsFullEvent?.Invoke();
            }
        }
    }

    private void CheckInventoryFull()
    {
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null || inventorySlots[i].totalCount < maxItemsPerSlot)
            {
                return;
            }
        }
        
        InventoryIsFullEvent?.Invoke();
    }


    public void PopulateInventorySaveData(ref InventorySaveData _saveData)
    {
        _saveData.money = money;
        _saveData.carrot = carrot;
        _saveData.goldOre = goldOre;
        _saveData.diamondOre = diamondOre;
        _saveData.prismOre = prismOre;
        _saveData.bHasEverAcquiredGoldOre = bHasEverAcquiredGoldOre;
        _saveData.bHasEverAcquiredDiamondOre = bHasEverAcquiredDiamondOre;
        _saveData.bHasEverAcquiredPrismOre = bHasEverAcquiredPrismOre;

        // 리스트 초기화 (구조체 내의 Initialize 활용)
        _saveData.Initialize(currentSlotCount);

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            InventorySlotSaveData slotData = new InventorySlotSaveData();
            slotData.totalCount = slot.totalCount;

            if (slot.itemData != null)
            {
                ItemSaveData itemSaveData = new ItemSaveData();
                itemSaveData.itemType = slot.itemData.itemType;
                itemSaveData.color = slot.itemData.color; // 컬러 저장

                if (slot.itemData is LogItemData logData)
                {
                    itemSaveData.treeType = logData.treeType;
                    itemSaveData.logState = logData.logState;
                    slotData.treeTypeCounts = slot.GetTreeTypeCounts();
                }
                else if (slot.itemData is LootItemData lootData)
                {
                    itemSaveData.lootType = lootData.lootType;
                }

                slotData.itemSaveData = itemSaveData;
            }

            _saveData.slots.Add(slotData);
        }
    }

    private bool IsSameItem(Item _item, ItemData _data)
    {
        if (_item.itemType != _data.itemType) return false;

        if (_item is LogItem logItem && _data is LogItemData logData)
        {
            // 같은 로그 상태와 나무 종류인 경우에만 같은 슬롯에 보관
            return logItem.logState == logData.logState && logItem.treeType == logData.treeType;
        }
        else if (_item is LootItem lootItem && _data is LootItemData lootData)
        {
            // 같은 전리품 종류라면 같은 슬롯에 보관
            return lootItem.LootType == lootData.lootType;
        }

        return true;
    }

    private ItemData CreateItemData(ItemType _type)
    {
        switch (_type)
        {
            case ItemType.Log:
                var logData = new LogItemData();
                logData.itemType = _type;
                return logData;
            case ItemType.Loot:
                var lootData = new LootItemData();
                lootData.itemType = _type;
                return lootData;
            default:
                var itemData = new ItemData();
                itemData.itemType = _type;
                return itemData;
        }
    }

    public void ItemDeleted(IInventorySlot _inventorySlot)
    {
        if (_inventorySlot == null) return;

        if (_inventorySlot is InventorySlot slot)
        {
            if (slot.itemData != null)
            {
                itemDataPool.Release(slot.itemData);
            }
            slot.Setup(null, 0);
            UpdateInventoryEmptyState();
            ItemRemoved();
        }
    }

    public List<InventorySlot> GetInventorySlots()
    {
        return inventorySlots;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public void MoneyEarned(long _money)
    {
        money += _money;
    }

    public void CarrotEarned(float _amount)
    {
        carrot += (long)_amount;
    }

    public long GetCurrentCarrot()
    {
        return carrot;
    }

    public long GetCurrentMoney()
    {
        return money;
    }

    public void DecreaseCarrot(long _amount)
    {
        carrot -= _amount;
        if (carrot < 0) carrot = 0;
        SpendMoneyEvent?.Invoke();
    }

    public void DecreaseMoney(long _amount)
    {
        money -= _amount;
        if (money < 0) money = 0;
        SpendMoneyEvent?.Invoke();
    }

    /// <summary>지금 주머니에 든 원석 총량(세 종류 합).</summary>
    public long TotalGemOre => goldOre + diamondOre + prismOre;

    /// <summary>
    /// 주머니에 더 담을 수 있는 양. 가득 찼으면 0이다.
    /// 빨려오는 중인 원석이 잡아둔 자리(pendingGemOre)까지 빼므로, 동시에 날아오는 것들끼리
    /// 같은 자리를 두고 겹치지 않는다.
    ///
    /// 한도보다 많이 들고 있는 경우(예: 주머니가 없던 시절의 세이브를 읽었거나, 특성 Undo로 한도가
    /// 줄어든 경우)에도 음수가 되지 않는다. 그때는 더 담지 못할 뿐, 갖고 있던 것을 뺏지는 않는다.
    /// </summary>
    public long GemOrePouchSpace => Math.Max(0, gemOrePouchCapacity - TotalGemOre - pendingGemOre);

    /// <summary>빨려오는 중인 원석이 잡아둔 자리.</summary>
    public long PendingGemOre => pendingGemOre;

    /// <summary>원석 주머니 한도. 세 종류를 합친 총량의 상한이다.</summary>
    public long GemOrePouchCapacity => gemOrePouchCapacity;

    /// <summary>
    /// 보석 원석을 주웠을 때 해당 재화를 올린다. 인벤토리 슬롯은 건드리지 않는다
    /// (원석은 칸을 차지하지 않고 돈처럼 쌓이는 재화다).
    ///
    /// 주머니 한도를 넘는 만큼은 받지 않는다. 알갱이 하나가 통째로 거절되는 것이 아니라
    /// <b>남는 자리만큼만 담고 나머지는 버린다</b>(자투리 용량이 영영 안 쓰이는 것을 막기 위함).
    /// 흡입 전에 자리를 잡아두므로(ReserveGemOre) 주운 원석이 여기서 통째로 거절되는 일은 없다.
    /// 잡은 자리보다 알갱이가 큰 경우에만 그 차액이 버려진다.
    /// </summary>
    /// <returns>실제로 담긴 양. 한 톨도 못 담았으면 0.</returns>
    public long GemOreEarned(GemOreType _gemOreType, long _amount)
    {
        if (_amount <= 0) return 0;

        long accepted = Math.Min(_amount, GemOrePouchSpace);
        if (accepted <= 0) return 0;

        switch (_gemOreType)
        {
            case GemOreType.Gold: goldOre += accepted; bHasEverAcquiredGoldOre = true; break;
            case GemOreType.Diamond: diamondOre += accepted; bHasEverAcquiredDiamondOre = true; break;
            case GemOreType.Prism: prismOre += accepted; bHasEverAcquiredPrismOre = true; break;
            default: return 0;
        }

        GemOreChangedEvent?.Invoke(GemOreTypeToMoneyType(_gemOreType));

        return accepted;
    }

    /// <summary>
    /// 빨려올 원석의 자리를 미리 잡는다. 실제로 잡힌 양을 돌려주며, 0이면 흡입을 시작하면 안 된다.
    /// 남은 자리보다 많이 요청하면 남은 만큼만 잡힌다.
    ///
    /// 자리가 없어 0을 돌려줄 때는 가득 찼다는 알림을 띄운다(원목의 CanAcquired와 같은 처리).
    /// </summary>
    public long ReserveGemOre(long _amount)
    {
        if (_amount <= 0) return 0;

        long reserved = Math.Min(_amount, GemOrePouchSpace);

        if (reserved <= 0)
        {
            InventoryIsFullEvent?.Invoke();
            return 0;
        }

        pendingGemOre += reserved;
        return reserved;
    }

    /// <summary>
    /// 잡아둔 자리를 돌려준다. 도착해서 담기 직전(그 자리에 실제로 담기도록), 또는 도착하지
    /// 못하고 사라질 때 부른다. 잡은 쪽이 잡은 만큼만 돌려주므로 음수로 내려갈 일은 없지만,
    /// 혹시 어긋나도 0 아래로는 가지 않게 막아둔다.
    /// </summary>
    public void CancelGemOreReservation(long _amount)
    {
        if (_amount <= 0) return;

        pendingGemOre = Math.Max(0, pendingGemOre - _amount);
    }

    public void DecreaseGemOre(GemOreType _gemOreType, long _amount)
    {
        if (_amount <= 0) return;

        switch (_gemOreType)
        {
            case GemOreType.Gold: goldOre = Math.Max(0, goldOre - _amount); break;
            case GemOreType.Diamond: diamondOre = Math.Max(0, diamondOre - _amount); break;
            case GemOreType.Prism: prismOre = Math.Max(0, prismOre - _amount); break;
            default: return;
        }

        GemOreChangedEvent?.Invoke(GemOreTypeToMoneyType(_gemOreType));
        SpendMoneyEvent?.Invoke();
    }

    public long GetCurrentGemOre(GemOreType _gemOreType)
    {
        switch (_gemOreType)
        {
            case GemOreType.Gold: return goldOre;
            case GemOreType.Diamond: return diamondOre;
            case GemOreType.Prism: return prismOre;
            default: return 0;
        }
    }

    /// <summary>
    /// 재화 종류로 현재 보유량을 돌려준다. HUD가 MoneyType 하나만 들고 값을 읽어갈 수 있게 하는 통합 진입점.
    /// </summary>
    public long GetMoney(MoneyType _moneyType)
    {
        switch (_moneyType)
        {
            case MoneyType.Coin: return money;
            case MoneyType.Carrot: return carrot;
            case MoneyType.SunEssence: return sunEssence;
            case MoneyType.MoonEssence: return moonEssence;
            case MoneyType.LightningEssnece: return lightningEssence;
            case MoneyType.GoldOre: return goldOre;
            case MoneyType.DiamondOre: return diamondOre;
            case MoneyType.PrismOre: return prismOre;
            default: return 0;
        }
    }

    /// <summary>
    /// 이 재화를 한 번이라도 얻은 적이 있는지. 다 써서 보유량이 0이 되어도 true로 남는다.
    /// 원석이 아닌 재화는 숨기는 개념이 없으므로 항상 true다.
    /// </summary>
    public bool HasEverAcquired(MoneyType _moneyType)
    {
        switch (_moneyType)
        {
            case MoneyType.GoldOre: return bHasEverAcquiredGoldOre;
            case MoneyType.DiamondOre: return bHasEverAcquiredDiamondOre;
            case MoneyType.PrismOre: return bHasEverAcquiredPrismOre;
            default: return true;
        }
    }

    /// <summary>원석 종류로 묻는 편의 오버로드. HasEverAcquired(MoneyType)와 같은 값을 돌려준다.</summary>
    public bool HasEverAcquiredGemOre(GemOreType _gemOreType)
    {
        return HasEverAcquired(GemOreTypeToMoneyType(_gemOreType));
    }

    public static MoneyType GemOreTypeToMoneyType(GemOreType _gemOreType)
    {
        switch (_gemOreType)
        {
            case GemOreType.Gold: return MoneyType.GoldOre;
            case GemOreType.Diamond: return MoneyType.DiamondOre;
            case GemOreType.Prism: return MoneyType.PrismOre;
            default: return MoneyType.None;
        }
    }

    private List<LogItem> reservedItems = new List<LogItem>(32);

    // CanAcquired()가 실제 슬롯 배치를 미리 시뮬레이션하기 위한 가상 슬롯 스냅샷
    private struct VirtualSlot
    {
        public bool hasItem;
        public ItemType itemType;
        public TreeType treeType;
        public LogState logState;
        public int count;
    }

    public bool CanAcquired(LogItem _item)
    {
        if (_item == null) return false;

        // 1. 기존 예약된 아이템 중 유효하지 않은 것(Sucking 상태가 아니거나 비활성화된 경우) 정리
        for (int i = reservedItems.Count - 1; i >= 0; i--)
        {
            var reserved = reservedItems[i];
            if (reserved == null || !reserved.gameObject.activeInHierarchy || reserved.MoveState != ItemMoveState.Sucking || reserved == _item)
            {
                reservedItems.RemoveAt(i);
            }
        }

        // 2. 실제 슬롯 상태를 가상 슬롯으로 복사
        //    힙이 아니라 스택에 잡는다. 이 판정은 바닥의 원목 하나당 초당 5회(Character의 아이템
        //    감지 틱) 돌고 그게 반경 안의 원목 수만큼 곱해지므로, 매번 배열을 새로 잡으면 가방이
        //    꽉 찬 채 원목 더미 위에 서 있는 동안 계속 쓰레기가 쌓인다.
        //    슬롯은 SYSTEM_VAR.MAX_INVENTORY_CNT개가 상한이라 최악이어도 20B * 10 = 200B다.
        int slotCount = Mathf.Min(currentSlotCount, inventorySlots.Count);
        Span<VirtualSlot> virtualSlots = stackalloc VirtualSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            var data = inventorySlots[i].itemData as ItemData;
            if (data != null)
            {
                virtualSlots[i].hasItem = true;
                virtualSlots[i].itemType = data.itemType;
                virtualSlots[i].count = inventorySlots[i].totalCount;
                if (data is LogItemData logData)
                {
                    virtualSlots[i].treeType = logData.treeType;
                    virtualSlots[i].logState = logData.logState;
                }
            }
        }

        // 3. 이미 예약된 아이템들을 예약된 순서대로 먼저 가상 배치한다.
        //    ItemAcquired()와 동일한 알고리즘(같은 종류 슬롯 우선 → 빈 슬롯)을 쓰기 때문에,
        //    종류가 다른 예약끼리도 같은 빈 슬롯을 중복으로 차지하지 못하게 된다.
        for (int i = 0; i < reservedItems.Count; i++)
        {
            TryPlaceVirtual(reservedItems[i], virtualSlots);
        }

        // 4. 이번 아이템도 같은 방식으로 배치를 시도한다. 성공하면 실제 ItemAcquired() 시점에도
        //    반드시 자리가 있음이 보장되므로 예약 목록에 추가하고 true를 반환한다.
        if (TryPlaceVirtual(_item, virtualSlots))
        {
            reservedItems.Add(_item);
            return true;
        }

        // 5. 들어올 수 없을 때 인벤토리 공간 상태 분석 및 이벤트 호출
        //    이 지점에 도달했다면 모든 슬롯이 이미 점유된 상태라는 뜻이다 - 비어있는 슬롯이
        //    하나라도 있었다면 TryPlaceVirtual()이 그 자리에 배치하고 true를 반환했을 것이다.
        //
        //    "원목이 안 먹어진다"가 곧 교체 발동 조건 1이므로, 교체 시스템에도 이 원목을 알려둔다.
        //    수종 개량이 이미 끝난 뒤에 불리는 경로라(LogItem.CheckAcquireCondition) 여기서 보는
        //    수종은 실제로 담겼을 수종이다.
        RequestLogSwap(_item.treeType, _item.logState);

        bool hasSpaceRemaining = false;
        for (int i = 0; i < slotCount; i++)
        {
            if (virtualSlots[i].count < maxItemsPerSlot)
            {
                hasSpaceRemaining = true;
                break;
            }
        }

        if (hasSpaceRemaining)
        {
            ItemCantAcquiedEvent?.Invoke();
        }
        else
        {
            InventoryIsFullEvent?.Invoke();
        }

        return false;
    }

    // ItemAcquired()와 동일한 순서(같은 종류 슬롯에 여유가 있으면 그곳에, 없으면 첫 빈 슬롯에)로
    // 가상 슬롯에 배치를 시도한다. CanAcquired()의 판정과 ItemAcquired()의 실제 결과가
    // 항상 일치하도록 두 곳의 배치 규칙을 반드시 동일하게 유지해야 한다.
    private bool TryPlaceVirtual(LogItem _item, Span<VirtualSlot> _virtualSlots)
    {
        for (int i = 0; i < _virtualSlots.Length; i++)
        {
            if (_virtualSlots[i].hasItem && _virtualSlots[i].count < maxItemsPerSlot &&
                _virtualSlots[i].itemType == _item.itemType &&
                _virtualSlots[i].treeType == _item.treeType &&
                _virtualSlots[i].logState == _item.logState)
            {
                _virtualSlots[i].count++;
                return true;
            }
        }

        for (int i = 0; i < _virtualSlots.Length; i++)
        {
            if (!_virtualSlots[i].hasItem)
            {
                _virtualSlots[i].hasItem = true;
                _virtualSlots[i].itemType = _item.itemType;
                _virtualSlots[i].treeType = _item.treeType;
                _virtualSlots[i].logState = _item.logState;
                _virtualSlots[i].count = 1;
                return true;
            }
        }

        return false;
    }

    // ── 교체 시스템(인벤토리) ────────────────────────────────────────────────────────
    //
    // 가방이 꽉 차 바닥의 원목을 먹지 못할 때, 값싼 슬롯 하나를 내려놓아 자리를 만드는 기능이다.
    // 누를지는 유저가 정한다. 시스템은 두 가지만 보장한다.
    //   (1) 손해인 거래는 아예 제안하지 않는다.
    //   (2) 제안은 환율 한 줄로 설명된다 - "소나무 3개 ↔ 자작나무 5개 (환율 3.3 : 1)".
    //
    // 제안 규칙 (셋 다 만족해야 한다)
    //   자격 : 버릴 슬롯의 개당 가치 < 들어올 원목의 개당 가치. 보석 등급 슬롯은 절대 버리지 않는다
    //          - 게임이 아우라와 효과음으로 "보석은 특별하다"고 가르쳐 놨는데 시스템이 버리면
    //          계산이 맞더라도 유저에겐 엉뚱한 슬롯이 된다.
    //   상한 : 버릴 슬롯의 총 가치 ≤ 들어올 원목의 개당 가치 × min(바닥에 보이는 개수, 슬롯 최대 중첩)
    //          - "지금 눈에 보이는 만큼"만 이득으로 친다. 미래 습득량을 추정하는 더 정확한 공식은
    //          만들 수 있지만 유저가 검증할 수 없고, 검증 못 하는 이득은 이득으로 느껴지지 않는다.
    //   선정 : 자격·상한을 통과한 슬롯 중 총 가치가 가장 낮은 것("가장 싸게 살 수 있는 칸").
    //          같으면 개당 가치가 낮은 쪽(더 싼 수종)을 골라 직관과 맞춘다.
    //
    // 가장 싼 수종이 들어올 때는 자격을 만족하는 슬롯이 없어 제안이 없다. "잡템 자리를 만들라"는
    // 제안은 구조적으로 나오지 않는다.
    //
    // 한 번 띄운 제안은 그것이 무효가 될 때까지 바꾸지 않는다(sticky). 0.2초마다 "못 먹은 원목"이
    // 바뀌면서 강조 슬롯이 튀어다니면 "뜬 걸 눌렀는데 그 사이 바뀌어 있었다"가 생긴다.
    // 교체 직후에는 잠시(SwapCooldown) 다음 제안을 띄우지 않아, 버린 원목이 내려앉고 새 원목이
    // 들어오는 결과를 눈으로 확인할 시간을 준다. 그 시간이 곧 신뢰가 쌓이는 시간이다.

    /// <summary>
    /// 버려질 슬롯이나 들어올 원목이 바뀌었을 때(생김 / 사라짐 / 다른 슬롯으로 옮겨감 / 개수 변화)
    /// 발생한다. 최신 내용은 GetLogSwapInfo()로 읽는다.
    /// </summary>
    public event Action SwapStateChangedEvent;

    // 들어올 원목(요청): 최근 감지 틱에서 못 먹은 원목 중 가장 비싼 종류와, 그 종류가 몇 개 보였는지.
    private TreeType swapRequestTreeType = TreeType.None;
    private LogState swapRequestLogState = LogState.Normal;
    private long swapRequestUnitValue = LogValue.NONE;
    private int swapRequestCount = 0;
    private float swapRequestTime = -999f;

    // 같은 프레임(= 같은 감지 틱) 안에서 거절된 원목을 종류별로 묶기 위한 누적기.
    // 캐릭터의 감지 틱은 반경 안의 원목 전부에 한 프레임 안에서 SetSuckTarget을 걸므로,
    // 프레임 번호가 같으면 같은 틱이다.
    private int rejectFrame = -1;
    private TreeType rejectBestTreeType = TreeType.None;
    private LogState rejectBestLogState = LogState.Normal;
    private long rejectBestUnitValue = LogValue.NONE;
    private int rejectBestCount = 0;

    /// <summary>
    /// "못 먹었다" 기록의 유효 시간. 캐릭터의 아이템 감지는 0.2초(5Hz)마다 돌면서 못 먹을 때마다
    /// 이 기록을 새로 찍으므로, 원목 위에 서 있는 동안은 계속 살아 있고 자리를 뜨면 곧 꺼진다.
    /// </summary>
    private const float SwapRequestLifetime = 0.5f;

    /// <summary>교체 직후 다음 제안을 띄우지 않는 시간. 결과를 눈으로 확인할 여유다.</summary>
    private const float SwapCooldown = 1.0f;

    /// <summary>현재 띄워 둔 제안의 슬롯. 무효가 되기 전까지는 더 나은 후보가 생겨도 바꾸지 않는다.</summary>
    private int stickyVictimSlotIndex = -1;

    private float swapCooldownUntil = -999f;

    /// <summary>마지막으로 알린 내용. 같은 내용을 거듭 알리지 않기 위한 것이다.</summary>
    private LogSwapSlotInfo lastNotifiedSwapInfo = LogSwapSlotInfo.None;

    /// <summary>
    /// 지금 교체하면 버려질 인벤토리 슬롯과 그 자리에 들어올 원목. 없으면 LogSwapSlotInfo.None.
    ///
    /// UI는 slotIndex로 자기 슬롯 뷰를 찾고(IInventory.inventorySlots와 같은 인덱스), 나머지로
    /// "소나무 3개 ↔ 자작나무 5개 (환율 3.3 : 1)"을 그리면 된다.
    /// </summary>
    public LogSwapSlotInfo GetLogSwapInfo()
    {
        if (!TryFindLogSwapVictimSlot(out int slotIndex)) return LogSwapSlotInfo.None;

        LogItemData logData = (LogItemData)inventorySlots[slotIndex].itemData;

        return new LogSwapSlotInfo
        {
            target = ELogSwapTarget.Inventory,
            slotIndex = slotIndex,
            treeType = logData.treeType,
            logState = logData.logState,
            count = inventorySlots[slotIndex].totalCount,
            unitValue = LogValue.GetUnitValue(logData.treeType, logData.logState),
            incomingTreeType = swapRequestTreeType,
            incomingLogState = swapRequestLogState,
            incomingCount = Mathf.Min(swapRequestCount, maxItemsPerSlot),
            incomingUnitValue = swapRequestUnitValue,
        };
    }

    /// <summary>
    /// 원목을 담지 못했음을 교체 시스템에 알린다(CanAcquired 실패 경로에서 부른다).
    /// 한 번의 감지 틱에 여러 원목이 거절될 수 있으므로 그중 가장 비싼 종류를 기준으로 삼고, 그 종류가
    /// 몇 개 보였는지 함께 센다 - 교체는 "제일 아쉬운 원목을 위해" 자리를 만드는 기능이고, 상한은
    /// "지금 보이는 만큼"이기 때문이다.
    /// </summary>
    private void RequestLogSwap(TreeType _treeType, LogState _logState)
    {
        long unitValue = LogValue.GetUnitValue(_treeType, _logState);
        int frame = Time.frameCount;

        // 1. 같은 프레임 안에서 종류별로 묶는다. 더 비싼 종류가 나타나면 그쪽으로 갈아타고 개수를 다시 센다.
        if (frame != rejectFrame || unitValue > rejectBestUnitValue)
        {
            rejectFrame = frame;
            rejectBestTreeType = _treeType;
            rejectBestLogState = _logState;
            rejectBestUnitValue = unitValue;
            rejectBestCount = 1;
        }
        else if (_treeType == rejectBestTreeType && _logState == rejectBestLogState)
        {
            rejectBestCount++;
        }

        // 2. 요청에 반영한다. 유효 시간은 "기록된 그 원목을 마지막으로 본 시각"부터 센다. 더 싼 원목이
        //    거절됐다고 시각을 갱신하면 안 된다 - 원목 더미 위를 계속 걷는 동안 기록이 영원히 살아남아,
        //    한 번 스쳐간 흑요목 때문에 참나무를 먹으려고 소나무를 버리는 손해 교체가 성립한다.
        bool expired = swapRequestUnitValue == LogValue.NONE
            || Time.time - swapRequestTime > SwapRequestLifetime;

        if (expired || rejectBestUnitValue >= swapRequestUnitValue)
        {
            swapRequestTreeType = rejectBestTreeType;
            swapRequestLogState = rejectBestLogState;
            swapRequestUnitValue = rejectBestUnitValue;
            swapRequestCount = rejectBestCount;
            swapRequestTime = Time.time;
        }
    }

    private void ClearLogSwapRequest()
    {
        swapRequestTreeType = TreeType.None;
        swapRequestLogState = LogState.Normal;
        swapRequestUnitValue = LogValue.NONE;
        swapRequestCount = 0;
        swapRequestTime = -999f;
        stickyVictimSlotIndex = -1;
    }

    /// <summary>
    /// 제안 내용이 달라졌으면 한 번만 알린다. Update에서 매 프레임 호출된다.
    /// (슬롯 수가 10칸 상한이라 매 프레임 훑어도 부담이 없고, 요청이 없으면 곧바로 빠져나온다)
    /// </summary>
    private void UpdateLogSwapState()
    {
        if (swapRequestUnitValue != LogValue.NONE && Time.time - swapRequestTime > SwapRequestLifetime)
        {
            ClearLogSwapRequest();
        }

        LogSwapSlotInfo info = GetLogSwapInfo();
        if (LogSwapSlotInfo.IsSame(in info, in lastNotifiedSwapInfo)) return;

        lastNotifiedSwapInfo = info;
        SwapStateChangedEvent?.Invoke();
    }

    /// <summary>
    /// 이 슬롯이 지금의 요청에 대해 자격과 상한을 모두 통과하는지. 통과하면 총 가치와 개당 가치를 준다.
    /// </summary>
    private bool IsLogSwapCandidate(int _slotIndex, out long _totalValue, out long _unitValue)
    {
        _totalValue = 0;
        _unitValue = 0;

        if (_slotIndex < 0 || _slotIndex >= Mathf.Min(currentSlotCount, inventorySlots.Count)) return false;

        InventorySlot slot = inventorySlots[_slotIndex];
        if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) return false;

        // 보석 보호
        if (LogValue.IsGemGrade(logData.logState)) return false;

        // 자격: 들어올 원목보다 개당 싸야 한다
        _unitValue = LogValue.GetUnitValue(logData.treeType, logData.logState);
        if (_unitValue >= swapRequestUnitValue) return false;

        // 상한: 잃는 총 가치가 "보이는 만큼"을 넘지 않아야 한다
        _totalValue = _unitValue * slot.totalCount;
        long gainBound = swapRequestUnitValue * Mathf.Min(swapRequestCount, maxItemsPerSlot);

        return _totalValue <= gainBound;
    }

    /// <summary>
    /// 버릴 슬롯을 고른다. 현재 제안이 아직 유효하면 그대로 유지하고(sticky), 아니면 후보 중
    /// 총 가치가 가장 낮은 것을(같으면 개당 가치가 낮은 쪽) 새로 고른다. 없으면 false.
    /// </summary>
    private bool TryFindLogSwapVictimSlot(out int _slotIndex)
    {
        _slotIndex = -1;

        if (swapRequestUnitValue == LogValue.NONE) return false;
        if (Time.time < swapCooldownUntil) return false;

        if (stickyVictimSlotIndex >= 0 && IsLogSwapCandidate(stickyVictimSlotIndex, out _, out _))
        {
            _slotIndex = stickyVictimSlotIndex;
            return true;
        }

        long bestTotalValue = long.MaxValue;
        long bestUnitValue = long.MaxValue;

        int slotCount = Mathf.Min(currentSlotCount, inventorySlots.Count);
        for (int i = 0; i < slotCount; i++)
        {
            if (!IsLogSwapCandidate(i, out long totalValue, out long unitValue)) continue;

            if (totalValue < bestTotalValue || (totalValue == bestTotalValue && unitValue < bestUnitValue))
            {
                bestTotalValue = totalValue;
                bestUnitValue = unitValue;
                _slotIndex = i;
            }
        }

        stickyVictimSlotIndex = _slotIndex;
        return _slotIndex >= 0;
    }

    /// <summary>
    /// 교체를 실행한다. 고른 슬롯의 <b>데이터는 즉시 사라지고</b>(UI도 ItemRemoved로 곧바로 갱신된다),
    /// 흘리는 연출만 뒤따라 재생된다. 비워진 자리에는 다음 감지 틱에 바닥의 원목이 알아서 들어온다.
    /// </summary>
    /// <returns>실제로 버린 슬롯의 내용. 교체할 것이 없었으면 LogSwapSlotInfo.None.</returns>
    public LogSwapSlotInfo ExecuteLogSwap(Transform _dropOriginTransform)
    {
        LogSwapSlotInfo info = GetLogSwapInfo();
        if (!info.bHasSlot) return LogSwapSlotInfo.None;

        InventorySlot slot = inventorySlots[info.slotIndex];

        // DropAllItem과 같은 순서를 지킨다 - 슬롯을 먼저 비우고 나서 알려야 UI가 이미 비워진 데이터를
        // 읽는다. 알림을 먼저 하면 UI가 아직 남아 있는 데이터를 그린 채로 굳는다.
        itemDataPool.Release((ItemData)slot.itemData);
        slot.Setup(null, 0);

        for (int i = 0; i < info.count; i++)
        {
            ItemRemoved();
        }

        UpdateInventoryEmptyState();

        // 자리가 생겼으니 이번 요청은 끝났다. 잠시 쉬었다가, 여전히 못 먹는 상황이면 감지 틱이 다시 찍는다.
        swapCooldownUntil = Time.time + SwapCooldown;
        ClearLogSwapRequest();
        UpdateLogSwapState();

        Vector3 dropPos = _dropOriginTransform != null ? _dropOriginTransform.position : transform.position;
        PlayLogDropVisuals(info.treeType, info.logState, info.count, dropPos);

        return info;
    }

    public void ExpandInventorySlotCnt(float _amount)
    {
        currentSlotCount = Mathf.Min(currentSlotCount + (int)_amount, SYSTEM_VAR.MAX_INVENTORY_CNT);
        InventorySpecChangedEvent?.Invoke();
    }

    public void LogCapacityIncrease(float _amount)
    {
        maxItemsPerSlot += (int)_amount;

        // "보이는 만큼"의 상한이 슬롯 용량이므로, 특성으로 용량이 바뀌면 흡입 정렬 쪽에도 알린다.
        LogValue.SetSlotCapacity(maxItemsPerSlot);
    }

    /// <summary>
    /// 원석 주머니 한도를 늘린다("원석 주머니 확장" 특성 - SC_GemOrePouchExpansion).
    /// 한도가 줄어도(특성 Undo) 갖고 있던 원석을 깎지는 않는다 - 더 담지 못할 뿐이다.
    /// </summary>
    public void IncreaseGemOrePouchCapacity(float _amount)
    {
        gemOrePouchCapacity = Math.Max(0, gemOrePouchCapacity + (long)_amount);

        // 슬롯 증설(ExpandInventorySlotCnt)과 같은 통로로 알린다. 이 이벤트는
        // InventorySpecChangedSignal -> UIView_Popup.InventorySpecChanged -> UI_Inventory.Refresh로
        // 이어지므로, 특성으로 주머니가 커지는 즉시 HUD가 새 한도를 읽어간다.
        InventorySpecChangedEvent?.Invoke();
    }

    public void LoadSaveData(InventorySaveData _data)
    {
        money = _data.money;
        carrot = _data.carrot;
        goldOre = _data.goldOre;
        diamondOre = _data.diamondOre;
        prismOre = _data.prismOre;

        // 이 필드가 없던 예전 세이브는 false로 읽힌다. 그때는 보유량으로 되짚어, 이미 캐 둔
        // 원석이 HUD에서 사라지지 않게 한다. 용광로가 없던 시절의 세이브라 "원석이 용광로에
        // 들어가 있어 보유량만 0"인 경우가 존재할 수 없으므로, 보유량만 봐도 판정이 정확하다.
        bHasEverAcquiredGoldOre = _data.bHasEverAcquiredGoldOre || goldOre > 0;
        bHasEverAcquiredDiamondOre = _data.bHasEverAcquiredDiamondOre || diamondOre > 0;
        bHasEverAcquiredPrismOre = _data.bHasEverAcquiredPrismOre || prismOre > 0;

        // 주머니 한도는 복원하지 않는다. 스킬 트리 복원이 시작 한도 위에 특성 몫을 다시 얹어
        // 이미 맞는 값이 들어와 있다(필드 선언부 주석 참고).

        // 기존 슬롯 초기화 (풀 반환)
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].itemData is ItemData itemData)
            {
                itemDataPool.Release(itemData);
            }
            inventorySlots[i].Setup(null, 0);
        }

        // 데이터 복구
        if (_data.slots != null)
        {
            for (int i = 0; i < _data.slots.Count; i++)
            {
                if (i >= inventorySlots.Count) break;

                var slotData = _data.slots[i];
                if (slotData.itemSaveData.itemType != ItemType.None)
                {
                    ItemData newData = itemDataPool.Get(slotData.itemSaveData.itemType);
                    if (newData != null)
                    {
                        newData.color = slotData.itemSaveData.color; // 컬러 복구

                        // 타입별 세부 정보 복구
                        if (newData is LogItemData logData)
                        {
                            logData.treeType = slotData.itemSaveData.treeType;
                            logData.logState = slotData.itemSaveData.logState;

                            var typeData = logItemTypeDataBase.Get(logData.treeType);
                            if (typeData != null)
                            {
                                // 황금/다이아/무지개 원목은 상태별 스프라이트를 써야 한다.
                                logData.sprite = typeData.GetSprite(logData.logState);
                            }
                        }
                        else if (newData is LootItemData lootData)
                        {
                            lootData.lootType = slotData.itemSaveData.lootType;
                        }

                        inventorySlots[i].Setup(newData, slotData.totalCount);

                        // 상세 나무 종류 개수 복구 (Log 아이템인 경우)
                        if (slotData.treeTypeCounts != null && slotData.treeTypeCounts.Length > 0)
                        {
                            inventorySlots[i].LoadTreeTypeCounts(slotData.treeTypeCounts);
                        }
                    }
                }
            }
        }

        SpendMoneyEvent?.Invoke();
        InventorySpecChangedEvent?.Invoke();
        UpdateInventoryEmptyState();
        Debug.Log("[InventoryManager] Inventory Save Data Loaded.");
    }

    public void SetLostAndFoundBoxEffect(bool _value)
    {
        hasLostAndFoundBoxEffect = _value;
    }

    /// <summary>
    /// "원목 보험 증서" 효과: 탈진으로 유실되기 직전, 유실 예정 원목의 <b>총 30%</b>를 나무 종류별로
    /// 나눠 담아 오프로드 컨테이너로 미리 빼낸다(연출 없이 즉시 커밋). 30%가 정확히 떨어지지 않아 생기는
    /// 잔여분은 고가 원목이 먼저 가져가므로, 종류별 비율은 30%보다 높거나 낮을 수 있고 합계만 30%다.
    /// 반드시 DropAllItem보다 먼저 호출해야 한다 - 여기서 미리 빼낸 만큼 슬롯 수량이 줄어든 상태로
    /// DropAllItem이 나머지만 정상 유실 처리하게 된다.
    ///
    /// 전체 합의 30%를 슬롯 앞에서부터 긁어오면 안 된다. 슬롯은 (나무 종류, 로그 상태) 단위로 쪼개지고
    /// 슬롯당 상한(maxItemsPerSlot)도 있어서 같은 종류가 여러 슬롯에 흩어지는데, 그러면 A 10개 + B 20개를
    /// 들고 있을 때 앞쪽 A 슬롯만 통째로 9개 빠지고 B는 한 개도 구제되지 않는다. 종류별로 목표치를 따로
    /// 잡아 A 3개 / B 6개가 담기게 한다.
    ///
    /// 단, 종류별로 각각 반올림하면 합계가 30%를 넘어버린다(2종류 x 5개 = 총 10개인데 종류별
    /// RoundToInt(1.5) = 2개씩, 합 4개 = 40%). 그래서 합계 목표를 먼저 확정하고, 종류별로는 내림만 한 뒤
    /// 모자란 잔여분을 한 개씩 나눠주는 방식으로 합을 30%에 맞춘다.
    /// </summary>
    public int RescueItemsToOffroadContainer(OffroadContainer _container)
    {
        if (!hasLostAndFoundBoxEffect)
        {
            return 0;
        }

        if (_container == null) return 0;

        // 결과와 무관하게 이번 런의 효과를 소모한다. 탈진은 런당 한 번뿐이고, 다음 던전 진입 때
        // InDungeonObjectManager.SetupForForestType()이 영구 획득 여부를 보고 다시 무장해준다.
        hasLostAndFoundBoxEffect = false;

        // 나무 종류별 유실 예정 수량 집계
        Array.Clear(rescueAtRiskCountsByTreeType, 0, rescueAtRiskCountsByTreeType.Length);
        int totalLogsAtRisk = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

            rescueAtRiskCountsByTreeType[(int)logData.treeType] += slot.totalCount;
            totalLogsAtRisk += slot.totalCount;
        }

        if (totalLogsAtRisk <= 0) return 0;

        BuildRescueTargets(totalLogsAtRisk);

        // 구제 실행도 고가 원목부터. 운반상자 여유가 모자랄 때 값싼 원목이 남은 슬롯을 선점해
        // 비싼 원목이 한 개도 구제되지 못하는 상황을 막는다.
        int rescuedCount = 0;
        for (int treeType = rescueTargetsByTreeType.Length - 1; treeType >= 0; treeType--)
        {
            if (rescueTargetsByTreeType[treeType] <= 0) continue;

            rescuedCount += RescueLogsOfTreeType(_container, (TreeType)treeType, rescueTargetsByTreeType[treeType]);
        }

        return rescuedCount;
    }

    /// <summary>
    /// rescueAtRiskCountsByTreeType(종류별 유실 예정 수량)을 보고 rescueTargetsByTreeType(종류별 구제
    /// 목표)을 채운다. 종류별 목표의 합은 항상 전체의 30%(반올림)와 정확히 일치한다.
    ///
    /// 종류별로 내림을 먼저 깔고, 합계 목표에 모자란 잔여분을 <b>고가 원목부터</b> 한 개씩 배분한다.
    /// 가치 순서는 TreeType enum 인덱스와 같다 - LogItemValueDataBase.asset의 기본 가치가 enum 순서대로
    /// 단조 증가한다(OakTree 4 → ObsidianTree 3천만). 그래서 인덱스를 내려가며 훑는 것이 곧 고가 순이다.
    /// (이 전제가 깨지면, 즉 enum 순서와 가치 순서가 어긋나게 되면 이 배분도 함께 고쳐야 한다)
    /// </summary>
    private void BuildRescueTargets(int _totalLogsAtRisk)
    {
        Array.Clear(rescueTargetsByTreeType, 0, rescueTargetsByTreeType.Length);

        int totalRescueTarget = Mathf.RoundToInt(_totalLogsAtRisk * LostAndFoundRescueRatio);
        int assigned = 0;

        for (int treeType = 0; treeType < rescueAtRiskCountsByTreeType.Length; treeType++)
        {
            int atRisk = rescueAtRiskCountsByTreeType[treeType];
            if (atRisk <= 0) continue;

            int floored = Mathf.FloorToInt(atRisk * LostAndFoundRescueRatio);
            rescueTargetsByTreeType[treeType] = floored;
            assigned += floored;
        }

        // 잔여분 배분. 한 바퀴에 종류마다 한 개씩만 얹으므로 특정 종류가 보유량을 넘겨 받지 않는다.
        // 모든 종류가 보유량 상한에 걸려 더 얹을 곳이 없으면 그 자리에서 멈춘다(무한 루프 방지).
        int remaining = totalRescueTarget - assigned;
        while (remaining > 0)
        {
            bool bProgressed = false;

            for (int treeType = rescueTargetsByTreeType.Length - 1; treeType >= 0 && remaining > 0; treeType--)
            {
                if (rescueTargetsByTreeType[treeType] >= rescueAtRiskCountsByTreeType[treeType]) continue;

                rescueTargetsByTreeType[treeType]++;
                remaining--;
                bProgressed = true;
            }

            if (!bProgressed) break;
        }
    }

    /// <summary>
    /// 지정한 나무 종류를 들고 있는 슬롯들을 훑어 _rescueTarget개를 컨테이너로 옮긴다.
    ///
    /// 컨테이너가 가득 차 거절당하면 그 슬롯만 포기하고 다음 슬롯으로 넘어간다. 컨테이너의 여유는
    /// (나무 종류, 로그 상태) 조합별로 판정되므로(OffroadContainer.CanAddItemByData) 한 조합이 막혀도
    /// 다른 조합은 아직 들어갈 수 있고, 여기서 전부 포기하면 뒤쪽 종류가 이유 없이 구제받지 못한다.
    /// </summary>
    private int RescueLogsOfTreeType(OffroadContainer _container, TreeType _treeType, int _rescueTarget)
    {
        int rescuedCount = 0;

        for (int i = 0; i < currentSlotCount && rescuedCount < _rescueTarget; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;
            if (logData.treeType != _treeType) continue;

            while (rescuedCount < _rescueTarget && slot.totalCount > 0)
            {
                // 컨테이너 쪽에 자리가 있는지 먼저 확인 후 성공했을 때만 캐릭터 슬롯에서 차감한다.
                // 순서를 반대로 하면(먼저 차감 후 실패 시 롤백) 데이터가 증발할 위험이 있다.
                if (!_container.TryAddLogItemDataDirect(logData, logData.logState, false))
                {
                    break;
                }

                slot.TakeOneItem();
                rescuedCount++;
                ItemRemoved();
            }

            if (slot.totalCount <= 0)
            {
                ItemDeleted(slot);
            }
        }

        return rescuedCount;
    }

    /// <summary>
    /// 던전 입장 시점(DungeonStartSignal)에 인벤토리에 남아있는 원목을 오프로드 컨테이너로 전량
    /// 이전한다(연출 없이 즉시 커밋 - 이 시점엔 캐릭터가 아직 조작 불가 상태라 날아가는 연출이 의미
    /// 없다). 컨테이너 쪽에 자리가 있는지 먼저 확인 후 성공했을 때만 인벤토리에서 차감해야
    /// RescueItemsToOffroadContainer와 마찬가지로 데이터 증발을 막을 수 있다. 컨테이너가 가득 차서
    /// 더 이상 옮길 수 없으면 그 시점에서 중단하고 남은 수량은 인벤토리에 그대로 남긴다.
    /// </summary>
    public int TransferAllLogItemsToOffroadContainer(OffroadContainer _container)
    {
        if (_container == null) return 0;

        int transferredCount = 0;

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

            while (slot.totalCount > 0)
            {
                if (!_container.TryAddLogItemDataDirect(logData, logData.logState, false))
                    break;

                slot.TakeOneItem();
                transferredCount++;
                ItemRemoved();
            }

            if (slot.totalCount <= 0)
            {
                ItemDeleted(slot);
            }
        }

        return transferredCount;
    }

    public int DropAllItem(Transform _charTransform)
    {
        if (_charTransform == null) return 0;

        int totalDroppedCount = 0;
        Vector3 startPos = _charTransform.position;

        // 실제로 보유한 나무 종류가 골고루 섞이도록 라운드로빈으로 짠, 최대 MaxDropVisualCount개의 연출 순서
        // (TreeType/LogState 값만 미리 복사해둔다 - 아래에서 슬롯이 비워지며 원본 LogItemData가 풀로 반환/리셋되므로
        //  코루틴이 나중에 참조를 그대로 쓰면 안 된다)
        List<(TreeType treeType, LogState logState)> dropVisualPlan = BuildLogDropVisualPlan();

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot.itemData == null || slot.totalCount <= 0) continue;

            int count = slot.totalCount;
            totalDroppedCount += count;

            // 슬롯 비우기 및 데이터 반환. 반드시 ItemRemoved()보다 먼저 해야 한다 - ItemRemoved()는
            // 인벤토리 UI 전체 갱신(UIView_Popup.ItemRemovedFromInventory → UI_Inventory.UpdateSlots)을
            // 유발하고 UI는 그때그때 슬롯 데이터를 다시 읽으므로, 비우기 전에 알리면 UI가 아직 안 비워진
            // 데이터를 그린다. 그러면 마지막으로 처리한 슬롯은 비워진 뒤 갱신을 유발할 이벤트가 더 없어
            // 화면에 그대로 남는다. 루프 뒤의 LoosAllInventoryItemSignal이 한 번 더 UI를 맞춰주지만
            // (UIView_Popup.LoosAllInventoryItems) 그건 안전망일 뿐이고, 근본 순서는 여기서 지킨다.
            itemDataPool.Release((ItemData)slot.itemData);
            slot.Setup(null, 0);

            for (int j = 0; j < count; j++)
            {
                ItemRemoved();
            }
        }

        UpdateInventoryEmptyState();
        LoosAllInventoryItemEvent?.Invoke();

        // 데이터는 위에서 즉시 전량 제거하고, 화면 연출만 한 번에 몰아서 터지지 않도록 순차적으로 하나씩 발사한다
        if (dropVisualsCoroutine != null)
        {
            StopCoroutine(dropVisualsCoroutine);
            dropVisualsCoroutine = null;
        }

        if (dropVisualPlan.Count > 0)
        {
            dropVisualsCoroutine = StartCoroutine(SpawnDropVisualsRoutine(dropVisualPlan, startPos));
        }

        return totalDroppedCount;
    }

    /// <summary>
    /// 같은 종류의 원목 여러 개를 "흘리는" 연출만 재생한다(데이터는 건드리지 않는다).
    ///
    /// 교체 시스템이 버린 슬롯을 DropAllItem과 같은 모습으로 흘리기 위해 쓴다. 운반 상자 쪽 교체도
    /// 이 통로를 쓰므로(연출 시작 위치만 상자 위치로 준다) 흘리는 연출의 구현은 한 곳에만 있다.
    ///
    /// 진행 중이던 흘리기 연출이 있으면 DropAllItem과 마찬가지로 끊고 새로 시작한다.
    /// </summary>
    public void PlayLogDropVisuals(TreeType _treeType, LogState _logState, int _count, Vector3 _startPos)
    {
        if (_count <= 0) return;

        // 버퍼를 다시 채우기 전에 진행 중인 연출을 먼저 멈춘다 - 그 코루틴이 같은 버퍼를 읽고 있을 수 있다.
        if (dropVisualsCoroutine != null)
        {
            StopCoroutine(dropVisualsCoroutine);
            dropVisualsCoroutine = null;
        }

        swapDropVisualPlan.Clear();

        int visualCount = Mathf.Min(MaxDropVisualCount, _count);
        for (int i = 0; i < visualCount; i++)
        {
            swapDropVisualPlan.Add((_treeType, _logState));
        }

        dropVisualsCoroutine = StartCoroutine(SpawnDropVisualsRoutine(swapDropVisualPlan, _startPos));
    }

    public float GetDropVisualDuration()
    {
        int totalLogCount = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData is LogItemData && inventorySlots[i].totalCount > 0)
            {
                totalLogCount += inventorySlots[i].totalCount;
            }
        }
        int count = Mathf.Min(MaxDropVisualCount, totalLogCount);
        if (count <= 0) return 0f;
        return (count - 1) * dropVisualInterval;
    }

    // DropAllItem에서 실제로 보유 중인 나무 종류(로그 슬롯)가 골고루 섞이도록 라운드로빈으로 연출 순서를 짠다.
    // 예) Oak 20개 + Pine 10개를 들고 있을 때 상한이 15라면, 어느 한 종류가 몰리지 않고
    // Oak-Pine-Oak-Pine... 순서로 번갈아 섞여서 최대 15개까지 순서대로 날아가도록 한다.
    private List<(TreeType treeType, LogState logState)> BuildLogDropVisualPlan()
    {
        List<(TreeType treeType, LogState logState)> plan = new List<(TreeType, LogState)>(MaxDropVisualCount);

        List<int> logSlotIndices = new List<int>();
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData is LogItemData && inventorySlots[i].totalCount > 0)
            {
                logSlotIndices.Add(i);
            }
        }

        if (logSlotIndices.Count == 0) return plan;

        int[] remaining = new int[currentSlotCount];
        int totalLogCount = 0;
        for (int i = 0; i < logSlotIndices.Count; i++)
        {
            int slotIndex = logSlotIndices[i];
            remaining[slotIndex] = inventorySlots[slotIndex].totalCount;
            totalLogCount += remaining[slotIndex];
        }

        int visualTarget = Mathf.Min(MaxDropVisualCount, totalLogCount);

        int cursor = 0;
        while (plan.Count < visualTarget)
        {
            int slotIndex = logSlotIndices[cursor % logSlotIndices.Count];
            if (remaining[slotIndex] > 0)
            {
                LogItemData logData = (LogItemData)inventorySlots[slotIndex].itemData;
                plan.Add((logData.treeType, logData.logState));
                remaining[slotIndex]--;
            }
            cursor++;
        }

        return plan;
    }

    // 미리 짜둔 순서(_plan)대로 나무 연출을 하나씩 연달아 발사한다.
    private IEnumerator SpawnDropVisualsRoutine(List<(TreeType treeType, LogState logState)> _plan, Vector3 _startPos)
    {
        for (int i = 0; i < _plan.Count; i++)
        {
            SpawnDropVisual(_plan[i].treeType, _plan[i].logState, _startPos);

            if (i < _plan.Count - 1)
            {
                yield return new WaitForSeconds(dropVisualInterval);
            }
        }

        dropVisualsCoroutine = null;
    }

    private void SpawnDropVisual(TreeType _treeType, LogState _logState, Vector3 _startPos)
    {
        LogItem logItem = logItemPoolingManager.GetLogItem(_treeType, _logState);
        if (logItem == null) return;

        logItem.SetbCanAcquired(false);
        logItem.transform.position = _startPos;
        logItem.SetInventoryChecker(this);
        logItem.IsDropItem(true);

        // 포물선 비행 도중 서서히 알파가 0이 되어, 착지하지 않고 공중에서 사라지는 연출
        logItem.SetFadeAndVanish(true);
        logItem.LogItemVanishedEvent -= LogItemVanished;
        logItem.LogItemVanishedEvent += LogItemVanished;

        activeDroppedItems.Add(logItem);

        // 나무를 벨 때(LogItemController.SpawnLogItem)와 같은 방식의 포물선 운동: 캐릭터 머리 위로
        // 높이 솟구쳤다가 무작위 방향으로 퍼지며 빙글빙글 회전하도록 한다.
        Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
        float randomDist = UnityEngine.Random.Range(1.25f, 1.75f);
        Vector3 endPos = _startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0f) * randomDist;

        float height = UnityEngine.Random.Range(1.8f, 2.8f);
        float randomRotation = UnityEngine.Random.Range(1, 3) * 360f * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

        logItem.SetVfxComponent(vfxComponent);
        logItem.Launch(_startPos, endPos, height, randomRotation);

        Sound.PlayUI(SoundID.OutItem);

        // 원목이 하나씩 순차적으로 튀어나가므로, 흘릴 때마다 1~2틱짜리 진동이 톡톡 이어진다.
        Rumble.Play(EHapticEvent.ItemDropped);
    }

    // 공중에서 페이드아웃되어 사라지는 연출(DropAllItem)이 끝난 LogItem을 즉시 풀로 반환한다.
    private void LogItemVanished(LogItem _item)
    {
        _item.LogItemVanishedEvent -= LogItemVanished;
        activeDroppedItems.Remove(_item);
        logItemPoolingManager.ReturnLogItem(_item);
    }

    public void ReleaseAllDroppedItem()
    {
        reservedItems.Clear();

        // 아직 순차 발사 중이던 나머지 연출은 씬 전환 시점에 더 이상 의미가 없으므로 중단한다
        if (dropVisualsCoroutine != null)
        {
            StopCoroutine(dropVisualsCoroutine);
            dropVisualsCoroutine = null;
        }

        if (activeDroppedItems.Count == 0) return;

        for (int i = 0; i < activeDroppedItems.Count; i++)
        {
            // 페이드아웃이 끝나기 전에 씬이 전환되는 경우를 대비해, 풀로 되돌리기 전에 구독을 먼저 해제한다
            // (해제하지 않으면 ReturnLogItem 이후 이 인스턴스가 재사용될 때 이전 구독이 남아있게 된다)
            activeDroppedItems[i].LogItemVanishedEvent -= LogItemVanished;
            logItemPoolingManager.ReturnLogItem(activeDroppedItems[i]);
        }
        activeDroppedItems.Clear();
    }

    public void ItemAdded()
    {
        ItemAddedEvent?.Invoke();
    }

    public void ItemRemoved()
    {
        ItemRemovedEvent?.Invoke();
    }

    public void TriggerItemCantAcquied()
    {
        ItemCantAcquiedEvent?.Invoke();
    }

    public void TriggerInventoryIsFull()
    {
        InventoryIsFullEvent?.Invoke();
    }

    public void SetMoney(long _money)
    {
        money = _money;
    }

    private void UpdateInventoryEmptyState()
    {
        bool isEmpty = true;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null && inventorySlots[i].totalCount > 0)
            {
                isEmpty = false;
                break;
            }
        }
        bInventoryIsEmpty = isEmpty;
    }
}
