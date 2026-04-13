using UnityEngine;

public class GameplayUIInstaller : MonoBehaviour
{
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;
    private SignalHub signalHub;

    private GameplayUIManager uiManager;
    private GameplayUICoordinator uICoordinator;
    private IInventory inventory;
    private IInventory container;
    private IInDungeonObjProvider inDungeonObjProvider;
    private ISkillSystemProvider skillSystemProvider;


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
        ILogCutter _logCutter, ISkillSystemProvider _skillSystemProvider)
    {
        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;
        signalHub = _signalHub;
        inventory = _inventory;
        inDungeonObjProvider = _inDungeonObjProvider;
        container = _container;
        skillSystemProvider = _skillSystemProvider;

        uiManager = GetComponent<GameplayUIManager>();
        uICoordinator = new GameplayUICoordinator();

        uiManager.Initialize(inputManager, inventory, inDungeonObjProvider, container, _logCutter, _skillSystemProvider);

        SetupUIElement();
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

        uICoordinator.Initialize(signalHub, inputManager, inventoryUI, hudUI, unitUI, worldPopupUI,
        menuPopupUI, tentUI);

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

    }

    private void ReleaseEvent()
    {

    }

    public void ReleaseDependency()
    {
        uiManager.ReleaseDependency();
    }
}
