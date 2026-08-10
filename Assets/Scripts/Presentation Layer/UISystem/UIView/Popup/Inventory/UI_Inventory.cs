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
    // [SerializeField] private UI_Homing uiHoming;
    [SerializeField] private CurrencyCounterHUD uiCoin;
    [SerializeField] private UI_Backpack uiBackpack;
    // [SerializeField] private HUD_NotificationBadge notificationBadge;
    [SerializeField] private UI_InventoryCapacityBar capacityBar;
    
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
    [SerializeField] private List<UI_InventorySlot> inventorySlots = new List<UI_InventorySlot>(32);

    // //내부 의존성
    private const int defaultPopupCap = 12;
    private const string backpackTag = "Backpack";
    private const string coinsTag = "Coins";
    private const string popupTag = "Popup";
    // private const string homingTag = "Homing";

    private IInventory inventory;
    private IMoneyData moneyData;
    private UI_InventoryPopup invPopup;
    private LocalizationManager locManager;



    public MapType CurrentMapType { get; set; } = MapType.Town;
    public bool IsOpening { get; private set; } = false;

    public Action inventoryHoverEvent;
    public Action inventoryUnHoverEvent;

    private bool isOpenAnimated = false;

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

        // InitHoning(_clickedHomingEvent);
        InitInventoryPopup();
        InitCoins();
        InitBackpack();
        // InitNotificationBadge();
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

        // 기존 슬롯이 있다면 재사용하기 위해 Clear() 대신 상태만 관리하도록 수정 가능하나, 
        // 여기서는 안전하게 리스트만 유지하고 부족분만 생성하는 방식으로 개선
        UpdateMaxSlotCount(SYSTEM_VAR.MAX_INVENTORY_CNT);
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

        if (0 >= _needCount)
            return;

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

            inventorySlots.Add(_slot);
        }
    }

    public void SendDeleteItem(IInventorySlot _inData)
    {
        if (null == inventory)
            return;

        sendDeleteItemEvent?.Invoke(_inData);
        UpdateSlots(inventory.inventorySlots);
    }

    public void Refresh()
    {
        if (null != inventory)
            UpdateSlots(inventory.inventorySlots);  
    } 

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        if (null == _items || null == inventory)
            return;

        int _itemCount = inventory.currentSlotCnt;
        int _maxSlots = inventorySlots.Count;

        for (int _i = 0; _i < _maxSlots; ++_i)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            IInventorySlot _item = _items[_i];
            
            if (null == _slot)
                continue;

            _slot.gameObject.SetActive(_i < _itemCount);
            _slot.UpdateBindSlotData(_item, inventory.maxItemCntPerSlot);
        }

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

    // private void InitHoning(Action _clickedHomingEvent)
    // {
    //     if (null == uiHoming)
    //         return;
    //
    //     uiHoming.Initialize();
    //     uiHoming.clickedEvent = _clickedHomingEvent;
    // }

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

    // private void InitNotificationBadge()
    // {
    //     if (null != notificationBadge)
    //         notificationBadge.Initialize();
    // }

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

        // if (null != uiHoming)
        //     uiHoming.currentMapType = _currentMap;

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
        // omp.PlayBackward(homingTag, bReset: true, _skip: true);
    }

    // public void UpdateNotification()
    // {
    //     if (null == notificationBadge)
    //         return;
    //
    //     notificationBadge.UpdateAndInteraction(!IsOpening ? ++hideAccCount : 0); 
    // }

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
            // omp.PlayBackward(homingTag, bReset: true);
            omp.PlayBackward(coinsTag, bReset: true);
        }
    }

    public void OnShow()
    {
        if (true == isOpenAnimated)
            return;

        IsOpening = isOpenAnimated = true;
        Sound.PlayUI(SoundID.HUDBackpackOpen);
        // hideAccCount = 0;

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
            // omp.Play(homingTag, bReset: true);
            omp.Play(coinsTag, bReset: true);
        }
    }

    private void OnShowCompletedAnimation()
    {
        isOpenAnimated = false;
    }

    public void Release()
    {
        for (int _i = 0; _i < inventorySlots.Count; _i++)
        {
            UI_InventorySlot _slot = inventorySlots[_i];
            
            if (null == _slot)
                continue;

            _slot.exitSlot -= inventoryUnHoverEvent;
        }
        
        inventorySlots.Clear();
    }

    // public void ClearNotification() => notificationBadge?.UpdateAndInteraction(0);


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDestroy()
    {
        Release();
    }
}
