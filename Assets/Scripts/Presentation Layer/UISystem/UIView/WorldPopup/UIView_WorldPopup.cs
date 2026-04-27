using System.Collections.Generic;
using UnityEngine;

public class UIView_WorldPopup : UIView
{
    private IInventory container;
    private ILogCutter logCutter;
    private IShopNPC shopNPC;


    //내부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiStoragePrefab;
    [SerializeField] private GameObject uiCutterPrefab;
    [SerializeField] private GameObject uiTraderCoinPrefab;

    private UI_Storage ui_Storage;
    private UI_TreeCutter ui_Cutter;
    private UI_Coin ui_TraderCoin;

    //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_UIStorage();
        Init_UICutter();
        Init_UITraderCoin();
    }

    private void BindEvents()
    {
        logCutter.CuttingStartEvent -= LogToCutter;
        logCutter.CuttingStartEvent += LogToCutter;

        logCutter.CuttingDoneEvent -= LogCuttingIsDone;
        logCutter.CuttingDoneEvent += LogCuttingIsDone;

        Bind_UITraderCoin();
    }

    private void ReleaseEvents()
    {
        logCutter.CuttingStartEvent -= LogToCutter;
        logCutter.CuttingDoneEvent -= LogCuttingIsDone;

        if (null != shopNPC)
        {
            shopNPC.ShopMoneyChangedEvent -= UpdateTraderMoneyText;
        }
    }

    public override void Release()
    {
        base.Release();

        ReleaseEvents();
    }

    private void Init_UIStorage()
    {
        if (null == uiStoragePrefab)
            return;

        ui_Storage = Instantiate(uiStoragePrefab, uiRoot).GetComponent<UI_Storage>();
        if (null == ui_Storage)
            return;

        ui_Storage.Initialize();
    }

    private void Init_UICutter()
    {
        if (null == uiCutterPrefab)
            return;

        ui_Cutter = Instantiate(uiCutterPrefab, uiRoot).GetComponent<UI_TreeCutter>();
        if (null == ui_Cutter)
            return;

        ui_Cutter.Initialize();
    }

    private void Init_UITraderCoin()
    {
        if (null == uiTraderCoinPrefab)
            return;

        ui_TraderCoin = Instantiate(uiTraderCoinPrefab, uiRoot).GetComponent<UI_Coin>();
        if (null == ui_TraderCoin)
            return;

        ui_TraderCoin.Initialize();

        // 상시로 On
        ui_TraderCoin.gameObject.SetActive(true);
    }

    private void Bind_UITraderCoin()
    {
        if (null == shopNPC)
            return;

        UpdateTraderMoneyText();

        Vector3 newPos = shopNPC.npcTransform.position;
        newPos.y += 0.5f;

        if (null != ui_TraderCoin)
            ui_TraderCoin.gameObject.transform.position = newPos;

        shopNPC.ShopMoneyChangedEvent -= UpdateTraderMoneyText;
        shopNPC.ShopMoneyChangedEvent += UpdateTraderMoneyText;
    }

    private void UpdateTraderMoneyText()
    {
        if (null == shopNPC || null == ui_TraderCoin)
            return;

        ui_TraderCoin.UpdateMoneyText(shopNPC.currentMoney);
    }

    public void DependencyInjection(IInventory _container, ILogCutter _logCutter, IShopNPC _shopNPC)
    {
        container = _container;
        logCutter = _logCutter;
        shopNPC = _shopNPC;

        ui_Storage?.BindStorage(container);
        ui_Cutter?.BindPosition(_logCutter.GetTransform().position);

        BindEvents();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public override void Update()
    {
        base.Update();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    //원목 보관함 최신화됨.
    public void ContainerUpdated()
    {
        if (container == null)
        {
            Debug.LogWarning("[UIView_WorldPopup] Container is null.");
            return;
        }

        ui_Storage?.Refresh();
    }

    // true : 원목 보관함과 상호작용 가능 거리에 들어옴
    // false : 상호작용 거리에서 나감
    public void LogContainerInteractStateChanged(bool _state)
    {
        if (true == _state)
        {
            if (null != ui_Storage)
            {
                ui_Storage.OnShow();
                ui_Storage.Refresh();
            }

            ui_Cutter?.OnShow();
        }
        else
        {
            ui_Storage?.OnHide();
            ui_Cutter?.OnHide();
        }
    }

    //원목이 절단기로 들어감.
    private void LogToCutter(ILogItemData _itemData)
    {
        //Debug.Log(logCutter.timeRemaining);
        //logCutter.logToCut -> 절단될 원목.
        //logCutter.timeRemaining -> 남은 절단 시간.

        if (null != ui_Cutter)
        {
            ui_Cutter.BindItemData(logCutter.logToCut);
            ui_Cutter.BindRemaining(logCutter.timeRemaining);
        }
    }

    public void LogContainerSpecChanged() //원목 보관함 스펙이 최신화됨.
    {
        ui_Storage?.Refresh();
    }

    private void LogCuttingIsDone()
    {
        ui_Cutter?.ResetCutter();
    }

    public override void Refresh()
    {
        if (null != ui_Cutter && null != logCutter.logToCut)
        {
            ui_Cutter.BindItemData(logCutter.logToCut);
            ui_Cutter.BindRemaining(logCutter.timeRemaining);
        }
        ui_Storage?.Refresh();
    }
}