using System;
using System.Collections.Generic;
using UnityEngine;

public class UI_Inventory : MonoBehaviour
{
    //이벤트
    public event Action<IInventorySlot> SendDeleteItemEvent;
    
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private List<UI_InventorySlot> inventorySlots;

    [SerializeField] private float popupYOffset = 30.0f;

    private const int defaultPopupCap = 12;

    private IInventory inventory;

    private UI_InventoryPopup invPopup;

    public void Initialize(Transform uiRoot)
    {
        inventorySlots.Clear();
        UpdateMaxSlotCount(SYSTEM_VAR.MAX_INVENTORY_CNT);
        Init_InventoryPopup();
    }

    public void BindInventory(IInventory _inventory)
    {
        inventory = _inventory;
    }

#region [ Inventory UI ]

    public void UpdateMaxSlotCount(int _cnt)
    {
        if (null == uiSlotPrefab)
            return;

        int needCount = _cnt - inventorySlots.Count;

        while (0 < needCount--)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, this.transform).GetComponent<UI_InventorySlot>();

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

        int itemCount = _items.Count;

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

        gameObject.SetActive(false);
    }

    public void OnShow()
    {
        gameObject.SetActive(true);

        InventoryShowEvent();
    }

    public void OnDestroy()
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
