using System.Collections.Generic;
using UnityEngine;

public class UIView_Inventory : UIView
{
    //외부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; 
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int startSlotCount = 2;
    [SerializeField] private List<UI_InventorySlot> inventorySlots;

    [SerializeField] private float popupYOffset = 30.0f;

    //내부 의존성
    private IInventory inventory;
    private UI_InventoryPopup invPopup;
    private const int defaultPopupCap = 12;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        inventorySlots.Clear();
        UpdateMaxSlotCount(startSlotCount);
        Init_InventoryPopup();
    }

    public void DependencyInjection(IInventory _inventory)
    {
        inventory = _inventory;
    }

    public void UpdateMaxSlotCount(int _cnt)
    {
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

        UpdateSlots(inventory.inventorySlots); 
    }

    public void InventoryShowEvent()
    {
        if (null == inventory)
            return;

        IReadOnlyList<IInventorySlot> items = inventory.inventorySlots;
        
        if (null == items)
            return;

        UpdateSlots(items); 
    }

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        int itemCount = _items.Count;
        UpdateMaxSlotCount(itemCount);

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

    // 유니티 이벤트 함수
    protected override void OnShow() 
    {
        base.OnShow();

        InventoryShowEvent();
    }

    protected override void OnHide() 
    {
        ExitPopup();

        base.OnHide();
    }

    public override void OnDestroy()
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
