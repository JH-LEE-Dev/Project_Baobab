using System;
using System.Collections.Generic;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using PresentationLayer.UISystem.CustomNumber;

public class UI_Inventory : MonoBehaviour
{
    //이벤트
    public event Action<IInventorySlot> SendDeleteItemEvent;

    [Header("Binding Obj")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private GameObject invBackground;
    [SerializeField] private UI_Homing uiHoming;
    [SerializeField] private CurrencyCounterHUD uiCoin;
    [SerializeField] private CurrencyCounterHUD uiSubCoin;
    [SerializeField] private UI_Backpack uiBackpack;
    [SerializeField] private UISelectionCursor selectionCursor;

    [Header("Prefabs")]
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private List<UI_InventorySlot> inventorySlots;
    [SerializeField] private float popupYOffset = 30.0f;

    private const int defaultPopupCap = 12;

    private IInventory inventory;
    private IMoneyData moneyData;

    private UI_InventoryPopup invPopup;

    public MapType currentMapType { get; set; } = MapType.Town;
    private MapType prevMapType = MapType.Town;

    public bool isOpening { get; private set; } = false;

    public Action inventoryHoverEvent;
    public Action inventoryUnHoverEvent;

    public void Initialize(Transform _uiRoot, Action _clickedHomingEvent, Action _hoverEvent, Action _unHoverEvent)
    {
        omp?.Initialize();

        Init_Honing(_clickedHomingEvent);
        Init_InventoryPopup();
        Init_SelectionCursor();
        Init_Coins();
        Init_Backpack();

        inventoryHoverEvent -= _hoverEvent;
        inventoryHoverEvent += _hoverEvent;

        inventoryUnHoverEvent -= _unHoverEvent;
        inventoryUnHoverEvent += _unHoverEvent;

        inventorySlots.Clear();
        UpdateMaxSlotCount(SYSTEM_VAR.MAX_INVENTORY_CNT);
    }

    public void BindData(IInventory _inventory, IMoneyData _moneyData)
    {
        inventory = _inventory;
        moneyData = _moneyData;

        uiCoin?.SetMoneyType(MoneyType.Coin);
        uiSubCoin?.SetMoneyType(MoneyType.Carrot);
        CharactersMoneyChanged();
    }

    #region [ Inventory UI ]

    public void UpdateMaxSlotCount(int _cnt)
    {
        if (null == uiSlotPrefab)
            return;

        int needCount = _cnt - inventorySlots.Count;

        while (0 < needCount--)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, invBackground.transform).GetComponent<UI_InventorySlot>();

            if (null == slot)
                return;

            slot.Initialize();

            slot.deleteItem -= SendDeleteItem;
            slot.deleteItem += SendDeleteItem;

            slot.enterSlot -= EnterPopup;
            slot.enterSlot += EnterPopup;

            slot.exitSlot -= ExitPopup;
            slot.exitSlot += ExitPopup;
            slot.exitSlot -= inventoryUnHoverEvent;
            slot.exitSlot += inventoryUnHoverEvent;

            inventorySlots.Add(slot);
        }
    }

    public void SendDeleteItem(IInventorySlot _inData)
    {
        if (null == inventory)
            return;

        SendDeleteItemEvent.Invoke(_inData);

        UpdateSlots(inventory.inventorySlots);
        //invPopup?.gameObject.SetActive(false);
    }

    public void Refresh()
    {
        UpdateSlots(inventory?.inventorySlots);  
    } 

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        if (null == _items)
            return;

        int itemCount = inventory.currentSlotCnt;

        for (int i = 0; i < inventorySlots.Count; ++i)
        {
            UI_InventorySlot slot = inventorySlots[i];

            if (i < itemCount)
            {
                IInventorySlot item = _items[i];

                if (false == slot.gameObject.activeSelf)
                    slot.gameObject.SetActive(true);

                slot.UpdateBindSlotData(item);
                slot.UpdateItemCount(item.count);
            }
            else
            {
                if (true == slot.gameObject.activeSelf)
                {
                    slot.ResetData();
                    slot.gameObject.SetActive(false);
                }
            }
        }
    }

    private void Init_InventoryPopup()
    {
        if (null == uiPopupPrefab)
            return;

        invPopup = Instantiate(uiPopupPrefab, this.transform.parent).GetComponent<UI_InventoryPopup>();

        if (null == invPopup)
            return;

        invPopup.Initialize(defaultPopupCap);
        invPopup.gameObject.SetActive(false);
    }

    private void Init_SelectionCursor()
    {
        if (null == selectionCursor)
            return;

        selectionCursor.Initialize(selectionCursor.CursorSize);
    }

#endregion

#region  [ Hover Event ]

    private void EnterPopup(UI_InventorySlot _slot, IItemData _itemData, Vector2 _position)
    {
        ILogItemData logItemData = _itemData as ILogItemData;
        
        selectionCursor?.Show(_slot.GetComponent<RectTransform>());

        inventoryHoverEvent?.Invoke();

        if (null == invPopup || null == logItemData)
            return;

        _position.y += popupYOffset;

        invPopup.SetupItem(logItemData, _position);
        invPopup.OnShow();
    }

    private void ExitPopup()
    {
        selectionCursor?.Hide();

        if (null == invPopup)
            return;
            
        invPopup.OnHide();
    }
    #endregion

    private void Init_Honing(Action clickedHomingEvent)
    {
        if (null == uiHoming)
            return;

        uiHoming.Initialize();

        uiHoming.clickedEvent = clickedHomingEvent;
    }

    private void Init_Coins()
    {
        uiCoin?.Initialize();
        uiSubCoin?.Initialize();
    }

    private void Init_Backpack()
    {
        uiBackpack?.Initialize();
    }

    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        if (MoneyType.Coin == _moneyType)
            uiCoin?.SetNumberAnimated(moneyData.money);
        else if (MoneyType.Carrot == _moneyType)
            uiSubCoin?.SetNumberAnimated(moneyData.carrot);
    }

    public void CharactersMoneyChanged()
    {
        uiCoin?.SetNumber(moneyData.money);
        uiSubCoin?.SetNumber(moneyData.carrot);
    }

    public void ChangedShowMoneyType()
    {
        if (null == uiSubCoin)
            return;

    }

    public void InventoryShowEvent()
    {
        if (null == inventory)
            return;

        IReadOnlyList<IInventorySlot> items = inventory.inventorySlots;

        UpdateSlots(items);
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

        ExitPopup();

        isOpening = false;

        omp.PlayBackward("Backpack", bReset: true,  _skip: true);
        omp.PlayBackward("Coins", bReset: true, _skip: true);
        omp.PlayBackward("Popup", bReset: true, _skip: true);
        omp.PlayBackward("Homing", bReset: true, _skip: true);
    }

    public void OnHide()
    {
        isOpening = false;

        omp.PlayBackward("Backpack", bReset: true);
        uiBackpack?.CloseInventory();
        ExitPopup();
        omp.PlayBackward("Popup", bReset: true);

        if (MapType.Town == currentMapType)
            return;

        omp.PlayBackward("Homing", bReset: true);
        omp.PlayBackward("Coins", bReset: true);
    }

    public void OnShow()
    {
        isOpening = true;

        omp.Play("Backpack", bReset: true);
        uiBackpack?.OpenInventory();
        InventoryShowEvent();
        omp.Play("Popup", bReset: true);

        if (MapType.Town == currentMapType)
            return;

        omp.Play("Homing", bReset: true);
        omp.Play("Coins", bReset: true);
    }

    public void Destory()
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            UI_InventorySlot slot = inventorySlots[i];
            
            if (null == slot)
                continue;

            slot.deleteItem -= SendDeleteItem;
            slot.enterSlot -= EnterPopup;
            slot.exitSlot -= ExitPopup;
            slot.exitSlot -= inventoryUnHoverEvent;
        }


    }

}
