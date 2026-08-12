using UnityEngine;
using System;

public class GameplayUIInstaller : MonoBehaviour
{
    public event Action SaveGameEvent;
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;
    private SignalHub signalHub;
    private LocalizationManager localizationManager;

    private GameplayUIManager uiManager;
    private GameplayUICoordinator uICoordinator;
    private IInventory inventory;
    private IInventory container;
    private IInDungeonObjProvider inDungeonObjProvider;
    private ISkillSystemProvider skillSystemProvider;
    private IShopNPC shopNPC;
    private IMoneyData moneyData;
    private IMapDataProvider mapDataProvider;
    private IWeatherProvider weatherProvider;
    private ITimeDataProvider timeDataProvider;
    private IInventory offroadContainer;
    private UIDepthController depthController;
    private IDungeonResultProvider dungeonResultProvider;

    //Canvas

    [Header("UI Canvas/CanvasRoot Objects")]
    [SerializeField] private CanvasRoot canvasRootPrefab;
    [SerializeField] private CanvasRoot screenSpaceCanvasRootPrefab;
    [SerializeField] private CanvasRoot worldCanvasRootPrefab;
    [SerializeField] private CanvasRoot overlayCanvasRootPrefab;
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private Canvas worldCanvasPrefab;
    [SerializeField] private Canvas scrennSpaceCanvasPrefab;
    [SerializeField] private Canvas overlayCanvasPrefab;

    //Gameplay Scene
    private Canvas canvas;
    private Canvas screenSpaceCanvas;
    private Canvas worldCanvas;
    private Canvas overlayCanvas;

    public void Initialize(IBootStrapProvider _bootStrapProvider, SignalHub _signalHub,
        InputManager _inputManager, IInventory _inventory, IInDungeonObjProvider _inDungeonObjProvider, IInventory _container,
        ILogCutter _logCutter, ISkillSystemProvider _skillSystemProvider, IShopNPC _shopNPC,
        IMoneyData _moneyData, LocalizationManager _localizeManager, IMapDataProvider _mapDataProvider,
        IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider, IInventory _offroadContainer,
        IDungeonResultProvider _dungeonResultProvider)
    {
        offroadContainer = _offroadContainer;
        mapDataProvider = _mapDataProvider;
        localizationManager = _localizeManager;
        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;
        signalHub = _signalHub;
        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
        container = _container;
        skillSystemProvider = _skillSystemProvider;
        shopNPC = _shopNPC;
        moneyData = _moneyData;
        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        dungeonResultProvider = _dungeonResultProvider;

        uiManager = GetComponent<GameplayUIManager>();
        depthController = GetComponent<UIDepthController>();
        if (depthController != null)
        {
            depthController.Initialize();
        }
        uICoordinator = new GameplayUICoordinator();

        uiManager.Initialize(inputManager, inventory, inDungeonObjProvider, container, _logCutter, _skillSystemProvider,
         shopNPC, moneyData, localizationManager, mapDataProvider, weatherProvider, timeDataProvider, offroadContainer, depthController,
         dungeonResultProvider);

        SetupUIElement();

        BindEvent();
    }

    public void Release()
    {
        uICoordinator.Release();
        uiManager.ReleaseAllUIView();

        ReleaseDependency();
        ReleaseEvent();
    }

    public void Refresh()
    {
        uICoordinator.Refresh();
    }

    public void SetupUIElement()
    {
        SetupCanvas();

        Transform overlayRoot = Instantiate(canvasRootPrefab.overlayLayerRoot, canvas.transform);

        Transform worldOverlayRoot = Instantiate(worldCanvasRootPrefab.overlayLayerRoot, worldCanvas.transform);

        Transform overlayCanvasRoot = Instantiate(overlayCanvasRootPrefab.overlayLayerRoot, overlayCanvas.transform);

        Transform ppCanvasOverlayRoot = Instantiate(screenSpaceCanvasRootPrefab.overlayLayerRoot, screenSpaceCanvas.transform);

        SetAnchorToCanvas(overlayRoot);
        SetAnchorToCanvas(overlayCanvasRoot);
        SetAnchorToCanvas(ppCanvasOverlayRoot);


        CanvasRoot tempRoot = new CanvasRoot();
        tempRoot.overlayLayerRoot = overlayRoot;

        CanvasRoot worldTempRoot = new CanvasRoot();
        worldTempRoot.overlayLayerRoot = worldOverlayRoot;

        CanvasRoot canvasRoot = new CanvasRoot();
        canvasRoot.overlayLayerRoot = overlayCanvasRoot;

        CanvasRoot ppCanvasRoot = new CanvasRoot();
        ppCanvasRoot.overlayLayerRoot = ppCanvasOverlayRoot;

        uiManager.SceneChanged(tempRoot, worldTempRoot, canvasRoot, ppCanvasRoot);

        OpenUIView();
    }

    public void SetupCanvas()
    {
        if (canvas == null)
            canvas = Instantiate(canvasPrefab, transform);
        if (worldCanvas == null)
            worldCanvas = Instantiate(worldCanvasPrefab, transform);
        if(overlayCanvas == null)
            overlayCanvas = Instantiate(overlayCanvasPrefab, transform);
        if (screenSpaceCanvas == null)
            screenSpaceCanvas = Instantiate(scrennSpaceCanvasPrefab, transform);

        uiManager.DI(screenSpaceCanvas, overlayCanvas);

        var canvasEnabler = canvas.GetComponent<CanvasEnabler>();
        if (canvasEnabler != null)
        {
            canvasEnabler.Initialize();
        }

        var worldCanvasEnabler = worldCanvas.GetComponent<WorldCanvasEnabler>();
        if (worldCanvasEnabler != null)
        {
            worldCanvasEnabler.Initialize();
        }

        var ppCanvasEnabler = screenSpaceCanvas.GetComponent<CanvasEnabler>();
        if (ppCanvasEnabler != null)
        {
            ppCanvasEnabler.Initialize();
        }
    }

    private void OpenUIView()
    {
        // 다른 UIView들이 자신의 Initialize()/SetupUI()에서 viewCtx.cursorBoxUI를 참조할 수 있도록,
        // CursorBox를 가장 먼저 생성해 viewCtx에 등록해둔다.
        UIView_CursorBox cursorBoxUI = uiManager.Open<UIView_CursorBox>();

        UIView_Popup inventoryUI = uiManager.Open<UIView_Popup>();
        inventoryUI.Hide();

        UIView_HUD hudUI = uiManager.Open<UIView_HUD>();

        UIView_Unit unitUI = uiManager.Open<UIView_Unit>();

        UIView_WorldPopup worldPopupUI = uiManager.Open<UIView_WorldPopup>();

        UIView_MenuPopup menuPopupUI = uiManager.Open<UIView_MenuPopup>();
        menuPopupUI.Hide();

        UIView_Tent tentUI = uiManager.Open<UIView_Tent>();
        tentUI.Hide();

        UIView_ESC escUI = uiManager.Open<UIView_ESC>();
        escUI.Hide();

        UIView_SkyProduction skyProductionUI = uiManager.Open<UIView_SkyProduction>();
        // GameplayUIInstaller는 GameInstaller 세션당 한 번만 초기화되므로(Town↔Dungeon 왕복 시 재사용됨),
        // 이 시점은 항상 MainMenu → Town 최초 진입 직후다. 화면이 아직 메인 메뉴에 가려져 있는 동안
        // 애니메이션 없이 "구름이 덮인" 상태로 미리 세팅해두고, StartMainMenuIntro()가 걷히는 연출만 재생하게 한다.
        skyProductionUI.SnapToCoveredState();

        UIView_Result resultUI = uiManager.Open<UIView_Result>();

        UIView_Warning warningUI = uiManager.Open<UIView_Warning>();
        warningUI.Hide();

        UIView_ScreenModal screenModalUI = uiManager.Open<UIView_ScreenModal>();

        // Show/Hide로 토글하지 않고 항상 Show 상태로 유지한다. 실제 연출은 PlayCompanyLogo() 등
        // 별도 함수 호출로 재생한다.
        UIView_OverUIPopup overUIPopupUI = uiManager.Open<UIView_OverUIPopup>();

        uICoordinator.Initialize(signalHub, inputManager, inventoryUI, hudUI, unitUI, worldPopupUI,
        menuPopupUI, tentUI, escUI, depthController, skyProductionUI, resultUI, warningUI, overUIPopupUI, screenModalUI);

        BindEvent();
    }

    private void SetAnchorToCanvas(Transform transform)
    {
        RectTransform rt = transform.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;   // (0, 0)
        rt.anchorMax = Vector2.one;    // (1, 1)

        rt.offsetMin = Vector2.zero;   // Left, Bottom
        rt.offsetMax = Vector2.zero;   // Right, Top
    }

    public void SetupUI()
    {
        BindEvent();
    }

    private void BindEvent()
    {
        uICoordinator.GoToMainMenuEvent -= GoToMainMenu;
        uICoordinator.GoToMainMenuEvent += GoToMainMenu;

        uICoordinator.SaveGameEvent -= SaveGame;
        uICoordinator.SaveGameEvent += SaveGame;
    }

    private void ReleaseEvent()
    {
        uICoordinator.SaveGameEvent -= SaveGame;
        uICoordinator.GoToMainMenuEvent -= GoToMainMenu;
    }

    public void ReleaseDependency()
    {
        uiManager.ReleaseDependency();
    }

    private void GoToMainMenu()
    {
        // 카메라 상승 연출이 끝난 뒤(TownSystem/InDungeonSystem → GoToMainMenuSignal → TeleportManager)에야
        // 실제 씬 전환이 일어나도록, 여기서 bootStrapProvider를 직접 부르지 않고 시그널만 발행한다.
        signalHub.Publish(new GoToMainMenuRequestedSignal());
    }

    private void SaveGame()
    {
        SaveGameEvent?.Invoke();
    }
}
