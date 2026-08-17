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
    private Character character;
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
        public bool fromCharacter;
        // null이 아니면 toCharacter 경로 대신 이 운반 NPC(예: OffroadPorterNPC)의 인벤토리로 도착 처리한다.
        public LumberjackInventoryComponent toCarrier;
        // 이 아이템이 flyingItems에 들어온 뒤 경과한 시간. 정상적인 비행은 길어도 1~2초 안에 끝나므로,
        // FLYING_TIMEOUT을 넘기면 연출 버그 등으로 영영 도착하지 않는 것으로 보고 강제로 도착 처리한다.
        public float elapsedTime;
    }
    private const float FLYING_TIMEOUT = 5f;
    private List<FlyingTransferItem> flyingItems = new List<FlyingTransferItem>(32);
    private List<FlyingTransferItem> dismissingItems = new List<FlyingTransferItem>(16);
    private bool bFlyingPaused = false;

    [Header("Get/Out 아이템 사운드")]
    [SerializeField] private float depositPitchStep = 0.05f; // 상자에 연속으로 넣을 때마다 GetItem 피치가 오르는 정도
    [SerializeField] private float depositVolumeBoostMax = 1.3f; // 피치가 최대(1.5)에 도달했을 때의 볼륨 배율
    // 마지막 전송(납품/인출) 이후 이 시간(초) 동안 추가 전송이 없으면 다음 전송 시점에 피치를 초기화한다.
    // 플레이어의 물리적 콜라이더 Exit(트리거 경계에서의 미세한 흔들림 등)에 의존하지 않도록,
    // "흐름이 끊겼다"는 판정을 실제 전송 간격 기준으로 바꾼 것이다.
    [SerializeField] private float depositPitchResetTimeout = 1f;
    private const float DEPOSIT_PITCH_MIN = 1.0f;
    private const float DEPOSIT_PITCH_MAX = 1.5f;

    // 납품(컨테이너로 들어오는 방향, toCharacter=false)은 캐릭터가 넣든 NPC가 넣든 "컨테이너가
    // 채워지는 흐름" 하나로 취급해 피치를 공유한다 - 누가 넣었는지는 중요하지 않다.
    private float currentDepositPitch = DEPOSIT_PITCH_MIN;
    private float lastDepositPitchTime = -999f;

    // 인출(캐릭터/운반 NPC 쪽으로 나가는 방향, toCharacter=true)은 "누가 가져가는가"에 따라 서로
    // 독립된 흐름으로 취급해야 한다. 캐릭터는 아래 전용 필드를, 운반 NPC는 carrier별 항목
    // (carrierWithdrawPitches)을 각각 사용하므로, 캐릭터와 포터가 동시에 인출하거나 포터 여러 명이
    // 동시에 인출해도 서로의 피치 진행을 방해하지 않는다.
    private float currentWithdrawPitchCharacter = DEPOSIT_PITCH_MIN;
    private float lastWithdrawPitchTimeCharacter = -999f;

    // 운반 NPC 1명당 하나씩 갖는 인출 피치 상태. PlayDepositPitchSound에 필드를 ref로 넘기기 위해
    // (List<struct>는 인덱스 되쓰기가 필요해 ref를 못 넘긴다) 참조 타입으로 둔다.
    private class WithdrawPitchState
    {
        public LumberjackInventoryComponent carrier;
        public float pitch;
        public float lastTime;
    }
    // 포터는 많아야 수 명 수준(TownUnitSpawner.npcCount 기본 3, 스킬로 증가)이라 선형 탐색으로 충분하고,
    // 매 프레임이 아니라 아이템 착지 시점에만 조회되므로 비용이 무시할 수준이다.
    private readonly List<WithdrawPitchState> carrierWithdrawPitches = new List<WithdrawPitchState>(8);

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
    // 지금 진행 중인 "닫힘->열림" 연출이 플레이어의 상호작용 키 입력으로 시작된 것인지 표시하는 1회성
    // 플래그. bIsInteracting(키를 누르고 있는 동안만 true)을 대신 쓰면, 연출이 끝나기 전에 키를 놓아도
    // (짧게 탭만 해도) SetContainerVisualOpened(true) 시점엔 이미 false가 되어 전송이 시작되지 않는
    // 문제가 있었다. 이 플래그는 SetContainerVisualOpened(true)에서 한 번 소비되면 즉시 false로 리셋된다.
    private bool bPlayerOpenRequested = false;

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

    public void SetCharacter(Character _character)
    {
        character = _character;
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
            flyingData.elapsedTime += _deltaTime;

            // ContainerTransferring 및 DynamicTransferring 상태도 비행 중인 상태로 간주
            bool bStillFlying = item.MoveState == ItemMoveState.Transferring ||
                item.MoveState == ItemMoveState.CurveTransferring ||
                item.MoveState == ItemMoveState.ContainerTransferring ||
                item.MoveState == ItemMoveState.DynamicTransferring;

            if (bStillFlying && flyingData.elapsedTime < FLYING_TIMEOUT)
            {
                flyingItems[i] = flyingData;
                continue;
            }

            if (bStillFlying)
            {
                // 방어 코드: 정상적인 비행은 몇 초 안에 끝나야 한다. 어떤 이유로든 비행 상태가
                // 비정상적으로 오래 지속되면, 던전이 끝날 때까지 flyingItems에 남아 같은 조합의
                // 납품/인출 여유공간 계산(예: CanAddToCharacterInventory의 pendingCount)을 영구히
                // 막는 것을 방지하기 위해 여기서 강제로 도착 처리한다.
                Debug.LogWarning($"[OffroadContainer] 비행 아이템이 {FLYING_TIMEOUT}초 넘게 도착하지 않아 강제로 도착 처리합니다. state={item.MoveState}");
            }

            // 도착 연출 완료(정상 도착 또는 타임아웃 강제 처리) - 실제 데이터 추가
            {
                arrivalDataBuffer.itemType = item.itemType;
                arrivalDataBuffer.sprite = item.sprite;
                arrivalDataBuffer.color = item.color;
                arrivalDataBuffer.treeType = item.treeType;
                arrivalDataBuffer.logState = item.logState;

                if (flyingData.toCharacter)
                {
                    // 착지 시점에 실제로 커밋한다(발사 시점엔 여유공간 계산에서 pendingCount로만
                    // 반영됨 - CanAcquireData/CanAddToCharacterInventory 호출부 참고).
                    if (flyingData.toCarrier != null)
                    {
                        flyingData.toCarrier.AddItemByData(arrivalDataBuffer, item.logState);

                        // 인출은 가져가는 주체별로 독립된 흐름이므로, 이 운반 NPC 전용 카운터를 쓴다.
                        WithdrawPitchState carrierPitch = GetCarrierWithdrawPitch(flyingData.toCarrier);
                        PlayDepositPitchSound(ref carrierPitch.pitch, ref carrierPitch.lastTime);
                    }
                    else
                    {
                        AddToCharacterInventory(arrivalDataBuffer, item.logState);
                        character?.PlayItemAcquireBounce();
                        character?.PlayItemAcquireFlash();
                        PlayDepositPitchSound(ref currentWithdrawPitchCharacter, ref lastWithdrawPitchTimeCharacter);
                    }
                }
                else
                {
                    // 컨테이너로 들어오는 납품도 착지 시점에 커밋한다(발사 시점엔 CanAddItemByData의
                    // pendingCount로만 반영됨).
                    AddItemByData(arrivalDataBuffer, item.logState);
                    TriggerBounce();

                    if (flyingData.fromCharacter)
                    {
                        CameraMoveController.Instance?.ShakeCamera(1f, 0.08f);
                    }

                    // 납품은 캐릭터/NPC 구분 없이 하나의 흐름(currentDepositPitch)을 공유한다.
                    PlayDepositPitchSound(ref currentDepositPitch, ref lastDepositPitchTime);
                }

                logItemPoolManager.ReturnLogItem(item);
                flyingItems.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 주어진 운반 NPC 전용 인출 피치 상태를 가져온다(없으면 새로 만든다).
    /// </summary>
    private WithdrawPitchState GetCarrierWithdrawPitch(LumberjackInventoryComponent _carrier)
    {
        for (int i = 0; i < carrierWithdrawPitches.Count; i++)
        {
            if (carrierWithdrawPitches[i].carrier == _carrier) return carrierWithdrawPitches[i];
        }

        // 새 항목을 만들기 전에, 이미 파괴된 NPC가 남긴 항목을 정리해 리스트가 무한정 늘어나지 않게 한다.
        for (int i = carrierWithdrawPitches.Count - 1; i >= 0; i--)
        {
            if (carrierWithdrawPitches[i].carrier == null) carrierWithdrawPitches.RemoveAt(i);
        }

        WithdrawPitchState newState = new WithdrawPitchState
        {
            carrier = _carrier,
            pitch = DEPOSIT_PITCH_MIN,
            lastTime = -999f
        };
        carrierWithdrawPitches.Add(newState);
        return newState;
    }

    // 마지막 재생 이후 depositPitchResetTimeout(초)가 넘게 흘렀다면 그 사이 흐름이 실제로 끊긴
    // 것으로 보고 피치를 초기화한다(콜라이더 Exit 이벤트에 의존하지 않음). 호출부에서 넘기는
    // (_currentPitch, _lastTime) 쌍에 따라 납품/캐릭터 인출/NPC 인출 흐름이 서로 독립적으로 진행된다.
    private void PlayDepositPitchSound(ref float _currentPitch, ref float _lastTime)
    {
        if (Time.time - _lastTime > depositPitchResetTimeout)
        {
            _currentPitch = DEPOSIT_PITCH_MIN;
        }
        _lastTime = Time.time;

        // 연속으로 넣거나 꺼낼수록 피치/볼륨이 1.0~1.5 범위에서 선형으로 올라간다.
        float depositT = (_currentPitch - DEPOSIT_PITCH_MIN) / (DEPOSIT_PITCH_MAX - DEPOSIT_PITCH_MIN);
        float depositVolumeMul = Mathf.Lerp(1f, depositVolumeBoostMax, depositT);
        Sound.Play(SoundID.GetItem, transform.position, depositVolumeMul, true, _currentPitch);
        _currentPitch = Mathf.Clamp(_currentPitch + depositPitchStep, DEPOSIT_PITCH_MIN, DEPOSIT_PITCH_MAX);
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
        currentDepositPitch = DEPOSIT_PITCH_MIN;
        lastDepositPitchTime = -999f;
        currentWithdrawPitchCharacter = DEPOSIT_PITCH_MIN;
        lastWithdrawPitchTimeCharacter = -999f;
        carrierWithdrawPitches.Clear();
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

            // countToTransfer는 시작 시점의 스냅샷일 뿐이라 루프 조건으로 쓰지 않는다 - 이 슬롯을
            // WithdrawToCarrierRoutine(운반 NPC 인출)이 동시에 비우고 있을 수 있어서, 매 반복마다
            // _sourceSlot.count를 직접 다시 확인해야 한다. 그렇지 않으면 실제로는 이미 빈 슬롯인데도
            // 정해진 횟수만큼 TakeOneItem()을 계속 호출하게 되고, TakeOneItem()은 안전하게 기본값을
            // 반환하므로 존재하지 않는 아이템이 날아가는(복제되는) 결과가 된다.
            while (_sourceSlot.count > 0)
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
                Sound.PlayUI(SoundID.OutItem);

                if (!_toCharacter)
                {
                    if (characterInventoryManager != null)
                    {
                        characterInventoryManager.ItemRemoved();
                    }

                    // 컨테이너로의 실제 데이터 커밋은 착지 시점(UpdateFlyingItems)에 한다. 발사 시점엔
                    // CanAddItemByData의 pendingCount 계산에 이 날아가는 아이템이 반영되어, 다른 조합이
                    // 같은 빈 슬롯을 이중으로 예약하는 것을 막아준다.
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

                flyingItems.Add(new FlyingTransferItem { item = flyingItem, toCharacter = _toCharacter, fromCharacter = !_toCharacter });

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

    // 컨테이너/캐릭터 슬롯과 마찬가지로, 캐릭터의 인벤토리도 착지 시점 커밋이라 "빈 슬롯 쟁탈전"
    // 문제에서 자유롭지 않다. TransferAllItemsRoutine은 한 슬롯의 발사가 끝나면(아직 착지 전이어도)
    // 곧바로 다음 슬롯(다른 나무종류일 수 있음)으로 넘어가므로, 서로 다른 두 종류가 거의 동시에
    // 캐릭터의 같은 빈 슬롯을 향해 날아오는 상황이 실제로 발생할 수 있다. 그래서 이미 확보된(같은
    // 종류) 슬롯 여유로 충분한지 먼저 보고, 부족하면 "물리적으로 남은 빈 슬롯 수"와 "이미 다른
    // 종류가 빈 슬롯을 예약 중인 개수"를 정확히 비교한다(OffroadContainer/LogContainer의
    // CanAddItemByData와 동일한 방식).
    private bool CanAddToCharacterInventory(ItemData _sourceData)
    {
        if (!(_sourceData is LogItemData logSource) || characterInventoryManager == null) return false;

        var slots = characterInventoryManager.GetInventorySlots();
        int maxItems = characterInventoryManager.GetMaxItemsPerSlot();

        int matchingExistingSpace = 0;
        int emptySlotCount = 0;
        for (int i = 0; i < characterInventoryManager.currentSlotCnt; i++)
        {
            if (slots[i].itemData == null)
            {
                emptySlotCount++;
            }
            else if (IsSameItemByData(_sourceData, slots[i].itemData))
            {
                matchingExistingSpace += Mathf.Max(0, maxItems - slots[i].totalCount);
            }
        }

        int pendingSameType = 0;
        int emptySlotsReservedByOthers = 0;
        for (int i = 0; i < flyingItems.Count; i++)
        {
            // toCarrier != null이면 운반 NPC(WithdrawToCarrierRoutine)로 향하는 아이템이라 캐릭터의
            // 자리를 전혀 차지하지 않는다. 이걸 걸러내지 않으면 NPC가 동시에 인출 중일 때 캐릭터
            // 몫으로 잘못 카운트되어, 실제로는 자리가 있는데도 없다고 오판할 수 있다.
            if (!flyingItems[i].toCharacter || flyingItems[i].toCarrier != null || flyingItems[i].item.itemType != ItemType.Log)
                continue;

            if (flyingItems[i].item.logState == logSource.logState && flyingItems[i].item.treeType == logSource.treeType)
            {
                pendingSameType++;
                continue;
            }

            // 다른 조합은 첫 등장에서 한 번만 처리한다.
            bool alreadyCounted = false;
            for (int j = 0; j < i; j++)
            {
                if (flyingItems[j].toCharacter && flyingItems[j].toCarrier == null && flyingItems[j].item.itemType == ItemType.Log &&
                    flyingItems[j].item.logState == flyingItems[i].item.logState &&
                    flyingItems[j].item.treeType == flyingItems[i].item.treeType)
                {
                    alreadyCounted = true;
                    break;
                }
            }
            if (alreadyCounted) continue;

            // 이 다른 조합이 실제로 몇 칸의 캐릭터 빈 슬롯을 필요로 하는지 계산한다. 대기 물량 중
            // "이미 확보된(같은 조합) 슬롯 여유"로 흡수되고 남은 초과분만 빈 슬롯으로 넘어가며, 그
            // 초과분을 슬롯당 최대 용량으로 나눠 올림한 값이 필요한 빈 슬롯 수다. (조합당 무조건 1칸으로만
            // 세면, 한 조합이 대량이라 빈 슬롯을 여러 칸 점유하는 경우를 놓쳐 초과 발사/증발이 생긴다.)
            int otherPending = 0;
            for (int k = i; k < flyingItems.Count; k++)
            {
                if (flyingItems[k].toCharacter && flyingItems[k].toCarrier == null && flyingItems[k].item.itemType == ItemType.Log &&
                    flyingItems[k].item.logState == flyingItems[i].item.logState &&
                    flyingItems[k].item.treeType == flyingItems[i].item.treeType)
                {
                    otherPending++;
                }
            }

            int otherExistingSpace = 0;
            for (int s = 0; s < characterInventoryManager.currentSlotCnt; s++)
            {
                if (slots[s].itemData is LogItemData otherSlotData &&
                    otherSlotData.logState == flyingItems[i].item.logState &&
                    otherSlotData.treeType == flyingItems[i].item.treeType)
                {
                    int remaining = maxItems - slots[s].totalCount;
                    if (remaining > 0) otherExistingSpace += remaining;
                }
            }

            int overflow = otherPending - otherExistingSpace;
            if (overflow > 0)
            {
                emptySlotsReservedByOthers += (overflow + maxItems - 1) / maxItems;
            }
        }

        // 총 여유 용량 = 기존에 확보된(같은 종류) 슬롯 여유 + (나에게 배정 가능한 빈 슬롯 수) * 슬롯당
        // 최대 용량. 배정 가능한 빈 슬롯 수는 다른 조합들이 실제로 필요로 하는 칸수를 뺀 값이다.
        int emptySlotsAvailableToMe = emptySlotCount - emptySlotsReservedByOthers;
        int totalCapacity = matchingExistingSpace;
        if (emptySlotsAvailableToMe > 0)
        {
            totalCapacity += maxItems * emptySlotsAvailableToMe;
        }

        bool isSuccess = pendingSameType < totalCapacity;

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
                        // 위 계산과 동일한 이유로, 운반 NPC로 향하는 아이템(toCarrier != null)은
                        // 캐릭터 슬롯을 차지하지 않으므로 여기서도 제외해야 한다.
                        if (flyingItems[j].toCharacter && flyingItems[j].toCarrier == null && IsSameItem(flyingItems[j].item, slots[i].itemData))
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

    /// <summary>
    /// 실제 상자 스프라이트(OffroadContainerVComponent)가 붙어 있는 비주얼 트랜스폼. OffroadContainer
    /// 자신의 transform(GetTransform)은 위치 동기화용 로직 트랜스폼일 뿐 하위에 SpriteRenderer가 없으므로,
    /// 셰이더 효과 등 실제 렌더러가 필요한 용도는 반드시 이쪽을 써야 한다. OffroadVehicleObj.ResetObject()가
    /// 마을/던전 진입 시마다 현재 활성화된 차량의 컨테이너 오브젝트로 갱신한다.
    /// </summary>
    public Transform GetVisualTransform()
    {
        return visualTransform;
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
                    bPlayerOpenRequested = true;
                    OpenContainerImmediately();
                    if (bContainerVisualOpened)
                    {
                        bPlayerOpenRequested = false;
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
            bPlayerOpenRequested = false;
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

    /// <summary>
    /// 저장 시점에 아직 어느 컨테이너 슬롯에도 커밋되지 않은 "운반 중" 로그를 세이브 데이터에만
    /// 가상으로 정산한다(라이브 상태는 건드리지 않음). 방향 규칙:
    ///  - 이 컨테이너 -> 캐릭터로 날아가던 것: 캐릭터 세이브로 착지시킨다.
    ///  - 이 컨테이너 -> 포터로 날아가던 것 / 포터가 인벤토리에 들고 있던 것: 저장되지 않는 포터
    ///    대신 소스인 이 컨테이너 세이브로 되돌린다(인출 되돌리기).
    ///  - 이 컨테이너로 납품되던 것: 목적지인 이 컨테이너 세이브로 착지시킨다.
    ///
    /// 되돌릴 대상(_offroadSave)이 가득 찬 경우(던전을 왕복하며 포터가 이월 로그를 든 채 컨테이너가
    /// 다시 채워진 상황 등)에는, 포터가 원래 향하던 목적지인 LogContainer 세이브로 전진 납품해
    /// 유실을 막는다(fallback). 이 fallback이 정확하려면 반드시 LogContainer의 운반분이 먼저
    /// 정산된 뒤(_logContainerSave가 최신 상태)에 이 메서드를 호출해야 한다.
    /// </summary>
    public void AppendTransitToSaveData(ref InventorySaveData _offroadSave, ref InventorySaveData _characterSave,
        ref InventorySaveData _logContainerSave, int _characterMaxPerSlot, int _logContainerMaxPerSlot,
        IReadOnlyList<OffroadPorterNPC> _porters)
    {
        // 1. 이 컨테이너와 얽힌 비행 중 로그 정산
        for (int i = 0; i < flyingItems.Count; i++)
        {
            LogItem item = flyingItems[i].item;
            if (item == null || item.itemType != ItemType.Log) continue;

            if (flyingItems[i].toCarrier != null)
            {
                // 포터로 향하던 것 -> 소스인 이 컨테이너로 되돌린다(가득이면 LogContainer로 전진).
                MergeRollback(ref _offroadSave, maxItemsPerSlot, ref _logContainerSave, _logContainerMaxPerSlot,
                    item.treeType, item.logState, item.color);
            }
            else if (flyingItems[i].toCharacter)
            {
                // 캐릭터로 향하던 것 -> 캐릭터 인벤토리로 착지(발사 시점 용량 검사로 자리 보장).
                if (!SaveDataMerge.AddLog(ref _characterSave, item.treeType, item.logState, item.color, _characterMaxPerSlot))
                    Debug.LogWarning("[OffroadContainer] 저장 정산: 캐릭터행 비행 로그를 넣을 자리가 없습니다.");
            }
            else
            {
                // 이 컨테이너로 납품되던 것 -> 목적지인 이 컨테이너로 착지(가득이면 LogContainer로 전진).
                MergeRollback(ref _offroadSave, maxItemsPerSlot, ref _logContainerSave, _logContainerMaxPerSlot,
                    item.treeType, item.logState, item.color);
            }
        }

        // 2. 포터가 인벤토리에 들고 있던 로그 정산 -> 소스인 이 컨테이너로 되돌린다(가득이면 LogContainer로 전진).
        if (_porters == null) return;
        for (int p = 0; p < _porters.Count; p++)
        {
            OffroadPorterNPC porter = _porters[p];
            if (porter == null || porter.inventory == null) continue;

            List<InventorySlot> porterSlots = porter.inventory.GetInventorySlots();
            int slotCnt = porter.inventory.currentSlotCnt;
            for (int s = 0; s < slotCnt; s++)
            {
                InventorySlot slot = porterSlots[s];
                if (!(slot.itemData is LogItemData logData) || slot.totalCount <= 0) continue;

                // 슬롯은 단일 (나무종류/등급) 조합이라 totalCount만큼 같은 로그를 되돌리면 된다.
                for (int c = 0; c < slot.totalCount; c++)
                {
                    MergeRollback(ref _offroadSave, maxItemsPerSlot, ref _logContainerSave, _logContainerMaxPerSlot,
                        logData.treeType, logData.logState, logData.color);
                }
            }
        }
    }

    /// <summary>
    /// 저장 정산용: 로그 1개를 우선 _primary(OffroadContainer) 세이브로 되돌리고, 자리가 없으면
    /// _fallback(LogContainer) 세이브로 전진 납품한다. 둘 다 가득이면(모든 컨테이너가 꽉 찬 극단
    /// 상황) 경고만 남긴다.
    /// </summary>
    private static void MergeRollback(ref InventorySaveData _primary, int _primaryMax,
        ref InventorySaveData _fallback, int _fallbackMax, TreeType _treeType, LogState _logState, Color _color)
    {
        if (SaveDataMerge.AddLog(ref _primary, _treeType, _logState, _color, _primaryMax)) return;
        if (SaveDataMerge.AddLog(ref _fallback, _treeType, _logState, _color, _fallbackMax)) return;
        Debug.LogWarning("[OffroadContainer] 저장 정산: 운반 로그를 넣을 자리가 없습니다(모든 컨테이너 가득 참).");
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
                                    // 황금/다이아/무지개 원목은 상태별 스프라이트를 써야 한다.
                                    logData.sprite = typeData.GetSprite(logData.logState);
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
    /// 날아가는 연출 없이, 여유 슬롯이 있으면 즉시 데이터를 커밋한다(캐릭터 사망 등 연출이 무의미한 상황용).
    /// 던전 입장 등 여러 아이템을 한 프레임에 연속으로 강제 이전할 때는 _playSound를 false로 넘겨
    /// SFX가 아이템 개수만큼 중첩 재생되는 것을 막는다.
    /// </summary>
    public bool TryAddLogItemDataDirect(LogItemData _sourceData, LogState _state, bool _playSound = true)
    {
        if (!CanAddItemByData(_sourceData)) return false;

        AddItemByData(_sourceData, _state);
        if (_playSound)
        {
            Sound.PlayUI(SoundID.GetItem);
        }
        return true;
    }

    /// <summary>
    /// 럼버잭 NPC 등 플레이어가 아닌 소비자가 로그를 컨테이너에 직접 납품할 때 사용하는 공개 API.
    /// 플레이어가 TransferOneSlotVisualRoutine으로 넣을 때와 동일하게 로그가 날아가는 연출(flyingItems)을
    /// 거치며, 슬롯 데이터는 착지 시점(UpdateFlyingItems)에 실제로 커밋된다.
    ///
    /// (서로 다른 나무종류/등급을 가진 두 NPC가 거의 동시에 같은 빈 슬롯을 향해 발사되면, 착지
    /// 전까지는 CanAddItemByData의 pendingCount 계산이 이미 발사된 물량을 반영해 여유 공간을
    /// 정확히 판단한다. 그래서 착지 시점 커밋이어도 슬롯 초과로 인한 증발이 발생하지 않는다.)
    ///
    /// 플레이어 전용 상호작용 상태(bIsInteracting/transferCoroutine/물리 오버랩)는 전혀 건드리지 않으므로
    /// 플레이어의 컨테이너 상호작용이나 다른 NPC의 납품 호출과 서로 간섭하지 않는다.
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

        flyingItems.Add(new FlyingTransferItem { item = flyingItem, toCharacter = false, fromCharacter = false });

        return true;
    }

    /// <summary>
    /// NPC 인벤토리의 로그 아이템들을 캐릭터의 컨테이너 전송과 동일한 시간 간격(인터벌)을 적용하여 천천히 납품합니다.
    /// 납품 연출이 모두 끝나면 _onComplete 콜백을 호출합니다. 인자는 "이번 시도에서 실제로 하나라도
    /// 넣었는지"이며, 납품 도중/직후에 흡입 중이던 다른 로그가 뒤늦게 착지해 인벤토리 총량이 달라져도
    /// 영향받지 않도록 넣은 개수를 여기서 직접 셉니다(호출부가 전/후 총량을 비교하지 않게 하기 위함).
    /// </summary>
    public void TransferFromNPC(LumberjackInventoryComponent _npcInventory, Vector3 _fromWorldPos, Action<bool> _onComplete)
    {
        StartCoroutine(NPCTransferRoutine(_npcInventory, _fromWorldPos, _onComplete));
    }

    private IEnumerator NPCTransferRoutine(LumberjackInventoryComponent _npcInventory, Vector3 _fromWorldPos, Action<bool> _onComplete)
    {
        // TEMP DEBUG
        LJDebugLog.Log($"[LJDebug] t={Time.time:F2} NPCTransferRoutine 시작. npc={_npcInventory.name}({_npcInventory.GetEntityId()}), 슬롯수={_npcInventory.currentSlotCnt}");

        bool anyDelivered = false;
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
                    // TEMP DEBUG
                    LJDebugLog.Log($"[LJDebug] t={Time.time:F2} NPCTransferRoutine 슬롯{i} 납품 중단. npc={_npcInventory.name}({_npcInventory.GetEntityId()}), 조합=({logData.treeType}/{logData.logState}), 남은수량={slot.totalCount}");
                    break;
                }

                slot.TakeOneItem();
                Sound.PlayUI(SoundID.OutItem);
                slotTransferredAny = true;
                anyDelivered = true;

                // 이 코루틴을 미래에 외부에서 취소하는 경로가 생기더라도(현재는 없음), 슬롯이 이번
                // 아이템으로 완전히 비었으면 즉시 정리해서 totalCount==0인데 itemData가 남아있는
                // "유령 점유" 슬롯이 생기지 않도록 방어한다(WithdrawToCarrierRoutine과 동일한 패턴).
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

        // TEMP DEBUG
        LJDebugLog.Log($"[LJDebug] t={Time.time:F2} NPCTransferRoutine 완료, 콜백 호출 직전. npc={_npcInventory.name}({_npcInventory.GetEntityId()}), anyDelivered={anyDelivered}");

        _onComplete?.Invoke(anyDelivered);
    }

    /// <summary>
    /// 운반 NPC(OffroadPorterNPC 등)가 이 컨테이너의 로그를 자신의 인벤토리로 꺼내갈 때 사용하는 공개 API.
    /// TransferFromNPC(납품)와 반대 방향으로, 컨테이너 -> 운반 NPC 인벤토리로 로그가 하나씩 날아가는 연출을 거친다.
    /// 반환하는 Coroutine 핸들은 CancelWithdraw로 도중에 취소할 때 쓴다.
    /// </summary>
    public Coroutine WithdrawToCarrier(LumberjackInventoryComponent _carrierInventory, Action<bool> _onComplete)
    {
        return StartCoroutine(WithdrawToCarrierRoutine(_carrierInventory, _onComplete));
    }

    /// <summary>
    /// 진행 중인 인출 코루틴을 중단한다. 이미 발사되어 flyingItems에 들어간 아이템은(발사 시점에
    /// 데이터가 이미 커밋되므로) 이 컨테이너의 UpdateFlyingItems에 의해 그대로 착지/습득되고,
    /// 아직 발사되지 않은(더 가져오려던) 나머지만 취소된다. _onComplete는 호출되지 않는다.
    /// </summary>
    public void CancelWithdraw(Coroutine _coroutine)
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
        }
    }

    private IEnumerator WithdrawToCarrierRoutine(LumberjackInventoryComponent _carrierInventory, Action<bool> _onComplete)
    {
        if (_carrierInventory == null)
        {
            _onComplete?.Invoke(false);
            yield break;
        }

        // 착지 시점 커밋이라 코루틴이 끝나는 시점엔 방금 발사한 아이템들이 아직 캐리어 인벤토리에
        // 반영되지 않았을 수 있다. bInventoryIsEmpty만으로 완료를 판단하면 "분명히 인출했는데
        // 아직 착지 전이라 비어있다고 착각해서" 바로 Idle로 돌아가 버리는 문제가 생기므로, 이번
        // 세션에서 실제로 하나라도 발사했는지를 별도로 반환한다.
        bool anyWithdrawn = false;

        for (int i = 0; i < currentSlotCount; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (!(slot.itemData is LogItemData sourceData) || slot.totalCount <= 0) continue;

            bool transferredAny = false;
            while (slot.totalCount > 0)
            {
                // 착지 시점(UpdateFlyingItems)에 실제로 커밋되므로, 아직 도착하지 않고 날아오는 중인
                // 물량까지 캐리어의 여유 공간과 비교해야 한다. FLY_INTERVAL(0.075초)이 실제 비행
                // 시간(0.8~1.2초)보다 훨씬 짧아 앞서 발사된 아이템들이 아직 반영 안 된 상태로 계속
                // 승인되면, 실제 용량보다 훨씬 많이 발사되어 나중에 도착한 아이템이 갈 곳을 잃는다.
                int pendingSameTypeForCarrier = 0;
                int emptySlotsReservedByOthers = 0;
                int carrierMaxPerSlot = _carrierInventory.maxItemCntPerSlot;
                for (int j = 0; j < flyingItems.Count; j++)
                {
                    if (!flyingItems[j].toCharacter || flyingItems[j].toCarrier != _carrierInventory ||
                        flyingItems[j].item.itemType != ItemType.Log)
                        continue;

                    if (flyingItems[j].item.logState == sourceData.logState && flyingItems[j].item.treeType == sourceData.treeType)
                    {
                        pendingSameTypeForCarrier++;
                        continue;
                    }

                    // 다른 조합은 첫 등장에서 한 번만 처리한다.
                    bool alreadyCounted = false;
                    for (int k = 0; k < j; k++)
                    {
                        if (flyingItems[k].toCharacter && flyingItems[k].toCarrier == _carrierInventory &&
                            flyingItems[k].item.itemType == ItemType.Log &&
                            flyingItems[k].item.logState == flyingItems[j].item.logState &&
                            flyingItems[k].item.treeType == flyingItems[j].item.treeType)
                        {
                            alreadyCounted = true;
                            break;
                        }
                    }
                    if (alreadyCounted) continue;

                    // 이 다른 조합이 실제로 몇 칸의 캐리어 빈 슬롯을 필요로 하는지 계산한다. 대기 물량 중
                    // "이미 확보된(같은 조합) 슬롯 여유"로 흡수되고 남은 초과분만 빈 슬롯으로 넘어가며, 그
                    // 초과분을 슬롯당 최대 용량으로 나눠 올림한 값이 필요한 빈 슬롯 수다. (조합당 무조건 1칸으로만
                    // 세면, 캐리어가 다중 슬롯일 때 한 조합이 빈 슬롯을 여러 칸 점유하는 경우를 놓쳐,
                    // 내가 초과 발사되고 나중에 착지하는 쪽이 갈 곳을 잃는 증발 버그가 생긴다.)
                    int otherPending = 0;
                    for (int k = j; k < flyingItems.Count; k++)
                    {
                        if (flyingItems[k].toCharacter && flyingItems[k].toCarrier == _carrierInventory &&
                            flyingItems[k].item.itemType == ItemType.Log &&
                            flyingItems[k].item.logState == flyingItems[j].item.logState &&
                            flyingItems[k].item.treeType == flyingItems[j].item.treeType)
                        {
                            otherPending++;
                        }
                    }

                    int otherExistingSpace = _carrierInventory.GetMatchingSlotSpaceFor(flyingItems[j].item.logState, flyingItems[j].item.treeType);

                    int overflow = otherPending - otherExistingSpace;
                    if (overflow > 0)
                    {
                        emptySlotsReservedByOthers += (overflow + carrierMaxPerSlot - 1) / carrierMaxPerSlot;
                    }
                }

                // 총 여유 용량 = 기존에 확보된(같은 종류) 슬롯 여유 + (나에게 배정 가능한 빈 슬롯 수) *
                // 슬롯당 최대 용량. 배정 가능한 빈 슬롯 수는 다른 조합들이 실제로 필요로 하는 칸수를 뺀 값이다.
                int matchingExistingSpace = _carrierInventory.GetMatchingSlotSpaceFor(sourceData);
                int emptySlotsAvailableToMe = _carrierInventory.GetEmptySlotCount() - emptySlotsReservedByOthers;
                int totalCapacityForCarrier = matchingExistingSpace;
                if (emptySlotsAvailableToMe > 0)
                {
                    totalCapacityForCarrier += carrierMaxPerSlot * emptySlotsAvailableToMe;
                }

                if (pendingSameTypeForCarrier >= totalCapacityForCarrier) break;

                LogState takenState = slot.TakeOneItem();
                Sound.PlayUI(SoundID.OutItem);

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
                Vector3 carrierPos = _carrierInventory.transform.position;

                Vector3 dir = (carrierPos - containerPos).normalized;
                if (dir == Vector3.zero) dir = Vector3.up;
                Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
                float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
                Vector3 trajectoryJitter = normal * arcPower;

                float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

                flyingItem.transform.position = containerPos;
                flyingItem.DynamicTransferLaunch(containerPos, _carrierInventory.transform, UnityEngine.Random.Range(0.8f, 1.2f), UnityEngine.Random.Range(0.5f, 0.5f), trajectoryJitter, rotationSpeed);

                flyingItems.Add(new FlyingTransferItem { item = flyingItem, toCharacter = true, fromCharacter = false, toCarrier = _carrierInventory });

                transferredAny = true;
                anyWithdrawn = true;

                // 슬롯이 이번 아이템으로 완전히 비었다면 바로 정리한다. 아래 yield 도중
                // CancelWithdraw(StopCoroutine)로 이 코루틴이 외부에서 즉시 중단되면(예: 텔레포트
                // 취소) 루프 밖의 정리 코드에 도달하지 못해, totalCount는 0인데 itemData는 남아있는
                // "유령 점유" 슬롯이 생겨 이후 같은 나무종류만 받을 수 있게 굳어버리는 문제가 있었다.
                if (slot.totalCount == 0)
                {
                    ItemDeleted(slot);
                    ContainerUpdatedEvent?.Invoke();
                }

                yield return new WaitForSeconds(FLY_INTERVAL / Mathf.Max(0.01f, itemTransferSpeedMul));
            }

            if (transferredAny)
            {
                yield return new WaitForSeconds(transferInterval / Mathf.Max(0.01f, itemTransferSpeedMul));
            }
        }

        _onComplete?.Invoke(anyWithdrawn);
    }

    // 로그를 실제로 슬롯에 커밋하는 시점이 착지 시점(UpdateFlyingItems)이므로, 아직 도착하지 않고
    // 날아오는 중인 물량까지 감안해야 한다. 단순히 "빈 슬롯 용량을 전부 더한 합"과 비교하면, 서로
    // 다른 조합이 같은 빈 슬롯 하나를 향해 거의 동시에 발사됐을 때 둘 다 "빈 슬롯 있음"으로 통과해
    // 버려 나중에 착지하는 쪽이 갈 곳을 잃는다(증발). 그래서 이미 확보된(같은 종류) 슬롯 여유로
    // 충분한지 먼저 보고, 부족하면 "물리적으로 남은 빈 슬롯 수"와 "이미 빈 슬롯을 예약 중인 서로
    // 다른 종류의 개수"를 정확히 비교한다. 단순 불리언(다른 종류가 하나라도 있으면 무조건 거절)으로
    // 하면 빈 슬롯이 2개 있는데 서로 다른 종류 2개가 동시에 들어와도 하나가 불필요하게 거절당해,
    // 럼버잭 NPC가 그 세션에 하나도 못 넣어서 재시도 없이 영구 정지(bPermanentlyStuck)하는 상황을
    // 실제보다 더 자주 유발할 수 있었다.
    public bool CanAddItemByData(ItemData _sourceData)
    {
        if (!(_sourceData is LogItemData logSource)) return false;

        int matchingExistingSpace = 0;
        int emptySlotCount = 0;
        for (int i = 0; i < currentSlotCount; i++)
        {
            if (inventorySlots[i].itemData == null)
            {
                emptySlotCount++;
            }
            else if (IsSameItemByData(_sourceData, inventorySlots[i].itemData))
            {
                int remaining = maxItemsPerSlot - inventorySlots[i].totalCount;
                if (remaining > 0) matchingExistingSpace += remaining;
            }
        }

        int pendingSameType = 0;
        int emptySlotsReservedByOthers = 0;
        for (int i = 0; i < flyingItems.Count; i++)
        {
            // !toCharacter인 항목은 캐릭터/NPC가 이 컨테이너로 납품 중인(아직 착지 안 한) 물량이다.
            if (flyingItems[i].toCharacter || flyingItems[i].item.itemType != ItemType.Log) continue;

            if (flyingItems[i].item.logState == logSource.logState && flyingItems[i].item.treeType == logSource.treeType)
            {
                pendingSameType++;
                continue;
            }

            // 다른 조합은 첫 등장에서 한 번만 처리한다.
            bool alreadyCounted = false;
            for (int j = 0; j < i; j++)
            {
                // flyingItems[j]가 flyingItems[i]와 같은 (다른) 조합이면 이미 처리된 것이다.
                if (!flyingItems[j].toCharacter && flyingItems[j].item.itemType == ItemType.Log &&
                    flyingItems[j].item.logState == flyingItems[i].item.logState &&
                    flyingItems[j].item.treeType == flyingItems[i].item.treeType)
                {
                    alreadyCounted = true;
                    break;
                }
            }
            if (alreadyCounted) continue;

            // 이 다른 조합이 실제로 몇 칸의 빈 슬롯을 필요로 하는지 계산한다. 대기 물량 중 "이미
            // 확보된(같은 조합) 슬롯 여유"로 흡수되고 남은 초과분만 빈 슬롯으로 넘어가며, 그 초과분을
            // 슬롯당 최대 용량으로 나눠 올림한 값이 필요한 빈 슬롯 수다. (조합당 무조건 1칸으로만 세면,
            // 한 조합이 대량이라 빈 슬롯을 여러 칸 점유하는 경우를 놓쳐 초과 발사/증발이 생긴다.)
            int otherPending = 0;
            for (int k = i; k < flyingItems.Count; k++)
            {
                if (!flyingItems[k].toCharacter && flyingItems[k].item.itemType == ItemType.Log &&
                    flyingItems[k].item.logState == flyingItems[i].item.logState &&
                    flyingItems[k].item.treeType == flyingItems[i].item.treeType)
                {
                    otherPending++;
                }
            }

            int otherExistingSpace = 0;
            for (int s = 0; s < currentSlotCount; s++)
            {
                if (inventorySlots[s].itemData is LogItemData otherSlotData &&
                    otherSlotData.logState == flyingItems[i].item.logState &&
                    otherSlotData.treeType == flyingItems[i].item.treeType)
                {
                    int remaining = maxItemsPerSlot - inventorySlots[s].totalCount;
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

        // flyingItems(운반 NPC 인출 등)만으로도 bContainerOpen -> ContainerOpenedEvent -> 뚜껑 열림
        // 연출 -> 이 메서드까지 이어질 수 있다. bPlayerOpenRequested(이번 열림이 플레이어의 상호작용
        // 키 입력으로 시작됐는지)를 확인하지 않으면, 캐릭터가 우연히 근처에 서 있기만 해도 NPC 활동으로
        // 열린 뚜껑에 반응해 캐릭터 전송이 자동으로 시작되어 버린다(운반 NPC가 가져가려던 로그를
        // 가로채는 문제). bIsInteracting(키를 누르고 있는 동안만 true) 대신 이 플래그를 쓰는 이유는,
        // 연출이 끝나기 전에 키를 놓아도(짧게 탭만 해도) 정상적으로 전송이 시작되어야 하기 때문이다.
        if (bInTown && bContainerVisualOpened && bPlayerOpenRequested && transferCoroutine == null)
        {
            bPlayerOpenRequested = false;

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
