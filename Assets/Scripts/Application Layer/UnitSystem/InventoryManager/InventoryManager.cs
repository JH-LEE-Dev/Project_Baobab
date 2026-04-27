using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Pool;

public class InventoryManager : MonoBehaviour, IInventory, IInventoryForSkill, IInventoryChecker, IInventoryCH, IMoneyData
{
    public event Action SpendMoneyEvent;
    public event Action InventorySpecChangedEvent;
    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    private int money = 10000;
    private int carrot = 10000;

    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;

    int IInventory.money => money;

    int IInventory.carrot => carrot;

    public int currentSlotCnt => currentSlotCount;

    int IMoneyData.money => money;

    int IMoneyData.carrot => carrot;

    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    public void Initialize()
    {
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

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null &&
                inventorySlots[i].totalCount < maxItemsPerSlot &&
                IsSameItem(_item, (ItemData)inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCount(_item);
                return;
            }
        }

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
                }
                return;
            }
        }

        // TODO: 인벤토리가 가득 찼을 때의 처리 (아이템 획득 불가 등)
    }


    public void PopulateInventorySaveData(ref InventorySaveData _saveData)
    {
        _saveData.money = money;
        _saveData.carrot = carrot;
        _saveData.currentSlotCount = currentSlotCount;
        
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
                    slotData.logStateCounts = slot.GetLogStateCounts();
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
            // 같은 나무 종류라면 같은 슬롯에 보관
            return logItem.treeType == logData.treeType;
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
        carrot += (int)_amount;
    }

    public int GetCurrentCarrot()
    {
        return carrot;
    }

    public int GetCurrentMoney()
    {
        return money;
    }

    public void DecreaseCarrot(int _amount)
    {
        carrot -= _amount;
        if (carrot < 0) carrot = 0;
        SpendMoneyEvent?.Invoke();
    }

    public void DecreaseMoney(int _amount)
    {
        money -= _amount;
        if (money < 0) money = 0;
        SpendMoneyEvent?.Invoke();
    }

    public bool CanAcquired(LogItem _item)
    {
        if (_item == null) return false;

        // 1. 현재 활성화된 슬롯 중 공간이 있는 동일 아이템 슬롯이 있는지 확인
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (i >= inventorySlots.Count) break;

            if (inventorySlots[i].itemData != null &&
                inventorySlots[i].totalCount < maxItemsPerSlot &&
                IsSameItem(_item, (ItemData)inventorySlots[i].itemData))
            {
                return true;
            }
        }

        // 2. 빈 슬롯이 있는지 확인
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (i >= inventorySlots.Count) break;

            if (inventorySlots[i].itemData == null)
            {
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
        currentSlotCount = _data.currentSlotCount;

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
                        
                        // 상세 상태 개수 복구 (Log 아이템인 경우)
                        if (slotData.logStateCounts != null && slotData.logStateCounts.Length > 0)
                        {
                            LoadLogStateCountsToSlot(inventorySlots[i], slotData.logStateCounts);
                        }
                    }
                }
            }
        }

        SpendMoneyEvent?.Invoke();
        InventorySpecChangedEvent?.Invoke();
        Debug.Log("[InventoryManager] Inventory Save Data Loaded.");
    }

    private void LoadLogStateCountsToSlot(InventorySlot _slot, int[] _counts)
    {
        // Reflection이나 별도 메서드 없이 직접 접근이 불가능하므로 
        // InventorySlot에 LoadLogStateCounts 메서드를 추가하는 것이 정석이나,
        // 여기서는 기존 AddCountByState를 활용하여 수동으로 채우거나 
        // (단, Setup 시 logStateCounts가 초기화되므로 주의)
        // 정석대로 InventorySlot에 메서드를 추가하겠습니다.
        _slot.LoadLogStateCounts(_counts);
    }
}
