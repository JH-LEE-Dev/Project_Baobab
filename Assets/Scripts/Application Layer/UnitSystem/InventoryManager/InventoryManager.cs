using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;

    long IInventory.money => money;

    long IInventory.carrot => carrot;

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

    public void Initialize()
    {
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
                ReleaseToPool(data);
            }
            inventorySlots[i].Setup(null, 0);
        }

        // 3. 모든 아이템 타입에 대해 풀 미리 생성 (None, Max 제외)
        for (int i = (int)ItemType.None + 1; i < (int)ItemType.Max; i++)
        {
            ItemType type = (ItemType)i;
            if (!itemDataPools.ContainsKey(type))
            {
                itemDataPools[type] = CreatePoolForType(type);
            }
        }
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
                    ItemData newData = GetFromPool(_item.itemType);
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
            ItemAdded();
        }
        else
        {
            // 인벤토리가 가득 찼을 때의 처리
            InventoryIsFullEvent?.Invoke();
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
        _saveData.currentSlotCount = currentSlotCount;
        _saveData.maxItemsPerSlot = maxItemsPerSlot;

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

    private ItemData GetFromPool(ItemType _type)
    {
        if (!itemDataPools.ContainsKey(_type))
        {
            itemDataPools[_type] = CreatePoolForType(_type);
        }

        return itemDataPools[_type].Get();
    }

    private void ReleaseToPool(ItemData _data)
    {
        if (_data == null) return;
        if (itemDataPools.TryGetValue(_data.itemType, out var pool))
        {
            pool.Release(_data);
        }
    }

    private IObjectPool<ItemData> CreatePoolForType(ItemType _type)
    {
        return new ObjectPool<ItemData>(
            createFunc: () => CreateItemData(_type),
            actionOnGet: (data) => { },
            actionOnRelease: (data) => data.Reset(),
            actionOnDestroy: (data) => { },
            collectionCheck: true,
            defaultCapacity: 5,
            maxSize: 50
        );
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
                ReleaseToPool(slot.itemData);
            }
            slot.Setup(null, 0);
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

        // 2. 현재 남은 총 공간 계산
        int totalSpace = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (i >= inventorySlots.Count) break;

            if (inventorySlots[i].itemData != null)
            {
                if (inventorySlots[i].totalCount < maxItemsPerSlot && IsSameItem(_item, (ItemData)inventorySlots[i].itemData))
                {
                    totalSpace += (maxItemsPerSlot - inventorySlots[i].totalCount);
                }
            }
            else
            {
                totalSpace += maxItemsPerSlot;
            }
        }

        // 3. 같은 종류의 아이템 중 현재 예약된(Sucking 중인) 아이템 개수 계산
        int reservedCount = 0;
        for (int i = 0; i < reservedItems.Count; i++)
        {
            var reserved = reservedItems[i];
            if (reserved.itemType == _item.itemType && reserved.treeType == _item.treeType && reserved.logState == _item.logState)
            {
                reservedCount++;
            }
        }

        // 4. 예약 가능한 공간이 있으면 true 반환 및 예약 목록에 추가
        if (totalSpace - reservedCount > 0)
        {
            reservedItems.Add(_item);
            return true;
        }

        // 5. 들어올 수 없을 때 인벤토리 공간 상태 분석 및 이벤트 호출
        bool isFull = true;
        bool hasSpaceRemaining = false;

        for (int i = 0; i < currentSlotCount; i++)
        {
            if (i >= inventorySlots.Count) break;

            if (inventorySlots[i].itemData == null)
            {
                isFull = false;
            }
            else
            {
                // 해당 슬롯에 예약된 아이템 개수 계산
                int slotReservedCount = 0;
                var slotItemData = (ItemData)inventorySlots[i].itemData;
                for (int j = 0; j < reservedItems.Count; j++)
                {
                    var reserved = reservedItems[j];
                    if (reserved.itemType == slotItemData.itemType)
                    {
                        if (reserved is LogItem reservedLog && slotItemData is LogItemData logData)
                        {
                            if (reservedLog.logState == logData.logState && reservedLog.treeType == logData.treeType)
                            {
                                slotReservedCount++;
                            }
                        }
                    }
                }

                if (inventorySlots[i].totalCount + slotReservedCount < maxItemsPerSlot)
                {
                    isFull = false;
                    hasSpaceRemaining = true;
                }
            }
        }

        if (isFull)
        {
            InventoryIsFullEvent?.Invoke();
        }
        else if (hasSpaceRemaining)
        {
            ItemCantAcquiedEvent?.Invoke();
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
        currentSlotCount = _data.currentSlotCount;
        maxItemsPerSlot = _data.maxItemsPerSlot;

        // 기존 슬롯 초기화 (풀 반환)
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].itemData is ItemData itemData)
            {
                ReleaseToPool(itemData);
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
                    ItemData newData = GetFromPool(slotData.itemSaveData.itemType);
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
        Debug.Log("[InventoryManager] Inventory Save Data Loaded.");
    }

    public void DropAllItem(Transform _charTransform)
    {
        if (_charTransform == null) return;

        Vector3 startPos = _charTransform.position;

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot.itemData == null || slot.totalCount <= 0) continue;

            int count = slot.totalCount;

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

                        logItem.Launch(startPos, endPos, height, duration);
                    }

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
            ReleaseToPool((ItemData)slot.itemData);
            slot.Setup(null, 0);
        }

        LoosAllInventoryItemEvent?.Invoke();
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
}
