using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using System;
using System.Text;
using Unity.VisualScripting;

public class LogContainer : MonoBehaviour, IInventory
{
    public event Action ContainerUpdatedEvent;

    private InputManager inputManager;
    private IInventory interactingContainer;

    //외부 의존성

    // 내부 의존성
    [SerializeField] private List<InventorySlot> containerSlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);
    [SerializeField] private float transferInterval = 1.0f;

    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => containerSlots;

    private bool bCanInteract = false;
    private Coroutine transferCoroutine;
    private WaitForSeconds transferWait;
    private float lastTransferTime = -1.0f;


    private const string PLAYER_TAG = "Player";

    [SerializeField] private bool bDebug = false;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        transferWait = new WaitForSeconds(transferInterval);
        lastTransferTime = -transferInterval; // 초기화 시 즉시 실행 가능하도록 설정

        // 1. 기존 슬롯의 데이터들을 풀로 반환하고 슬롯 초기화
        if (containerSlots.Count == 0)
        {
            for (int i = 0; i < SYSTEM_VAR.MAX_INVENTORY_CNT; i++)
            {
                containerSlots.Add(new InventorySlot());
            }
        }
        else
        {
            for (int i = 0; i < containerSlots.Count; i++)
            {
                if (containerSlots[i].itemData is ItemData data)
                {
                    ReleaseToPool(data);
                }
                containerSlots[i].Setup(null, 0);
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

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void DI_Inventory(IInventory _inventory)
    {
        interactingContainer = _inventory;
    }

    public void ItemAcquired(Item _item)
    {
        if (_item == null) return;

        // 1. 기존 슬롯 확인 (중첩 가능한지)
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i].itemData != null && IsSameItem(_item, (ItemData)containerSlots[i].itemData))
            {
                containerSlots[i].AddCount(_item);
                return;
            }
        }

        // 2. 새로운 타입인 경우 첫 번째 빈 슬롯을 찾아 데이터 가져와 추가
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i].itemData == null)
            {
                ItemData newData = GetFromPool(_item.itemType);
                if (newData != null)
                {
                    newData.CopyFrom(_item);
                    containerSlots[i].Setup(newData, 1);
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

    public List<InventorySlot> GetContainerSlots()
    {
        return containerSlots;
    }

    private void InteractionKeyPressed()
    {
        if (!bCanInteract || interactingContainer == null) return;

        if (transferCoroutine == null)
        {
            transferCoroutine = StartCoroutine(TransferRoutine());
        }
    }

    private IEnumerator TransferRoutine()
    {
        while (true)
        {
            // 이전 전송으로부터 인터벌이 지날 때까지 대기 (연타 대응)
            while (Time.time - lastTransferTime < transferInterval)
            {
                yield return null;
            }

            if (!TryTransferOneItem())
            {
                break;
            }
        }
        transferCoroutine = null;
    }

    private bool TryTransferOneItem()
    {
        if (!bCanInteract || interactingContainer == null) return false;

        var charSlots = interactingContainer.inventorySlots;
        for (int i = 0; i < charSlots.Count; i++)
        {
            if (charSlots[i] is InventorySlot charSlot && charSlot.itemData != null && charSlot.count > 0)
            {
                // 캐릭터 인벤토리에서 아이템 하나 추출 (가장 높은 등급의 로그부터)
                ItemData sourceData = charSlot.itemData;
                LogState takenState = charSlot.TakeOneItem();

                // 컨테이너에 아이템 추가
                AddItemByData(sourceData, takenState);

                // 만약 캐릭터 슬롯이 비었다면 정리 (풀 반환 등)
                if (charSlot.count == 0)
                {
                    if (interactingContainer is InventoryManager invManager)
                    {
                        invManager.ItemDeleted(charSlot);
                    }
                    else if (interactingContainer is LogContainer container)
                    {
                        container.ItemDeleted(charSlot);
                    }
                }

                lastTransferTime = Time.time; // 전송 시점 기록

                DebugLogCharacterInventory();

                ContainerUpdatedEvent?.Invoke();
                return true;
            }
        }
        return false;
    }

    private void AddItemByData(ItemData _sourceData, LogState _state)
    {
        if (_sourceData == null) return;

        // 1. 기존 슬롯 확인 (중첩 가능한지)
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i].itemData != null && IsSameItemByData(_sourceData, containerSlots[i].itemData))
            {
                containerSlots[i].AddCountByState(_state);
                return;
            }
        }

        // 2. 새로운 타입인 경우 첫 번째 빈 슬롯을 찾아 데이터 가져와 추가
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i].itemData == null)
            {
                ItemData newData = GetFromPool(_sourceData.itemType);
                if (newData != null)
                {
                    // 데이터 복사
                    newData.itemType = _sourceData.itemType;
                    newData.sprite = _sourceData.sprite;
                    newData.color = _sourceData.color;

                    if (newData is LogItemData newLogData && _sourceData is LogItemData sourceLogData)
                    {
                        newLogData.treeType = sourceLogData.treeType;
                        newLogData.logState = _state;
                    }

                    containerSlots[i].Setup(newData, 0);
                    containerSlots[i].AddCountByState(_state);
                }
                return;
            }
        }
    }

    private bool IsSameItemByData(ItemData _data1, ItemData _data2)
    {
        if (_data1.itemType != _data2.itemType) return false;

        if (_data1 is LogItemData log1 && _data2 is LogItemData log2)
        {
            return log1.treeType == log2.treeType;
        }

        return true;
    }

    private void DebugLogCharacterInventory()
    {
        if (interactingContainer == null || bDebug == false) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<color=cyan>--- Character Inventory Status ---</color>");
        var slots = interactingContainer.inventorySlots;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.itemData != null && slot.count > 0)
            {
                if (slot.itemData is LogItemData logData)
                {
                    sb.AppendFormat("Slot[{0}]: {1} Log (Total: {2})\n", i, logData.treeType, slot.count);

                    // 각 LogState별 상세 수량 정보 출력
                    var stateCounts = slot.logStateCounts;
                    for (int j = 0; j < stateCounts.Length; j++)
                    {
                        if (stateCounts[j].count > 0)
                        {
                            sb.AppendFormat("  - {0}: {1}\n", stateCounts[j].state, stateCounts[j].count);
                        }
                    }
                }
                else
                {
                    sb.AppendFormat("Slot[{0}]: {1} x{2}\n", i, slot.itemData.itemType, slot.count);
                }
            }
        }
        Debug.Log(sb.ToString());
    }

    private void InteractionKeyCanceled()
    {
        if (transferCoroutine != null)
        {
            StopCoroutine(transferCoroutine);
            transferCoroutine = null;
        }
    }

    private void BindEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;

        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
        inputManager.inputReader.InteractionKeyCanceledEvent += InteractionKeyCanceled;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bCanInteract = true;
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bCanInteract = false;
            if (transferCoroutine != null)
            {
                StopCoroutine(transferCoroutine);
                transferCoroutine = null;
            }
        }
    }
}
