using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.LowLevelPhysics2D.PhysicsShape;

public class UI_Inventory : MonoBehaviour
{
    //이벤트
    public event Action<IInventorySlot> SendDeleteItemEvent;

    [Header("Binding Obj")]
    [SerializeField] private GameObject uiBackground;
    [SerializeField] private UI_Homing uiHoming;
    [SerializeField] private UI_Coin uiCoin;
    [SerializeField] private UI_Coin uiSubCoin;
    [SerializeField] private UI_Backpack uiBackpack;

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

    public bool isOpening { get; private set; } = false;

    public void Initialize(Transform uiRoot, Action clickedHomingEvent)
    {
        Init_Background();
        Init_Honing(clickedHomingEvent);
        Init_InventoryPopup();
        Init_Coins();

        inventorySlots.Clear();
        UpdateMaxSlotCount(SYSTEM_VAR.MAX_INVENTORY_CNT);
    }

    public void BindData(IInventory _inventory, IMoneyData _moneyData)
    {
        inventory = _inventory;
        moneyData = _moneyData;

        uiCoin?.BindMoneyData(moneyData, MoneyType.Coin);
        uiSubCoin?.BindMoneyData(moneyData, MoneyType.Carrot);
    }

    #region [ Inventory UI ]

    public void UpdateMaxSlotCount(int _cnt)
    {
        if (null == uiSlotPrefab)
            return;

        int needCount = _cnt - inventorySlots.Count;

        while (0 < needCount--)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, uiBackground.transform).GetComponent<UI_InventorySlot>();

            if (null == slot)
                return;

            slot.Initialize();

            slot.deleteItem -= SendDeleteItem;
            slot.deleteItem += SendDeleteItem;

            slot.enterSlot -= EnterPopup;
            slot.enterSlot += EnterPopup;

            slot.exitSlot -= ExitPopup;
            slot.exitSlot += ExitPopup;

            inventorySlots.Add(slot);
        }
    }

    public void SendDeleteItem(IInventorySlot _inData)
    {
        if (null == inventory)
            return;

        SendDeleteItemEvent.Invoke(_inData);

        UpdateSlots(inventory.inventorySlots);
        invPopup?.gameObject.SetActive(false);
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

#endregion

#region  [ Hover Event ]

    private void EnterPopup(IItemData _itemData, LogStateCount[] _logStateCounts, Vector2 _position)
    {
        ILogItemData logItemData = _itemData as ILogItemData;
        
        if (null == invPopup || null == logItemData)
            return;

        invPopup.gameObject.SetActive(true);
        
        _position.y += popupYOffset;
        invPopup.ShowItems(logItemData, _logStateCounts, _position);
    }

    private void ExitPopup()
    {
        if (null == invPopup)
            return;

        invPopup.InvisibleSlots();
        invPopup.gameObject.SetActive(false);
    }
    #endregion

    private void Init_Background()
    {
        uiBackground?.SetActive(false);
    }

    private void Init_Honing(Action clickedHomingEvent)
    {
        if (null == uiHoming)
            return;

        uiHoming.gameObject.SetActive(false);

        uiHoming.Initialize();
        // TODO :: 지워질 때 빼줘야 함
        uiHoming.clickedEvent -= clickedHomingEvent;
        uiHoming.clickedEvent += clickedHomingEvent;
    }

    private void Init_Coins()
    {
        uiCoin?.Initialize();
        uiSubCoin?.Initialize();
    }

    public void CharacterEarnMoney(MoneyType _moneyType)
    {
        if (MoneyType.Coin == _moneyType)
            uiCoin?.UpdateMoneyText();
        else
            uiSubCoin?.UpdateMoneyText();
    }

    public void CharactersMoneyChanged()
    {
        uiCoin?.UpdateMoneyText();
        uiSubCoin?.UpdateMoneyText();
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

    public void OnHide()
    {
        ExitPopup();

        isOpening = false;

        uiBackground?.SetActive(isOpening);
        uiHoming?.gameObject.SetActive(isOpening);
        uiBackpack?.CloseBackpack();
    }

    public void OnShow()
    {
        isOpening = true;

        uiBackground?.SetActive(isOpening);
        uiHoming?.gameObject.SetActive(isOpening);
        uiBackpack?.OpenBackpack();

        InventoryShowEvent();
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
        }
    }

}
