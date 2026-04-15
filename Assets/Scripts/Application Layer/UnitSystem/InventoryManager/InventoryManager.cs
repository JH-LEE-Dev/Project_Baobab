using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InventoryManager : MonoBehaviour, IInventory
{
    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    private int money;
    
    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;

    int IInventory.money => money;

    public void Initialize()
    {
        // 1. 슬롯 리스트 초기화 및 개수 확보
        if (inventorySlots.Count < currentSlotCount)
        {
            int needCount = currentSlotCount - inventorySlots.Count;
            for (int i = 0; i < needCount; i++)
            {
                inventorySlots.Add(new InventorySlot());
            }
        }

        // 2. 기존 슬롯의 데이터들을 풀로 반환하고 슬롯 초기화
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
        int targetCount = Mathf.Min(currentSlotCount + _amount, SYSTEM_VAR.MAX_INVENTORY_CNT);
        int addCount = targetCount - inventorySlots.Count;

        for (int i = 0; i < addCount; i++)
        {
            inventorySlots.Add(new InventorySlot());
        }

        currentSlotCount = targetCount;
        
        // UI 갱신을 위한 시그널 등이 필요할 수 있으나, 현재는 슬롯 리스트 확장까지만 구현
    }
    
    public void ItemAcquired(Item _item)
    {
        if (_item == null) return;

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능한지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (i >= inventorySlots.Count) break;

            if (inventorySlots[i].itemData != null && IsSameItem(_item, (ItemData)inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCount(_item);
                return;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (i >= inventorySlots.Count) break;

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
}
