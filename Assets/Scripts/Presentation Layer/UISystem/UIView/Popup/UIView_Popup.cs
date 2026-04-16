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
    [SerializeField] private GameObject uiHomingPrefab;
    [SerializeField] private GameObject uiCoinPrefab;
    [SerializeField] private GameObject uiCarrotCoinPrefab;

    //내부 의존성
    private IInventory inventory;
    private UI_Inventory uI_Inventory;
    private UI_Homing uI_Homing;
    private UI_Coin uI_Coin;
    private UI_Coin uI_CarrotCoin;

    private const int defaultPopupCap = 12;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_Homing();
        Init_Inventory();
        Init_Coin();
        Init_CarrotCoin();

        BindEvents();
    }

    public void DependencyInjection(IInventory _inventory)
    {
        inventory = _inventory;

        uI_Inventory?.BindInventory(inventory);
        uI_Coin?.BindInventory(inventory, MoneyType.Coin);
        uI_CarrotCoin?.BindInventory(inventory, MoneyType.Carrot);
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

        uI_Inventory.Initialize(uiRoot);
        uI_Inventory.OnHide();
    }

    private void SendDeleteItem(IInventorySlot _inData)
    {
        SendDeleteItemEvent.Invoke(_inData);
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

        uI_Homing.clickedEvent -= OnHomingButtonClicked;
        uI_Homing.clickedEvent += OnHomingButtonClicked;

        uI_Homing.gameObject.SetActive(false);
    }

    #endregion

#region [ Coin UI ]

    private void Init_Coin()
    {
        if (null == uiCoinPrefab)
            return;

        uI_Coin = Instantiate(uiCoinPrefab, this.transform.parent).GetComponent<UI_Coin>();

        if (null == uI_Coin)
            return;

        uI_Coin.Initialize();
        uI_Coin.OnHide();
    }

    private void Init_CarrotCoin()
    {
        if (null == uiCarrotCoinPrefab)
            return;

        uI_CarrotCoin = Instantiate(uiCarrotCoinPrefab, this.transform.parent).GetComponent<UI_Coin>();

        if (null == uI_CarrotCoin)
            return;

        uI_CarrotCoin.Initialize();
        uI_CarrotCoin.OnHide();
    }

    public void CharacterEarnMoney(MoneyType _moneyType) //캐릭터가 돈을 얻었을 때,
    {
        uI_Coin?.UpdateMoneyText();
    }

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
        uI_Homing?.OnShow();
        uI_Coin?.OnShow();
        uI_CarrotCoin?.OnShow();
    }

    protected override void OnHide()
    {
        uI_Inventory?.OnHide();
        uI_Homing?.OnHide();
        uI_Coin?.OnHide();
        uI_CarrotCoin?.OnHide();

        base.OnHide();
    }

    public override void OnDestroy()
    {
        ReleaseEvents();

        uI_Inventory?.OnDestroy();
        //uI_Homing?.OnDestroy();
        //uI_Coin?.OnDestroy();
    }

    private void OnHomingButtonClicked()
    {
        GoHomeButtonClickedEvent.Invoke();
    }
}
