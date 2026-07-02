using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffroadContainer : MonoBehaviour, IInventory, IOffroadContainerCH
{
    public event Action ItemTransferToContainerEvent;
    public event Action ContainerOpenedEvent;
    public event Action ContainerClosedEvent;
    public event Action<bool> InteractStateEvent;
    public event Action ContainerUpdatedEvent;
    public event Action SpecChangedEvent;
    public event Action InventoryIsFullEvent { add { } remove { } }

    // 외부 의존성
    private IInventory characterInventory;
    private InventoryManager characterInventoryManager;
    private Transform charTransform;
    private LogItemPoolingManager logItemPoolManager;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);
    [SerializeField] private float transferInterval = 0.5f;

    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private ItemDataPool itemDataPool;

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;
    long IInventory.money => 0;
    long IInventory.carrot => 0;
    public int maxCapacity => currentSlotCount * maxItemsPerSlot;
    public int currentItemCount
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

    public int maxItemCntPerSlot => maxItemsPerSlot;

    private const string PLAYER_TAG = "Player";

    // 시각적 연출을 위한 변수
    private Coroutine transferCoroutine;
    private const float FLY_INTERVAL = 0.075f;

    private struct FlyingTransferItem
    {
        public LogItem item;
        public bool toCharacter;
    }
    private List<FlyingTransferItem> flyingItems = new List<FlyingTransferItem>(32);
    private List<FlyingTransferItem> dismissingItems = new List<FlyingTransferItem>(16);
    private bool bFlyingPaused = false;

    private HashSet<InventorySlot> transferringSlots = new HashSet<InventorySlot>();
    private LogItemData arrivalDataBuffer = new LogItemData();
    private SpriteRenderer sr;
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.2f;

    private bool bCollisionEnabled = true;

    private bool bInTown = true;

    private InputManager inputManager;
    public bool bCanInteract = false;
    private bool bPhysicalOverlapped = false;
    private bool bLastInteractState = false;
    private float lastTransferTime = -1.0f;
    private bool bCanReach = true;

    // 컨테이너 연출 이벤트 제어 변수
    private bool bContainerOpen = false;
    private float closeTimer = -1f;

    private bool bContainerVisualOpened = false;
    private bool bIsInteracting = false;

    public Collider2D col;

    public float itemTransferSpeedMul = 1f;
    public float colliderRangeMul =1f;

    public void Initialize(IInventory _characterInventory, InputManager _inputManager)
    {
        if (itemDataPool == null) itemDataPool = new ItemDataPool(CreateItemData);

        characterInventory = _characterInventory;
        characterInventoryManager = _characterInventory as InventoryManager;
        inputManager = _inputManager;

        logItemPoolManager = GetComponent<LogItemPoolingManager>();
        logItemPoolManager.Initialize(false);

        col = GetComponent<Collider2D>();

        sr = GetComponent<SpriteRenderer>();

        lastTransferTime = -transferInterval;

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

        BindEvents();
    }

    public void SetVisualTransform(Transform _transform)
    {
        visualTransform = _transform;
    }

    public void SetCharacterTransform(Transform _transform)
    {
        charTransform = _transform;
    }

    private void Update()
    {
        UpdateFlyingItems(Time.deltaTime);
        UpdateBounce(Time.deltaTime);
        UpdateContainerState(Time.deltaTime);
    }

    private void UpdateFlyingItems(float _deltaTime)
    {
        UpdateDismissingItems(_deltaTime);

        if (bFlyingPaused) return;

        for (int i = flyingItems.Count - 1; i >= 0; i--)
        {
            var flyingData = flyingItems[i];
            LogItem item = flyingData.item;
            item.ManualUpdate(_deltaTime);

            // ContainerTransferring 및 DynamicTransferring 상태도 비행 중인 상태로 간주
            if (item.MoveState != ItemMoveState.Transferring &&
                item.MoveState != ItemMoveState.CurveTransferring &&
                item.MoveState != ItemMoveState.ContainerTransferring &&
                item.MoveState != ItemMoveState.DynamicTransferring)
            {
                // 도착 연출 완료 (Scale 0 시점) - 실제 데이터 추가
                arrivalDataBuffer.itemType = item.itemType;
                arrivalDataBuffer.sprite = item.sprite;
                arrivalDataBuffer.color = item.color;
                arrivalDataBuffer.treeType = item.treeType;
                arrivalDataBuffer.logState = item.logState;

                if (flyingData.toCharacter)
                {
                    AddToCharacterInventory(arrivalDataBuffer, item.logState);
                }
                else
                {
                    AddItemByData(arrivalDataBuffer, item.logState);
                    TriggerBounce();
                }

                logItemPoolManager.ReturnLogItem(item);
                flyingItems.RemoveAt(i);
            }
        }
    }

    private void UpdateDismissingItems(float _deltaTime)
    {
        for (int i = dismissingItems.Count - 1; i >= 0; i--)
        {
            var flyingData = dismissingItems[i];
            LogItem item = flyingData.item;

            Vector3 scale = item.transform.localScale;
            scale -= Vector3.one * _deltaTime * 3f;

            if (scale.x <= 0.01f)
            {
                item.transform.localScale = Vector3.zero;
                item.ResetItem();
                logItemPoolManager.ReturnLogItem(item);
                dismissingItems.RemoveAt(i);
            }
            else
            {
                item.transform.localScale = scale;
            }
        }
    }

    /// <summary>
    /// 현재 날아가고 있는 모든 LogItem의 이동을 일시정지합니다.
    /// </summary>
    public void PauseAllFlyingItems()
    {
        bFlyingPaused = true;
    }

    /// <summary>
    /// 일시정지된 모든 LogItem의 이동을 재개합니다.
    /// </summary>
    public void ResumeAllFlyingItems()
    {
        bFlyingPaused = false;
    }

    public void DismissAllFlyingItems()
    {
        bFlyingPaused = false;
        for (int i = flyingItems.Count - 1; i >= 0; i--)
        {
            dismissingItems.Add(flyingItems[i]);
        }
        flyingItems.Clear();
    }

    /// <summary>
    /// 던전/마을 재진입 시 오프로드 컨테이너의 논리적 상태(열림 여부 등)를 강제 초기화합니다.
    /// </summary>
    public void ResetState()
    {
        bContainerOpen = false;
        bContainerVisualOpened = false;
        closeTimer = -1f;
        DismissAllFlyingItems();

        if (transferCoroutine != null)
        {
            StopCoroutine(transferCoroutine);
            transferCoroutine = null;
        }

        transferringSlots.Clear();
        bIsInteracting = false;
        lastTransferTime = -transferInterval;
    }

    private void TriggerBounce()
    {
        bounceTime = 0f;
    }

    private void UpdateBounce(float _deltaTime)
    {
        if (bounceTime >= BOUNCE_DURATION)
        {
            if (visualTransform != null && visualTransform.localScale != Vector3.one)
                visualTransform.localScale = Vector3.one;
            return;
        }

        bounceTime += _deltaTime;
        float t = bounceTime / BOUNCE_DURATION;

        // 쫀득함(Squash & Stretch) 연출: 감쇠 진동 곡선(Damped Sine Wave) 사용
        // t가 0~1로 흐를 때 1.5회(3번의 방향 전환) 진동하며 진폭이 점차 줄어듦
        float curve = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 0.3f;

        if (visualTransform != null)
        {
            // X축 확대 시 Y축 축소 (Squash & Stretch)
            visualTransform.localScale = new Vector3(1f + curve, 1f - curve, 1f);
        }
    }

    private IEnumerator TransferAllItemsRoutine()
    {
        if (characterInventory == null) yield break;

        while (true)
        {
            // 이전 전송으로부터 인터벌이 지날 때까지 대기
            while (Time.time - lastTransferTime < (transferInterval / Mathf.Max(0.01f, itemTransferSpeedMul)))
            {
                yield return null;
            }

            // 현재 전송 중인 슬롯이 있다면 완료될 때까지 대기
            while (transferringSlots.Count > 0)
            {
                yield return null;
            }

            if (!TryTransferOneSlot())
            {
                break;
            }

            // 방금 시작한 슬롯의 전송이 끝날 때까지 대기
            while (transferringSlots.Count > 0)
            {
                yield return null;
            }

            // 한 슬롯이 비워진 시점에 키 입력을 뗀 상태라면 중단
            if (!bIsInteracting)
            {
                break;
            }
        }
        transferCoroutine = null;
    }

    private bool TryTransferOneSlot()
    {
        if (!bCanInteract || characterInventory == null) return false;

        if (bInTown)
        {
            for (int i = 0; i < currentSlotCount; i++)
            {
                if (inventorySlots[i].itemData != null && inventorySlots[i].count > 0)
                {
                    if (transferringSlots.Contains(inventorySlots[i])) continue;
                    if (!(inventorySlots[i].itemData is LogItemData logSourceData)) continue;

                    if (!CanAddToCharacterInventory(logSourceData)) continue;

                    StartCoroutine(TransferOneSlotVisualRoutine(inventorySlots[i], true));
                    lastTransferTime = Time.time;
                    return true;
                }
            }
        }
        else
        {
            var charSlots = characterInventory.inventorySlots;
            for (int i = 0; i < characterInventory.currentSlotCnt; i++)
            {
                if (charSlots[i] is InventorySlot charSlot && charSlot.itemData != null && charSlot.count > 0)
                {
                    if (transferringSlots.Contains(charSlot)) continue;
                    if (!(charSlot.itemData is LogItemData logSourceData)) continue;

                    if (!CanAddItemByData(logSourceData)) continue;

                    StartCoroutine(TransferOneSlotVisualRoutine(charSlot, false));
                    lastTransferTime = Time.time;
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator TransferOneSlotVisualRoutine(InventorySlot _sourceSlot, bool _toCharacter)
    {
        transferringSlots.Add(_sourceSlot);

        try
        {
            LogItemData sourceData = _sourceSlot.itemData as LogItemData;
            int countToTransfer = _sourceSlot.count;

            if (!_toCharacter && countToTransfer > 0)
            {
                ItemTransferToContainerEvent?.Invoke();
            }

            for (int i = 0; i < countToTransfer; i++)
            {
                if (_toCharacter)
                {
                    if (!CanAddToCharacterInventory(sourceData)) break;
                }
                else
                {
                    if (!CanAddItemByData(sourceData)) break;
                }

                LogState takenState = _sourceSlot.TakeOneItem();

                if (!_toCharacter && characterInventoryManager != null)
                {
                    characterInventoryManager.ItemRemoved();
                }

                LogItemData visualData = new LogItemData
                {
                    treeType = sourceData.treeType,
                    logState = takenState,
                    color = sourceData.color
                };

                LogItem flyingItem = logItemPoolManager.GetLogItem(visualData);
                flyingItem.SetFlyingItemSortingLayer();
                flyingItem.IsDropItem(false);
                flyingItem.spriteRenderer.sortingOrder = 100;

                Vector3 containerPos = transform.position + new Vector3(0f, 0.2f, 0f);
                Vector3 charPos = charTransform != null ? charTransform.position : transform.position;

                Vector3 start = _toCharacter ? containerPos : charPos;
                Vector3 end = _toCharacter ? charPos : containerPos;

                Vector3 dir = (end - start).normalized;
                if (dir == Vector3.zero) dir = Vector3.up;
                Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
                float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector3 trajectoryJitter = normal * arcPower;

                float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

                flyingItem.transform.position = start;

                if (_toCharacter)
                {
                    flyingItem.DynamicTransferLaunch(start, charTransform, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.5f), trajectoryJitter, rotationSpeed);
                }
                else
                {
                    flyingItem.ContainerTransferLaunch(start, end, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.5f), trajectoryJitter, rotationSpeed);
                }

                flyingItems.Add(new FlyingTransferItem { item = flyingItem, toCharacter = _toCharacter });

                yield return new WaitForSeconds(FLY_INTERVAL / Mathf.Max(0.01f, itemTransferSpeedMul));
            }

            if (_sourceSlot.count == 0)
            {
                if (_toCharacter)
                {
                    ItemDeleted(_sourceSlot);
                    ContainerUpdatedEvent?.Invoke();
                }
                else
                {
                    if (characterInventoryManager != null)
                    {
                        characterInventoryManager.ItemDeleted(_sourceSlot);
                    }
                }
            }
            else if (_toCharacter)
            {
                ContainerUpdatedEvent?.Invoke();
            }
        }
        finally
        {
            transferringSlots.Remove(_sourceSlot);
        }
    }

    private bool CanAddToCharacterInventory(ItemData _sourceData)
    {
        if (_sourceData == null || characterInventoryManager == null) return false;

        int pendingCount = 0;
        if (_sourceData is LogItemData logSource)
        {
            for (int i = 0; i < flyingItems.Count; i++)
            {
                if (flyingItems[i].toCharacter &&
                    flyingItems[i].item.itemType == ItemType.Log &&
                    flyingItems[i].item.logState == logSource.logState &&
                    flyingItems[i].item.treeType == logSource.treeType)
                    pendingCount++;
            }
        }

        int availableSpace = 0;
        var slots = characterInventoryManager.GetInventorySlots();
        int maxItems = characterInventoryManager.GetMaxItemsPerSlot();

        for (int i = 0; i < characterInventoryManager.currentSlotCnt; i++)
        {
            if (slots[i].itemData != null && IsSameItemByData(_sourceData, slots[i].itemData))
            {
                availableSpace += Mathf.Max(0, maxItems - slots[i].totalCount);
            }
            else if (slots[i].itemData == null)
            {
                availableSpace += maxItems;
            }
        }

        bool isSuccess = pendingCount < availableSpace;
        if (!isSuccess)
        {
            bool isFull = true;
            bool hasSpaceRemaining = false;

            for (int i = 0; i < characterInventoryManager.currentSlotCnt; i++)
            {
                if (slots[i].itemData == null)
                {
                    isFull = false;
                }
                else
                {
                    int slotPendingCount = 0;
                    for (int j = 0; j < flyingItems.Count; j++)
                    {
                        if (flyingItems[j].toCharacter && IsSameItem(flyingItems[j].item, slots[i].itemData))
                        {
                            slotPendingCount++;
                        }
                    }

                    if (slots[i].totalCount + slotPendingCount < maxItems)
                    {
                        isFull = false;
                        hasSpaceRemaining = true;
                    }
                }
            }

            if (isFull)
            {
                characterInventoryManager.TriggerInventoryIsFull();
            }
            else if (hasSpaceRemaining)
            {
                characterInventoryManager.TriggerItemCantAcquied();
            }
        }

        return isSuccess;
    }

    private void AddToCharacterInventory(ItemData _sourceData, LogState _state)
    {
        if (_sourceData == null || characterInventoryManager == null) return;

        var slots = characterInventoryManager.GetInventorySlots();
        int maxItems = characterInventoryManager.GetMaxItemsPerSlot();

        for (int i = 0; i < characterInventoryManager.currentSlotCnt; i++)
        {
            if (slots[i].itemData != null &&
                slots[i].totalCount < maxItems &&
                IsSameItemByData(_sourceData, slots[i].itemData))
            {
                slots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                characterInventoryManager.ItemAdded();
                return;
            }
        }

        for (int i = 0; i < characterInventoryManager.currentSlotCnt; i++)
        {
            if (slots[i].itemData == null)
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

                    slots[i].Setup(newData, 0);
                    slots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                    characterInventoryManager.ItemAdded();
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
            // 같은 로그 상태와 나무 종류인 경우에만 같은 슬롯에 보관
            return log1.logState == log2.logState && log1.treeType == log2.treeType;
        }

        return true;
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

    private void InteractionKeyPressed()
    {
        if (!bCanInteract || characterInventory == null) return;

        bIsInteracting = true;

        if (transferCoroutine == null)
        {
            if (HasAnyItemToTransfer())
            {
                if (bInTown)
                {
                    OpenContainerImmediately();
                    if (bContainerVisualOpened)
                    {
                        transferCoroutine = StartCoroutine(TransferAllItemsRoutine());
                    }
                }
                else
                {
                    transferCoroutine = StartCoroutine(TransferAllItemsRoutine());
                }
            }
        }
    }

    private void InteractionKeyCanceled()
    {
        bIsInteracting = false;
    }

    private void BindEvents()
    {
        if (inputManager == null) return;
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;

        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
        inputManager.inputReader.InteractionKeyCanceledEvent += InteractionKeyCanceled;
    }

    private void ReleaseEvents()
    {
        if (inputManager == null) return;
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void UpdateInteractState()
    {
        bool currentState = bCollisionEnabled && bCanReach && bPhysicalOverlapped;

        if (currentState != bLastInteractState)
        {
            bLastInteractState = currentState;
            bCanInteract = currentState;
            InteractStateEvent?.Invoke(currentState);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (bCollisionEnabled == false)
            return;

        if (_other.CompareTag(PLAYER_TAG))
        {
            bPhysicalOverlapped = true;
            UpdateInteractState();
        }
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        if (bCollisionEnabled == false)
            return;

        if (_other.CompareTag(PLAYER_TAG))
        {
            if (bPhysicalOverlapped == false)
            {
                bPhysicalOverlapped = true;
                UpdateInteractState();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bPhysicalOverlapped = false;
            bIsInteracting = false;
            UpdateInteractState();

            if (transferCoroutine != null)
            {
                StopCoroutine(transferCoroutine);
                transferCoroutine = null;
            }
        }
    }

    public void ExpandInventorySlotCnt(float _amount)
    {
        currentSlotCount = Mathf.Min(currentSlotCount + (int)_amount, SYSTEM_VAR.MAX_INVENTORY_CNT);
        SpecChangedEvent?.Invoke();
    }

    public void LogCapacityIncrease(float _amount)
    {
        maxItemsPerSlot += (int)_amount;
    }

    public void PopulateSaveData(ref InventorySaveData _saveData)
    {
        _saveData.money = 0;
        _saveData.carrot = 0;

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
                itemSaveData.color = slot.itemData.color;

                if (slot.itemData is LogItemData logData)
                {
                    itemSaveData.treeType = logData.treeType;
                    itemSaveData.logState = logData.logState;
                    slotData.treeTypeCounts = slot.GetTreeTypeCounts();
                }

                slotData.itemSaveData = itemSaveData;
            }

            _saveData.slots.Add(slotData);
        }
    }

    public void LoadSaveData(InventorySaveData _data)
    {
        // 기존 슬롯 초기화
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].itemData is ItemData itemData)
            {
                itemDataPool.Release(itemData);
            }
            inventorySlots[i].Setup(null, 0);
        }

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
                        newData.color = slotData.itemSaveData.color;

                        if (newData is LogItemData logData)
                        {
                            logData.treeType = slotData.itemSaveData.treeType;
                            logData.logState = slotData.itemSaveData.logState;

                            if (logItemTypeDataBase != null)
                            {
                                var typeData = logItemTypeDataBase.Get(logData.treeType);
                                if (typeData != null)
                                {
                                    logData.sprite = typeData.sprite;
                                }
                            }
                        }

                        inventorySlots[i].Setup(newData, slotData.totalCount);

                        if (slotData.treeTypeCounts != null && slotData.treeTypeCounts.Length > 0)
                        {
                            inventorySlots[i].LoadTreeTypeCounts(slotData.treeTypeCounts);
                        }
                    }
                }
            }
        }

        ContainerUpdatedEvent?.Invoke();
        SpecChangedEvent?.Invoke();
    }

    public void DisableCollision()
    {
        bCollisionEnabled = false;
        bIsInteracting = false;
        UpdateInteractState();

        if (transferCoroutine != null)
            StopCoroutine(transferCoroutine);

        transferCoroutine = null;
    }

    public void EnableCollision()
    {
        bCollisionEnabled = true;
        UpdateInteractState();
    }

    public void SetInTown(bool _boolean)
    {
        bInTown = _boolean;
    }

    /// <summary>
    /// 주어진 월드 좌표가 컨테이너의 실제 충돌 반경(collider) 안에 들어와 있는지 확인합니다.
    /// NPC가 길찾기로 컨테이너를 향해 이동하다가 이 반경에 들어오는 순간 납품을 시작한다.
    /// </summary>
    public bool IsWithinInteractRadius(Vector3 _worldPos)
    {
        if (col == null) return false;
        return col.OverlapPoint(_worldPos);
    }

    /// <summary>
    /// 럼버잭 NPC 등 플레이어가 아닌 소비자가 로그를 컨테이너에 직접 납품할 때 사용하는 공개 API.
    /// 플레이어가 TransferOneSlotVisualRoutine으로 넣을 때와 동일하게 로그가 날아가는 연출(flyingItems)을
    /// 거쳐 도착 시점에 실제로 슬롯에 더해진다 - 즉시 데이터만 추가하지 않는다.
    ///
    /// 플레이어 전용 상호작용 상태(bIsInteracting/transferCoroutine/물리 오버랩)는 전혀 건드리지 않으므로
    /// 플레이어의 컨테이너 상호작용이나 다른 NPC의 납품 호출과 서로 간섭하지 않는다.
    /// CanAddItemByData가 이미 flyingItems에 대기 중인(아직 도착 안 한) 항목까지 포함해 공간을 계산하므로,
    /// 같은 프레임에 캐릭터/여러 NPC가 연달아 이 메서드를 호출해도(Unity 싱글스레드) 서로의 몫을 침범하지 않고
    /// 컨테이너 용량을 초과해서 예약되는 일이 없다.
    /// </summary>
    public bool TryDepositLogItemVisual(LogItemData _sourceData, Vector3 _fromWorldPos, LogState _state)
    {
        if (!CanAddItemByData(_sourceData)) return false;

        LogItemData visualData = new LogItemData
        {
            treeType = _sourceData.treeType,
            logState = _state,
            color = _sourceData.color
        };

        LogItem flyingItem = logItemPoolManager.GetLogItem(visualData);
        flyingItem.SetFlyingItemSortingLayer();
        flyingItem.IsDropItem(false);
        flyingItem.spriteRenderer.sortingOrder = 100;

        Vector3 containerPos = transform.position + new Vector3(0f, 0.2f, 0f);

        Vector3 dir = (containerPos - _fromWorldPos).normalized;
        if (dir == Vector3.zero) dir = Vector3.up;
        Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
        float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
        Vector3 trajectoryJitter = normal * arcPower;

        float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

        flyingItem.transform.position = _fromWorldPos;
        flyingItem.ContainerTransferLaunch(_fromWorldPos, containerPos, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.5f), trajectoryJitter, rotationSpeed);

        flyingItems.Add(new FlyingTransferItem { item = flyingItem, toCharacter = false });

        return true;
    }

    /// <summary>
    /// NPC 인벤토리의 로그 아이템들을 캐릭터의 컨테이너 전송과 동일한 시간 간격(인터벌)을 적용하여 천천히 납품합니다.
    /// 납품 연출이 모두 끝나면 _onComplete 콜백을 호출합니다.
    /// </summary>
    public void TransferFromNPC(LumberjackInventoryComponent _npcInventory, Vector3 _fromWorldPos, Action _onComplete)
    {
        StartCoroutine(NPCTransferRoutine(_npcInventory, _fromWorldPos, _onComplete));
    }

    private IEnumerator NPCTransferRoutine(LumberjackInventoryComponent _npcInventory, Vector3 _fromWorldPos, Action _onComplete)
    {
        var slots = _npcInventory.GetInventorySlots();
        for (int i = 0; i < _npcInventory.currentSlotCnt; i++)
        {
            var slot = slots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

            bool slotTransferredAny = false;
            while (slot.totalCount > 0)
            {
                if (!TryDepositLogItemVisual(logData, _fromWorldPos, logData.logState))
                {
                    break;
                }

                slot.TakeOneItem();
                slotTransferredAny = true;

                yield return new WaitForSeconds(FLY_INTERVAL / Mathf.Max(0.01f, itemTransferSpeedMul));
            }

            if (slot.totalCount == 0)
            {
                _npcInventory.ItemDeleted(slot);
            }

            if (slotTransferredAny)
            {
                yield return new WaitForSeconds(transferInterval / Mathf.Max(0.01f, itemTransferSpeedMul));
            }
        }

        _onComplete?.Invoke();
    }

    private bool CanAddItemByData(ItemData _sourceData)
    {
        if (_sourceData == null) return false;

        // 현재 비행 중인 동일 타입 아이템 개수 계산
        int pendingCount = 0;
        if (_sourceData is LogItemData logSource)
        {
            for (int i = 0; i < flyingItems.Count; i++)
            {
                if (!flyingItems[i].toCharacter &&
                    flyingItems[i].item.itemType == ItemType.Log &&
                    flyingItems[i].item.logState == logSource.logState &&
                    flyingItems[i].item.treeType == logSource.treeType)
                    pendingCount++;
            }
        }

        // 전체 수용 가능한 동일 아이템 남은 공간 계산
        int availableSpace = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null && IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                availableSpace += Mathf.Max(0, maxItemsPerSlot - inventorySlots[i].totalCount);
            }
            else if (inventorySlots[i].itemData == null)
            {
                availableSpace += maxItemsPerSlot;
            }
        }

        return pendingCount < availableSpace;
    }

    private void AddItemByData(ItemData _sourceData, LogState _state)
    {
        if (_sourceData == null) return;

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null &&
                inventorySlots[i].totalCount < maxItemsPerSlot &&
                IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                ContainerUpdatedEvent?.Invoke();
                return;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null)
            {
                ItemData newData = itemDataPool.Get(_sourceData.itemType);
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

                    inventorySlots[i].Setup(newData, 0);
                    inventorySlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                    ContainerUpdatedEvent?.Invoke();
                }

                return;
            }
        }
    }

    public void SetCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    private void UpdateContainerState(float _deltaTime)
    {
        bool _isTransferring = (transferCoroutine != null) || (flyingItems.Count > 0);

        if (_isTransferring)
        {
            closeTimer = -1f;

            if (!bContainerOpen)
            {
                bContainerOpen = true;
                ContainerOpenedEvent?.Invoke();
            }
        }
        else
        {
            if (bContainerOpen)
            {
                if (closeTimer < 0f)
                {
                    closeTimer = 2f;
                }
                else
                {
                    closeTimer -= _deltaTime;
                    if (closeTimer <= 0f)
                    {
                        bContainerOpen = false;
                        closeTimer = -1f;
                        ContainerClosedEvent?.Invoke();
                    }
                }
            }
        }
    }

    private void OpenContainerImmediately()
    {
        closeTimer = -1f;
        if (!bContainerOpen)
        {
            bContainerOpen = true;
            ContainerOpenedEvent?.Invoke();
        }
    }

    private bool HasAnyItemToTransfer()
    {
        if (!bCanInteract || characterInventory == null) return false;

        if (bInTown)
        {
            for (int _i = 0; _i < currentSlotCount; _i++)
            {
                if (inventorySlots[_i].itemData != null && inventorySlots[_i].count > 0)
                {
                    if (transferringSlots.Contains(inventorySlots[_i])) continue;
                    if (!(inventorySlots[_i].itemData is LogItemData _logSourceData)) continue;

                    if (CanAddToCharacterInventory(_logSourceData))
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            var _charSlots = characterInventory.inventorySlots;
            for (int _i = 0; _i < characterInventory.currentSlotCnt; _i++)
            {
                if (_charSlots[_i] is InventorySlot _charSlot && _charSlot.itemData != null && _charSlot.count > 0)
                {
                    if (transferringSlots.Contains(_charSlot)) continue;
                    if (!(_charSlot.itemData is LogItemData _logSourceData)) continue;

                    if (CanAddItemByData(_logSourceData))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void SetContainerVisualOpened(bool _boolean)
    {
        bContainerVisualOpened = _boolean;
        if (bInTown && bContainerVisualOpened && transferCoroutine == null)
        {
            if (HasAnyItemToTransfer())
            {
                transferCoroutine = StartCoroutine(TransferAllItemsRoutine());
            }
        }
    }

    public void ItemTransferSpeedUP(float _amount)
    {
        itemTransferSpeedMul += (_amount / 100f);
    }

    public void ColliderRangeIncrease(float _amount)
    {
        float previousMul = colliderRangeMul;
        colliderRangeMul += (_amount / 100f);
        
        if (col == null || previousMul <= 0f) return;

        float scaleRatio = colliderRangeMul / previousMul;

        if (col is BoxCollider2D box)
        {
            box.size *= scaleRatio;
        }
        else if (col is CircleCollider2D circle)
        {
            circle.radius *= scaleRatio;
        }
        else if (col is CapsuleCollider2D capsule)
        {
            capsule.size *= scaleRatio;
        }
    }
}
