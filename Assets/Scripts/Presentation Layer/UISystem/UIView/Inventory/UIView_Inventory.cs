using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class UIView_Inventory : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiPrefab; //생성할 uiPrefab, 여러 개 추가해도 됨.
    [SerializeField] private GameObject uiSlotPrefab;

    [Header("Inventory Settings")]
    [SerializeField] private int startSlotCount = 2;
    [SerializeField] private List<UI_InventorySlot> inventorySlots;

    private GameObject ui;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        for (int i = 0; i < startSlotCount; ++i)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, this.transform).GetComponent<UI_InventorySlot>();
            slot.Initialize(this);
            inventorySlots.Add(slot);
        }

        if (uiPrefab != null) 
            ui = Instantiate(uiPrefab, uiRoot);

        if (ui != null) 
            ui.SetActive(false);
    }

    public override void OnDestroy()
    {

    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();

        
    }

    protected override void OnHide() //이 UI가 꺼졌을 때 호출 됨.
    {


        base.OnHide();
    }

    public void UpdateMaxSlotCount(int cnt)
    {
        int needCount = inventorySlots.Count() - cnt;

        while(0 < needCount--)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, this.transform).GetComponent<UI_InventorySlot>();
            slot.Initialize(this);
            inventorySlots.Add(slot);
        }
    }
}
