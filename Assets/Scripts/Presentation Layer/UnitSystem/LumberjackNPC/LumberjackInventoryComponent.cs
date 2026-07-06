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
                // 빈 슬롯이라도, 이미 다른 조합이 이 빈 슬롯을 먼저 예약(흡입 중)해뒀다면 이 아이템에게는
                // 공간이 없는 것으로 취급한다. 그렇지 않으면 서로 다른 조합의 로그 두 개가 거의 동시에
                // 감지될 때 "빈 슬롯이니 둘 다 들어갈 수 있다"고 착각해 둘 다 흡입을 승인해버리고,
                // 나중에 도착한 쪽이 ItemAcquired에서 거부되면서 실제로는 가득 차지도 않았는데
                // InventoryIsFullEvent가 잘못 발생하는 문제가 있었다.
                bool slotClaimedByOtherCombo = false;
                for (int r = 0; r < reservedItems.Count; r++)
                {
                    var reserved = reservedItems[r];
                    if (reserved.itemType != _item.itemType || reserved.treeType != _item.treeType || reserved.logState != _item.logState)
                    {
                        slotClaimedByOtherCombo = true;
                        break;
                    }
                }

                if (!slotClaimedByOtherCombo)
                {
                    totalSpace += maxItemsPerSlot;
                }
            }
        }

        // 3. 같은 종류로 이미 예약(흡입 중)된 개수 계산
        int reservedCount = 0;
        for (int i = 0; i < reservedItems.Count; i++)
        {
            var reserved = reservedItems[i];
            if (reserved.itemType == _item.itemType && reserved.treeType == _item.treeType && reserved.logState == _item.logState)
            {
                reservedCount++;
            }
        }

        if (totalSpace - reservedCount > 0)
        {
            reservedItems.Add(_item);
            return true;
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
    /// 물리적 LogItem 흡입(ItemAcquired)과 달리, 컨테이너 간 이동 연출(날아가는 아이템)이 도착했을 때
    /// 데이터만으로 슬롯을 채우기 위한 경로입니다. (예: OffroadContainer -> 운반 NPC)
    /// </summary>
    public bool CanAcquireData(ItemData _sourceData)
    {
        if (_sourceData == null) return false;

        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null) return true;

            if (inventorySlots[i].totalCount < maxItemsPerSlot && IsSameItemByData(_sourceData, inventorySlots[i].itemData))
                return true;
        }

        return false;
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
