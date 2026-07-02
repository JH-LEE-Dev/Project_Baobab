using System;
using System.Collections.Generic;
using UnityEngine.Pool;

/// <summary>
/// ItemType별로 ItemData 인스턴스를 풀링하는 재사용 가능한 헬퍼.
/// InventoryManager/OffroadContainer/LogContainer/LumberjackInventoryComponent가 각자 들고 있던
/// 동일한 풀 관리 로직(Dictionary + 지연 생성 + Get/Release)을 하나로 모은 것.
/// 아이템 타입별 실제 인스턴스 생성 로직은 소비자마다 다를 수 있어(예: LogContainer는 Loot 타입을
/// 전용 처리하지 않음) 팩토리 델리게이트로 주입받아 기존 동작을 그대로 보존한다.
/// </summary>
public class ItemDataPool
{
    private readonly Func<ItemType, ItemData> createItemData;
    private readonly Dictionary<ItemType, IObjectPool<ItemData>> pools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    public ItemDataPool(Func<ItemType, ItemData> _createItemData)
    {
        createItemData = _createItemData;
    }

    /// <summary>None/Max를 제외한 모든 아이템 타입에 대해 풀을 미리 생성해둔다.</summary>
    public void WarmAll()
    {
        for (int i = (int)ItemType.None + 1; i < (int)ItemType.Max; i++)
        {
            ItemType type = (ItemType)i;
            if (!pools.ContainsKey(type))
            {
                pools[type] = CreatePoolForType(type);
            }
        }
    }

    public ItemData Get(ItemType _type)
    {
        if (!pools.ContainsKey(_type))
        {
            pools[_type] = CreatePoolForType(_type);
        }

        return pools[_type].Get();
    }

    public void Release(ItemData _data)
    {
        if (_data == null) return;
        if (pools.TryGetValue(_data.itemType, out var pool))
        {
            pool.Release(_data);
        }
    }

    private IObjectPool<ItemData> CreatePoolForType(ItemType _type)
    {
        return new ObjectPool<ItemData>(
            createFunc: () => createItemData(_type),
            actionOnGet: (data) => { },
            actionOnRelease: (data) => data.Reset(),
            actionOnDestroy: (data) => { },
            collectionCheck: true,
            defaultCapacity: 5,
            maxSize: 50
        );
    }
}
