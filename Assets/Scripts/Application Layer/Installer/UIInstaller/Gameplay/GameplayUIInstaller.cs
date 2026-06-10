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
    [SerializeField] private CanvasRoot ppCanvasRootPrefab;
    [SerializeField] private CanvasRoot worldCanvasRootPrefab;
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private Canvas worldCanvasPrefab;
    [SerializeField] private Canvas ppCanvasPrefab;

    //Gameplay Scene
    private Canvas canvas;
    private Canvas ppCanvas;
    private Canvas worldCanvas;

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
         dungeonResultProvider, ppCanvas);

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

        Transform ppOverlayRoot = Instantiate(ppCanvasRootPrefab.overlayLayerRoot, ppCanvas.transform);

        SetAnchorToCanvas(ppOverlayRoot);
        SetAnchorToCanvas(overlayRoot);

        CanvasRoot tempRoot = new CanvasRoot();
        tempRoot.overlayLayerRoot = overlayRoot;

        CanvasRoot worldTempRoot = new CanvasRoot();
        worldTempRoot.overlayLayerRoot = worldOverlayRoot;

        CanvasRoot ppTempRoot = new CanvasRoot();
        ppTempRoot.overlayLayerRoot = ppOverlayRoot;

        uiManager.SceneChanged(tempRoot, worldTempRoot, ppTempRoot);

        OpenUIView();
    }

    public void SetupCanvas()
    {
        if (canvas == null)
            canvas = Instantiate(canvasPrefab, transform);
        if (worldCanvas == null)
            worldCanvas = Instantiate(worldCanvasPrefab, transform);
        if (ppCanvas == null)
            ppCanvas = Instantiate(ppCanvasPrefab, transform);

        var CanvasEnabler = canvas.GetComponent<CanvasEnabler>();
        if (CanvasEnabler != null)
        {
            //CanvasEnabler.Initialize();
        }

        var worldCanvasEnabler = worldCanvas.GetComponent<WorldCanvasEnabler>();
        if (worldCanvasEnabler != null)
        {
            worldCanvasEnabler.Initialize();
        }
    }

    private void OpenUIView()
    {
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

        UIView_Result resultUI = uiManager.Open<UIView_Result>();

        UIView_Warning warningUI = uiManager.Open<UIView_Warning>();
        warningUI.Hide();

        uICoordinator.Initialize(signalHub, inputManager, inventoryUI, hudUI, unitUI, worldPopupUI,
        menuPopupUI, tentUI, escUI, depthController, skyProductionUI, resultUI, warningUI);

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
        bootStrapProvider.GoToMainMenuScene();
    }

    private void SaveGame()
    {
        SaveGameEvent?.Invoke();
    }
}
