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
        inventory = _inventory;
        moneyData = _moneyData;

        if (null != uiCoin)
            uiCoin.SetMoneyType(MoneyType.Coin);
            
        CharactersMoneyChanged();
    }

    public void UpdateMaxSlotCount(int _cnt)
    {
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
        if (null == inventory)
            return;

        sendDeleteItemEvent?.Invoke(_inData);
        UpdateSlots(inventory.inventorySlots);
    }

    public void Refresh() //저장 파일 로드할 때도 호출됨.
    {
        if (null != inventory)
            UpdateSlots(inventory.inventorySlots);

        // 세이브 로드로 money가 직접 대입되면 별도 이벤트가 없어 uiCoin이 갱신되지 않고 예전 값(주로 0)에
        // 머무른다. 이 경우 이후 첫 실제 획득 시 SetNumberAnimated가 그 간극만큼 통째로 애니메이션을
        // 재생해버리므로, 여기서 매번 스냅으로 최신값을 맞춰둔다.
        CharactersMoneyChanged();
    }

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        if (null == _items || null == inventory)
            return;

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
            _slot.UpdateBindSlotData(_item, inventory.maxItemCntPerSlot);
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

        UpdateCapacityBar();

        if (true == IsOpening)
        {
            HandleLogSwapInfoChanged();
        }
    }

    private void UpdateCapacityBar()
    {
        if (null == capacityBar || null == inventory)
            return;

        capacityBar.UpdateCapacity(inventory.currentItemCount, inventory.maxCapacity);
    }

    public void PlayCapacityFeedback()
    {
        if (null != capacityBar)
            capacityBar.PlayFeedbackAnimation();
    }

    public void PlayCapacityRemoveFeedback()
    {
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
            iconsRoot.position = iconsAnchor.position;
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


    public void InventoryShowEvent()
    {
        if (null != inventory)
            UpdateSlots(inventory.inventorySlots);

        if (null != capacityBar)
            capacityBar.transform.localScale = Vector3.one;

        UpdateCapacityBar();
    }

    public void MapChanged(MapType _currentMap)
    {
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
