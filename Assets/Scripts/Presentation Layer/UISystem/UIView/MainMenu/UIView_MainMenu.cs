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

    [Header("Sub Views")]
    [SerializeField] private UI_Option optionUI; // 공용 옵션 UI

    [Header("Background Overlay")]
    [SerializeField, Tooltip("메인 메뉴 뒤에 깔릴 검은색 셀로판지(Dimmer)")] 
    private Image backgroundDimmer; 
    [SerializeField] private float dimmerTargetAlpha = 0.3f;
    [SerializeField] private float dimmerFadeDuration = 1f;

    [Header("Debug")]
    [SerializeField, Tooltip("체크하면 에디터 환경에서 스플래시와 로고 연출을 건너뛰고 바로 메인 메뉴를 출력합니다.")]
    private bool skipIntroInEditor = true;

    [Header("Exit Animation")]
    [SerializeField] private float exitMoveDuration = 1.8f;
    // SkyProduction.prefab의 cloudImage 이동 거리(410 → -1890)와 동일한 2300으로 맞춤
    [SerializeField] private float exitMoveDistance = 2300f;
    // GameInstaller.prefab의 SkyCameraProductionManager.moveEase 직렬화 값(10)과 동일
    [SerializeField] private Ease exitMoveEase = (Ease)10;

    private RectTransform rootRectTransform;
    private Vector2 restAnchoredPosition;

    private UIViewContext context;
    private int mainMenuUIJsonId = 8; // MainMenuUI.json의 ID

    private IMainMenuSaveSystem saveSystem;

    public void DependencyInjection(IMainMenuSaveSystem _saveSystem)
    {
        saveSystem = _saveSystem;

        if (null != mainMenuUI)
        {
            mainMenuUI.UpdateLoadGameButtonState();
        }
    }

    public bool HasSaveData()
    {
        return null != saveSystem && saveSystem.HasSaveData();
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        context = _ctx;

        rootRectTransform = GetComponent<RectTransform>();
        if (null != rootRectTransform)
        {
            restAnchoredPosition = rootRectTransform.anchoredPosition;
        }

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
        
        if (null != backgroundDimmer)
        {
            backgroundDimmer.DOKill();
        }

        if (null != rootRectTransform)
        {
            rootRectTransform.DOKill();
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        // 초기화 시 딤 처리 초기화 (투명하게 숨김)
        if (null != backgroundDimmer)
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
            if (null != this.splashScreenUI) this.splashScreenUI.gameObject.SetActive(false);
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
            mainMenuUI.ResetAndShowButtons();
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    private TweenCallback invokeNewGameEventCallback;
    private TweenCallback invokeLoadGameEventCallback;

    public void OnNewGameStartButton()
    {
        if (null == invokeNewGameEventCallback) invokeNewGameEventCallback = InvokeNewGameEvent;
        PlayGameStartSequence(invokeNewGameEventCallback);
    }

    public void OnLoadGameButtonClicked()
    {
        if (null == invokeLoadGameEventCallback) invokeLoadGameEventCallback = InvokeLoadGameEvent;
        PlayGameStartSequence(invokeLoadGameEventCallback);
    }

    private void PlayGameStartSequence(TweenCallback _onSequenceCompleted)
    {
        if (null != mainMenuUI)
        {
            mainMenuUI.gameObject.SetActive(false);
        }

        Sequence _seq = DOTween.Sequence();
        
        if (null != backgroundDimmer)
        {
            _seq.Append(backgroundDimmer.DOFade(0f, dimmerFadeDuration));
        }

        if (null != logoAnimUI)
        {
            CanvasGroup _logoCanvas = logoAnimUI.GetComponent<CanvasGroup>();
            if (null != _logoCanvas)
            {
                _seq.Append(_logoCanvas.DOFade(0f, 0.5f));
            }
        }

        if (null != _onSequenceCompleted)
        {
            _seq.OnComplete(_onSequenceCompleted);
        }
    }

    private Action onOptionUIClosedCallback;

    public void OnOptionButtonClicked()
    {
        if (null == onOptionUIClosedCallback) onOptionUIClosedCallback = OnOptionUIClosed;

        if (null != optionUI)
        {
            optionUI.Show(onOptionUIClosedCallback);
        }
    }

    private void OnOptionUIClosed()
    {
        if (null != mainMenuUI)
        {
            mainMenuUI.ReleaseOptionButtonState();
        }
    }

    private void InvokeNewGameEvent()
    {
        NewGameButtonClickedEvent?.Invoke();
    }

    private void InvokeLoadGameEvent()
    {
        LoadGameButtonClickedEvent?.Invoke();
    }

    private Action currentExitCompleteAction;
    private TweenCallback onExitAnimationCompleteCallback;

    public void PlayExitAnimation(Action _onComplete)
    {
        if (null == rootRectTransform)
        {
            Hide();
            _onComplete?.Invoke();
            return;
        }

        if (null == onExitAnimationCompleteCallback)
            onExitAnimationCompleteCallback = OnExitAnimationComplete;

        currentExitCompleteAction = _onComplete;
        rootRectTransform.DOKill();

        Vector2 _targetPos = rootRectTransform.anchoredPosition + Vector2.up * exitMoveDistance;

        rootRectTransform.DOAnchorPos(_targetPos, exitMoveDuration)
            .SetEase(exitMoveEase)
            .OnComplete(onExitAnimationCompleteCallback);
    }

    private void OnExitAnimationComplete()
    {
        // 파괴하지 않고 화면 밖(위)에 그대로 둔다 - 다음에 PlayEnterAnimation()으로 같은 인스턴스를 재사용한다.
        currentExitCompleteAction?.Invoke();
        currentExitCompleteAction = null;
    }

    private Action currentEnterCompleteAction;
    private TweenCallback onEnterAnimationCompleteCallback;

    // PlayExitAnimation()의 반대 방향: 화면 밖(위)에서 원래 위치로 슬라이드 인 (버튼/딤머/로고는 아직 안 건드림)
    public void PlayEnterAnimation(Action _onComplete)
    {
        if (null == rootRectTransform)
        {
            _onComplete?.Invoke();
            return;
        }

        if (null == onEnterAnimationCompleteCallback)
            onEnterAnimationCompleteCallback = OnEnterAnimationComplete;

        currentEnterCompleteAction = _onComplete;
        rootRectTransform.DOKill();

        rootRectTransform.DOAnchorPos(restAnchoredPosition, exitMoveDuration)
            .SetEase(exitMoveEase)
            .OnComplete(onEnterAnimationCompleteCallback);
    }

    private void OnEnterAnimationComplete()
    {
        currentEnterCompleteAction?.Invoke();
        currentEnterCompleteAction = null;
    }

    // PlayGameStartSequence()의 반대 방향: 씬 진입 후 스플래시 연출(팀 로고) 직후의 연출부터 다시 시작합니다.
    public void PlayButtonsRevealAnimation(Action _onComplete = null)
    {
        // 1. PlayGameStartSequence에서 페이드 아웃 시켰던 상태를 리셋합니다.
        if (null != backgroundDimmer)
        {
            backgroundDimmer.DOKill();
            Color color = backgroundDimmer.color;
            color.a = 0f;
            backgroundDimmer.color = color;
            backgroundDimmer.gameObject.SetActive(false);
        }

        if (null != logoAnimUI)
        {
            logoAnimUI.ResetToInitialState();
            
            CanvasGroup _logoCanvas = logoAnimUI.GetComponent<CanvasGroup>();
            if (null != _logoCanvas)
            {
                _logoCanvas.DOKill();
                _logoCanvas.alpha = 1f;
            }
        }

        // 2. 스플래시 스크린(팀 로고) 이후의 초기 시작 연출을 그대로 다시 트리거합니다.
        PrepareNextUIAfterSplash();

        if (null == this.pressAnyKeyUI)
        {
            if (null != this.logoAnimUI)
            {
                this.logoAnimUI.PlayRevealSequence(() =>
                {
                    ShowMainMenu();
                    _onComplete?.Invoke();
                });
            }
            else
            {
                ShowMainMenu();
                _onComplete?.Invoke();
            }
        }
        else
        {
            // pressAnyKeyUI가 활성화되면 사용자가 키를 입력할 때 OnPressAnyKeyCompleted()에서 
            // logoAnimUI.PlayRevealSequence(ShowMainMenu)가 진행되므로 여기선 콜백만 호출합니다.
            _onComplete?.Invoke();
        }
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
