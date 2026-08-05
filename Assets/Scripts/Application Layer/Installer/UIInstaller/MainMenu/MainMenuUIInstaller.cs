using System;
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

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager, IMainMenuSaveSystem _saveSystem)
    {
        bootStrapProvider = _bootStrapProvider;
        inputManager = _inputManager;
        localizationManager = _localizeManager;
        uiManager = GetComponent<MainMenuUIManager>();

        uiManager.Initialize(inputManager, localizationManager, null, _saveSystem);
    }

    public void PlayExitAnimation(Action _onComplete)
    {
        UIView_MainMenu mainMenuUIView = uiManager.GetView<UIView_MainMenu>();

        if (mainMenuUIView != null)
        {
            mainMenuUIView.PlayExitAnimation(_onComplete);
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    public void PlayEnterAnimation(Action _onComplete)
    {
        UIView_MainMenu mainMenuUIView = uiManager.GetView<UIView_MainMenu>();

        if (mainMenuUIView != null)
        {
            mainMenuUIView.PlayEnterAnimation(_onComplete);
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    public void PlayButtonsRevealAnimation(Action _onComplete = null)
    {
        UIView_MainMenu mainMenuUIView = uiManager.GetView<UIView_MainMenu>();

        if (mainMenuUIView != null)
        {
            mainMenuUIView.PlayButtonsRevealAnimation(_onComplete);
        }
        else
        {
            _onComplete?.Invoke();
        }
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
        uiManager.SceneChanged(tempRoot, default, default, default);

        OpenUIView();
        SetupCanvasChilds();
    }

    public void SetupCanvas()
    {
        // 부모 없이 Instantiate하면 MainMenuScene 소속 루트 오브젝트가 되어, mainMenuInstaller의
        // DontDestroyOnLoad가 적용되지 않고 Town 씬 로드 시 그대로 파괴돼버린다(GameplayUIInstaller.SetupCanvas()와 동일 패턴으로 맞춤).
        canvas = Instantiate(canvasPrefab, transform);
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

    private void NewGameStart()
    {
        bootStrapProvider.GoToDungeonFromMainMenu();
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
