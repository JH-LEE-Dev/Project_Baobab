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


    //Canvas

    [Header("UI Canvas/CanvasRoot Objects")]
    [SerializeField] private CanvasRoot canvasRootPrefab;
    [SerializeField] private CanvasRoot worldCanvasRootPrefab;
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private Canvas worldCanvasPrefab;

    //Gameplay Scene
    private CanvasRoot canvasRoot;
    private CanvasRoot worldCanvasRoot;
    private Canvas canvas;
    private Canvas worldCanvas;

    public void Initialize(IBootStrapProvider _bootStrapProvider, SignalHub _signalHub,
        InputManager _inputManager, IInventory _inventory, IInDungeonObjProvider _inDungeonObjProvider, IInventory _container,
        ILogCutter _logCutter, ISkillSystemProvider _skillSystemProvider, IShopNPC _shopNPC,
        IMoneyData _moneyData, LocalizationManager _localizeManager)
    {
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

        uiManager = GetComponent<GameplayUIManager>();
        uICoordinator = new GameplayUICoordinator();

        uiManager.Initialize(inputManager, inventory, inDungeonObjProvider, container, _logCutter, _skillSystemProvider, shopNPC, moneyData, localizationManager);

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

    public void SetupUIElement()
    {
        SetupCanvas();

        Transform overlayRoot = Instantiate(canvasRootPrefab.overlayLayerRoot, canvas.transform);
        //Transform screenLayerRoot = Instantiate(gameplayLevelRoots_Prefab.screenLayerRoot, canvas_GamplayScene.transform);
        //Transform tooltipLayerRoot = Instantiate(gameplayLevelRoots_Prefab.tooltipLayerRoot, canvas_GamplayScene.transform);

        Transform worldOverlayRoot = Instantiate(worldCanvasRootPrefab.overlayLayerRoot, worldCanvas.transform);

        SetAnchorToCanvas(overlayRoot);

        CanvasRoot tempRoot = new CanvasRoot();
        tempRoot.overlayLayerRoot = overlayRoot;

        CanvasRoot worldTempRoot = new CanvasRoot();
        worldTempRoot.overlayLayerRoot = worldOverlayRoot;

        uiManager.SceneChanged(tempRoot, worldTempRoot);

        OpenUIView();
    }

    public void SetupCanvas()
    {
        if (canvas == null)
            canvas = Instantiate(canvasPrefab, transform);
        if (worldCanvas == null)
            worldCanvas = Instantiate(worldCanvasPrefab, transform);

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

        UIView_Tent tentUI = uiManager.Open<UIView_Tent>();

        UIView_ESC escUI = uiManager.Open<UIView_ESC>();
        escUI.Hide();

        uICoordinator.Initialize(signalHub, inputManager, inventoryUI, hudUI, unitUI, worldPopupUI,
        menuPopupUI, tentUI, escUI);

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
