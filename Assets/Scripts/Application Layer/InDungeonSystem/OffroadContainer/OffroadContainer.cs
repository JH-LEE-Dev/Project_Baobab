using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class OffroadContainer : MonoBehaviour, IInventory
{
    // 외부 의존성
    private IInventory characterInventory;
    private Transform charTransform;
    private LogItemPoolingManager logItemPoolManager;

    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> inventorySlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);

    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private Dictionary<ItemType, IObjectPool<ItemData>> itemDataPools = new Dictionary<ItemType, IObjectPool<ItemData>>();

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => inventorySlots;
    long IInventory.money => 0;
    long IInventory.carrot => 0;
    public int currentSlotCnt => currentSlotCount;

    private const string PLAYER_TAG = "Player";

    // 시각적 연출을 위한 변수
    private Coroutine transferCoroutine;
    private const float FLY_INTERVAL = 0.075f;
    private List<LogItem> flyingItems = new List<LogItem>(32);
    private HashSet<InventorySlot> transferringSlots = new HashSet<InventorySlot>();
    private LogItemData arrivalDataBuffer = new LogItemData();
    private SpriteRenderer sr;
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.4f;

    public event Action ItemAcquiredEvent;

    public void Initialize(IInventory _characterInventory, Transform _charTransform)
    {
        charTransform = _charTransform;
        characterInventory = _characterInventory;
        logItemPoolManager = GetComponent<LogItemPoolingManager>();
        logItemPoolManager.Initialize();

        sr = GetComponent<SpriteRenderer>();
        if (sr != null) visualTransform = sr.transform;

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
        UpdateFlyingItems(Time.deltaTime);
        UpdateBounce(Time.deltaTime);
    }

    private void UpdateFlyingItems(float _deltaTime)
    {
        for (int i = flyingItems.Count - 1; i >= 0; i--)
        {
            LogItem item = flyingItems[i];
            item.ManualUpdate(_deltaTime);

            if (item.MoveState != ItemMoveState.Transferring && item.MoveState != ItemMoveState.CurveTransferring)
            {
                // 도착 연출 완료 (Scale 0 시점) - 실제 데이터 추가
                arrivalDataBuffer.itemType = item.itemType;
                arrivalDataBuffer.sprite = item.sprite;
                arrivalDataBuffer.color = item.color;
                arrivalDataBuffer.treeType = item.treeType;
                arrivalDataBuffer.logState = item.logState;

                AddItemByData(arrivalDataBuffer, item.logState);

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

        // 쫀득함 제거: 여러 번 진동하는 Sin 대신 단순한 반원 Sin 곡선 사용
        // t=0에서 0, t=0.5에서 최대, t=1에서 0이 되는 정직한 바운스
        float curve = Mathf.Sin(t * Mathf.PI) * 0.15f;

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
            bool anyTransferred = false;
            var charSlots = characterInventory.inventorySlots;

            for (int i = 0; i < characterInventory.currentSlotCnt; i++)
            {
                if (charSlots[i] is InventorySlot charSlot && charSlot.itemData != null && charSlot.count > 0)
                {
                    // 이미 전송 중인 슬롯이면 건너뛰기
                    if (transferringSlots.Contains(charSlot)) continue;

                    if (!(charSlot.itemData is LogItemData logSourceData)) continue;

                    // 해당 아이템을 OffroadContainer에 넣을 수 있는지 체크
                    if (!CanAddItemByData(logSourceData)) continue;

                    // 해당 슬롯 전송 코루틴 시작
                    StartCoroutine(TransferOneSlotVisualRoutine(charSlot));
                    anyTransferred = true;
                    
                    // 슬롯 단위 전송 시작 시 약간의 간격을 주어 겹치지 않게 함
                    yield return new WaitForSeconds(0.1f);
                }
            }

            if (!anyTransferred)
            {
                yield return new WaitForSeconds(0.2f); // 보낼 게 없으면 잠시 대기 후 재확인
            }
            else
            {
                yield return null;
            }
        }
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
                if (!CanAddItemByData(sourceData)) break;

                LogState takenState = _charSlot.TakeOneItem();

                LogItemData visualData = new LogItemData
                {
                    treeType = sourceData.treeType,
                    logState = takenState,
                    color = sourceData.color
                };

                LogItem flyingItem = logItemPoolManager.GetLogItem(visualData);
                flyingItem.IsDropItem(false);
                flyingItem.spriteRenderer.sortingOrder = 100;

                Vector3 start = charTransform != null ? charTransform.position : transform.position;
                Vector3 end = transform.position; // 도착점은 OffroadContainer의 위치

                // 포물선이 모든 각도에서 어색하지 않도록 구현 (직교 벡터 활용)
                Vector3 dir = (end - start).normalized;
                Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
                // 양방향 중 하나로 랜덤하게 약간 휘어지게
                float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector3 trajectoryJitter = normal * arcPower;

                // 회전 속도 및 방향 결정
                float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

                flyingItem.transform.position = start;

                // 전용 전송 메서드 호출 (시점, 종점, 높이, 시간, 궤적 지터, 회전 속도)
                flyingItem.TransferLaunch(start, end, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.5f), trajectoryJitter, rotationSpeed);
                flyingItems.Add(flyingItem);

                yield return new WaitForSeconds(FLY_INTERVAL);
            }

            // 슬롯이 비었다면 정리
            if (_charSlot.count == 0)
            {
                if (characterInventory is InventoryManager invManager)
                {
                    invManager.ItemDeleted(_charSlot);
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
                if (flyingItems[i].itemType == ItemType.Log &&
                    flyingItems[i].logState == logSource.logState &&
                    flyingItems[i].treeType == logSource.treeType)
                    pendingCount++;
            }
        }

        // 1. 현재 활성화된 슬롯 범위 내에서 기존 슬롯 확인 (중첩 가능하고 공간이 있는지)
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData != null &&
                (inventorySlots[i].totalCount + pendingCount) < maxItemsPerSlot &&
                IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                return true;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯이 있는지 확인
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null)
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
            if (inventorySlots[i].itemData != null &&
                inventorySlots[i].totalCount < maxItemsPerSlot &&
                IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                inventorySlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                return;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null)
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

                    inventorySlots[i].Setup(newData, 0);
                    inventorySlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
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

    private void OnDisable()
    {
        if (transferCoroutine != null)
        {
            StopCoroutine(transferCoroutine);
            transferCoroutine = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            if (transferCoroutine == null)
            {
                transferCoroutine = StartCoroutine(TransferAllItemsRoutine());
            }
        }
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            if (transferCoroutine == null)
            {
                transferCoroutine = StartCoroutine(TransferAllItemsRoutine());
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            if (transferCoroutine != null)
            {
                StopCoroutine(transferCoroutine);
                transferCoroutine = null;
            }
        }
    }
}
