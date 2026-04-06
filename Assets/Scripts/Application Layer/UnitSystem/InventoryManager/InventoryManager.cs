using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InventoryManager : MonoBehaviour, IInventory
{
    // 내부 의존성
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    private int money;
    
    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;

    int IInventory.money => money;

    public void Initialize()
    {
        // 1. 기존 슬롯의 데이터들을 풀로 반환하고 슬롯 초기화
        if (inventorySlots.Count == 0)
        {
            for (int i = 0; i < SYSTEM_VAR.MAX_INVENTORY_CNT; i++)
            {
                inventorySlots.Add(new InventorySlot());
            }
        }
        else
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (inventorySlots[i].itemData is ItemData data)
                {
                    ReleaseToPool(data);
                }
                inventorySlots[i].Setup(null, 0);
            }
        }

        // 2. 모든 아이템 타입에 대해 풀 미리 생성 (None, Max 제외)
        for (int i = (int)ItemType.None + 1; i < (int)ItemType.Max; i++)
        {
            ItemType type = (ItemType)i;
            if (!itemDataPools.ContainsKey(type))
            {
                itemDataPools[type] = CreatePoolForType(type);
            }
        }
    }
    
    public void ItemAcquired(Item _item)
    {
        if (_item == null) return;

        // 1. 기존 슬롯 확인 (중첩 가능한지)
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].itemData != null && IsSameItem(_item, (ItemData)inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCount(_item);
                return;
            }
        }

        // 2. 새로운 타입인 경우 첫 번째 빈 슬롯을 찾아 데이터 가져와 추가
        for (int i = 0; i < inventorySlots.Count; i++)
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
    }


    private bool IsSameItem(Item _item, ItemData _data)
    {
        if (_item.itemType != _data.itemType) return false;

        if (_item is LogItem logItem && _data is LogItemData logData)
        {
            // 같은 나무 종류라면 같은 슬롯에 보관
            return logItem.treeType == logData.treeType;
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
