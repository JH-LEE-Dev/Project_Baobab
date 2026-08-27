using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Collections;
using UnityEngine.Rendering;

public class LogContainer : MonoBehaviour, IInventory, IContainerCH
{
    public event Action LogContainerIsEmptyEvent;
    public event Action ItemAddedEvent;
    public event Action ContainerSpecChangedEvent;
    public event Action<LogItemData> LogOutEvent;
    public event Action<bool> InteractStateEvent;
    public event Action ContainerUpdatedEvent;
    public event Action InventoryIsFullEvent { add { } remove { } }

    private InputManager inputManager;
    private IInventory characterInventory;
    private InventoryManager characterInventoryManager;
    private Transform charTransform;
    private LogItemPoolingManager logItemPoolManager;
    [SerializeField] private Transform inputTransform;

    [SerializeField] private GameObject outLineObject;

    private SpriteRenderer sr;
    private SpriteRenderer outlineSr;
    private Collider2D col;

    //외부 의존성

    // 내부 의존성
    [SerializeField] private int currentSlotCount = 2; // 기본 슬롯 2개
    [SerializeField] private int maxItemsPerSlot = 5; // 슬롯당 최대 보관 개수
    [SerializeField] private List<InventorySlot> containerSlots = new List<InventorySlot>(SYSTEM_VAR.MAX_INVENTORY_CNT);
    [SerializeField] private float transferInterval = 2f;
    // 캐릭터가 자기 인벤토리를 이 컨테이너로 납품할 때 슬롯 하나를 다 발사한 뒤 다음 슬롯을
    // 시작하기까지의 대기(TransferRoutine 전용). transferInterval은 자동 출고(TakeFirstItem)
    // 주기 및 NPC 납품(NPCTransferRoutine)의 슬롯 간 대기와 공유되므로, 플레이어 전송 간격만
    // OffroadContainer와 맞추기 위해 별도 필드로 분리한다. 현재 OffroadContainer가 슬롯 간
    // 대기 없이(transferInterval=0) 연속 전송하므로 여기도 0으로 맞춰 두었다. 이 값을 0이 아닌
    // 값으로 되돌려도 NPC 납품 간격은 영향받지 않는다.
    [SerializeField] private float transferSlotInterval = 0f;
    // 타입별 아이템 데이터 풀링 (GC 최적화)
    private ItemDataPool itemDataPool;

    public Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.4f;

    IReadOnlyList<IInventorySlot> IInventory.inventorySlots => containerSlots;
    public long money => 0;
    public long carrot => 0;
    int IInventory.maxCapacity => currentSlotCount * maxItemsPerSlot;
    int IInventory.currentItemCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < currentSlotCount; i++)
            {
                if (containerSlots[i].itemData != null)
                {
                    total += containerSlots[i].totalCount;
                }
            }
            return total;
        }
    }

    public int currentSlotCnt => currentSlotCount;

    public int maxItemCntPerSlot => maxItemsPerSlot;

    private bool bCanInteract = false;
    private bool bPhysicalOverlapped = false;
    private bool bCanReach = true;
    private bool bLastInteractState = false;

    public bool isPhysicalOverlapped => bPhysicalOverlapped;
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
    private struct FlyingTransferItem
    {
        public LogItem item;
        public bool fromCharacter;
    }
    private List<FlyingTransferItem> flyingItems = new List<FlyingTransferItem>(32);
    private HashSet<InventorySlot> transferringSlots = new HashSet<InventorySlot>();
    private const float FLY_INTERVAL = 0.075f;
    private LogItemData arrivalDataBuffer = new LogItemData();

    [Header("Get/Out 아이템 사운드")]
    [SerializeField] private float depositPitchStep = 0.05f; // 컨테이너에 연속으로 넣을 때마다 GetItem 피치가 오르는 정도
    [SerializeField] private float depositVolumeBoostMax = 1.3f; // 피치가 최대(1.5)에 도달했을 때의 볼륨 배율
    // 마지막 납품 이후 이 시간(초) 동안 추가 납품이 없으면 다음 납품 시점에 피치를 초기화한다.
    // 플레이어의 물리적 콜라이더 Exit(트리거 경계에서의 미세한 흔들림 등)에 의존하지 않도록,
    // "흐름이 끊겼다"는 판정을 실제 납품 간격 기준으로 바꾼 것이다.
    [SerializeField] private float depositPitchResetTimeout = 1f;
    private const float DEPOSIT_PITCH_MIN = 1.0f;
    private const float DEPOSIT_PITCH_MAX = 1.5f;
    private float currentDepositPitch = DEPOSIT_PITCH_MIN;
    private float lastDepositPitchTime = -999f;

    private CustomSortable customSortable;

    [SerializeField] private Sprite noLogSprite;
    [SerializeField] private Sprite fewLogSprite;
    [SerializeField] private Sprite MiddleLogSprite;
    [SerializeField] private Sprite ManyLogSprite;

    public float itemTransferSpeedMul = 1f;
    private float globalSpeedMultiplier = 1f;

    private MapType mapType;

    public void SetGlobalSpeedMultiplier(float _mul)
    {
        globalSpeedMultiplier = _mul;
    }

    // LogCutter.GetSoundVolume()과 동일한 규칙: 마을이 아니면(=던전에 있는 동안 배경에서 계속
    // 납품이 진행되는 상태) 납품 사운드도 재생하지 않는다.
    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;
    }

    private float GetSoundVolume()
    {
        return mapType == MapType.Town ? 1f : 0f;
    }

    public void Initialize(InputManager _inputManager, LogItemPoolingManager logItemPoolingManager)
    {
        if (itemDataPool == null) itemDataPool = new ItemDataPool(CreateItemData);

        inputManager = _inputManager;
        logItemPoolManager = logItemPoolingManager;

        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(GetComponent<SortingGroup>());

        // 시각적 효과를 위한 트랜스폼 캐싱
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        if (outLineObject != null)
        {
            outlineSr = outLineObject.GetComponent<SpriteRenderer>();
            if (outlineSr == null)
            {
                outlineSr = outLineObject.GetComponentInChildren<SpriteRenderer>();
            }
        }

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
                itemDataPool.Release(data);
            }
            containerSlots[i].Setup(null, 0);
        }

        // 3. 모든 아이템 타입에 대해 풀 미리 생성
        itemDataPool.WarmAll();

        BindEvents();
        UpdateSprite();
    }

    public void SetCharTransform(Transform _transform)
    {
        charTransform = _transform;
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void OnDisable()
    {
        // GameObject 비활성화 시 진행 중이던 슬롯 코루틴(TransferOneSlotVisualRoutine)은 finally
        // 없이 중단되어 transferringSlots에 유령 항목이 남을 수 있다. 재활성화 후 TransferRoutine의
        // 직렬화 대기(while transferringSlots.Count > 0)가 영구 정지하거나, 스테일 transferCoroutine
        // 핸들 때문에 전송이 아예 재시작되지 않는 것을 방지하기 위해 여기서 정리한다.
        // (OffroadContainer.ResetState의 transferringSlots.Clear()와 동일한 방어.)
        transferringSlots.Clear();
        transferCoroutine = null;
        currentDepositPitch = DEPOSIT_PITCH_MIN;
        lastDepositPitchTime = -999f;
    }

    public void DI_Inventory(IInventory _inventory)
    {
        characterInventory = _inventory;
        characterInventoryManager = _inventory as InventoryManager;
    }

    private void Update()
    {
        UpdateFlyingItems(Time.deltaTime);
        UpdateBounce(Time.deltaTime);

        // 자동 출고 타이머는 더 이상 여기서 돌리지 않는다. 각 LogProcessLine이
        // LogProcessingManager를 통해 자기만의 타이머로 독립적으로 TakeFirstItem()을
        // 호출해 원목을 가져간다(라인별 진입 간격이 다른 라인 상태에 영향받지 않도록).
        if (customSortable != null)
            customSortable.SetHeight(0f);
    }

    /// <summary>
    /// 라인이 하나라도 꺼내갈 수 있는 원목이 남아있는지 확인합니다(실제로 꺼내지는 않음).
    /// </summary>
    public bool HasAvailableItem()
    {
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData != null && containerSlots[i].count > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 속도 배율이 반영된 실제 출고 간격. 각 라인이 자기 타이머를 이 간격과 비교해
    /// TakeFirstItem() 호출 시점을 판단한다.
    /// </summary>
    public float GetEffectiveTransferInterval()
    {
        return transferInterval / (Mathf.Max(0.01f, itemTransferSpeedMul) * Mathf.Max(0.01f, globalSpeedMultiplier));
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }

    private void UpdateFlyingItems(float _deltaTime)
    {
        for (int i = flyingItems.Count - 1; i >= 0; i--)
        {
            var flyingData = flyingItems[i];
            LogItem item = flyingData.item;
            item.ManualUpdate(_deltaTime);

            if (item.MoveState != ItemMoveState.Transferring)
            {
                // 도착 연출 완료 (Scale 0 시점) - 실제 데이터 커밋은 여기서 한다(발사 시점엔
                // CanAddItemByData의 pendingCount로만 반영됨).
                arrivalDataBuffer.itemType = item.itemType;
                arrivalDataBuffer.sprite = item.sprite;
                arrivalDataBuffer.color = item.color;
                arrivalDataBuffer.treeType = item.treeType;
                arrivalDataBuffer.logState = item.logState;

                AddItemByData(arrivalDataBuffer, item.logState);
                TriggerBounce();
                
                if (flyingData.fromCharacter)
                {
                    CameraMoveController.Instance?.ShakeCamera(1f, 0.08f);

                    // 원목이 상자에 박히는 순간. fromCharacter 가드가 곧 "캐릭터가 넣은 것"이라,
                    // 벌목 NPC가 납품하는 동안에는 울리지 않는다.
                    Rumble.Play(EHapticEvent.ItemImpact);
                }

                // 마지막 납품 이후 depositPitchResetTimeout(초)가 넘게 흘렀다면 그 사이 흐름이
                // 실제로 끊긴 것으로 보고 피치를 초기화한다(콜라이더 Exit 이벤트에 의존하지 않음).
                if (Time.time - lastDepositPitchTime > depositPitchResetTimeout)
                {
                    currentDepositPitch = DEPOSIT_PITCH_MIN;
                }
                lastDepositPitchTime = Time.time;

                // 컨테이너에 연속으로 넣을수록 피치/볼륨이 1.0~1.5 범위에서 선형으로 올라간다.
                float depositT = (currentDepositPitch - DEPOSIT_PITCH_MIN) / (DEPOSIT_PITCH_MAX - DEPOSIT_PITCH_MIN);
                float depositVolumeMul = Mathf.Lerp(1f, depositVolumeBoostMax, depositT);
                Sound.Play(SoundID.GetItem, transform.position, depositVolumeMul * GetSoundVolume(), true, currentDepositPitch);
                currentDepositPitch = Mathf.Clamp(currentDepositPitch + depositPitchStep, DEPOSIT_PITCH_MIN, DEPOSIT_PITCH_MAX);

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
        float curve = Mathf.Sin(t * Mathf.PI * 3f) * Mathf.Exp(-t * 4f) * 0.25f;

        if (visualTransform != null)
        {
            // X축 확대 시 Y축 축소 (Squash & Stretch)
            visualTransform.localScale = new Vector3(1f + curve, 1f - curve, 1f);
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

    public List<InventorySlot> GetContainerSlots()
    {
        return containerSlots;
    }

    private void InteractionKeyPressed()
    {
        if (!bCanInteract || characterInventory == null) return;

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
            while (Time.time - lastTransferTime < (transferSlotInterval / Mathf.Max(0.01f, itemTransferSpeedMul)))
            {
                yield return null;
            }

            // 현재 전송 중인 슬롯이 있다면 완료될 때까지 대기 (OffroadContainer.TransferAllItemsRoutine과
            // 동일하게, 슬롯 하나의 전송이 모두 끝나야 다음 슬롯을 시작하도록 직렬화)
            while (transferringSlots.Count > 0)
            {
                yield return null;
            }

            if (!TryTransferOneItem())
            {
                break;
            }

            // 방금 시작한 슬롯의 전송이 끝날 때까지 대기
            while (transferringSlots.Count > 0)
            {
                yield return null;
            }
        }

        transferCoroutine = null;
    }

    private bool TryTransferOneItem()
    {
        if (!bCanInteract || characterInventory == null) return false;

        var charSlots = characterInventory.inventorySlots;
        for (int i = 0; i < characterInventory.currentSlotCnt; i++)
        {
            if (charSlots[i] is InventorySlot charSlot && charSlot.itemData != null && charSlot.count > 0)
            {
                // 이미 전송 중인 슬롯이면 건너뛰기
                if (transferringSlots.Contains(charSlot)) continue;

                if (!(charSlot.itemData is LogItemData logSourceData)) continue;

                // 넣을 자리가 없는 슬롯은 여기서 걸러낸다(OffroadContainer.TryTransferOneSlot과 동일).
                // 이 검사를 빼면 컨테이너가 가득 찼을 때 TransferOneSlotVisualRoutine이 첫 CanAddItemByData
                // 에서 곧바로 break되어 yield 없이 동기적으로 끝나버리는데, TryTransferOneItem은 그래도
                // true를 반환하므로 TransferRoutine의 while(true)가 한 프레임 안에서 무한히 도는 문제가
                // 생긴다(transferSlotInterval=0이라 상단 대기 루프도 yield하지 않음).
                // 또한 앞 슬롯이 가득 차 못 들어가도 뒤쪽의 다른 나무종류 슬롯은 계속 전송할 수 있게 된다.
                if (!CanAddItemByData(logSourceData)) continue;

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
                Sound.PlayUI(SoundID.OutItem);

                if (characterInventoryManager != null)
                {
                    characterInventoryManager.ItemRemoved();
                }

                // 컨테이너로의 실제 데이터 커밋은 착지 시점(UpdateFlyingItems)에 한다. 발사 시점엔
                // CanAddItemByData의 pendingCount 계산에 이 날아가는 아이템이 반영되어, 다른 조합이
                // 같은 빈 슬롯을 이중으로 예약하는 것을 막아준다.

                // 시각적 비행 아이템 생성
                LogItemData visualData = new LogItemData
                {
                    treeType = sourceData.treeType,
                    logState = takenState,
                    color = sourceData.color
                };

                LogItem flyingItem = logItemPoolManager.GetLogItem(visualData);
                flyingItem.SetFlyingItemSortingLayer();
                flyingItem.IsDropItem(false);

                Vector3 start = charTransform != null ? charTransform.position : transform.position;
                Vector3 end = inputTransform != null ? inputTransform.position : transform.position;

                // 궤적 jitter를 대폭 줄여서 포물선 형태가 뭉개지지 않도록 수정
                Vector3 trajectoryJitter = new Vector3(UnityEngine.Random.Range(-0.3f, 0.0f), UnityEngine.Random.Range(-0.2f, 0.0f), 0f);

                // 회전 속도 및 방향 결정 (빠르지 않게: 90~270도/s 정도)
                float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

                flyingItem.transform.position = start;

                // 전용 전송 메서드 호출 (시점, 종점, 높이, 시간, 궤적 지터, 회전 속도)
                // 비행 시간은 OffroadContainer와 동일하게 0.5초 고정.
                flyingItem.TransferLaunch(start, end, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.5f), trajectoryJitter, rotationSpeed);
                flyingItems.Add(new FlyingTransferItem { item = flyingItem, fromCharacter = true });

                ContainerUpdatedEvent?.Invoke();

                yield return new WaitForSeconds(FLY_INTERVAL / Mathf.Max(0.01f, itemTransferSpeedMul));
            }

            // 슬롯이 비었다면 정리
            if (_charSlot.count == 0)
            {
                if (characterInventoryManager != null)
                {
                    characterInventoryManager.ItemDeleted(_charSlot);
                }
                else if (characterInventory is LogContainer container)
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

    // 로그를 실제로 슬롯에 커밋하는 시점이 착지 시점(UpdateFlyingItems)이므로, 아직 도착하지 않고
    // 날아오는 중인 물량까지 감안해야 한다. 단순히 "빈 슬롯 용량을 전부 더한 합"과 비교하면, 서로
    // 다른 조합(캐릭터가 넣는 것 + 운반 NPC가 넣는 것 등)이 같은 빈 슬롯 하나를 향해 거의 동시에
    // 발사됐을 때 둘 다 "빈 슬롯 있음"으로 통과해버려 나중에 착지하는 쪽이 갈 곳을 잃는다(증발).
    // 그래서 이미 확보된(같은 종류) 슬롯 여유로 충분한지 먼저 보고, 부족하면 "물리적으로 남은 빈
    // 슬롯 수"와 "이미 빈 슬롯을 예약 중인 서로 다른 종류의 개수"를 정확히 비교한다. 단순 불리언
    // (다른 종류가 하나라도 있으면 무조건 거절)으로 하면 빈 슬롯이 2개 있어도 하나가 불필요하게
    // 거절당해, 운반 NPC가 그 세션에 하나도 못 넣어서 재시도 없이 멈추는 상황을 실제보다 더 자주
    // 유발할 수 있다.
    private bool CanAddItemByData(ItemData _sourceData)
    {
        if (!(_sourceData is LogItemData logSource)) return false;

        int matchingExistingSpace = 0;
        int emptySlotCount = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData == null)
            {
                emptySlotCount++;
            }
            else if (IsSameItemByData(_sourceData, containerSlots[i].itemData))
            {
                int remaining = maxItemsPerSlot - containerSlots[i].totalCount;
                if (remaining > 0) matchingExistingSpace += remaining;
            }
        }

        int pendingSameType = 0;
        int emptySlotsReservedByOthers = 0;
        for (int i = 0; i < flyingItems.Count; i++)
        {
            var flyingData = flyingItems[i];
            LogItem itemI = flyingData.item;
            
            // LogContainer의 flyingItems는 전부 "이 컨테이너로 들어오는 중"인 항목뿐이다
            // (여기서 밖으로 꺼내가는 경로는 없음).
            if (itemI.itemType != ItemType.Log) continue;

            if (itemI.logState == logSource.logState && itemI.treeType == logSource.treeType)
            {
                pendingSameType++;
                continue;
            }

            // 다른 조합은 첫 등장에서 한 번만 처리한다.
            bool alreadyCounted = false;
            for (int j = 0; j < i; j++)
            {
                var itemJ = flyingItems[j].item;
                if (itemJ.itemType == ItemType.Log &&
                    itemJ.logState == itemI.logState &&
                    itemJ.treeType == itemI.treeType)
                {
                    alreadyCounted = true;
                    break;
                }
            }
            if (alreadyCounted) continue;

            // 이 다른 조합이 실제로 몇 칸의 빈 슬롯을 필요로 하는지 계산한다. 대기 물량 중 "이미
            // 확보된(같은 조합) 슬롯 여유"로 흡수되고 남은 초과분만 빈 슬롯으로 넘어가며, 그 초과분을
            // 슬롯당 최대 용량으로 나눠 올림한 값이 필요한 빈 슬롯 수다. (조합당 무조건 1칸으로만 세면,
            // 한 조합이 대량이라 빈 슬롯을 여러 칸 점유하는 경우를 놓쳐, 내가 초과 발사되고 나중에
            // 착지하는 쪽이 갈 곳을 잃는 증발 버그가 생긴다.)
            int otherPending = 0;
            for (int k = i; k < flyingItems.Count; k++)
            {
                var itemK = flyingItems[k].item;
                if (itemK.itemType == ItemType.Log &&
                    itemK.logState == itemI.logState &&
                    itemK.treeType == itemI.treeType)
                {
                    otherPending++;
                }
            }

            int otherExistingSpace = 0;
            for (int s = 0; s < currentSlotCount; s++)
            {
                if (containerSlots[s].itemData is LogItemData otherSlotData &&
                    otherSlotData.logState == itemI.logState &&
                    otherSlotData.treeType == itemI.treeType)
                {
                    int remaining = maxItemsPerSlot - containerSlots[s].totalCount;
                    if (remaining > 0) otherExistingSpace += remaining;
                }
            }

            int overflow = otherPending - otherExistingSpace;
            if (overflow > 0)
            {
                emptySlotsReservedByOthers += (overflow + maxItemsPerSlot - 1) / maxItemsPerSlot;
            }
        }

        // 총 여유 용량 = 기존에 확보된(같은 종류) 슬롯 여유 + (나에게 배정 가능한 빈 슬롯 수) * 슬롯당
        // 최대 용량. 배정 가능한 빈 슬롯 수는 다른 조합들이 실제로 필요로 하는 칸수를 뺀 값이다.
        int emptySlotsAvailableToMe = emptySlotCount - emptySlotsReservedByOthers;
        int totalCapacity = matchingExistingSpace;
        if (emptySlotsAvailableToMe > 0)
        {
            totalCapacity += maxItemsPerSlot * emptySlotsAvailableToMe;
        }

        return pendingSameType < totalCapacity;
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
                containerSlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                ItemAddedEvent?.Invoke();
                ContainerUpdatedEvent?.Invoke();
                return;
            }
        }

        // 2. 현재 활성화된 슬롯 범위 내에서 빈 슬롯을 찾아 추가
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (containerSlots[i].itemData == null)
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

                    containerSlots[i].Setup(newData, 0);
                    containerSlots[i].AddCountByState(_state, (_sourceData as LogItemData)?.treeType ?? TreeType.None);
                    ItemAddedEvent?.Invoke();
                    ContainerUpdatedEvent?.Invoke();
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

    private void DebugLogCharacterInventory()
    {
        if (characterInventory == null || bDebug == false) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<color=cyan>--- Character Inventory Status ---</color>");
        var slots = characterInventory.inventorySlots;
        for (int i = 0; i < characterInventory.currentSlotCnt; i++)
        {
            var slot = slots[i];
            if (slot.itemData != null && slot.count > 0)
            {
                if (slot.itemData is LogItemData logData)
                {
                    sb.AppendFormat("Slot[{0}]: {1} Log (Total: {2})\n", i, logData.logState, slot.count);

                    // 각 나무 종류별 상세 수량 정보 출력
                    var treeCounts = slot.treeTypeCounts;
                    for (int j = 0; j < treeCounts.Length; j++)
                    {
                        if (treeCounts[j].count > 0)
                        {
                            sb.AppendFormat("  - {0}: {1}\n", treeCounts[j].treeType, treeCounts[j].count);
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

        ContainerUpdatedEvent -= UpdateSprite;
        ContainerUpdatedEvent += UpdateSprite;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        ContainerUpdatedEvent -= UpdateSprite;
    }

    private void UpdateSprite()
    {
        if (sr == null) return;

        int totalLogCount = 0;
        for (int i = 0; i < containerSlots.Count; i++)
        {
            if (containerSlots[i] != null)
            {
                totalLogCount += containerSlots[i].count;
            }
        }

        Sprite targetSprite = null;
        if (totalLogCount >= 16)
        {
            targetSprite = ManyLogSprite;
        }
        else if (totalLogCount >= 6)
        {
            targetSprite = MiddleLogSprite;
        }
        else if (totalLogCount >= 1)
        {
            targetSprite = fewLogSprite;
        }
        else
        {
            targetSprite = noLogSprite;
        }

        sr.sprite = targetSprite;
        if (outlineSr != null)
        {
            outlineSr.sprite = targetSprite;
        }
    }

    private void UpdateInteractState()
    {
        bool currentState = bCanReach && bPhysicalOverlapped;
        if (currentState != bLastInteractState)
        {
            bLastInteractState = currentState;
            bCanInteract = currentState;
            InteractStateEvent?.Invoke(currentState);
            outLineObject.SetActive(currentState);

            if (!currentState)
            {
                if (transferCoroutine != null)
                {
                    StopCoroutine(transferCoroutine);
                    transferCoroutine = null;
                }
            }
        }
    }

    public void SetCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bPhysicalOverlapped = true;
            UpdateInteractState();
        }
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
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
            UpdateInteractState();
        }
    }

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// 주어진 월드 좌표가 이 컨테이너의 실제 충돌 반경(collider) 안에 들어와 있는지 확인합니다.
    /// 운반 NPC가 길찾기로 이 컨테이너를 향해 이동하다가 이 반경에 들어오는 순간 납품을 시작한다.
    /// </summary>
    public bool IsWithinInteractRadius(Vector3 _worldPos)
    {
        if (col == null) return false;
        return col.OverlapPoint(_worldPos);
    }

    /// <summary>
    /// 운반 NPC(OffroadPorterNPC 등)가 로그를 이 컨테이너에 직접 납품할 때 사용하는 공개 API.
    /// 슬롯 데이터는 착지 시점(UpdateFlyingItems)에 커밋된다. 발사 시점엔 CanAddItemByData의
    /// pendingCount 계산이 이미 발사된 물량을 반영하므로, 서로 다른 조합이 같은 빈 슬롯을 동시에
    /// 예약해서 나중에 착지하는 쪽 데이터가 사라지는 문제는 생기지 않는다.
    /// </summary>
    public bool TryDepositLogItemVisual(LogItemData _sourceData, Vector3 _fromWorldPos, LogState _state)
    {
        LogItemData visualData = new LogItemData
        {
            // itemType을 반드시 원본과 동일하게(Log) 세팅해야 한다. 빼먹으면 기본값 None이 되어
            // CanAddItemByData 내부 IsSameItemByData의 itemType 비교에서 기존 슬롯과 절대 매칭되지
            // 않아, 실제로는 기존 슬롯에 자리가 있는데도 새 빈 슬롯이 필요하다고 오판한다.
            itemType = _sourceData.itemType,
            treeType = _sourceData.treeType,
            logState = _state,
            color = _sourceData.color
        };

        // 잭팟 등으로 _state가 _sourceData.logState와 달라질 수 있으므로, 공간 체크도 실제로
        // 착지할 상태(visualData) 기준으로 해야 서로 다른 슬롯 조합끼리 용량이 어긋나지 않는다.
        if (!CanAddItemByData(visualData)) return false;

        LogItem flyingItem = logItemPoolManager.GetLogItem(visualData);
        flyingItem.SetFlyingItemSortingLayer();
        flyingItem.IsDropItem(false);

        Vector3 end = inputTransform != null ? inputTransform.position : transform.position;

        Vector3 dir = (end - _fromWorldPos).normalized;
        if (dir == Vector3.zero) dir = Vector3.up;
        Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
        float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
        Vector3 trajectoryJitter = normal * arcPower;

        float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

        flyingItem.transform.position = _fromWorldPos;
        flyingItem.TransferLaunch(_fromWorldPos, end, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.7f), trajectoryJitter, rotationSpeed);
        flyingItems.Add(new FlyingTransferItem { item = flyingItem, fromCharacter = false });

        ContainerUpdatedEvent?.Invoke();

        return true;
    }

    /// <summary>
    /// 운반 NPC 인벤토리의 로그 아이템들을 캐릭터 납품과 동일한 연출로 천천히 이 컨테이너에 납품합니다.
    /// 납품 연출이 모두 끝나면(상자가 가득 차 일부가 남더라도) _onComplete 콜백을 호출합니다.
    /// _jackpotChance(퍼센트, 0~100)를 넘기면, 납품되는 로그 하나하나에 대해 그 확률로 한 단계 높은
    /// LogState로 승급되어 납품됩니다(OffroadPorterNPCJackpot 스킬용, 이미 최고 등급 Perfect은 제외).
    /// </summary>
    public void TransferFromNPC(LumberjackInventoryComponent _npcInventory, Vector3 _fromWorldPos, Action _onComplete, float _jackpotChance = 0f)
    {
        StartCoroutine(NPCTransferRoutine(_npcInventory, _fromWorldPos, _onComplete, _jackpotChance));
    }

    private IEnumerator NPCTransferRoutine(LumberjackInventoryComponent _npcInventory, Vector3 _fromWorldPos, Action _onComplete, float _jackpotChance)
    {
        var slots = _npcInventory.GetInventorySlots();
        for (int i = 0; i < _npcInventory.currentSlotCnt; i++)
        {
            var slot = slots[i];
            if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

            bool slotTransferredAny = false;
            while (slot.totalCount > 0)
            {
                LogState originalState = logData.logState;
                LogState depositState = originalState;

                // 잭팟이 터지면 현재 등급에서 한 단계 높은 LogState로 승급 시도한다. 이미 최고 등급
                // (Perfect)이면 더 올릴 곳이 없으므로 제외한다.
                if (_jackpotChance > 0f && originalState < LogState.Perfect && UnityEngine.Random.Range(0f, 100f) < _jackpotChance)
                {
                    depositState = originalState + 1;
                }

                // 승급 등급 기준으로 착지 자리가 있는지까지 포함해 발사한다(TryDepositLogItemVisual
                // 내부에서 CanAddItemByData로 확인). 착지 시점(AddItemByData)에 자리가 없으면 로그가
                // 조용히 사라지므로, 이 발사 단계 검사에서 반드시 걸러야 한다.
                bool deposited = TryDepositLogItemVisual(logData, _fromWorldPos, depositState);

                // 승급 등급으로는 들어갈 자리가 아예 없으면(빈 슬롯도 없고 그 등급 슬롯도 없음) 승급
                // (변환)을 취소하고 원래 등급으로 다시 시도한다. 실패한 첫 호출은 CanAddItemByData
                // 단계에서 막혀 부수효과가 없으므로 재시도는 안전하다.
                if (!deposited && depositState != originalState)
                {
                    deposited = TryDepositLogItemVisual(logData, _fromWorldPos, originalState);
                }

                // 승급/원래 등급 어느 쪽으로도 자리가 없으면 컨테이너가 이 종류로 가득 찬 것이므로 이
                // 슬롯 납품을 멈춘다. 남은 로그는 NPC 인벤토리에 그대로 남아 다음 기회에 납품된다(유실 없음).
                if (!deposited)
                {
                    break;
                }

                slot.TakeOneItem();
                Sound.PlayUI(SoundID.OutItem, GetSoundVolume());
                slotTransferredAny = true;

                // 이 코루틴을 미래에 외부에서 취소하는 경로가 생기더라도(현재는 없음), 슬롯이 이번
                // 아이템으로 완전히 비었으면 즉시 정리해서 totalCount==0인데 itemData가 남아있는
                // "유령 점유" 슬롯이 생기지 않도록 방어한다(OffroadContainer.WithdrawToCarrierRoutine과
                // 동일한 패턴).
                if (slot.totalCount == 0)
                {
                    _npcInventory.ItemDeleted(slot);
                }

                yield return new WaitForSeconds(FLY_INTERVAL / Mathf.Max(0.01f, itemTransferSpeedMul));
            }

            if (slotTransferredAny)
            {
                yield return new WaitForSeconds(transferInterval / Mathf.Max(0.01f, itemTransferSpeedMul));
            }
        }

        _onComplete?.Invoke();
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
                // 보관함 -> 레일(가공 라인) 출고는 인벤토리 인출과 성격이 달라 전용 SFX를 따로 붙일
                // 예정이므로 여기서는 OutItem을 재생하지 않는다.
                // (캐릭터/NPC가 보관함으로 납품하는 경로의 OutItem은 그대로 유지)

                // 2. 외부로 반환할 데이터 생성 (풀링 활용)
                LogItemData resultData = itemDataPool.Get(ItemType.Log) as LogItemData;
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
                        itemDataPool.Release(data);
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

                // LogOutEvent는 동기적으로 처리되고(구독자는 resultData의 필드만 읽어 즉시 LogItem을
                // 생성함) 아무도 참조를 보관하지 않으므로, 디스패치가 끝난 이 시점에 풀로 반납해 재사용한다.
                // 반납하지 않으면 출고 때마다 새 LogItemData가 할당되어 풀링이 무력화된다.
                itemDataPool.Release(resultData);

                if (((IInventory)this).currentItemCount == 0)
                {
                    LogContainerIsEmptyEvent?.Invoke();
                }

                return;
            }
        }
    }

    public void PopulateContainerSaveData(ref InventorySaveData _saveData)
    {
        _saveData.money = 0;
        _saveData.carrot = 0;

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
                    slotData.treeTypeCounts = slot.GetTreeTypeCounts();
                }

                slotData.itemSaveData = itemSaveData;
            }

            _saveData.slots.Add(slotData);
        }
    }

    /// <summary>
    /// 저장 시점에 이 컨테이너로 날아오던(아직 착지=커밋되지 않은) 로그를 세이브 데이터에만 가상으로
    /// 착지시킨다(라이브 상태는 건드리지 않음). LogContainer의 flyingItems는 전부 "이 컨테이너로
    /// 들어오는 중"인 항목뿐이라(포터/캐릭터 납품분) 방향 구분 없이 이 컨테이너 세이브로 합산한다.
    /// </summary>
    public void AppendTransitToSaveData(ref InventorySaveData _saveData)
    {
        for (int i = 0; i < flyingItems.Count; i++)
        {
            LogItem item = flyingItems[i].item;
            if (item == null || item.itemType != ItemType.Log) continue;

            if (!SaveDataMerge.AddLog(ref _saveData, item.treeType, item.logState, item.color, maxItemsPerSlot))
                Debug.LogWarning("[LogContainer] 저장 정산: 납품 비행 로그를 넣을 자리가 없습니다.");
        }
    }

    public void LoadSaveData(LogProcessingSaveData _data)
    {
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
                itemDataPool.Release(itemData);
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
                    ItemData newData = itemDataPool.Get(slotData.itemSaveData.itemType);
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
                                // 황금/다이아/무지개 원목은 상태별 스프라이트를 써야 한다.
                                logData.sprite = typeData.GetSprite(logData.logState);
                            }
                        }

                        containerSlots[i].Setup(newData, slotData.totalCount);

                        if (slotData.treeTypeCounts != null && slotData.treeTypeCounts.Length > 0)
                        {
                            containerSlots[i].LoadTreeTypeCounts(slotData.treeTypeCounts);
                        }
                    }
                }
            }
        }

        ContainerUpdatedEvent?.Invoke();
        ContainerSpecChangedEvent?.Invoke();
        UpdateSprite();

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

    public void ItemTransferSpeedUP(float _amount)
    {
        itemTransferSpeedMul += (_amount / 100f);
    }
}
