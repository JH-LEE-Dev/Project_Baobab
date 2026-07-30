using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 럼버잭 NPC 전용 경량 인벤토리. InventoryManager와 슬롯/아이템 로직은 동일하지만
/// money/carrot/essence, 세이브·로드, UI 이벤트, VFX 드랍 등 플레이어 전용 기능은 없다.
/// </summary>
public class LumberjackInventoryComponent : MonoBehaviour, IInventory, IInventoryChecker, IItemAcquirer
{
    public event Action ItemAddedEvent;
    public event Action ItemRemovedEvent;
    public event Action InventoryIsFullEvent;

    [SerializeField] private int currentSlotCount = 1;
    [SerializeField] private int maxItemsPerSlot = 3;
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    private ItemDataPool itemDataPool;

    public bool bInventoryIsEmpty { get; private set; }
    public bool bInventoryIsFull { get; private set; }

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;
    long IInventory.money => 0;
    long IInventory.carrot => 0;
    int IInventory.maxCapacity => currentSlotCount * maxItemsPerSlot;
    int IInventory.currentItemCount => GetTotalItemCount();

    public int currentSlotCnt => currentSlotCount;
    public int maxItemCntPerSlot => maxItemsPerSlot;

    /// <summary>
    /// 공용 StatComponent(예: OffroadPorterStatComponent)의 슬롯 용량 값을 그대로 적용할 때 사용한다.
    /// 스킬로 용량이 오를 때마다 반영되도록 매 프레임 호출될 수 있으므로, 값이 실제로 바뀌었을 때만
    /// 뒷정리를 한다.
    /// </summary>
    public void SetSlotCount(int _count)
    {
        int newCount = Mathf.Clamp(_count, 1, SYSTEM_VAR.MAX_INVENTORY_CNT);
        if (newCount == currentSlotCount) return;

        currentSlotCount = newCount;

        // 슬롯 수가 바뀌면 "가득 찼는지" 캐시(bInventoryIsFull)는 더 이상 맞지 않는다 - 용량이 늘면
        // 방금까지 가득 찼던 인벤토리도 이제는 자리가 있는 것이다. 이 캐시를 그대로 두면 실제로는
        // 자리가 났는데도 PorterState_Idle이 계속 수령을 건너뛰게 된다. 다만 여기서 InventoryIsFullEvent
        // 까지 쏘면 매 프레임 상태 전환이 반복될 수 있으므로 상태 값만 갱신한다.
        bInventoryIsFull = IsAllSlotsFull();
        UpdateInventoryEmptyState();
    }

    private bool IsAllSlotsFull()
    {
        int slotCount = Mathf.Min(currentSlotCount, inventorySlots.Count);
        if (slotCount <= 0) return false;

        for (int i = 0; i < slotCount; i++)
        {
            if (inventorySlots[i].itemData == null || inventorySlots[i].totalCount < maxItemsPerSlot)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 현재 모든 슬롯에 들어있는 아이템의 총 개수. 납품 시도 전/후 개수를 비교해서
    /// "하나라도 넣었는지"를 판단하는 데 사용한다(LJState_Deliver).
    /// </summary>
    public int GetTotalItemCount()
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

    private List<LogItem> reservedItems = new List<LogItem>(8);

    public void Initialize()
    {
        if (itemDataPool == null) itemDataPool = new ItemDataPool(CreateItemData);

        // 1. 슬롯 리스트 최대 개수(SYSTEM_VAR.MAX_INVENTORY_CNT)만큼 미리 생성
        if (inventorySlots.Count < SYSTEM_VAR.MAX_INVENTORY_CNT)
        {
            int needCount = SYSTEM_VAR.MAX_INVENTORY_CNT - inventorySlots.Count;
            for (int i = 0; i < needCount; i++)
            {
                inventorySlots.Add(new InventorySlot());
            }
        }

        // 2. 모든 슬롯의 데이터를 풀로 반환하고 슬롯 초기화 (풀 재사용 대비)
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

        reservedItems.Clear();

        // bInventoryIsFull은 CheckInventoryFull()/ItemDeleted()에서만 갱신되므로, 슬롯을 위에서
        // 직접 비웠어도(Setup(null,0)) 이전 생애에 가득 찬 상태였다면 true로 남아있는다.
        // 그대로 두면 방금 리셋된 빈 인벤토리인데도 즉시 LJState_Deliver로 빠지는 버그가 생기므로 명시적으로 리셋한다.
        bInventoryIsFull = false;
        UpdateInventoryEmptyState();
    }

    public void ItemAcquired(Item _item)
    {
        if (_item == null) return;

        bool itemAdded = false;

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
            ItemAddedEvent?.Invoke();
            Sound.PlayUI(SoundID.GetItem);
        }
        else
        {
            InventoryIsFullEvent?.Invoke();
        }
    }

    private void CheckInventoryFull()
    {
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null || inventorySlots[i].totalCount < maxItemsPerSlot)
            {
                bInventoryIsFull = false;
                return;
            }
        }

        bInventoryIsFull = true;
        InventoryIsFullEvent?.Invoke();
    }

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

        // 1. 유효하지 않은 예약(흡입 중이 아니거나 비활성화된 경우) 정리
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
        //    종류가 다른 예약끼리도 같은 빈 슬롯을 중복으로 차지하지 못하게 된다. 이걸 안 하면
        //    서로 다른 조합의 로그 두 개가 거의 동시에 감지될 때 "빈 슬롯이니 둘 다 들어갈 수
        //    있다"고 착각해 둘 다 흡입을 승인해버리고, 나중에 도착한 쪽이 ItemAcquired에서
        //    거부되면서 실제로는 가득 차지도 않았는데 InventoryIsFullEvent가 잘못 발생한다.
        //    반대로 "다른 조합의 예약이 하나라도 있으면 빈 슬롯 전체를 막는" 식으로 처리하면,
        //    빈 슬롯이 여러 개일 때 실제로는 남는 자리가 있는데도 불필요하게 거절하게 된다.
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

    /// <summary>
    /// CanAcquired가 예약을 승인했지만, 실제로는 다른 소비자(다른 NPC나 캐릭터)가 같은 프레임에
    /// 먼저 SetSuckTarget을 걸어 이 NPC에게는 오지 않게 된 경우, 남아있는 유령 예약을 지운다.
    /// 이걸 안 지우면 실제로 나에게 오지 않을 아이템 때문에 다른 진짜 아이템이 계속 거부당한다.
    /// </summary>
    public void CancelReservation(LogItem _item)
    {
        reservedItems.Remove(_item);
    }

    private bool IsSameItem(Item _item, ItemData _data)
    {
        if (_item.itemType != _data.itemType) return false;

        if (_item is LogItem logItem && _data is LogItemData logData)
        {
            return logItem.logState == logData.logState && logItem.treeType == logData.treeType;
        }
        else if (_item is LootItem lootItem && _data is LootItemData lootData)
        {
            return lootItem.LootType == lootData.lootType;
        }

        return true;
    }

    private bool IsSameItemByData(ItemData _data1, ItemData _data2)
    {
        if (_data1.itemType != _data2.itemType) return false;

        if (_data1 is LogItemData log1 && _data2 is LogItemData log2)
        {
            return log1.logState == log2.logState && log1.treeType == log2.treeType;
        }

        return true;
    }

    /// <summary>
    /// 착지 시점 커밋 방식에서, _sourceData와 같은 종류로 이미 채워진 슬롯에 남은 여유 공간만
    /// 계산한다(빈 슬롯은 포함하지 않음). 호출부가 이미 발사되어 날아오는 중인 같은 종류의
    /// pendingCount와 비교해, "기존에 확보된 슬롯"만으로 충분한지 먼저 판단할 때 쓴다.
    /// </summary>
    public int GetMatchingSlotSpaceFor(ItemData _sourceData)
    {
        if (_sourceData == null) return 0;

        int space = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null && IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                int remaining = maxItemsPerSlot - inventorySlots[i].totalCount;
                if (remaining > 0) space += remaining;
            }
        }

        return space;
    }

    /// <summary>
    /// GetMatchingSlotSpaceFor(ItemData)와 동일하되, ItemData가 아니라 (logState, treeType) 조합으로
    /// 직접 조회한다. 인출 경로(WithdrawToCarrierRoutine)에서 이미 발사되어 날아오는 다른 조합의
    /// 기존 슬롯 여유를 구할 때 쓴다(비행 아이템은 ItemData가 아니라 상태/종류만 갖고 있으므로).
    /// </summary>
    public int GetMatchingSlotSpaceFor(LogState _logState, TreeType _treeType)
    {
        int space = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData is LogItemData logData &&
                logData.logState == _logState && logData.treeType == _treeType)
            {
                int remaining = maxItemsPerSlot - inventorySlots[i].totalCount;
                if (remaining > 0) space += remaining;
            }
        }

        return space;
    }

    /// <summary>
    /// 현재 완전히 비어있는 슬롯의 개수. 기존에 확보된 슬롯만으로 부족해서 새 빈 슬롯이 필요한
    /// 종류를 승인할지 판단할 때 쓴다. 호출부는 이 개수를, 이미 다른 종류가 빈 슬롯을 예약(발사)
    /// 중인 "서로 다른 종류의 개수"와 비교해야 한다 - 단순히 "빈 슬롯이 하나라도 있는지"만 보면,
    /// 빈 슬롯이 2개 있어도 다른 종류가 하나만 대기 중일 때 나에게 남는 자리가 있는데도 불필요하게
    /// 거절해버릴 수 있다.
    /// </summary>
    public int GetEmptySlotCount()
    {
        int count = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null) count++;
        }

        return count;
    }

    public void AddItemByData(ItemData _sourceData, LogState _state)
    {
        if (_sourceData == null) return;

        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null &&
                inventorySlots[i].totalCount < maxItemsPerSlot &&
                IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                CheckInventoryFull();
                UpdateInventoryEmptyState();
                ItemAddedEvent?.Invoke();
                return;
            }
        }

        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null)
            {
                ItemData newData = itemDataPool.Get(_sourceData.itemType);
                if (newData != null)
                {
                    newData.itemType = _sourceData.itemType;
                    newData.sprite = _sourceData.sprite;
                    newData.color = _sourceData.color;

                    if (newData is LogItemData newLogData && _sourceData is LogItemData sourceLogData)
                    {
                        newLogData.treeType = sourceLogData.treeType;
                        newLogData.logState = _state;
                    }

                    inventorySlots[i].Setup(newData, 0);
                    inventorySlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                    CheckInventoryFull();
                    UpdateInventoryEmptyState();
                    ItemAddedEvent?.Invoke();
                }

                return;
            }
        }
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
            bInventoryIsFull = false;
            ItemRemovedEvent?.Invoke();
        }
    }

    /// <summary>
    /// 인벤토리에 있는 로그 아이템들을 슬롯별로 순서대로 꺼내 _tryDeposit에 위임합니다.
    /// _tryDeposit이 false를 반환하면(대상 공간 부족 등) 그 슬롯에서 즉시 멈추고 남은 수량은
    /// 인벤토리에 그대로 남깁니다(유실 없음). 슬롯 내부 상태(TakeOneItem/ItemDeleted)는 이 안에서만 다룬다.
    /// </summary>
    public void TransferLogItemsTo(Func<LogItemData, LogState, bool> _tryDeposit)
    {
        if (_tryDeposit == null) return;

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

            while (slot.totalCount > 0)
            {
                if (!_tryDeposit(logData, logData.logState)) break;
                slot.TakeOneItem();
            }

            if (slot.totalCount == 0)
            {
                ItemDeleted(slot);
            }
        }
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public List<InventorySlot> GetInventorySlots()
    {
        return inventorySlots;
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
