using System.Collections.Generic;
using UnityEngine;

public class UIView_Inventory : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject uiPopupPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int startSlotCount = 2;
    [SerializeField] private List<UI_InventorySlot> inventorySlots;

    private UI_InventoryPopup invPopup;
    [SerializeField] private float popupYOffset = 30.0f;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        inventorySlots.Clear(); // Ensure the list starts fresh, ignoring any editor-assigned slots.
        UpdateMaxSlotCount(startSlotCount);
        Init_InventoryPopup();
    }

    public override void OnDestroy()
    {
        foreach (UI_InventorySlot slot in inventorySlots)
        {
            slot.enterSlot -= EnterPopup;
            slot.exitSlot -= ExitPopup;
        }
    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();

        
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

        invPopup.Initialize(12);
        invPopup.gameObject.SetActive(false);
    }

    private void EnterPopup(Item it, Vector2 position)
    {
        if (null == invPopup)
            return;

        invPopup.gameObject.SetActive(true);

        // TODO :: 현재 선택된 아이템의 해당 하는 종류 ( 자작, 참, 소 등 ) 타입을 구분해서 그 리스트를 꺼내온 뒤
        // ShowItems 에 리스트 넣기
        
        position.y += popupYOffset;
        invPopup.ShowItems(position);
    }

    private void ExitPopup()
    {
        if (null == invPopup)
            return;

        invPopup.InvisibleSlots();
        invPopup.gameObject.SetActive(false);
    }   

    public void SendDeleteItem(Item it)
    {
        // TODO :: 삭제할 아이템을 위로 올려 보냄.
        // 이후에 노출 되어야 하는 아이템 슬롯 재정렬
    }
}
