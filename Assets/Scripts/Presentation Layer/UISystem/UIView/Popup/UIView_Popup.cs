using System.Collections.Generic;
using UnityEngine;

public class UIView_Popup : UIView
{
    //외부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; 
    [SerializeField] private GameObject uiInventoryPrefab;
    [SerializeField] private GameObject uiHomingPrefab;

    //내부 의존성
    private IInventory inventory;
    private UI_Inventory uI_Inventory;
    private UI_Homing uI_Homing;

    private const int defaultPopupCap = 12;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_Homing();
        Init_Inventory();
    }

    public void DependencyInjection(IInventory _inventory)
    {
        inventory = _inventory;

        uI_Inventory?.BindInventory(inventory);
    }

#region [ Inventory UI ]
    private void Init_Inventory()
    {
        if (null == uiInventoryPrefab)
            return;

        uI_Inventory = Instantiate(uiInventoryPrefab, this.transform.parent).GetComponent<UI_Inventory>();

        if (null == uI_Inventory)
            return;

        uI_Inventory.Initialize(uiRoot);
        uI_Inventory.OnHide();
    }

     public void InventoryShowEvent() => uI_Inventory?.InventoryShowEvent();
#endregion

#region [ Homing UI ]

    private void Init_Homing()
    {
        if (null == uiHomingPrefab)
            return;

        uI_Homing = Instantiate(uiHomingPrefab, this.transform.parent).GetComponent<UI_Homing>();

        if (null == uI_Homing)
            return;

        uI_Homing.Initialize();
        uI_Homing.gameObject.SetActive(false);
    }

#endregion

    // 유니티 이벤트 함수
    protected override void OnShow() 
    {
        base.OnShow();

        Debug.Log("켜짐");

        uI_Inventory?.OnShow();
        uI_Homing?.OnShow();
    }

    protected override void OnHide() 
    {
        uI_Inventory?.OnHide();
        uI_Homing?.OnHide();

        base.OnHide();
    }

    public override void OnDestroy()
    {
        uI_Inventory?.OnDestroy();
    }
}
