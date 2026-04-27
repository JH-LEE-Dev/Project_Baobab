using UnityEngine;

public class MainMenuUIInstaller : MonoBehaviour
{
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;
    private LocalizationManager localizationManager;

    private MainMenuUIManager uiManager;

    [Header("UI Canvas/CanvasRoot Objects")]
    [SerializeField] private CanvasRoot canvasRootPrefab;
    [SerializeField] private Canvas canvasPrefab;

    //Gameplay Scene
    private CanvasRoot canvasRoot;
    private Canvas canvas;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager)
    {
        bootStrapProvider = _bootStrapProvider;
        inputManager = _inputManager;
        uiManager = GetComponent<MainMenuUIManager>();

        uiManager.Initialize(inputManager, localizationManager);
    }

    public void Release()
    {
        ReleaseEvent();
    }

    public void MainMenuLevelStarted()
    {
        SetupCanvas();

        Transform overlayRoot = Instantiate(canvasRootPrefab.overlayLayerRoot, canvas.transform);
        //Transform popupLayerRoot = Instantiate(canvasRootPrefab.popupLayerRoot, canvas.transform);
        //Transform screenLayerRoot = Instantiate(canvasRootPrefab.screenLayerRoot, canvas.transform);
        //Transform tooltipLayerRoot = Instantiate(canvasRootPrefab.tooltipLayerRoot, canvas.transform);

        SetAnchorToCanvas(overlayRoot);
        //SetAnchorToCanvas(popupLayerRoot);

        CanvasRoot tempRoot = new CanvasRoot();
        tempRoot.overlayLayerRoot = overlayRoot;
        //tempRoot.popupLayerRoot = popupLayerRoot;
        uiManager.SceneChanged(tempRoot,default);

        OpenUIView();
        SetupCanvasChilds();
    }

    public void SetupCanvas()
    {
        canvas = Instantiate(canvasPrefab);
    }

    private void SetupCanvasChilds()
    {

    }

    private void OpenUIView()
    {
        UIView_MainMenu mainMenuUIView = uiManager.Open<UIView_MainMenu>();

        BindEvent();
    }

    private void BindEvent()
    {
        UIView_MainMenu mainMenuUIView = uiManager.GetView<UIView_MainMenu>();

        if (mainMenuUIView != null)
        {
            mainMenuUIView.NewGameButtonClickedEvent -= NewGameStart;
            mainMenuUIView.NewGameButtonClickedEvent += NewGameStart;

            mainMenuUIView.LoadGameButtonClickedEvent -= LoadGame;
            mainMenuUIView.LoadGameButtonClickedEvent += LoadGame;

            mainMenuUIView.ExitButtonClickedEvent -= ExitGame;
            mainMenuUIView.ExitButtonClickedEvent += ExitGame;
        }
    }

    private void SetAnchorToCanvas(Transform transform)
    {
        RectTransform rt = transform.GetComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;   // (0, 0)
        rt.anchorMax = Vector2.one;    // (1, 1)

        rt.offsetMin = Vector2.zero;   // Left, Bottom
        rt.offsetMax = Vector2.zero;   // Right, Top
    }

    public void ReleaseEvent()
    {
        UIView_MainMenu mainMenuUIView = uiManager.Open<UIView_MainMenu>();

        mainMenuUIView.NewGameButtonClickedEvent -= NewGameStart;
        mainMenuUIView.LoadGameButtonClickedEvent -= LoadGame;
        mainMenuUIView.ExitButtonClickedEvent -= ExitGame;
    }

    private void NewGameStart()
    {
        bootStrapProvider.GoToTownScene(true);
    }

    private void LoadGame()
    {
        bootStrapProvider.GoToTownScene(false);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
