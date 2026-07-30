using System;
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

    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    private long money = 0;
    private long carrot = 0;
    [SerializeField] private long sunEssence;
    [SerializeField] private long moonEssence;
    [SerializeField] private long lightningEssence;

    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private ItemDataPool itemDataPool;

    // "분실물 보관함" 루트 아이템 효과 보유 상태 (세션 한정, 리타이어 1회 소모)
    private bool hasLostAndFoundBoxEffect;

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

    public int maxItemCntPerSlot => maxItemsPerSlot;

    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    private LogItemPoolingManager logItemPoolingManager;
    private List<LogItem> activeDroppedItems = new List<LogItem>(64);

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

        UpdateInventoryEmptyState();
    }

    private void Update()
    {
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

    public void MoneyEarned(int _money)
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
        int slotCount = Mathf.Min(currentSlotCount, inventorySlots.Count);
        var virtualSlots = new VirtualSlot[slotCount];
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
    private bool TryPlaceVirtual(LogItem _item, VirtualSlot[] _virtualSlots)
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

    public void ExpandInventorySlotCnt(float _amount)
    {
        currentSlotCount = Mathf.Min(currentSlotCount + (int)_amount, SYSTEM_VAR.MAX_INVENTORY_CNT);
        InventorySpecChangedEvent?.Invoke();
    }

    public void LogCapacityIncrease(float _amount)
    {
        maxItemsPerSlot += (int)_amount;
    }

    public void LoadSaveData(InventorySaveData _data)
    {
        money = _data.money;
        carrot = _data.carrot;

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
                                logData.sprite = typeData.sprite;
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
    /// "분실물 보관함" 효과: 리타이어로 유실되기 직전, 유실 예정 원목의 20~30%를 오프로드 컨테이너로
    /// 미리 빼낸다(연출 없이 즉시 커밋). 반드시 DropAllItem보다 먼저 호출해야 한다 - 여기서 미리 빼낸
    /// 만큼 슬롯 수량이 줄어든 상태로 DropAllItem이 나머지만 정상 유실 처리하게 된다. 1회성 효과이므로
    /// 호출 시점에 성공 여부와 무관하게 플래그를 소모한다.
    /// </summary>
    public int RescueItemsToOffroadContainer(OffroadContainer _container)
    {
        if (!hasLostAndFoundBoxEffect)
        {
            return 0;
        }

        hasLostAndFoundBoxEffect = false;

        if (_container == null) return 0;

        int totalLogsAtRisk = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot.itemData is LogItemData && slot.totalCount > 0)
            {
                totalLogsAtRisk += slot.totalCount;
            }
        }

        if (totalLogsAtRisk <= 0) return 0;

        int rescueTarget = Mathf.RoundToInt(totalLogsAtRisk * UnityEngine.Random.Range(0.2f, 0.3f));
        int rescuedCount = 0;
        bool containerFull = false;

        for (int i = 0; i < currentSlotCount && rescuedCount < rescueTarget && !containerFull; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

            while (rescuedCount < rescueTarget && slot.totalCount > 0)
            {
                // 컨테이너 쪽에 자리가 있는지 먼저 확인 후 성공했을 때만 캐릭터 슬롯에서 차감한다.
                // 순서를 반대로 하면(먼저 차감 후 실패 시 롤백) 데이터가 증발할 위험이 있다.
                if (!_container.TryAddLogItemDataDirect(logData, logData.logState))
                {
                    containerFull = true;
                    break;
                }

                slot.TakeOneItem();
                Sound.PlayUI(SoundID.OutItem);
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
                if (!_container.TryAddLogItemDataDirect(logData, logData.logState))
                    break;

                slot.TakeOneItem();
                Sound.PlayUI(SoundID.OutItem);
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

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot.itemData == null || slot.totalCount <= 0) continue;

            int count = slot.totalCount;
            totalDroppedCount += count;

            // Log 아이템인 경우 처리
            if (slot.itemData is LogItemData logData)
            {
                for (int j = 0; j < count; j++)
                {
                    LogItem logItem = logItemPoolingManager.GetLogItem(logData);
                    logItem.SetbCanAcquired(false);

                    if (logItem != null)
                    {
                        logItem.transform.position = startPos;
                        logItem.SetInventoryChecker(this);
                        logItem.IsDropItem(true);

                        activeDroppedItems.Add(logItem);

                        // 무작위 방향 및 거리 설정
                        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                        float distance = UnityEngine.Random.Range(0.5f, 1.2f);
                        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
                        Vector3 endPos = startPos + offset;

                        float height = UnityEngine.Random.Range(0.3f, 0.6f);
                        float duration = UnityEngine.Random.Range(0.4f, 0.6f);  

                        logItem.SetVfxComponent(vfxComponent);
                        logItem.Launch(startPos, endPos, height, duration);
                    }

                    Sound.PlayUI(SoundID.OutItem);
                    ItemRemoved();
                }
            }
            else
            {
                for (int j = 0; j < count; j++)
                {
                    ItemRemoved();
                }
            }

            // 슬롯 비우기 및 데이터 반환
            itemDataPool.Release((ItemData)slot.itemData);
            slot.Setup(null, 0);
        }

        UpdateInventoryEmptyState();
        LoosAllInventoryItemEvent?.Invoke();

        return totalDroppedCount;
    }

    public void ReleaseAllDroppedItem()
    {
        reservedItems.Clear();
        
        if (activeDroppedItems.Count == 0) return;

        for (int i = 0; i < activeDroppedItems.Count; i++)
        {
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

    public void SetMoney(int _money)
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
