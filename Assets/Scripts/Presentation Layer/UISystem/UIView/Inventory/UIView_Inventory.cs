using System.Collections.Generic;
using UnityEngine;
using VFolders.Libs;

public class UIView_Inventory : UIView
{
    private IInventory inventory;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int startSlotCount = 2;
    [SerializeField] private List<UI_InventorySlot> inventorySlots;

    private UI_InventoryPopup invPopup;
    [SerializeField] private float popupYOffset = 30.0f;

    private const int defaultPopupCap = 12;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        inventorySlots.Clear(); // Ensure the list starts fresh, ignoring any editor-assigned slots.
        UpdateMaxSlotCount(startSlotCount);
        Init_InventoryPopup();
    }

    public void DependencyInjection(IInventory _inventory)
    {
        inventory = _inventory;
    }

    public override void OnDestroy()
    {
        foreach (UI_InventorySlot slot in inventorySlots)
        {
            slot.deleteItem -= SendDeleteItem;
            slot.enterSlot -= EnterPopup;
            slot.exitSlot -= ExitPopup;
        }
    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();

        InventoryShowEvent();
    }

    protected override void OnHide() //이 UI가 꺼졌을 때 호출 됨.
    {
        ExitPopup();

        base.OnHide();
    }

    public void UpdateMaxSlotCount(int cnt)
    {
        int needCount = cnt - inventorySlots.Count;

        while(0 < needCount--)
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
    private void EnterPopup(IItemData itemData, LogStateCount[] _logStateCounts, Vector2 position)
    {
        ILogItemData logItemData = itemData as ILogItemData;
        if (null == invPopup || null == logItemData)
            return;

        invPopup.gameObject.SetActive(true);
        
        position.y += popupYOffset;
        invPopup.ShowItems(logItemData, _logStateCounts, position);
    }

    private void ExitPopup()
    {
        if (null == invPopup)
            return;

        invPopup.InvisibleSlots();
        invPopup.gameObject.SetActive(false);
    }   
#endregion

    public void SendDeleteItem(IItemData it)
    {
        // TODO :: 삭제할 아이템을 위로 올려 보냄.
        UpdateSlots(inventory.inventorySlots); 
    }

    public void InventoryShowEvent()
    {
        if (null == inventory)
            return;

        var items = inventory.inventorySlots;
        if (null == items)
            return;

        UpdateSlots(items); 
    }

    private void UpdateSlots(IReadOnlyList<IInventorySlot> items)
    {
        UpdateMaxSlotCount(items.Count);

        for (int i = 0; i < items.Count; ++i)
        {
            if (inventorySlots[i].ShowItemData == items[i].itemData)
            {
                if (inventorySlots[i].ShowCnt == items[i].count)
                    continue;

                inventorySlots[i].UpdateItemCount(items[i].count);
            }

            else
            {
                inventorySlots[i].UpdateBindSlotData(items[i].itemData, items[i].logStateCounts);
                inventorySlots[i].UpdateItemCount(items[i].count);
                inventorySlots[i].UpdateImage(items[i].itemData);
            }
        }
    }
}
