using System;
using System.Collections.Generic;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
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
    [SerializeField] private UI_Homing uiHoming;
    [SerializeField] private CurrencyCounterHUD uiCoin;
    [SerializeField] private CurrencyCounterHUD uiSubCoin;
    [SerializeField] private UI_Backpack uiBackpack;
    [SerializeField] private UISelectionCursor selectionCursor;
    [SerializeField] private HUD_NotificationBadge notificationBadge;

    [Header("Prefabs")]
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private List<UI_InventorySlot> inventorySlots = new List<UI_InventorySlot>(32);
    [SerializeField] private float popupYOffset = 30.0f;

    // //내부 의존성
    private const int defaultPopupCap = 12;

    private IInventory inventory;
    private IMoneyData moneyData;
    private UI_InventoryPopup invPopup;
    private MapType prevMapType = MapType.Town;

    private int hideAccCount = 0;

    public MapType currentMapType { get; set; } = MapType.Town;
    public bool isOpening { get; private set; } = false;

    public Action inventoryHoverEvent;
    public Action inventoryUnHoverEvent;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(Transform _uiRoot, Action _clickedHomingEvent, Action _hoverEvent, Action _unHoverEvent)
    {
        if (null != omp)
            omp.Initialize();

        InitHoning(_clickedHomingEvent);
        InitInventoryPopup();
        // InitSelectionCursor();
        InitCoins();
        InitBackpack();
        InitNotificationBadge();

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
        
        if (null != uiSubCoin)
            uiSubCoin.SetMoneyType(MoneyType.Carrot);
            
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

            // _slot.deleteItem -= SendDeleteItem;
            // _slot.deleteItem += SendDeleteItem;

            // _slot.enterSlot -= HandleEnterPopup;
            // _slot.enterSlot += HandleEnterPopup;

            // _slot.exitSlot -= HandleExitPopup;
            // _slot.exitSlot += HandleExitPopup;
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
    }

    private void InitInventoryPopup()
    {
        if (null == uiPopupPrefab)
            return;

        GameObject _popupObj = Instantiate(uiPopupPrefab, transform.parent);
        invPopup = _popupObj.GetComponent<UI_InventoryPopup>();

        if (null != invPopup)
        {
            invPopup.Initialize(defaultPopupCap);
            invPopup.gameObject.SetActive(false);
        }
    }

    private void HandleExitPopup()
    {
        if (null != invPopup)
            invPopup.OnHide();
    }

    private void InitHoning(Action _clickedHomingEvent)
    {
        if (null == uiHoming)
            return;

        uiHoming.Initialize();
        uiHoming.clickedEvent = _clickedHomingEvent;
    }

    private void InitCoins()
    {
        if (null != uiCoin) 
            uiCoin.Initialize();
        if (null != uiSubCoin) 
            uiSubCoin.Initialize();
    }

    private void InitBackpack()
    {
        if (null != uiBackpack)
            uiBackpack.Initialize();
    }

    private void InitNotificationBadge()
    {
        if (null != notificationBadge)
            notificationBadge.Initialize();
    }

    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        if (null == moneyData)
            return;

        if (MoneyType.Coin == _moneyType)
            uiCoin?.SetNumberAnimated(moneyData.money);
        else if (MoneyType.Carrot == _moneyType)
            uiSubCoin?.SetNumberAnimated(moneyData.carrot);
    }

    public void CharactersMoneyChanged()
    {
        if (null == moneyData)
            return;

        uiCoin?.SetNumber(moneyData.money);
        uiSubCoin?.SetNumber(moneyData.carrot);
    }

    public void InventoryShowEvent()
    {
        if (null != inventory)
            UpdateSlots(inventory.inventorySlots);

        UpdateNotification();
    }

    public void MapChanged(MapType _currentMap)
    {
        prevMapType = currentMapType;
        currentMapType = _currentMap;

        if (null != uiHoming)
            uiHoming.currentMapType = _currentMap;

        CloseInvAndAnimSkip();
    }

    private void CloseInvAndAnimSkip()
    {
        if (null == omp)
            return;

        HandleExitPopup();
        isOpening = false;

        omp.PlayBackward("Backpack", bReset: true, _skip: true);
        omp.PlayBackward("Coins", bReset: true, _skip: true);
        omp.PlayBackward("Popup", bReset: true, _skip: true);
        omp.PlayBackward("Homing", bReset: true, _skip: true);
    }

    public void UpdateNotification()
    {
        if (null == notificationBadge)
            return;

        notificationBadge.UpdateAndInteraction(!isOpening ? ++hideAccCount : 0); 
    }

    public void OnHide()
    {
        isOpening = false;

        if (null != omp)
        {
            omp.PlayBackward("Backpack", bReset: true);
            omp.PlayBackward("Popup", bReset: true);
        }

        uiBackpack?.CloseInventory();
        HandleExitPopup();

        if (MapType.Town == currentMapType)
            return;

        if (null != omp)
        {
            omp.PlayBackward("Homing", bReset: true);
            omp.PlayBackward("Coins", bReset: true);
        }
    }

    public void OnShow()
    {
        isOpening = true;
        hideAccCount = 0;

        if (null != omp)
        {
            omp.Play("Backpack", bReset: true);
            omp.Play("Popup", bReset: true);
        }

        uiBackpack?.OpenInventory();
        InventoryShowEvent();

        if (MapType.Town == currentMapType)
            return;

        if (null != omp)
        {
            omp.Play("Homing", bReset: true);
            omp.Play("Coins", bReset: true);
        }
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

    public void ClearNotification() => notificationBadge?.UpdateAndInteraction(0);
}
