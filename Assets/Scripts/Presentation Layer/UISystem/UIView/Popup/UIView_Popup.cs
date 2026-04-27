using System;
using UnityEngine;

public class UIView_Popup : UIView
{
    //이벤트
    public event Action GoHomeButtonClickedEvent;
    public event Action<IInventorySlot> SendDeleteItemEvent;

    //외부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiInventoryPrefab;

    //내부 의존성
    private IInventory inventory;
    private IMoneyData moneyData;
    private UI_Inventory uI_Inventory;

    private const int defaultPopupCap = 12;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_Inventory();
        BindEvents();
    }

    public void DependencyInjection(IInventory _inventory, IMoneyData _moneyData)
    {
        inventory = _inventory;
        moneyData = _moneyData;

        uI_Inventory.BindData(inventory, _moneyData);
    }

    private void BindEvents()
    {
        if (uI_Inventory != null)
        {
            uI_Inventory.SendDeleteItemEvent -= SendDeleteItem;
            uI_Inventory.SendDeleteItemEvent += SendDeleteItem;
        }
    }

    private void ReleaseEvents()
    {
        uI_Inventory.SendDeleteItemEvent -= SendDeleteItem;
    }

#region [ Inventory UI ]
    private void Init_Inventory()
    {
        if (null == uiInventoryPrefab)
            return;

        uI_Inventory = Instantiate(uiInventoryPrefab, this.transform.parent).GetComponent<UI_Inventory>();

        if (null == uI_Inventory)
            return;

        uI_Inventory.Initialize(uiRoot, OnHomingButtonClicked);
        uI_Inventory.OnHide();
    }

    private void SendDeleteItem(IInventorySlot _inData)
    {
        SendDeleteItemEvent.Invoke(_inData);
    }

    public void InventoryShowEvent() => uI_Inventory?.InventoryShowEvent();

    public void InventorySpecChanged() //인벤토리 스펙이 변경되었을 때,
    {
        if (null != uI_Inventory)
        {
            if (uI_Inventory.isOpening)
                uI_Inventory.InventoryShowEvent();
        }
    }

    #endregion



    protected override void OnShow()
    {
        base.OnShow();

        uI_Inventory?.OnShow();
    }

    protected override void OnHide()
    {
        uI_Inventory?.OnHide();

        base.OnHide();
    }

    public override void OnDestroy()
    {
        ReleaseEvents();

        uI_Inventory?.Destory();
    }

    private void OnHomingButtonClicked()
    {
        GoHomeButtonClickedEvent.Invoke();
    }

    public void CharacterEarnMoney(MoneyType _moneyType) //캐릭터가 돈을 얻었을 때,
    {
        uI_Inventory?.CharacterEarnMoney(_moneyType);
    }

    public void CharactersMoneyChanged()
    {
        uI_Inventory?.CharactersMoneyChanged();
    }

    public override void Refresh()
    {

    }

    // 나중에 맵에 따른 보여줘야 할 머니 타입을 교체 해야 함.
}
