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
    // //이벤트
    public event Action<IInventorySlot> sendDeleteItemEvent;

    // //외부 의존성
    [Header("Binding Obj")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private GameObject invBackground;
    [SerializeField] private RectTransform itemsRoot;
    [SerializeField] private CurrencyCounterHUD uiPrism;
    [SerializeField] private CurrencyCounterHUD uiDiamond;
    [SerializeField] private CurrencyCounterHUD uiGold;
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

    // //내부 의존성
    private const int defaultPopupCap = 12;
    private const string backpackTag = "Backpack";
    private const string coinsTag = "Coins";
    private const string popupTag = "Popup";

    private IInventory inventory;
    private IMoneyData moneyData;
    private UI_InventoryPopup invPopup;
    private LocalizationManager locManager;

    public MapType CurrentMapType { get; set; } = MapType.Town;
    public bool IsOpening { get; private set; } = false;

    public Action inventoryHoverEvent;
    public Action inventoryUnHoverEvent;

    private bool isOpenAnimated = false;
    private int previousLogCount = 0;
    private bool isFirstDataBind = true;

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
            for (int i = 0; i < keyboardImages.Length; i++)
            {
                if (null != keyboardImages[i]) keyboardImages[i].Initialize(_inputManager);
            }
        }

        inventoryHoverEvent = _hoverEvent;
        inventoryUnHoverEvent = _unHoverEvent;

        if (null != uiSlotPrefab && null != invBackground)
        {
            int _needPrewarm = maxSlotPrewarmCount - inventorySlots.Count;
            for (int i = 0; i < _needPrewarm; i++)
            {
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
            }
        }
    }

    public void BindData(IInventory _inventory, IMoneyData _moneyData)
    {
        inventory = _inventory;
        moneyData = _moneyData;

        if (null != uiPrism)
            uiPrism.SetMoneyType(MoneyType.PrismOre);

        if (null != uiDiamond)
            uiDiamond.SetMoneyType(MoneyType.DiamondOre);

        if (null != uiGold)
            uiGold.SetMoneyType(MoneyType.GoldOre);

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
            for (int _i = 0; _i < _needCount; _i++)
            {
                GameObject _slotObj = Instantiate(uiSlotPrefab, invBackground.transform);
                UI_InventorySlot _slot = _slotObj.GetComponent<UI_InventorySlot>();

                if (null == _slot)
                    continue;

                _slot.Initialize();
                _slot.SetLayer(uiSlotLayerName);

                _slot.exitSlot -= inventoryUnHoverEvent;
                _slot.exitSlot += inventoryUnHoverEvent;
                _slot.gameObject.SetActive(false);

                inventorySlots.Add(_slot);
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

        for (int _i = 0; _i < _maxSlots; ++_i)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            IInventorySlot _item = _i < _items.Count ? _items[_i] : null;
            
            if (_i < _itemCount && null != _item && null != _item.itemData && ItemType.Log == _item.itemData.itemType)
            {
                currentLogCount += _item.count;
            }
            
            if (null == _slot)
                continue;

            _slot.gameObject.SetActive(_i < _itemCount);
            _slot.UpdateBindSlotData(_item, inventory.maxItemCntPerSlot);
        }

        if (false == isFirstDataBind)
        {
            if (previousLogCount < currentLogCount && false == IsOpening)
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

    private void UpdateIconsPosition()
    {
        if (null != iconsRoot && null != iconsAnchor)
        {
            iconsRoot.position = iconsAnchor.position;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateIconsPosition();
    }
#endif

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
        InitCurrencyHUD(uiPrism, MoneyType.PrismOre, true, 0);
        InitCurrencyHUD(uiDiamond, MoneyType.DiamondOre, true, 1);
        InitCurrencyHUD(uiGold, MoneyType.GoldOre, true, 2);
        InitCurrencyHUD(uiCoin, MoneyType.Coin, false, 3);
    }

    private void InitCurrencyHUD(CurrencyCounterHUD _hud, MoneyType _moneyType, bool _dimWhenZero, int _siblingIndex)
    {
        if (null == _hud)
            return;

        _hud.Initialize();
        _hud.SetMoneyType(_moneyType);
        _hud.SetDimWhenZero(_dimWhenZero, 0.35f);
        _hud.transform.SetSiblingIndex(_siblingIndex);

        if (_dimWhenZero)
        {
            _hud.gameObject.SetActive(false);
        }
        else
        {
            _hud.gameObject.SetActive(true);
        }
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

        switch (_moneyType)
        {
            case MoneyType.Coin:
                uiCoin?.SetNumberAnimated(moneyData.money);
                break;
            case MoneyType.GoldOre:
                DiscoverAndShowCurrency(uiGold);
                uiGold?.SetNumberAnimated(moneyData.goldOre);
                break;
            case MoneyType.DiamondOre:
                DiscoverAndShowCurrency(uiDiamond);
                uiDiamond?.SetNumberAnimated(moneyData.diamondOre);
                break;
            case MoneyType.PrismOre:
                DiscoverAndShowCurrency(uiPrism);
                uiPrism?.SetNumberAnimated(moneyData.prismOre);
                break;
        }
    }

    #region 원석 주머니 데이터 (그리는 코드는 아직 없음)

    // 아래 셋은 "12 / 30" 같은 주머니 표시를 붙일 수 있도록 데이터만 열어둔 것이다.
    // 값을 캐싱하지 않고 그때그때 읽으므로 언제 불러도 최신이다. 갱신 시점은 기존 경로를 그대로 쓰면 된다.
    //   원석을 주웠을 때        CharacterEarnMoney(MoneyType)
    //   용광로에 넣어 줄었을 때  CharactersMoneyChanged()
    //   특성으로 한도가 커졌을 때 Refresh() (InventorySpecChangedSignal 경유)

    /// <summary>주머니 한도. 황금/다이아/프리즘을 <b>합친</b> 총량의 상한이다.</summary>
    public long GemOrePouchCapacity => null != moneyData ? moneyData.GemOrePouchCapacity : 0L;

    /// <summary>지금 주머니에 든 원석 총량(세 종류 합). 한도와 함께 "12 / 30"처럼 쓰면 된다.</summary>
    public long TotalGemOre => null != moneyData ? moneyData.TotalGemOre : 0L;

    /// <summary>
    /// 주머니가 가득 찼는지. 가득 차면 바닥의 원석을 아예 줍지 않으므로, 경고 표시를 띄울 때 쓰면 된다.
    /// 한도보다 많이 들고 있는 상태(주머니 이전 세이브 등)에서도 true다.
    /// </summary>
    public bool IsGemOrePouchFull => null != moneyData && moneyData.TotalGemOre >= moneyData.GemOrePouchCapacity;

    #endregion

    public void CharactersMoneyChanged()
    {
        if (null == moneyData)
            return;

        uiCoin?.SetNumber(moneyData.money);

        UpdateCurrencyState(uiPrism, MoneyType.PrismOre, moneyData.prismOre);
        UpdateCurrencyState(uiDiamond, MoneyType.DiamondOre, moneyData.diamondOre);
        UpdateCurrencyState(uiGold, MoneyType.GoldOre, moneyData.goldOre);
    }

    /// <summary>
    /// 방금 얻은 재화의 칸을 즉시 띄운다. 획득 이력은 InventoryManager가 이미 기록한 뒤에
    /// 이 호출이 오므로(UnitSystem.GemOreAcquired), 여기서는 보이기만 하면 된다.
    /// </summary>
    private void DiscoverAndShowCurrency(CurrencyCounterHUD _hud)
    {
        if (null == _hud)
            return;

        if (true == _hud.gameObject.activeSelf)
            return;

        _hud.gameObject.SetActive(true);
        RebuildItemsLayout();
    }

    /// <summary>
    /// 한 번이라도 얻은 적이 있는 재화만 보여준다.
    ///
    /// 판정은 세이브에 저장되는 획득 이력(IMoneyData.HasEverAcquired)을 그대로 따른다.
    /// 예전처럼 보유량으로 판정하면 원석을 전부 용광로에 넣은 순간 칸이 다시 숨겨지고,
    /// 게임을 껐다 켜면 발견 사실 자체가 사라진다.
    /// </summary>
    private void UpdateCurrencyState(CurrencyCounterHUD _hud, MoneyType _moneyType, long _amount)
    {
        if (null == _hud)
            return;

        bool _bDiscovered = null != moneyData && moneyData.HasEverAcquired(_moneyType);

        if (_bDiscovered != _hud.gameObject.activeSelf)
        {
            _hud.gameObject.SetActive(_bDiscovered);
            RebuildItemsLayout();
        }

        _hud.SetNumber(_amount);
    }

    private void RebuildItemsLayout()
    {
        if (null != itemsRoot)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);
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
        IsOpening = false;

        omp.PlayBackward(backpackTag, bReset: true, _skip: true);
        omp.PlayBackward(coinsTag, bReset: true, _skip: true);
        omp.PlayBackward(popupTag, bReset: true, _skip: true);
    }

    public void OnHide()
    {
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
    }

    /// <summary>
    /// 파괴 전용 정리입니다. 호출부는 이 클래스의 OnDestroy와 UIView_Popup.OnDestroy 둘뿐이며,
    /// 둘 다 파괴 경로라 "정리 후에도 계속 살아서 동작해야 하는" 경우가 없습니다.
    /// (두 번 불려도 -= 와 Clear()가 모두 멱등이라 문제되지 않습니다)
    /// </summary>
    public void Release()
    {
        // LocalizationManager는 BootStrap의 자식이라 앱이 켜져 있는 내내 살아남는다.
        // 여기서 반납하지 않으면 이 뷰가 파괴돼도 구독이 남아, 메인 메뉴를 왕복할 때마다
        // 죽은 구독자가 하나씩 쌓이고 그만큼의 오브젝트 그래프가 회수되지 않는다.
        // (RefreshLocalizedTexts가 전부 null 가드라 증상은 없지만, 같은 이벤트를 쓰는
        //  나머지 10개 뷰는 전부 파괴 시 반납하고 있어 여기만 예외였다)
        if (null != locManager)
        {
            locManager.OnLanguageChanged -= RefreshLocalizedTexts;
        }

        for (int _i = 0; _i < inventorySlots.Count; _i++)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            
            if (null == _slot)
                continue;

            _slot.exitSlot -= inventoryUnHoverEvent;
        }
        
        inventorySlots.Clear();
    }

    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDestroy()
    {
        Release();
    }
}
