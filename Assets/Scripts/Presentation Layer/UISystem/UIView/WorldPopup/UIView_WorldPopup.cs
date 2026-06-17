using UnityEngine;

public class UIView_WorldPopup : UIView
{
    private IInventory container;
    private ILogCutter logCutter;
    private IShopNPC shopNPC;
    private IInventory offroadContainer;
    private ICharacter character;

    [SerializeField] private Vector2 storageOffset = new Vector2(-2f, 0.5f);
    [SerializeField] private Vector2 carStorageOffset = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 cutterOffset = new Vector2(-1f, 0f);

    [SerializeField] private bool bTraderCoinAnim = false;
    [SerializeField] private Vector2 traderCoinOffset = new Vector2(0.5f, 0.5f);


    //내부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiStoragePrefab;
    [SerializeField] private GameObject uiCarStoragePrefab;
    [SerializeField] private GameObject uiCutterPrefab;
    [SerializeField] private GameObject uiTraderCoinPrefab;

    private UI_Storage ui_Storage;
    private UI_Storage ui_CarStorage;
    private UI_TreeCutter ui_Cutter;
    private UI_TraderCoin ui_TraderCoin;

    private bool isLogProcesserActive = false;

    //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_UIStorage();
        Init_UICarStorage();
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

        ui_Storage.Initialize(storageOffset);
    }

    private void Init_UICarStorage()
    {
        if (null == uiCarStoragePrefab)
            return;

        ui_CarStorage = Instantiate(uiCarStoragePrefab, uiRoot).GetComponent<UI_Storage>();
        if (null == ui_CarStorage)
            return;

        ui_CarStorage.Initialize(carStorageOffset);
    }


    private void Init_UICutter()
    {
        if (null == uiCutterPrefab)
            return;

        ui_Cutter = Instantiate(uiCutterPrefab, uiRoot).GetComponent<UI_TreeCutter>();
        if (null == ui_Cutter)
            return;

        ui_Cutter.Initialize(cutterOffset);
    }

    private void Init_UITraderCoin()
    {
        if (null == uiTraderCoinPrefab)
            return;

        ui_TraderCoin = Instantiate(uiTraderCoinPrefab, uiRoot).GetComponent<UI_TraderCoin>();
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

        Vector2 newPos = shopNPC.npcTransform.position;
        newPos += traderCoinOffset;

        if (null != ui_TraderCoin)
            ui_TraderCoin.gameObject.transform.position = newPos;

        shopNPC.ShopMoneyChangedEvent -= UpdateTraderMoneyText;
        shopNPC.ShopMoneyChangedEvent += UpdateTraderMoneyText;
    }

    private void UpdateTraderMoneyText()
    {
        if (null == shopNPC || null == ui_TraderCoin)
            return;

        if (bTraderCoinAnim)
        {
            ui_TraderCoin.UpdateMoneyText_Anim(shopNPC.currentMoney);
            return;
        }

        ui_TraderCoin.UpdateMoneyText(shopNPC.currentMoney);
    }

    public void DependencyInjection(IInventory _container, ILogCutter _logCutter, IShopNPC _shopNPC, IInventory _offroadContainer)
    {
        offroadContainer = _offroadContainer;
        container = _container;
        logCutter = _logCutter;
        shopNPC = _shopNPC;

        ui_Storage?.BindStorage(container);
        ui_CarStorage?.BindStorage(offroadContainer);
        ui_Cutter?.BindPosition(_logCutter.GetTransform().position);
        ui_Cutter?.BindLogCutter(_logCutter);

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

        ui_Storage?.UpdateSlots();
    }

    // true : 원목 보관함과 상호작용 가능 거리에 들어옴
    // false : 상호작용 거리에서 나감
    public void LogContainerInteractStateChanged(bool _state)
    {
        if (null != ui_Storage)
            ui_Storage.IsCollShow = _state;

        if (null != ui_Cutter)
            ui_Cutter.IsCollShow = _state;

        if(false == isLogProcesserActive)
            ShowLogProcessor(_state);
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
        }
    }

    public void LogContainerSpecChanged() //원목 보관함 스펙이 최신화됨.
    {
        if (null == container)
            return;

        ui_Storage?.UpdateMaxSlotCount(container.inventorySlots.Count);
    }

    private void LogCuttingIsDone()
    {
        ui_Cutter?.ResetCutter();
    }

    public override void Refresh()
    {
        ResetLogCutterUI();

        ui_Storage?.Refresh();
        ui_CarStorage?.Refresh();
    }

    private void ResetLogCutterUI()
    {
        if (null != ui_Cutter && null != logCutter.logToCut && logCutter.bIsCutting == true)
        {
            ui_Cutter.BindItemData(logCutter.logToCut);
        }
        else
        {
            ui_Cutter?.ResetCutter();
        }
    }

    //true -> 오프로드 박스에 진입, false -> 그 반대.
    public void OffroadContainerInteractStateChanged(bool _state)
    {
        if (true == _state)
        {
            ui_CarStorage?.OnShow();
            ui_CarStorage?.Refresh();
        }
        else
            ui_CarStorage?.OnHide();
    }

    //오프로드 박스 스펙이 바뀌었음.
    public void OffraodContainerSpecChanged()
    {
        if (null == offroadContainer)
            return;

        ui_CarStorage?.UpdateMaxSlotCount(offroadContainer.inventorySlots.Count);
    }

    //오프로드 박스가 최신화됨.
    public void OffroadContainerUpdated()
    {
        if (container == null)
            return;

        ui_CarStorage?.UpdateSlots();
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        ui_CarStorage?.BindPlayer(character.GetTransform());
    }

    //true -> 제재소 동작중 , false -> 제재소 동작 끝
    public void LogItemProcessorActiveStateChange(bool _boolean)
    {
        ShowLogProcessor(_boolean);
        isLogProcesserActive = _boolean;
    }

    public void ShowLogProcessor(bool _state)
    {
        if (true == _state)
        {
            if (null != ui_Storage)
            {
                ui_Storage.OnShow();
                ui_Storage.Refresh();
            }

            ui_Cutter?.OnShow();
            ResetLogCutterUI();
        }
        else
        {
            ui_Storage?.OnHide();
            ui_Cutter?.OnHide();
        }
    }

    public void WorldPopupGoDown()
    {
        if (true == ui_Storage.IsOpen)
             ui_Storage.OnHide();
        if (true == ui_CarStorage.IsOpen)
            ui_CarStorage.OnHide();

        ui_TraderCoin?.OnHide();
    }

    public void WorldPopupGoUp()
    {
        ui_TraderCoin?.OnShow();
    }
}