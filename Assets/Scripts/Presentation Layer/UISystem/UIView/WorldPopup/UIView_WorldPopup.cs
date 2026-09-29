using System.Collections.Generic;
using UnityEngine;

public class UIView_WorldPopup : UIView
{
    private IInventory container;
    private ILogCutterProvider logCutterProvider;
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
    [SerializeField] private UI_BlastFurnaceStatus blastFurnaceStatusPrefab;
    [SerializeField] private Vector2 blastFurnaceStatusOffset = new Vector2(0f, 1.5f);

    private UI_Storage ui_Storage;
    private UI_Storage ui_CarStorage;
    // 가공 라인 번호와 1:1로 대응한다. 증설로 라인이 늘어날 때 필요한 만큼만 생성하고,
    // 철거로 줄어든 라인의 UI는 파괴하지 않고 숨겨둔다.
    private readonly List<UI_TreeCutter> ui_Cutters = new List<UI_TreeCutter>(3);
    private UI_TraderCoin ui_TraderCoin;
    private readonly List<UI_BlastFurnaceStatus> blastFurnaceStatuses = new List<UI_BlastFurnaceStatus>(3);
    private IReadOnlyList<BlastFurnaceUIData> blastFurnaceDatas;
    private bool hasCurrentMapType;
    private bool worldPopupDown;
    private const float WorldPixelsPerUnit = 32f;

    private MapType currentMapType;
    private ForestType currentForestType;

    private bool isLogProcesserActive = false;
    private bool isPlayerNearLogProcessor = false;
    private bool bLogProcessorShown = false;

    //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_UIStorage();
        Init_UICarStorage();
        Init_UITraderCoin();
        RefreshBlastFurnaceStatuses();
    }

    private void BindEvents()
    {
        if (null != logCutterProvider)
        {
            logCutterProvider.ActiveLineCountChangedEvent -= ActiveLineCountChanged;
            logCutterProvider.ActiveLineCountChangedEvent += ActiveLineCountChanged;
        }

        Bind_UITraderCoin();
    }

    private void ReleaseEvents()
    {
        if (null != logCutterProvider)
            logCutterProvider.ActiveLineCountChangedEvent -= ActiveLineCountChanged;

        for (int i = 0; i < ui_Cutters.Count; i++)
            ui_Cutters[i]?.UnbindLogCutter();

        if (null != shopNPC)
        {
            shopNPC.ShopMoneyChangedEvent -= UpdateTraderMoneyText;
            shopNPC.RemoteDepositModeChangedEvent -= RemoteDepositModeChanged;
        }
    }

    public override void Release()
    {
        base.Release();

        ReleaseEvents();
        blastFurnaceDatas = null;
        RefreshBlastFurnaceStatuses();
    }

    private void Init_UIStorage()
    {
        if (null == uiStoragePrefab)
            return;

        ui_Storage = Instantiate(uiStoragePrefab, uiRoot).GetComponent<UI_Storage>();
        if (null == ui_Storage)
            return;

        ui_Storage.Initialize(storageOffset, viewCtx?.inputManager);
    }

    private void Init_UICarStorage()
    {
        if (null == uiCarStoragePrefab)
            return;

        ui_CarStorage = Instantiate(uiCarStoragePrefab, uiRoot).GetComponent<UI_Storage>();
        if (null == ui_CarStorage)
            return;

        ui_CarStorage.Initialize(carStorageOffset, viewCtx?.inputManager);
    }


    // 활성 라인 수에 맞춰 커터 UI를 생성/표시하고, 나머지(철거된 라인)는 숨긴다.
    private void RefreshCutterUIs()
    {
        int activeCount = null != logCutterProvider ? logCutterProvider.ActiveLineCount : 0;

        for (int i = 0; i < activeCount; i++)
        {
            UI_TreeCutter cutterUI = GetOrCreateCutterUI(i);
            if (null == cutterUI)
                continue;

            // 커터 이벤트 구독은 멱등이다. Release 후 다시 주입된 경우에도 여기서 재구독된다.
            cutterUI.BindLogCutter(logCutterProvider.GetCutter(i));

            if (true == bLogProcessorShown)
                cutterUI.OnShow();

            cutterUI.SyncWithCutter();
        }

        for (int i = activeCount; i < ui_Cutters.Count; i++)
        {
            if (null == ui_Cutters[i])
                continue;

            ui_Cutters[i].ResetCutter();
            ui_Cutters[i].OnHide();
        }
    }

    private UI_TreeCutter GetOrCreateCutterUI(int _lineIdx)
    {
        while (ui_Cutters.Count <= _lineIdx)
            ui_Cutters.Add(null);

        if (null != ui_Cutters[_lineIdx])
            return ui_Cutters[_lineIdx];

        if (null == uiCutterPrefab || null == logCutterProvider)
            return null;

        ILogCutter cutter = logCutterProvider.GetCutter(_lineIdx);
        if (null == cutter)
            return null;

        UI_TreeCutter cutterUI = Instantiate(uiCutterPrefab, uiRoot).GetComponent<UI_TreeCutter>();
        if (null == cutterUI)
            return null;

        cutterUI.Initialize(cutterOffset);
        cutterUI.BindPosition(cutter.GetTransform().position);

        ui_Cutters[_lineIdx] = cutterUI;
        return cutterUI;
    }

    // 던전에 있는 동안 제재소는 화면 밖(DisableShopObj)으로 치워지므로, 그때 증설되어 생성된
    // 커터 UI는 엉뚱한 위치에 붙는다. 마을로 돌아와 제재소가 제자리로 온 뒤 위치를 다시 맞춘다.
    private void RebindCutterPositions()
    {
        if (null == logCutterProvider)
            return;

        for (int i = 0; i < ui_Cutters.Count; i++)
        {
            if (null == ui_Cutters[i])
                continue;

            ILogCutter cutter = logCutterProvider.GetCutter(i);
            if (null != cutter)
                ui_Cutters[i].BindPosition(cutter.GetTransform().position);
        }
    }

    private void ActiveLineCountChanged(int _activeLineCount)
    {
        RefreshCutterUIs();
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

        shopNPC.RemoteDepositModeChangedEvent -= RemoteDepositModeChanged;
        shopNPC.RemoteDepositModeChangedEvent += RemoteDepositModeChanged;
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

    private void RemoteDepositModeChanged(bool _bLocked)
    {
        if (null == ui_TraderCoin)
            return;

        if (_bLocked)
        {
            ui_TraderCoin.gameObject.SetActive(false);
        }
        else if (MapType.Town == currentMapType)
        {
            ui_TraderCoin.gameObject.SetActive(true);
        }
    }

    public void DependencyInjection(IInventory _container, ILogCutterProvider _logCutterProvider, IShopNPC _shopNPC, IInventory _offroadContainer)
    {
        offroadContainer = _offroadContainer;
        container = _container;
        logCutterProvider = _logCutterProvider;
        shopNPC = _shopNPC;

        ui_Storage?.BindStorage(container);
        ui_CarStorage?.BindStorage(offroadContainer);
        RefreshCutterUIs();

        BindEvents();
    }

    protected override void OnShow()
    {
        base.OnShow();
        RefreshBlastFurnaceStatuses();
    }

    protected override void OnHide()
    {
        base.OnHide();
        RefreshBlastFurnaceStatuses();
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
        isPlayerNearLogProcessor = _state;
        UpdateLogProcessorVisibility();
    }

    public void LogContainerSpecChanged() //원목 보관함 스펙이 최신화됨.
    {
        if (null == container)
            return;

        ui_Storage?.UpdateMaxSlotCount(container.inventorySlots.Count);
    }

    public override void Refresh() //저장 파일 로드할 때도 호출됨.
    {
        ResetLogCutterUIs();

        ui_Storage?.Refresh();
        ui_CarStorage?.Refresh();
        SnapTraderMoneyText();
    }

    // 세이브 로드 직후 반영 시에는 bTraderCoinAnim 설정과 무관하게 항상 스냅으로 갱신해야 한다.
    // (LoadSaveData가 발행하는 ShopMoneyChangedEvent는 실제 골드 획득과 동일하게 취급되어 애니메이션이
    // 걸릴 수 있으므로, Refresh()에서 이 값을 다시 스냅으로 덮어써 애니메이션/사운드가 재생되지 않게 한다.)
    private void SnapTraderMoneyText()
    {
        if (null == shopNPC || null == ui_TraderCoin)
            return;

        ui_TraderCoin.SyncMoneyTextSilent(shopNPC.currentMoney);
    }

    private void ResetLogCutterUIs()
    {
        int activeCount = null != logCutterProvider ? logCutterProvider.ActiveLineCount : 0;
        for (int i = 0; i < activeCount && i < ui_Cutters.Count; i++)
            ui_Cutters[i]?.SyncWithCutter();
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

    /// <summary>
    /// 지금 교체하면 버려질 <b>운반 상자 슬롯</b>이 달라졌습니다.
    ///
    /// _activeTarget은 교체 키가 실제로 건드릴 쪽입니다. 상자 쪽에 후보가 있으면 상자가 우선이므로
    /// 보통 OffroadContainer지만, 후보가 없어지면 인벤토리 쪽으로 넘어갑니다.
    /// (교체 키 입력을 실제로 처리하는 곳은 GameplayUICoordinator이고, 여기는 표시 전용입니다)
    /// </summary>
    public void LogSwapTargetChanged(in LogSwapSlotInfo _info, ELogSwapTarget _activeTarget)
    {
        ui_CarStorage?.SetLogSwapInfo(in _info, _activeTarget);
    }

    /// <summary>
    /// 운반 상자 슬롯 하나가 교체로 실제로 버려졌습니다. _info에는 방금 버려진 내용이 담겨 있습니다
    /// (데이터는 이미 지워진 뒤라 슬롯을 다시 읽으면 비어 있습니다).
    /// </summary>
    public void LogSwapExecuted(in LogSwapSlotInfo _info)
    {
        ui_CarStorage?.LogSwapExecuted(in _info);
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

    // BlastFurnaceManager reuses this list and replaces its entries each frame.
    // Configuration changes arrive by signal; progress and position are read every frame.
    public IReadOnlyList<BlastFurnaceUIData> BlastFurnaceDatas => blastFurnaceDatas;

    public void BlastFurnaceStateChanged(IReadOnlyList<BlastFurnaceUIData> datas)
    {
        blastFurnaceDatas = datas;
        RefreshBlastFurnaceStatuses();
    }

    private void LateUpdate()
    {
        if (false == IsVisible || false == hasCurrentMapType
            || MapType.Town != currentMapType || true == worldPopupDown)
            return;
        RefreshBlastFurnaceStatuses();
    }

    private void RefreshBlastFurnaceStatuses()
    {
        bool show = true == IsVisible && true == hasCurrentMapType
            && MapType.Town == currentMapType && false == worldPopupDown
            && null != blastFurnaceDatas;
        int count = show ? blastFurnaceDatas.Count : 0;

        for (int i = 0; i < count; i++)
        {
            BlastFurnaceUIData data = blastFurnaceDatas[i];
            if (false == data.unlocked)
            {
                if (i < blastFurnaceStatuses.Count && null != blastFurnaceStatuses[i])
                    blastFurnaceStatuses[i].gameObject.SetActive(false);
                continue;
            }

            if (null == blastFurnaceStatusPrefab || null == uiRoot)
                continue;
            while (blastFurnaceStatuses.Count <= i)
                blastFurnaceStatuses.Add(null);
            if (null == blastFurnaceStatuses[i])
                blastFurnaceStatuses[i] = Instantiate(blastFurnaceStatusPrefab, uiRoot);

            UI_BlastFurnaceStatus status = blastFurnaceStatuses[i];
            if (false == status.gameObject.activeSelf)
                status.gameObject.SetActive(true);
            status.SetData(data);

            Vector3 target = data.position + (Vector3)blastFurnaceStatusOffset;
            target.x = Mathf.Round(target.x * WorldPixelsPerUnit) / WorldPixelsPerUnit;
            target.y = Mathf.Round(target.y * WorldPixelsPerUnit) / WorldPixelsPerUnit;
            status.transform.position = target;
        }

        for (int i = count; i < blastFurnaceStatuses.Count; i++)
        {
            if (null != blastFurnaceStatuses[i])
                blastFurnaceStatuses[i].gameObject.SetActive(false);
        }
    }

    //true -> 제재소 동작중 , false -> 제재소 동작 끝
    public void LogItemProcessorActiveStateChange(bool _boolean)
    {
        isLogProcesserActive = _boolean;
        UpdateLogProcessorVisibility();
    }

    private void UpdateLogProcessorVisibility()
    {
        bool _shouldShow = isLogProcesserActive || isPlayerNearLogProcessor;
        ShowLogProcessor(_shouldShow);
    }

    public void ShowLogProcessor(bool _state)
    {
        bLogProcessorShown = _state;

        if (true == _state)
        {
            if (null != ui_Storage)
            {
                ui_Storage.OnShow();
                ui_Storage.Refresh();
            }

            int activeCount = null != logCutterProvider ? logCutterProvider.ActiveLineCount : 0;
            for (int i = 0; i < activeCount && i < ui_Cutters.Count; i++)
                ui_Cutters[i]?.OnShow();

            ResetLogCutterUIs();
        }
        else
        {
            ui_Storage?.OnHide();

            for (int i = 0; i < ui_Cutters.Count; i++)
                ui_Cutters[i]?.OnHide();
        }
    }

    public void WorldPopupGoDown()
    {
        worldPopupDown = true;
        RefreshBlastFurnaceStatuses();
        if (true == ui_Storage.IsOpen)
            ui_Storage.OnHide();
        if (true == ui_CarStorage.IsOpen)
            ui_CarStorage.OnHide();

        ui_TraderCoin?.OnHide();
    }

    public void WorldPopupGoUp()
    {
        worldPopupDown = false;
        RefreshBlastFurnaceStatuses();
        if (MapType.Town == currentMapType)
            ui_TraderCoin?.OnShow();

        // GoDown으로 강제로 꺼졌던 제재소 UI 상태를 현재 조건에 맞게 복구
        UpdateLogProcessorVisibility();
    }

    public void SetCurrentMapType(MapType _currentMapType, ForestType _currentForestType)
    {
        currentMapType = _currentMapType;
        currentForestType = _currentForestType;
        hasCurrentMapType = true;

        // 마을 진입(TownStartedSignal)은 EnableShopObj 이후에 오므로 커터 위치가 확정된 상태다.
        if (MapType.Town == currentMapType)
            RebindCutterPositions();

        // 용광로 목록은 시그널(BlastFurnaceStateChanged)로만 받는다. 마을이 시작될 때
        // TownSystem이 반드시 한 번 보내주므로 여기서 따로 찾아올 필요가 없다.
        RefreshBlastFurnaceStatuses();
    }
}