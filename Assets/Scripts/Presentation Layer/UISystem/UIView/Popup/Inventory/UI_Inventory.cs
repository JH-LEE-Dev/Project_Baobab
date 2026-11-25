using System;
using System.Collections.Generic;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using TMPro;
using PresentationLayer.UISystem.CustomNumber;

/// <summary>
/// 인벤토리 UI의 전체적인 로직을 관리하는 클래스입니다.
/// 슬롯 생성, 데이터 바인딩, 재화 표시 및 팝업 연동을 담당합니다.
/// </summary>
public class UI_Inventory : MonoBehaviour
{
    // //이벤트 및 대리자
    public event Action<IInventorySlot> sendDeleteItemEvent;

    /// <summary>
    /// 교체 대상 슬롯 정보가 달라졌을 때 발생합니다(생김 / 사라짐 / 다른 슬롯으로 이동 / 개수 변화).
    /// LogSwapInfo와 ActiveLogSwapTarget을 다시 읽어 표시를 맞추세요.
    /// </summary>
    public event Action LogSwapInfoChangedEvent;

    /// <summary>
    /// 교체가 실제로 일어나 인벤토리 슬롯 하나가 비워졌을 때 발생합니다. 인자는 방금 버려진 슬롯의
    /// 내용(slotIndex / treeType / logState / count)입니다 - 데이터는 이미 지워진 뒤입니다.
    /// </summary>
    public event Action<LogSwapSlotInfo> LogSwapExecutedEvent;

    public Action inventoryHoverEvent;
    public Action inventoryUnHoverEvent;

    // //외부 의존성
    [Header("Binding Obj")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private GameObject invBackground;
    [SerializeField] private CurrencyCounterHUD uiCoin;
    [SerializeField] private UI_Backpack uiBackpack;
    [SerializeField] private UI_InventoryCapacityBar capacityBar;
    [SerializeField] private UI_RedDot newAlertRedDot;
    
    [Header("Keyboard Icons")]
    [SerializeField] private UI_KeyboardImage[] keyboardImages;

    [Header("Icons Follower")]
    [SerializeField] private RectTransform iconsRoot;
    [SerializeField] private RectTransform iconsAnchor;

    [Header("Localization")]
    [SerializeField] private TextMeshProUGUI openText;

    [Header("Prefabs")]
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;
    [SerializeField] private string uiSlotLayerName = "ScreenSpaceUI";

    [Header("Inventory Settings")]
    [Tooltip("사전 생성할 최대 인벤토리 슬롯 수")]
    [SerializeField] private int maxSlotPrewarmCount = 32;
    [SerializeField] private List<UI_InventorySlot> inventorySlots = new List<UI_InventorySlot>(32);

    [Header("Smart Swap Settings")]
    [Tooltip("스마트 스왑 단일 인디케이터 프리팹 (Icons 하위에 1회 인스턴스화)")]
    [SerializeField] private GameObject swapIndicatorPrefab;
    [Tooltip("인디케이터가 대상 슬롯 머리 위를 가리키는 로컬 오프셋")]
    [SerializeField] private Vector2 indicatorOffset = new Vector2(0f, 24f);

    // //내부 의존성
    private const string backpackTag = "Backpack";
    private const string coinsTag = "Coins";
    private const string popupTag = "Popup";

    private UI_SwapIndicator sharedSwapIndicator;
    private int currentSwapSlotIndex = -1;

    private IInventory inventory;
    private IMoneyData moneyData;
    private UI_InventoryPopup invPopup;
    private LocalizationManager locManager;

    private bool isOpenAnimated = false;
    private int previousLogCount = 0;
    private bool isFirstDataBind = true;

    // 원목 갱신 구간(ItemPickupBatch) 안에서 미뤄 둔 획득 갱신. 구간이 끝나면 한 번에 처리한다(FlushPendingItemAdded).
    private int pendingItemAddedCount = 0;       // 미뤄 둔 획득 횟수
    private int pendingRedDotActivateCount = 0;  // 그 사이 빨간 점을 켰어야 하는 횟수(같은 프레임의 세 번째부터는 두 번째와 결과가 같아 2까지만 센다)
    private int pendingLogCount = 0;             // 마지막으로 미룬 시점의 원목 개수(previousLogCount가 될 값)
    private int pendingItemCount = 0;            // 마지막으로 미룬 시점의 용량바 값
    private int pendingMaxCapacity = 0;

    // 교체 대상 슬롯 정보. 가방이 닫혀 있는 동안에도 값은 그대로 유지한다.
    private LogSwapSlotInfo logSwapInfo = LogSwapSlotInfo.None;          // 인벤토리 쪽: 버려질 가방 슬롯
    private LogSwapSlotInfo outgoingSwapInfo = LogSwapSlotInfo.None;     // 상자 쪽: 상자로 넘어갈 가방 슬롯
    private ELogSwapTarget activeLogSwapTarget = ELogSwapTarget.None;

    // //프로퍼티
    public MapType CurrentMapType { get; set; } = MapType.Town;
    public bool IsOpening { get; private set; } = false;

    /// <summary>
    /// 지금 교체하면 버려질 인벤토리 슬롯입니다. bHasSlot이 false면 교체 대상이 없습니다.
    /// slotIndex는 inventory.inventorySlots(= 이 UI가 그리는 슬롯 목록)와 같은 인덱스입니다.
    /// </summary>
    public LogSwapSlotInfo LogSwapInfo => logSwapInfo;

    /// <summary>
    /// 교체 키를 누르면 실제로 버려지는 쪽입니다. Inventory가 아니면(운반 상자가 우선인 상황 등)
    /// 이 창의 후보는 "예정"일 뿐 지금 키를 눌러도 버려지지 않습니다.
    /// </summary>
    public ELogSwapTarget ActiveLogSwapTarget => activeLogSwapTarget;

    /// <summary>교체 키가 이 인벤토리의 슬롯을 버리게 되는 상태인지입니다(안내를 켜는 기본 조건).</summary>
    public bool IsLogSwapReady => logSwapInfo.bHasSlot && ELogSwapTarget.Inventory == activeLogSwapTarget;

    /// <summary>
    /// <b>운반 상자 쪽</b> 교체 제안입니다. 상자 앞에서 상자가 가득 차 있을 때 잡히며, 이때 가방 UI가
    /// 보여줄 것은 "버려질 슬롯"이 아니라 <b>"상자로 넘어갈 슬롯"</b>입니다 -
    /// <c>OutgoingSwapInfo.incomingSlotIndex</c>가 그 가방 슬롯의 인덱스입니다.
    /// 상자 쪽 제안이 없으면 bHasSlot == false.
    /// </summary>
    public LogSwapSlotInfo OutgoingSwapInfo => outgoingSwapInfo;

    /// <summary>
    /// 교체 키를 누르면 상자로 넘어갈 가방 슬롯의 인덱스. 없으면 -1.
    /// <c>inventory.inventorySlots</c>(= 이 UI가 그리는 슬롯 목록)와 같은 인덱스입니다.
    /// </summary>
    public int OutgoingSlotIndex => outgoingSwapInfo.bHasSlot ? outgoingSwapInfo.incomingSlotIndex : -1;

    /// <summary>교체 키가 운반 상자의 슬롯을 버리고 이 가방의 슬롯을 넘기게 되는 상태인지입니다.</summary>
    public bool IsOutgoingSwapReady => outgoingSwapInfo.bHasSlot && ELogSwapTarget.OffroadContainer == activeLogSwapTarget;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(Transform _uiRoot, Action _hoverEvent, Action _unHoverEvent, InputManager _inputManager, LocalizationManager _locManager)
    {
        locManager = _locManager;
        
        if (null != locManager)
        {
            locManager.OnLanguageChanged -= RefreshLocalizedTexts;
            locManager.OnLanguageChanged += RefreshLocalizedTexts;
        }
        
        RefreshLocalizedTexts();

        if (null != omp)
            omp.Initialize();

        InitInventoryPopup();
        InitCoins();
        InitBackpack();
        InitCapacityBar();
        UpdateIconsPosition();
        if (null != iconsRoot)
            iconsRoot.SetAsLastSibling();
        
        if (null != keyboardImages)
        {
            for (int i = 0; keyboardImages.Length > i; i++)
            {
                if (null != keyboardImages[i]) keyboardImages[i].Initialize(_inputManager);
            }
        }

        inventoryHoverEvent = _hoverEvent;
        inventoryUnHoverEvent = _unHoverEvent;

        if (null != uiSlotPrefab && null != invBackground)
        {
            int _needPrewarm = maxSlotPrewarmCount - inventorySlots.Count;
            for (int i = 0; _needPrewarm > i; i++)
            {
                CreateSlotInstance();
            }
        }

        InitSwapIndicator(_inputManager);

        LogSwapInfoChangedEvent -= HandleLogSwapInfoChanged;
        LogSwapInfoChangedEvent += HandleLogSwapInfoChanged;

        LogSwapExecutedEvent -= HandleLogSwapExecuted;
        LogSwapExecutedEvent += HandleLogSwapExecuted;

        ItemPickupBatch.Ended -= FlushPendingItemAdded;
        ItemPickupBatch.Ended += FlushPendingItemAdded;
    }

    private UI_InventorySlot CreateSlotInstance()
    {
        if (null == uiSlotPrefab || null == invBackground)
            return null;

        GameObject _slotObj = Instantiate(uiSlotPrefab, invBackground.transform);
        UI_InventorySlot _slot = _slotObj.GetComponent<UI_InventorySlot>();

        if (null != _slot)
        {
            _slot.Initialize();
            _slot.SetLayer(uiSlotLayerName);
            _slot.exitSlot -= inventoryUnHoverEvent;
            _slot.exitSlot += inventoryUnHoverEvent;
            _slot.gameObject.SetActive(false);
            inventorySlots.Add(_slot);
        }

        return _slot;
    }

    private void InitSwapIndicator(InputManager _inputManager)
    {
        if (null == sharedSwapIndicator && null != swapIndicatorPrefab)
        {
            Transform _parent = transform;
            GameObject _inst = Instantiate(swapIndicatorPrefab, _parent);
            if (null != _inst)
            {
                sharedSwapIndicator = _inst.GetComponent<UI_SwapIndicator>();
                if (null != sharedSwapIndicator)
                {
                    sharedSwapIndicator.Initialize(_inputManager);
                    sharedSwapIndicator.transform.SetAsLastSibling();
                    sharedSwapIndicator.HideImmediate();
                }
            }
        }
        else if (null != sharedSwapIndicator)
        {
            sharedSwapIndicator.Initialize(_inputManager);
            sharedSwapIndicator.transform.SetAsLastSibling();
            sharedSwapIndicator.HideImmediate();
        }
    }

    public void BindData(IInventory _inventory, IMoneyData _moneyData)
    {
        FlushPendingItemAdded();

        inventory = _inventory;
        moneyData = _moneyData;

        if (null != uiCoin)
            uiCoin.SetMoneyType(MoneyType.Coin);
            
        CharactersMoneyChanged();
    }

    public void UpdateMaxSlotCount(int _cnt)
    {
        FlushPendingItemAdded();

        if (null == uiSlotPrefab || null == invBackground)
            return;

        int _currentCount = inventorySlots.Count;
        int _needCount = _cnt - _currentCount;

        if (0 < _needCount)
        {
            Debug.LogWarning($"[UI_Inventory] maxSlotPrewarmCount({maxSlotPrewarmCount}) 부족으로 런타임 동적 생성됨.");
            for (int _i = 0; _needCount > _i; _i++)
            {
                CreateSlotInstance();
            }
        }
    }

    public void SendDeleteItem(IInventorySlot _inData)
    {
        FlushPendingItemAdded();

        if (null == inventory)
            return;

        sendDeleteItemEvent?.Invoke(_inData);
        UpdateSlots(inventory.inventorySlots);
    }

    public void Refresh() //저장 파일 로드할 때도 호출됨.
    {
        FlushPendingItemAdded();

        if (null != inventory)
            UpdateSlots(inventory.inventorySlots);

        // 세이브 로드로 money가 직접 대입되면 별도 이벤트가 없어 uiCoin이 갱신되지 않고 예전 값(주로 0)에
        // 머무른다. 이 경우 이후 첫 실제 획득 시 SetNumberAnimated가 그 간극만큼 통째로 애니메이션을
        // 재생해버리므로, 여기서 매번 스냅으로 최신값을 맞춰둔다.
        CharactersMoneyChanged();
    }

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        UpdateSlots(_items, true);
    }

    /// <summary>
    /// _bUpdateCapacityBar가 false면 끝의 용량바 갱신만 건너뛴다. 용량바를 직후에 다시 갱신하는 호출부
    /// (InventoryShowEvent)가 두 번의 갱신을 UpdateCapacityTwice 한 번으로 합쳐 처리하기 위한 것이다.
    /// 반환값은 용량바 갱신 지점까지 도달했는지(앞의 null 검사로 빠지지 않았는지)다.
    /// </summary>
    private bool UpdateSlots(IReadOnlyList<IInventorySlot> _items, bool _bUpdateCapacityBar)
    {
        if (null == _items || null == inventory)
            return false;

        int _itemCount = inventory.currentSlotCnt;
        int _maxSlots = inventorySlots.Count;

        int currentLogCount = 0;

        for (int _i = 0; _maxSlots > _i; ++_i)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            IInventorySlot _item = _items.Count > _i ? _items[_i] : null;
            
            if (_itemCount > _i && null != _item && null != _item.itemData && ItemType.Log == _item.itemData.itemType)
            {
                currentLogCount += _item.count;
            }
            
            if (null == _slot)
                continue;

            _slot.gameObject.SetActive(_itemCount > _i);
            _slot.UpdateBindSlotData(_item, inventory.maxItemCntPerSlot, false, false == isFirstDataBind);
        }

        if (false == isFirstDataBind)
        {
            if (currentLogCount > previousLogCount && false == IsOpening)
            {
                if (null != newAlertRedDot)
                    newAlertRedDot.Activate();
            }
            else if (0 >= currentLogCount)
            {
                if (null != newAlertRedDot)
                    newAlertRedDot.Deactivate();
            }
        }
        else
        {
            isFirstDataBind = false;
            if (0 >= currentLogCount && null != newAlertRedDot)
            {
                newAlertRedDot.Deactivate();
            }
        }

        previousLogCount = currentLogCount;

        if (true == _bUpdateCapacityBar)
            UpdateCapacityBar();

        if (true == IsOpening)
        {
            HandleLogSwapInfoChanged();
        }

        return true;
    }

    private void UpdateCapacityBar()
    {
        if (null == capacityBar || null == inventory)
            return;

        capacityBar.UpdateCapacity(inventory.currentItemCount, inventory.maxCapacity);
    }

    public void PlayCapacityFeedback()
    {
        FlushPendingItemAdded();

        if (null != capacityBar)
            capacityBar.PlayFeedbackAnimation();
    }

    public void PlayCapacityRemoveFeedback()
    {
        FlushPendingItemAdded();

        if (null != capacityBar)
            capacityBar.PlayRemoveFeedbackAnimation();
    }

    private void InitInventoryPopup()
    {
        if (null == uiPopupPrefab)
            return;

        GameObject _popupObj = Instantiate(uiPopupPrefab, transform.parent);
        invPopup = _popupObj.GetComponent<UI_InventoryPopup>();

        if (null != invPopup)
        {
            invPopup.Initialize();
            invPopup.gameObject.SetActive(false);
        }

        if (null != iconsRoot)
            iconsRoot.SetAsLastSibling();
    }

    private void UpdateIconsPosition()
    {
        if (null != iconsRoot && null != iconsAnchor)
        {
            // 이미 그 자리면 대입하지 않는다(대입은 변환 갱신 알림을 일으킨다). 비교는 iconsRoot의 현재 값 기준이다.
            Vector3 _anchorPos = iconsAnchor.position;
            Vector3 _rootPos = iconsRoot.position;

            if (_rootPos.x != _anchorPos.x || _rootPos.y != _anchorPos.y || _rootPos.z != _anchorPos.z)
                iconsRoot.position = _anchorPos;
        }
    }

    private void HandleExitPopup()
    {
        if (null != invPopup)
            invPopup.OnHide();
    }

    private void RefreshLocalizedTexts()
    {
        if (null != locManager && null != openText)
        {
            openText.text = locManager.GetText("Open");
        }
    }

    private void InitCoins()
    {
        if (null != uiCoin) 
            uiCoin.Initialize();
    }

    private void InitBackpack()
    {
        if (null != uiBackpack)
            uiBackpack.Initialize();
    }

    private void InitCapacityBar()
    {
        if (null != capacityBar)
            capacityBar.Initialize();
    }

    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        if (null == moneyData)
            return;

        if (MoneyType.Coin == _moneyType)
            uiCoin?.SetNumberAnimated(moneyData.money);
    }

    public void CharactersMoneyChanged()
    {
        if (null == moneyData)
            return;

        uiCoin?.SetNumber(moneyData.money);
    }

    /// <summary>[로그라이크 전용] 코인 HUD를 켜고 끈다. 본편에서는 호출되지 않는다.</summary>
    public void SetCoinVisibleForRun(bool _bVisible)
    {
        if (null != uiCoin)
            uiCoin.gameObject.SetActive(_bVisible);
    }

    /// <summary>
    /// [로그라이크 전용] 인벤토리 HUD(가방·용량 게이지·열기 키 아이콘)를 켜고 끈다. 로그라이크는 아이템 드롭·인벤토리를 쓰지 않아
    /// 표시하지 않는다. 코인 HUD도 런 동안 SetCoinVisibleForRun으로 따로 숨긴다. 본편에서는 호출되지 않는다.
    /// </summary>
    public void SetInventoryVisibleForRun(bool _bVisible)
    {
        FlushPendingItemAdded();

        if (null != uiBackpack)
            uiBackpack.gameObject.SetActive(_bVisible);

        if (null != capacityBar)
            capacityBar.gameObject.SetActive(_bVisible);

        if (null != iconsRoot)
            iconsRoot.gameObject.SetActive(_bVisible);
    }

    /// <summary>
    /// 특정 인벤토리 슬롯의 스마트 스왑 아웃라인을 켜고, 공유 인디케이터를 대상 슬롯 위치로 이동시켜 활성화합니다.
    /// </summary>
    public void SetSwapCandidateSlot(int _slotIndex, bool _active, bool _immediate = false)
    {
        if (0 > _slotIndex || inventorySlots.Count <= _slotIndex)
            return;

        UI_InventorySlot _slot = inventorySlots[_slotIndex];
        if (null == _slot)
            return;

        // 꺼져 있는 슬롯은 레이아웃이 잡히지 않아 좌표가 낡아 있다. 그 위에 인디케이터를 붙이면
        // 허공을 가리키게 되므로, 켜 달라는 요청이어도 받지 않고 전부 끈다. 지금 배선으로는
        // 여기에 걸릴 일이 없으니(SelectVictim이 currentSlotCnt 안에서만 고른다), 걸렸다면
        // 교체 판정과 슬롯 뷰가 어긋났다는 뜻이라 흔적을 남긴다.
        if (true == _active && false == _slot.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[UI_Inventory] 교체 대상 슬롯({_slotIndex})이 꺼져 있어 인디케이터를 띄우지 않습니다. 교체 판정과 슬롯 뷰가 어긋났습니다.");
            ClearAllSwapIndicators(_immediate);
            return;
        }

        if (true == _active)
        {
            // 이전에 켜져 있던 슬롯이 있다면 아웃라인 해제
            if (-1 != currentSwapSlotIndex && _slotIndex != currentSwapSlotIndex && inventorySlots.Count > currentSwapSlotIndex)
            {
                UI_InventorySlot _prevSlot = inventorySlots[currentSwapSlotIndex];
                if (null != _prevSlot)
                {
                    _prevSlot.SetSwapIndicator(false, _immediate);
                }
            }

            bool _isSameSlot = (currentSwapSlotIndex == _slotIndex);
            currentSwapSlotIndex = _slotIndex;
            _slot.SetSwapIndicator(true, _immediate);

            if (null != sharedSwapIndicator)
            {
                Vector3 _targetWorldPos = _slot.transform.TransformPoint(indicatorOffset);
                if (true == _isSameSlot && true == sharedSwapIndicator.IsActiveAndShowing)
                {
                    // 같은 슬롯을 계속 가리키는 중이면 위치만 맞춘다. 여기서 SetAsLastSibling까지
                    // 부르면 제안이 갱신될 때마다 캔버스 계층이 더럽혀져 매번 리빌드가 걸린다.
                    sharedSwapIndicator.UpdateTargetPosition(_targetWorldPos);
                }
                else
                {
                    sharedSwapIndicator.transform.SetAsLastSibling();
                    sharedSwapIndicator.Show(_targetWorldPos);
                }
            }
        }
        else
        {
            if (_slotIndex == currentSwapSlotIndex)
            {
                currentSwapSlotIndex = -1;
            }

            _slot.SetSwapIndicator(false, _immediate);

            if (null != sharedSwapIndicator)
            {
                if (true == _immediate)
                {
                    sharedSwapIndicator.HideImmediate();
                }
                else
                {
                    sharedSwapIndicator.Hide();
                }
            }
        }
    }

    /// <summary>
    /// 모든 인벤토리 슬롯의 스마트 스왑 아웃라인 및 공유 인디케이터를 해제합니다.
    /// </summary>
    public void ClearAllSwapIndicators(bool _immediate = false)
    {
        currentSwapSlotIndex = -1;

        int _count = inventorySlots.Count;
        for (int _i = 0; _i < _count; ++_i)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            if (null != _slot)
            {
                _slot.SetSwapIndicator(false, _immediate);
            }
        }

        if (null != sharedSwapIndicator)
        {
            if (true == _immediate)
            {
                sharedSwapIndicator.HideImmediate();
            }
            else
            {
                sharedSwapIndicator.Hide();
            }
        }
    }


    /// <summary>
    /// 아이템 획득 갱신(InventoryShowEvent + PlayCapacityFeedback)을 원목 갱신 구간이 끝날 때까지 미룰 수 있으면 미루고 true를
    /// 돌려준다. false면 호출부가 예전대로 바로 갱신한다.
    ///
    /// 흡입 반경이 넓으면 한 프레임에 원목 수십 개가 들어와 같은 갱신이 그 수만큼 되풀이된다. 이미 묶여 있는 슬롯에 개수만
    /// 늘어난 획득은 슬롯 자신이 SlotUpdatedEvent로 먼저 맞춰 두므로 전체 갱신의 슬롯 처리는 아무것도 바꾸지 않는다. 그런 슬롯은
    /// 건너뛰고, 남는 일(빨간 점, 원목 개수 기록, 용량바, 획득 연출)은 구간 끝에서 한 번에 한다. 한 프레임 안이라 트윈이 그 사이에
    /// 진행되지 않으므로 결과는 하나씩 처리한 것과 같다. 새 슬롯에 처음 들어온 경우처럼 고쳐 그려야 하는 슬롯은 그 슬롯만
    /// 그 자리에서 예전과 같은 처리를 하고, 가방이 열려 있을 때의 교체 표시 갱신도 그 자리에서 한다(이미 전부 꺼져 있으면 생략).
    /// 다음 경우에는 미루지 않는다:
    ///   - 구간 밖, 첫 데이터 바인딩
    ///   - 원목이 하나도 없음(빨간 점을 끄는 경로), 용량이 범위를 벗어나거나 미뤄 둔 값보다 줄어듦
    /// </summary>
    public bool TryDeferItemAdded()
    {
        if (false == ItemPickupBatch.IsActive)
            return false;

        if (null == inventory || null == capacityBar || true == isFirstDataBind)
            return false;

        IReadOnlyList<IInventorySlot> _items = inventory.inventorySlots;
        if (null == _items)
            return false;

        int _current = inventory.currentItemCount;
        int _max = inventory.maxCapacity;
        if (0 >= _max || 0 > _current || _max < _current)
            return false;

        // 용량바는 값이 늘기만 하는 동안에는 마지막 값으로 한 번 갱신한 것과 하나씩 갱신한 것이 같다.
        if (0 < pendingItemAddedCount && (_max != pendingMaxCapacity || _current < pendingItemCount))
            return false;

        int _itemCount = inventory.currentSlotCnt;
        int _maxPerSlot = inventory.maxItemCntPerSlot;
        int _maxSlots = inventorySlots.Count;

        // 원목 개수(전체 갱신이 세는 것과 같은 범위)와 빨간 점 판정. 슬롯을 건드리기 전에, 미루지 못하는 경우를 먼저 가린다.
        int _logCount = 0;
        int _countable = Mathf.Min(_itemCount, Mathf.Min(_items.Count, _maxSlots));

        for (int _i = 0; _countable > _i; ++_i)
        {
            IInventorySlot _item = _items[_i];

            if (null != _item && null != _item.itemData && ItemType.Log == _item.itemData.itemType)
                _logCount += _item.count;
        }

        // 가방이 열려 있는 동안에는 빨간 점을 켜지 않는다(전체 갱신과 같은 조건).
        int _prevLogCount = 0 < pendingItemAddedCount ? pendingLogCount : previousLogCount;
        bool _bActivateRedDot = _logCount > _prevLogCount && false == IsOpening;

        if (false == _bActivateRedDot && 0 >= _logCount)
            return false;

        // 슬롯: 전체 갱신이 해도 달라지지 않는 슬롯은 건너뛰고, 달라지는 슬롯(새 슬롯에 처음 들어온 경우 등)만 전체 갱신이
        // 하던 처리를 그대로 지금 한다. 슬롯끼리는 서로 영향을 주지 않으므로 전부 처리한 것과 결과가 같다.
        bool _bAnySwapOutline = false;

        for (int _i = 0; _maxSlots > _i; ++_i)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            if (null == _slot)
                continue;

            IInventorySlot _item = _items.Count > _i ? _items[_i] : null;
            bool _bActive = _itemCount > _i;

            if (false == _slot.IsBindUpToDate(_item, _maxPerSlot, _bActive))
            {
                _slot.gameObject.SetActive(_bActive);
                _slot.UpdateBindSlotData(_item, _maxPerSlot, false, true);
            }

            if (false == _slot.IsSwapOutlineOff)
                _bAnySwapOutline = true;
        }

        // 가방이 열려 있으면 전체 갱신이 끝에서 교체 표시를 다시 맞춘다. 슬롯 테두리와 표시기만 건드려 미뤄 둔 것과 겹치지 않으므로
        // 지금 한다. 교체 대상이 없어 "전부 끄기"만 하는 경우에 이미 전부 꺼져 있으면(가득 찬 가방이 열려 있는 흔한 상태) 생략한다.
        if (true == IsOpening && (true == _bAnySwapOutline || false == IsSwapDisplayAlreadyCleared()))
            HandleLogSwapInfoChanged();

        ++pendingItemAddedCount;

        if (true == _bActivateRedDot && 2 > pendingRedDotActivateCount)
            ++pendingRedDotActivateCount;

        pendingLogCount = _logCount;
        pendingItemCount = _current;
        pendingMaxCapacity = _max;

        return true;
    }

    /// <summary>
    /// 가방이 열려 있을 때의 HandleLogSwapInfoChanged가 지금 아무것도 바꾸지 않는 경우인지. 슬롯 테두리가 전부 꺼져 있다는 것은
    /// 호출부가 확인한 뒤에 부른다. 표시를 켜는 경로는 여기서 다루지 않는다(false → 호출부가 그대로 실행).
    /// </summary>
    private bool IsSwapDisplayAlreadyCleared()
    {
        int _slotIndex = -1;

        if (true == IsOutgoingSwapReady)
            _slotIndex = OutgoingSlotIndex;
        else if (true == IsLogSwapReady)
            _slotIndex = logSwapInfo.slotIndex;

        if (0 <= _slotIndex)
        {
            // 열리는 연출 중에는 원래도 아무것도 하지 않고, 그 밖에 대상 슬롯이 있으면 표시를 켜는 경로다.
            if (true == isOpenAnimated)
                return true;

            if (inventorySlots.Count > _slotIndex)
                return false;
        }

        // ClearAllSwapIndicators(false)가 하는 일: 대상 기록 지우기, 슬롯 테두리 끄기(호출부가 확인), 표시기 숨기기.
        if (-1 != currentSwapSlotIndex)
            return false;

        return null == sharedSwapIndicator || true == sharedSwapIndicator.IsHideRedundant;
    }

    /// <summary>
    /// TryDeferItemAdded가 미뤄 둔 획득 갱신을 처리한다. 원목 갱신 구간이 끝난 직후(ItemPickupBatch.Ended)에 불리고,
    /// 같은 상태를 읽거나 바꾸는 진입점들도 들어가기 전에 먼저 부른다(순서 유지).
    /// 하는 일과 순서는 미루지 않았을 때 획득 한 번이 하던 것과 같다: 빨간 점 → 원목 개수 기록 → 용량바 → 획득 연출.
    /// </summary>
    private void FlushPendingItemAdded()
    {
        if (0 >= pendingItemAddedCount)
            return;

        int _activateCount = pendingRedDotActivateCount;
        pendingItemAddedCount = 0;
        pendingRedDotActivateCount = 0;

        if (null != newAlertRedDot)
        {
            // 꺼져 있던 점은 첫 번째에 등장 연출, 두 번째부터 뽀잉 연출로 바뀐다. 세 번째부터는 두 번째를 끊고 똑같이 다시 만드는 것이라 생략한다.
            for (int _i = 0; _activateCount > _i; ++_i)
                newAlertRedDot.Activate();
        }

        previousLogCount = pendingLogCount;

        if (null != capacityBar)
        {
            capacityBar.transform.localScale = Vector3.one;
            capacityBar.UpdateCapacityTwice(pendingItemCount, pendingMaxCapacity);
            capacityBar.PlayFeedbackAnimation();
        }
    }

    public void InventoryShowEvent()
    {
        FlushPendingItemAdded();

        // 예전에는 슬롯 갱신 끝에서 한 번, 여기서 한 번, 같은 값으로 용량바를 두 번 갱신했다. 결과는 그대로 두고
        // 두 번째 갱신 자리에서 한 번에 처리한다(UI_InventoryCapacityBar.UpdateCapacityTwice 참고). 사이에 끼는
        // 교체 표시 갱신과 크기 초기화는 용량바의 게이지·트윈을 건드리지 않으므로 순서를 옮겨도 결과가 같다.
        bool _bSlotsUpdated = false;
        if (null != inventory)
            _bSlotsUpdated = UpdateSlots(inventory.inventorySlots, false);

        if (null != capacityBar)
            capacityBar.transform.localScale = Vector3.one;

        if (true == _bSlotsUpdated)
        {
            if (null != capacityBar && null != inventory)
                capacityBar.UpdateCapacityTwice(inventory.currentItemCount, inventory.maxCapacity);
        }
        else
        {
            UpdateCapacityBar();
        }
    }

    public void MapChanged(MapType _currentMap)
    {
        FlushPendingItemAdded();

        CurrentMapType = _currentMap;

        CloseInvAndAnimSkip();
    }

    private void CloseInvAndAnimSkip()
    {
        if (null == omp)
            return;

        HandleExitPopup();
        IsOpening = isOpenAnimated = false;
        ClearAllSwapIndicators(true);

        omp.PlayBackward(backpackTag, bReset: true, _skip: true);
        omp.PlayBackward(coinsTag, bReset: true, _skip: true);
        omp.PlayBackward(popupTag, bReset: true, _skip: true);
    }

    /// <summary>
    /// 교체 대상 슬롯 정보를 갱신합니다(UIView_Popup이 호출). 내용이 실제로 달라졌을 때만
    /// LogSwapInfoChangedEvent를 발생시킵니다.
    /// </summary>
    public void SetLogSwapInfo(in LogSwapSlotInfo _info, in LogSwapSlotInfo _containerInfo, ELogSwapTarget _activeTarget)
    {
        bool _bChanged = false == LogSwapSlotInfo.IsSame(in _info, in logSwapInfo)
            || false == LogSwapSlotInfo.IsSame(in _containerInfo, in outgoingSwapInfo)
            || _activeTarget != activeLogSwapTarget;

        logSwapInfo = _info;
        outgoingSwapInfo = _containerInfo;
        activeLogSwapTarget = _activeTarget;

        if (true == _bChanged)
            LogSwapInfoChangedEvent?.Invoke();
    }

    /// <summary>교체로 슬롯 하나가 버려졌습니다(UIView_Popup이 호출).</summary>
    public void LogSwapExecuted(in LogSwapSlotInfo _info)
    {
        LogSwapExecutedEvent?.Invoke(_info);
    }

    private void HandleLogSwapInfoChanged()
    {
        if (false == IsOpening)
        {
            ClearAllSwapIndicators(false);
            return;
        }

        // 가방이 강조할 칸은 지금이 어느 쪽 교체인지에 따라 뜻이 다르다.
        //   - 인벤토리 교체: logSwapInfo.slotIndex     = "이 칸이 바닥으로 버려진다"
        //   - 운반 상자 교체: OutgoingSlotIndex        = "이 칸이 상자로 넘어간다"(버려지는 건 상자 슬롯)
        // activeTarget이 한쪽만 가리키므로 두 조건이 동시에 참이 되는 일은 없다. 상자 쪽이 우선이라
        // 같은 순서로 따진다. 표시는 양쪽 다 같은 아웃라인이라, 무엇이 일어나는지는 함께 뜨는
        // 상자 UI가 구분해 준다.
        int _slotIndex = -1;

        if (true == IsOutgoingSwapReady)
        {
            // 상자로 넘어갈 가방 슬롯을 아직 못 고른 상태면(-1) 강조할 칸이 없다.
            _slotIndex = OutgoingSlotIndex;
        }
        else if (true == IsLogSwapReady)
        {
            _slotIndex = logSwapInfo.slotIndex;
        }

        if (0 > _slotIndex)
        {
            ClearAllSwapIndicators(false);
            return;
        }

        if (true == isOpenAnimated)
            return;

        if (inventorySlots.Count > _slotIndex)
        {
            SetSwapCandidateSlot(_slotIndex, true);
        }
        else
        {
            ClearAllSwapIndicators(false);
        }
    }

    private void HandleLogSwapExecuted(LogSwapSlotInfo _info)
    {
        HandleLogSwapInfoChanged();
    }

    public void OnHide()
    {
        FlushPendingItemAdded();

        ClearAllSwapIndicators(false);

        bool _wasOpening = IsOpening || isOpenAnimated;
        IsOpening = isOpenAnimated = false;

        if (true == _wasOpening)
            Sound.PlayUI(SoundID.HUDBackpackClose);

        if (null != omp)
        {
            omp.PlayBackward(backpackTag, bReset: true);
            omp.PlayBackward(popupTag, bReset: true);
        }

        uiBackpack?.CloseInventory();
        HandleExitPopup();

        if (MapType.Town == CurrentMapType)
            return;

        if (null != omp)
        {
            omp.PlayBackward(coinsTag, bReset: true);
        }
    }

    public void OnShow()
    {
        FlushPendingItemAdded();

        if (true == isOpenAnimated)
            return;

        IsOpening = isOpenAnimated = true;
        Sound.PlayUI(SoundID.HUDBackpackOpen);

        if (null != newAlertRedDot)
        {
            newAlertRedDot.Deactivate();
        }

        if (null != omp)
        {
            omp.Play(backpackTag, bReset: true, _onComplete: OnShowCompletedAnimation);
            omp.Play(popupTag, bReset: true);
        }
        else
        {
            OnShowCompletedAnimation();
        }

        uiBackpack?.OpenInventory();
        InventoryShowEvent();

        if (MapType.Town == CurrentMapType)
            return;

        if (null != omp)
        {
            omp.Play(coinsTag, bReset: true);
        }
    }

    private void OnShowCompletedAnimation()
    {
        isOpenAnimated = false;

        Canvas.ForceUpdateCanvases();
        HandleLogSwapInfoChanged();
    }

    /// <summary>
    /// 파괴 전용 정리입니다. 호출부는 이 클래스의 OnDestroy와 UIView_Popup.OnDestroy 둘뿐이며,
    /// 둘 다 파괴 경로라 "정리 후에도 계속 살아서 동작해야 하는" 경우가 없습니다.
    /// (두 번 불려도 -= 와 Clear()가 모두 멱등이라 문제되지 않습니다)
    /// </summary>
    public void Release()
    {
        ItemPickupBatch.Ended -= FlushPendingItemAdded;
        pendingItemAddedCount = 0;
        pendingRedDotActivateCount = 0;

        LogSwapInfoChangedEvent -= HandleLogSwapInfoChanged;
        LogSwapExecutedEvent -= HandleLogSwapExecuted;
        ClearAllSwapIndicators(true);

        // LocalizationManager는 BootStrap의 자식이라 앱이 켜져 있는 내내 살아남는다.
        // 여기서 반납하지 않으면 이 뷰가 파괴돼도 구독이 남아, 메인 메뉴를 왕복할 때마다
        // 죽은 구독자가 하나씩 쌓이고 그만큼의 오브젝트 그래프가 회수되지 않는다.
        // (RefreshLocalizedTexts가 전부 null 가드라 증상은 없지만, 같은 이벤트를 쓰는
        //  나머지 10개 뷰는 전부 파괴 시 반납하고 있어 여기만 예외였다)
        if (null != locManager)
        {
            locManager.OnLanguageChanged -= RefreshLocalizedTexts;
        }

        for (int _i = 0; inventorySlots.Count > _i; _i++)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            
            if (null == _slot)
                continue;

            _slot.exitSlot -= inventoryUnHoverEvent;
        }
        
        inventorySlots.Clear();
    }

    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnEnable()
    {
        UpdateIconsPosition();
        if (null != iconsRoot)
            iconsRoot.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        FlushPendingItemAdded();

        UpdateIconsPosition();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateIconsPosition();
    }
#endif

    private void OnDestroy()
    {
        Release();
    }
}
