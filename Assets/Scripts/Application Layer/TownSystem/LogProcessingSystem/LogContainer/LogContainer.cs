using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using System;
using System.Text;

public class LogContainer : MonoBehaviour, IInventory, IContainerCH
{
    public event Action ContainerSpecChangedEvent;
    public event Action<LogItemData> LogOutEvent;
    public event Action<bool> InteractStateEvent;
    public event Action ContainerUpdatedEvent;

    private InputManager inputManager;
    private IInventory interactingContainer;
    private Transform charTransform;
    private LogItemPoolingManager logItemPoolManager;
    [SerializeField] private Transform inputTransform;


    //외부 의존성

    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> containerSlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);
    [SerializeField] private float transferInterval = 2f;
    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.4f;

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => containerSlots;

    public long money => 0;

    public int carrot => 0;

    public int currentSlotCnt => currentSlotCount;

    private bool bCanInteract = false;
    private Coroutine transferCoroutine;
    private WaitForSeconds transferWait;
    private float lastTransferTime = -1.0f;
    private float lastOutputTime = -1.0f;
    private float lastInterval = 0f;


    private const string PLAYER_TAG = "Player";

    [SerializeField] private bool bDebug = false;

    private bool bStop = false;

    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // // 시각적 효과 (비행 중인 아이템 관리)
    private List<LogItem> flyingItems = new List<LogItem>(32);
    private HashSet<InventorySlot> transferringSlots = new HashSet<InventorySlot>();
    private const float FLY_INTERVAL = 0.075f;
    private LogItemData arrivalDataBuffer = new LogItemData();

    public void Initialize(InputManager _inputManager, LogItemPoolingManager logItemPoolingManager)
    {
        inputManager = _inputManager;
        logItemPoolManager = logItemPoolingManager;

        // 시각적 효과를 위한 트랜스폼 캐싱
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) visualTransform = sr.transform;

        transferWait = new WaitForSeconds(transferInterval);
        lastTransferTime = -transferInterval;
        lastOutputTime = -transferInterval; // 초기화 시 즉시 실행 가능하도록 설정

        // 1. 슬롯 리스트 최대 개수(SYSTEM_VAR.MAX_INVENTORY_CNT)만큼 미리 생성
        if (containerSlots.Count < SYSTEM_VAR.MAX_INVENTORY_CNT)
        {
            int needCount = SYSTEM_VAR.MAX_INVENTORY_CNT - containerSlots.Count;
            for (int i = 0; i < needCount; i++)
            {
                containerSlots.Add(new InventorySlot());
            }
        }

        // 2. 모든 슬롯(최대 개수)의 데이터들을 풀로 반환하고 슬롯 초기화
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i].itemData is ItemData data)
            {
                ReleaseToPool(data);
            }
            containerSlots[i].Setup(null, 0);
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

        BindEvents();
    }

    public void SetCharTransform(Transform _transform)
    {
        charTransform = _transform;
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

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData != null &&
                containerSlots[i].totalCount < maxItemsPerSlot &&
                IsSameItem(_item, (ItemData)containerSlots[i].itemData))
            {
                containerSlots[i].AddCount(_item);
                ContainerUpdatedEvent?.Invoke();
                return;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData == null)
            {
                ItemData newData = GetFromPool(_item.itemType);
                if (newData != null)
                {
                    newData.CopyFrom(_item);
                    containerSlots[i].Setup(newData, 1);
                }
                ContainerUpdatedEvent?.Invoke();
                return;
            }
        }
    }

    private void Update()
    {
        UpdateFlyingItems(Time.deltaTime);
        UpdateBounce(Time.deltaTime);

        if (bStop == true)
        {
            lastOutputTime = Time.time - lastInterval;
            return;
        }

        lastInterval = Time.time - lastOutputTime;
        if (lastInterval >= transferInterval)
        {
            TakeFirstItem();
            lastOutputTime = Time.time;
            lastInterval = 0f;
        }
    }

    private void UpdateFlyingItems(float _deltaTime)
    {
        for (int i = flyingItems.Count - 1; i >= 0; i--)
        {
            LogItem item = flyingItems[i];
            item.ManualUpdate(_deltaTime);

            if (item.MoveState != ItemMoveState.Transferring)
            {
                // 도착 연출 완료 (Scale 0 시점) - 실제 데이터 추가
                arrivalDataBuffer.itemType = item.itemType;
                arrivalDataBuffer.sprite = item.sprite;
                arrivalDataBuffer.color = item.color;
                arrivalDataBuffer.treeType = item.treeType;

                AddItemByData(arrivalDataBuffer, item.logState);
                ContainerUpdatedEvent?.Invoke();

                TriggerBounce();

                logItemPoolManager.ReturnLogItem(item);
                flyingItems.RemoveAt(i);
            }
        }
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
        
        // 진폭을 0.4로 키우고 감쇠를 3f로 늦춰 더 찰진 느낌 부여
        float curve = Mathf.Sin(t * Mathf.PI * 5f) * Mathf.Exp(-t * 3f) * 0.4f;
        
        if (visualTransform != null)
        {
            // X축 확대 시 Y축 축소 (Squash & Stretch)
            visualTransform.localScale = new Vector3(1f + curve, 1f - curve, 1f);
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
        for (int i = 0; i < interactingContainer.currentSlotCnt; i++)
        {
            if (charSlots[i] is InventorySlot charSlot && charSlot.itemData != null && charSlot.count > 0)
            {
                // 이미 전송 중인 슬롯이면 건너뛰기
                if (transferringSlots.Contains(charSlot)) continue;

                if (!(charSlot.itemData is LogItemData logSourceData)) continue;

                // 해당 슬롯 전송 시작
                StartCoroutine(TransferOneSlotVisualRoutine(charSlot));
                lastTransferTime = Time.time;
                return true;
            }
        }
        return false;
    }

    private IEnumerator TransferOneSlotVisualRoutine(InventorySlot _charSlot)
    {
        transferringSlots.Add(_charSlot);

        try
        {
            LogItemData sourceData = _charSlot.itemData as LogItemData;
            int countToTransfer = _charSlot.count;

            for (int i = 0; i < countToTransfer; i++)
            {
                // 컨테이너가 꽉 찼는지 매번 체크 (비행 중인 아이템까지 고려)
                if (!CanAddItemByData(sourceData)) break;

                LogState takenState = _charSlot.TakeOneItem();
                // AddItemByData(sourceData, takenState); // [제거] 도착 시점으로 연기

                // 시각적 비행 아이템 생성
                LogItemData visualData = new LogItemData
                {
                    treeType = sourceData.treeType,
                    logState = takenState,
                    color = sourceData.color
                };

                LogItem flyingItem = logItemPoolManager.GetLogItem(visualData);
                flyingItem.IsDropItem(false);

                Vector3 start = charTransform != null ? charTransform.position : transform.position;
                Vector3 end = inputTransform != null ? inputTransform.position : transform.position;

                // 궤적 jitter를 대폭 줄여서 포물선 형태가 뭉개지지 않도록 수정
                Vector3 trajectoryJitter = new Vector3(UnityEngine.Random.Range(-0.3f, 0.0f), UnityEngine.Random.Range(-0.2f, 0.0f), 0f);

                // 전용 전송 메서드 호출 (시점, 종점, 높이, 시간, 궤적 지터)
                flyingItem.TransferLaunch(start, end, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.7f), trajectoryJitter);
                flyingItems.Add(flyingItem);

                ContainerUpdatedEvent?.Invoke();

                yield return new WaitForSeconds(FLY_INTERVAL);
            }

            // 슬롯이 비었다면 정리
            if (_charSlot.count == 0)
            {
                if (interactingContainer is InventoryManager invManager)
                {
                    invManager.ItemDeleted(_charSlot);
                }
                else if (interactingContainer is LogContainer container)
                {
                    container.ItemDeleted(_charSlot);
                }
            }
        }
        finally
        {
            transferringSlots.Remove(_charSlot);
        }
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
                if (flyingItems[i].itemType == ItemType.Log && flyingItems[i].treeType == logSource.treeType)
                    pendingCount++;
            }
        }

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData != null &&
                (containerSlots[i].totalCount + pendingCount) < maxItemsPerSlot &&
                IsSameItemByData(_sourceData, containerSlots[i].itemData))
            {
                return true;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯이 있는지 확인
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData == null)
            {
                // 빈 슬롯이 있으면 진입 가능 (비행 중인 것들이 이 슬롯을 채울 것임)
                return pendingCount < maxItemsPerSlot;
            }
        }

        return false;
    }

    private void AddItemByData(ItemData _sourceData, LogState _state)
    {
        if (_sourceData == null) return;

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData != null &&
                containerSlots[i].totalCount < maxItemsPerSlot &&
                IsSameItemByData(_sourceData, containerSlots[i].itemData))
            {
                containerSlots[i].AddCountByState(_state);
                return;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
        for (int i = 0; i < currentSlotCount; i++)
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
        for (int i = 0; i < interactingContainer.currentSlotCnt; i++)
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
            InteractStateEvent?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bCanInteract = false;
            InteractStateEvent?.Invoke(false);

            if (transferCoroutine != null)
            {
                StopCoroutine(transferCoroutine);
                transferCoroutine = null;
            }
        }
    }

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// 보관함의 첫 번째 슬롯에서 아이템을 하나 꺼내어 반환합니다.
    /// 슬롯이 비게 되면 뒤의 아이템들을 앞으로 당깁니다.
    /// </summary>
    public void TakeFirstItem()
    {
        if (containerSlots == null)
            return;

        for (int i = 0; i < currentSlotCount; i++)
        {
            var slot = containerSlots[i];

            // 아이템이 있는 첫 번째 슬롯 발견
            if (slot.itemData != null && slot.count > 0)
            {
                // 1. 해당 슬롯에서 상태 하나 추출 (가장 높은 등급 우선)
                LogState takenState = slot.TakeOneItem();

                // 2. 외부로 반환할 데이터 생성 (풀링 활용)
                LogItemData resultData = GetFromPool(ItemType.Log) as LogItemData;
                if (resultData != null && slot.itemData is LogItemData sourceLog)
                {
                    // 원본 데이터 복사
                    resultData.itemType = sourceLog.itemType;
                    resultData.sprite = sourceLog.sprite;
                    resultData.color = sourceLog.color;
                    resultData.treeType = sourceLog.treeType;
                    resultData.logState = takenState;
                }

                // 3. 만약 아이템을 뺀 후 슬롯이 완전히 비었다면 리스트 정렬
                if (slot.count == 0)
                {
                    // 데이터 풀 반환 및 슬롯 초기화
                    if (slot.itemData is ItemData data)
                    {
                        ReleaseToPool(data);
                    }
                    slot.Setup(null, 0);

                    // 빈 슬롯을 리스트의 맨 뒤로 보내서 "앞으로 당기기" 구현
                    // 단, currentSlotCount 범위 내에서만 정렬하는 것이 아니라 
                    // 리스트 전체(MAX_INVENTORY_CNT)를 유지하면서 이동
                    containerSlots.RemoveAt(i);
                    containerSlots.Add(slot);
                }

                // 4. 이벤트 호출 및 결과 반환
                ContainerUpdatedEvent?.Invoke();

                LogOutEvent?.Invoke(resultData);
                return;
            }
        }
    }

    public void PopulateContainerSaveData(ref InventorySaveData _saveData)
    {
        _saveData.money = 0;
        _saveData.carrot = 0;
        _saveData.currentSlotCount = currentSlotCount;

        _saveData.Initialize(currentSlotCount);

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = containerSlots[i];
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

                slotData.itemSaveData = itemSaveData;
            }

            _saveData.slots.Add(slotData);
        }
    }

    public void LoadSaveData(LogProcessingSaveData _data)
    {
        currentSlotCount = _data.containerInventoryData.currentSlotCount;
        maxItemsPerSlot = _data.maxItemsPerSlot;
        bStop = _data.bStop;
        transferInterval = _data.transferInterval;

        // 타이밍 정보 복구 (상대 시간 -> 현재 Time.time 기준 절대 시간)
        lastTransferTime = Time.time - _data.lastTransferTimeElapsed;
        lastOutputTime = Time.time - _data.lastOutputTimeElapsed;
        lastInterval = _data.lastInterval;

        // 기존 슬롯 초기화 (풀 반환)
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i].itemData is ItemData itemData)
            {
                ReleaseToPool(itemData);
            }
            containerSlots[i].Setup(null, 0);
        }

        // 데이터 복구
        var inventoryData = _data.containerInventoryData;
        if (inventoryData.slots != null)
        {
            for (int i = 0; i < inventoryData.slots.Count; i++)
            {
                if (i >= containerSlots.Count) break;

                var slotData = inventoryData.slots[i];
                if (slotData.itemSaveData.itemType != ItemType.None)
                {
                    ItemData newData = GetFromPool(slotData.itemSaveData.itemType);
                    if (newData != null)
                    {
                        newData.color = slotData.itemSaveData.color; // 컬러 복구

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

                        containerSlots[i].Setup(newData, slotData.totalCount);

                        if (slotData.logStateCounts != null && slotData.logStateCounts.Length > 0)
                        {
                            containerSlots[i].LoadLogStateCounts(slotData.logStateCounts);
                        }
                    }
                }
            }
        }

        ContainerUpdatedEvent?.Invoke();
        ContainerSpecChangedEvent?.Invoke();
        Debug.Log("[LogContainer] Container Save Data Loaded.");
    }

    public bool GetbStop()
    {
        return bStop;
    }

    public float GetTransferInterval()
    {
        return transferInterval;
    }

    public float GetLastTransferTimeElapsed() => Time.time - lastTransferTime;
    public float GetLastOutputTimeElapsed() => Time.time - lastOutputTime;
    public float GetLastInterval() => lastInterval;

    public int GetMaxItemsPerSlot()
    {
        return maxItemsPerSlot;
    }

    public void SetbStop(bool _bStop)
    {
        bStop = _bStop;
    }

    public void ExpandContainerSlotCnt(float _amount)
    {
        currentSlotCount = Mathf.Min(currentSlotCount + (int)_amount, SYSTEM_VAR.MAX_INVENTORY_CNT);
        ContainerUpdatedEvent?.Invoke();
        ContainerSpecChangedEvent?.Invoke();
    }

    public void LogCapacityIncrease(float _amount)
    {
        maxItemsPerSlot += (int)_amount;
        ContainerUpdatedEvent?.Invoke();
    }
}
