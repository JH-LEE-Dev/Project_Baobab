using System;
using UnityEngine;

public class MainMenuUIInstaller : MonoBehaviour
{
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;
    private LocalizationManager localizationManager;

    private MainMenuUIManager uiManager;
    private UIDepthController depthController;

    // mainMenuInstaller는 DontDestroyOnLoad로 절대 파괴되지 않고 Town/Dungeon 씬에서도 계속 살아있으므로,
    // ESCButtonPressedEvent 구독도 평생 유지된다. Main Menu가 화면에 없는 동안(Town/Dungeon 진입 중)
    // ESC를 눌러도 이 뎁스 스택에는 반응하지 않도록 "지금 Main Menu가 실제로 보이는 화면인지"를 직접 추적한다.
    private bool isMainMenuActive = false;

    [Header("UI Canvas/CanvasRoot Objects")]
    [SerializeField] private CanvasRoot canvasRootPrefab;
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private CanvasRoot overlayCanvasRootPrefab;
    [SerializeField] private Canvas overlayCanvasPrefab;

    //Gameplay Scene
    private CanvasRoot canvasRoot;
    private Canvas canvas;
    private Canvas overlayCanvas;

    // SetupUILayout()이 MainMenuReturned()마다 재호출되므로, 레이어 루트를 매번 새로 Instantiate하면
    // 이전에 생성된 루트가 파괴되지 않고 고아 오브젝트로 계속 쌓인다. 이미 캐시된 UIView(CursorBox,
    // MainMenu 등)는 Open<T>() 재호출 시 Initialize()가 다시 불리지 않아 새 루트로 재배치되지도 않으므로,
    // 루트는 최초 1회만 생성하고 이후에는 재사용한다.
    private Transform overlayRoot;
    private Transform overlayCanvasOverlayRoot;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager, IMainMenuSaveSystem _saveSystem)
    {
        bootStrapProvider = _bootStrapProvider;
        inputManager = _inputManager;
        localizationManager = _localizeManager;
        uiManager = GetComponent<MainMenuUIManager>();

        // GameplayUIInstaller와 동일한 구조: UIDepthController를 통해 ESC로 UI 뎁스(옵션 창 등)를
        // 순서대로 빠져나올 수 있게 한다. 메인메뉴 프리팹에는 아직 컴포넌트가 없을 수 있어 없으면 추가한다.
        depthController = GetComponent<UIDepthController>();
        if (null == depthController)
        {
            depthController = gameObject.AddComponent<UIDepthController>();
        }
        depthController.Initialize();

        uiManager.Initialize(inputManager, localizationManager, depthController, _saveSystem);

        // 최초 부팅 시 Main Menu가 바로 화면에 보이는 시점이므로 여기서 활성화한다.
        // (이후 Town/Dungeon ↔ Main Menu 왕복은 PlayExitAnimation/PlayEnterAnimation이 갱신한다.)
        isMainMenuActive = true;

        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        inputManager.inputReader.ESCButtonPressedEvent += EscButtonPressed;
    }

    private void EscButtonPressed()
    {
        if (false == isMainMenuActive) return;

        depthController?.TryCloseTopView();
    }

    public void PlayExitAnimation(Action _onComplete)
    {
        // Town/Dungeon으로 떠나는 시작 시점이므로, 그 동안(연출 중 포함) ESC는 이 화면의 뎁스 스택에
        // 반응하지 않게 막는다. 실제 GameplayUICoordinator의 ESC 처리와 중복 반응하는 것을 방지한다.
        isMainMenuActive = false;

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
        // Main Menu로 복귀하는 시작 시점이므로 다시 활성화한다.
        isMainMenuActive = true;

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
        SetupUILayout();
        OpenUIView();
        SetupCanvasChilds();
    }

    /// <summary>
    /// Town/Dungeon에서 메인 메뉴로 복귀할 때 호출한다.
    /// Canvas 인스턴스는 DontDestroyOnLoad로 살아있으므로 재생성하지 않고,
    /// UIView 계층(SceneChanged + OpenUIView)만 재구성한다.
    /// </summary>
    public void MainMenuReturned()
    {
        // SceneChanged → CloseAll → bVisible 리셋 → Open<T>에서 Initialize 재실행
        // 순서로 UIView_CursorBox를 포함한 모든 UIView를 올바른 Canvas에 다시 배치한다.
        SetupUILayout();
        OpenUIView();
    }

    private void SetupUILayout()
    {
        // 최초 1회만 레이어 루트를 생성한다. canvas/overlayCanvas는 DontDestroyOnLoad로 계속 살아있으므로
        // MainMenuReturned()에서 다시 호출되어도 새 루트를 만들 필요가 없다(만들면 이전 루트가 고아로 남는다).
        if (null == overlayRoot)
        {
            overlayRoot = Instantiate(canvasRootPrefab.overlayLayerRoot, canvas.transform);
            //Transform popupLayerRoot = Instantiate(canvasRootPrefab.popupLayerRoot, canvas.transform);
            //Transform screenLayerRoot = Instantiate(canvasRootPrefab.screenLayerRoot, canvas.transform);
            //Transform tooltipLayerRoot = Instantiate(canvasRootPrefab.tooltipLayerRoot, canvas.transform);
            SetAnchorToCanvas(overlayRoot);
        }

        if (null == overlayCanvasOverlayRoot)
        {
            overlayCanvasOverlayRoot = Instantiate(overlayCanvasRootPrefab.overlayLayerRoot, overlayCanvas.transform);
            SetAnchorToCanvas(overlayCanvasOverlayRoot);
        }
        //SetAnchorToCanvas(popupLayerRoot);

        CanvasRoot tempRoot = new CanvasRoot();
        tempRoot.overlayLayerRoot = overlayRoot;
        //tempRoot.popupLayerRoot = popupLayerRoot;

        CanvasRoot overlayCanvasRoot = new CanvasRoot();
        overlayCanvasRoot.overlayLayerRoot = overlayCanvasOverlayRoot;

        // GameplayUIManager.GetLayerRoot()와 동일하게, bOverlay UIView(예: UIView_CursorBox)는
        // 3번째 인자(overlayCanvasRoot)의 레이어 루트를 사용한다.
        uiManager.SceneChanged(tempRoot, default, overlayCanvasRoot, default);
    }

    public void SetupCanvas()
    {
        // 부모 없이 Instantiate하면 MainMenuScene 소속 루트 오브젝트가 되어, mainMenuInstaller의
        // DontDestroyOnLoad가 적용되지 않고 Town 씬 로드 시 그대로 파괴돼버린다(GameplayUIInstaller.SetupCanvas()와 동일 패턴으로 맞춤).
        canvas = Instantiate(canvasPrefab, transform);
        overlayCanvas = Instantiate(overlayCanvasPrefab, transform);

        uiManager.DI(null, overlayCanvas);
    }

    private void SetupCanvasChilds()
    {

    }

    private void OpenUIView()
    {
        // 다른 UIView들이 자신의 Initialize()/SetupUI()에서 viewCtx.cursorBoxUI를 참조할 수 있도록,
        // CursorBox를 가장 먼저 생성해 viewCtx에 등록해둔다.
        UIView_CursorBox cursorBoxUI = uiManager.Open<UIView_CursorBox>();

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
