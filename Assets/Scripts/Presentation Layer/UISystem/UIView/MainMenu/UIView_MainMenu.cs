using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;


public class UIView_MainMenu : UIView
{
    public event Action NewGameButtonClickedEvent;
    public event Action LoadGameButtonClickedEvent;
    public event Action ExitButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private UI_MainMenu mainMenuUI; // 메인 메뉴
    [SerializeField] private UI_PressAnyKey pressAnyKeyUI; // 아무 키나 누르세요 화면
    [SerializeField] private UI_SplashScreen splashScreenUI; // 스플래시 스크린
    [SerializeField] private UI_LogoAnim logoAnimUI; // 로고 애니메이션 객체
    [SerializeField] private UI_MainMenuBackground backgroundUI; // 동적 배경 관리 객체

    [Header("Background Overlay")]
    [SerializeField, Tooltip("메인 메뉴 뒤에 깔릴 검은색 셀로판지(Dimmer)")] 
    private Image backgroundDimmer; 
    [SerializeField] private float dimmerTargetAlpha = 0.3f;
    [SerializeField] private float dimmerFadeDuration = 1f;

    [Header("Debug")]
    [SerializeField, Tooltip("체크하면 에디터 환경에서 스플래시와 로고 연출을 건너뛰고 바로 메인 메뉴를 출력합니다.")]
    private bool skipIntroInEditor = true;

    [Header("Exit(Curtain Rollback) Animation")]
    [SerializeField, Tooltip("MainMenu → Town 진입 시, Town 카메라 인트로와 동시에 메인 메뉴 전체가 위로 이동해 화면 밖으로 사라지는 데 걸리는 시간. " +
        "GameInstaller.prefab의 SkyCameraProductionManager.moveDuration(=1.8)과 동일한 값 — 카메라 하강이 전체 yOffset 거리를 그 시간 동안 내려오므로 여기도 같은 값을 써야 한다. moveDuration이 바뀌면 같이 맞춰야 한다.")]
    private float exitMoveDuration = 1.8f;
    [SerializeField, Tooltip("메인 메뉴가 위로 이동해 사라질 거리(px). 화면 높이보다 넉넉하게 잡는다.")]
    private float exitMoveDistance = 1500f;

    private RectTransform rootRectTransform;

    private UIViewContext context;
    private int mainMenuUIJsonId = 8; // MainMenuUI.json의 ID

    private IMainMenuSaveSystem saveSystem;

    // 게임 플레이 도중 ESC 메뉴를 통해 메인 메뉴로 돌아온 경우 true
    public bool CameFromEscMenu { get; private set; }

    public void DependencyInjection(IMainMenuSaveSystem _saveSystem, bool _cameFromEscMenu)
    {
        saveSystem = _saveSystem;
        CameFromEscMenu = _cameFromEscMenu;

        if (null != mainMenuUI)
        {
            mainMenuUI.UpdateLoadGameButtonState();
        }
    }

    public bool HasSaveData()
    {
        return saveSystem != null && saveSystem.HasSaveData();
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        context = _ctx;

        // 커튼 롤백(위로 슬라이드 아웃) 연출용
        rootRectTransform = GetComponent<RectTransform>();

        // 프리팹을 인스턴스화하지 않고, 이미 바인딩된 컴포넌트를 바로 초기화
        if (null != mainMenuUI)
        {
            mainMenuUI.Initialize(this, _ctx);
        }

        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Initialize(this);
            pressAnyKeyUI.SetText(_ctx.localizationManager.GetText(mainMenuUIJsonId, 99));
        }
        
        if (null != logoAnimUI)
        {
            logoAnimUI.Initialize();
        }

        if (null != backgroundUI)
        {
            backgroundUI.Initialize();
        }
    }

    public override void OnDestroy()
    {
        NewGameButtonClickedEvent = null;
        LoadGameButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
        
        if (backgroundDimmer != null)
        {
            backgroundDimmer.DOKill();
        }

        if (rootRectTransform != null)
        {
            rootRectTransform.DOKill();
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        // 초기화 시 딤 처리 초기화 (투명하게 숨김)
        if (backgroundDimmer != null)
        {
            Color c = backgroundDimmer.color;
            c.a = 0f;
            backgroundDimmer.color = c;
            backgroundDimmer.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        if (this.skipIntroInEditor)
        {
            if (null != this.splashScreenUI) this.splashScreenUI.gameObject.SetActive(false);
            if (null != this.pressAnyKeyUI) this.pressAnyKeyUI.Hide();
            
            this.ShowDimmer();

            if (null != this.logoAnimUI)
            {
                this.logoAnimUI.gameObject.SetActive(true);
                this.logoAnimUI.PlayRevealSequence(this.ShowMainMenu);
            }
            else
            {
                this.ShowMainMenu();
            }
            return;
        }
#endif

        if (null != this.splashScreenUI)
        {
            this.splashScreenUI.gameObject.SetActive(true);
            if (null != this.pressAnyKeyUI) this.pressAnyKeyUI.Hide();
            if (null != this.mainMenuUI) this.mainMenuUI.gameObject.SetActive(false);
            if (null != this.logoAnimUI) this.logoAnimUI.gameObject.SetActive(false);

            // 마지막 페이드아웃 직전에 UI를 미리 켜고, 완료 시 스플래시 자체를 끕니다.
            this.splashScreenUI.PlaySequence(this.OnSplashScreenCompleted, this.PrepareNextUIAfterSplash);
        }
        else
        {
            this.PrepareNextUIAfterSplash();
            this.OnSplashScreenCompleted();
        }
    }

    private void PrepareNextUIAfterSplash()
    {
        // 시작 시 분기: Press Any Key 화면이 있으면 먼저 띄우고 메인 메뉴 숨김
        if (null != this.pressAnyKeyUI)
        {
            this.pressAnyKeyUI.Show();
            if (null != this.mainMenuUI) this.mainMenuUI.gameObject.SetActive(false);
            if (null != this.logoAnimUI) this.logoAnimUI.gameObject.SetActive(true); // 로고는 항상 먼저 보여야 함
        }
        else
        {
            this.ShowDimmer(); // 로고 애니메이션(또는 메인메뉴) 시작 시 딤 처리 실행

            if (null != this.logoAnimUI)
            {
                this.logoAnimUI.gameObject.SetActive(true);
            }
        }
    }

    private void OnSplashScreenCompleted()
    {
        if (null != this.splashScreenUI)
        {
            this.splashScreenUI.gameObject.SetActive(false);
        }

        if (null == this.pressAnyKeyUI)
        {
            if (null != this.logoAnimUI)
            {
                this.logoAnimUI.PlayRevealSequence(this.ShowMainMenu);
            }
            else
            {
                this.ShowMainMenu();
            }
        }
    }

    public void OnChangedLanguage()
    {
        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.SetText(context.localizationManager.GetText(mainMenuUIJsonId, 99));
        }

        if (null != mainMenuUI)
        {
            mainMenuUI.SetLocalization();
        }
    }

    /// <summary>
    /// UI_PressAnyKey에서 아무 키 입력이 감지되었을 때 호출됩니다.
    /// </summary>
    public void OnPressAnyKeyCompleted()
    {
        if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
        
        ShowDimmer(); // 로고 애니메이션(또는 메인메뉴) 시작 시 딤 처리 실행

        if (null != logoAnimUI)
        {
            // 로고 애니메이션 실행 후 끝나는 시점에 메인 메뉴 노출
            logoAnimUI.PlayRevealSequence(ShowMainMenu);
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void ShowDimmer()
    {
        if (null != backgroundDimmer)
        {
            backgroundDimmer.gameObject.SetActive(true);
            
            Color color = backgroundDimmer.color;
            color.a = 0f;
            backgroundDimmer.color = color;
            
            backgroundDimmer.DOFade(dimmerTargetAlpha, dimmerFadeDuration);
        }
    }

    private void ShowMainMenu()
    {
        if (null != mainMenuUI) 
        {
            mainMenuUI.gameObject.SetActive(true);
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    public void OnNewGameStartButton()
    {
        // Town 씬이 뒤에서 셋업을 마치고 카메라 인트로를 시작할 때까지 메인 메뉴는 화면에 그대로 떠 있어야 하므로,
        // 페이드 없이 즉시 이벤트를 발행한다. 실제 퇴장 연출은 PlayExitAnimation()에서 별도로 트리거된다.
        NewGameButtonClickedEvent?.Invoke();
    }

    public void OnLoadGameButtonClicked()
    {
        LoadGameButtonClickedEvent?.Invoke();
    }

    /// <summary>
    /// Town 카메라 인트로 연출이 시작되는 시점에 맞춰, 메인 메뉴 전체가 위로 이동해 화면 밖으로 빠져나가는 연출을 재생한다.
    /// 페이드가 아니라 카메라가 구름을 뚫고 내려가는 동안 메뉴가 자연스럽게 시야 위쪽으로 벗어나는 느낌을 준다.
    /// </summary>
    public void PlayExitAnimation(Action _onComplete)
    {
        if (rootRectTransform == null)
        {
            Hide();
            _onComplete?.Invoke();
            return;
        }

        rootRectTransform.DOKill();

        Vector2 targetPos = rootRectTransform.anchoredPosition + Vector2.up * exitMoveDistance;

        rootRectTransform.DOAnchorPos(targetPos, exitMoveDuration).SetEase(Ease.InCubic).OnComplete(() =>
        {
            Hide();
            _onComplete?.Invoke();
        });
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
