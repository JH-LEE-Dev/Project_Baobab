using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;


public class UIView_MainMenu : UIView
{
    public event Action NewGameButtonClickedEvent;
    public event Action LoadGameButtonClickedEvent;
    public event Action ExitButtonClickedEvent;
    public event Action<EOptionLanguage> OnLanguageOptionChangedEvent;

    [Header("UI References")]
    [SerializeField] private UI_MainMenu mainMenuUI; // 메인 메뉴
    [SerializeField] private UI_PressAnyKey pressAnyKeyUI; // 아무 키나 누르세요 화면
    [SerializeField] private UI_SplashScreen splashScreenUI; // 스플래시 스크린
    [SerializeField] private UI_LogoAnim logoAnimUI; // 로고 애니메이션 객체
    [SerializeField] private UI_MainMenuBackground backgroundUI; // 동적 배경 관리 객체
    [SerializeField] private CanvasGroup otherCanvasGroup; // 게임 버전 등 기타 UI 최상위 캔버스 그룹
    [SerializeField] private UI_ExternalLinkButton discordButton; // 디스코드 바로가기 버튼

    [Header("Sub Views")]
    [SerializeField] private UI_Option optionUI; // 공용 옵션 UI
    [SerializeField] private UI_Credit creditUI; // 크레딧 UI
    [SerializeField] private UI_InitialSetupPopup initialSetupPopup; // 초기 언어 및 약관 동의 팝업

    [Header("Background Overlay")]
    [SerializeField, Tooltip("메인 메뉴 뒤에 깔릴 검은색 셀로판지(Dimmer)")] 
    private Image backgroundDimmer; 
    [SerializeField] private float dimmerTargetAlpha = 0.3f;
    [SerializeField] private float dimmerFadeDuration = 1f;

    [Header("Debug")]
    [SerializeField, Tooltip("체크하면 에디터 환경에서 스플래시와 로고 연출을 건너뛰고 바로 메인 메뉴를 출력합니다.")]
    private bool skipIntroInEditor = true;

    // Town/Dungeon에서 메인메뉴로 복귀할 때 MainMenuReturned()가 CloseAll()로 bVisible을 리셋시켜
    // OnShow()가 다시 호출된다. 스플래시(팀 로고)는 앱 부팅 후 최초 1회만 재생되어야 하므로 추적한다.
    private bool hasIntroPlayed = false;

    [Header("Exit Animation")]
    [SerializeField] private float exitMoveDuration = 1.8f;
    // MainMenu 모드에서 꺼지는 SkyProduction.prefab의 skyImage 이동 거리(635 → 135)와 동일한 500으로 맞춤
    // (MainMenu가 Sky의 자리를 대신하므로 cloudImage가 아닌 skyImage 기준)
    [SerializeField] private float exitMoveDistance = 500f;
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

        if (null == discordButton && null != otherCanvasGroup)
        {
            discordButton = otherCanvasGroup.GetComponentInChildren<UI_ExternalLinkButton>(true);
        }

        if (null != discordButton)
        {
            discordButton.SetCursorBoxUI(_ctx?.cursorBoxUI, _ctx?.inputManager);
        }

        // 프리팹을 인스턴스화하지 않고, 이미 바인딩된 컴포넌트를 바로 초기화
        if (null != mainMenuUI)
        {
            mainMenuUI.Initialize(this, _ctx);
            if (null != discordButton)
            {
                mainMenuUI.SetDiscordButton(discordButton);
            }
        }

        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Initialize(this, _ctx?.inputManager);
            pressAnyKeyUI.SetText(_ctx.localizationManager.GetText(mainMenuUIJsonId, 99));
        }
        
        if (null != logoAnimUI)
        {
            logoAnimUI.Initialize();
        }

        if (null != backgroundUI)
        {
            backgroundUI.Initialize(_ctx?.inputManager);
        }

        if (null != _ctx && null != _ctx.localizationManager)
        {
            _ctx.localizationManager.OnLanguageChanged -= OnChangedLanguage;
            _ctx.localizationManager.OnLanguageChanged += OnChangedLanguage;
        }

        if (null != optionUI)
        {
            optionUI.Initialize(_ctx);
            optionUI.OnLanguageOptionChangedEvent -= HandleLanguageOptionChanged;
            optionUI.OnLanguageOptionChangedEvent += HandleLanguageOptionChanged;
        }

        if (null != creditUI)
        {
            creditUI.Initialize(HideCredit);
        }

        if (null != initialSetupPopup)
        {
            initialSetupPopup.Initialize(_ctx?.inputManager, _ctx?.localizationManager, _ctx?.cursorBoxUI);
        }
    }

    private void HandleLanguageOptionChanged(EOptionLanguage _lang)
    {
        OnChangedLanguage();
        OnLanguageOptionChangedEvent?.Invoke(_lang);
    }

    public override void OnDestroy()
    {
        NewGameButtonClickedEvent = null;
        LoadGameButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
        OnLanguageOptionChangedEvent = null;

        if (null != context && null != context.localizationManager)
        {
            context.localizationManager.OnLanguageChanged -= OnChangedLanguage;
        }
        
        if (null != optionUI)
        {
            optionUI.OnLanguageOptionChangedEvent -= HandleLanguageOptionChanged;
        }

        if (null != backgroundDimmer)
        {
            backgroundDimmer.DOKill();
        }

        if (null != rootRectTransform)
        {
            rootRectTransform.DOKill();
        }
    }

    // 한 번 스플래시를 본 이후(예: 인게임에서 ESC로 메인 메뉴로 돌아왔을 때) 스플래시를 생략하기 위한 정적 변수
    private static bool hasPlayedSplash = false;

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

        if (null != otherCanvasGroup)
        {
            otherCanvasGroup.DOKill();
            otherCanvasGroup.alpha = 0f;
        }

        // 최초 1회만 스플래시(팀 로고)를 재생한다. 복귀 시의 실제 연출은 Bootstrap.SetupMainMenuScene()가
        // 곧이어 호출하는 PlayButtonsRevealAnimation()이 전담하므로, 여기서는 조용히 빠져나간다.
        if (this.hasIntroPlayed)
        {
            if (null != this.splashScreenUI) this.splashScreenUI.gameObject.SetActive(false);
            return;
        }
        this.hasIntroPlayed = true;

#if UNITY_EDITOR
        if (this.skipIntroInEditor)
        {
            if (null != this.splashScreenUI) this.splashScreenUI.gameObject.SetActive(false);
            if (null != this.pressAnyKeyUI) this.pressAnyKeyUI.Hide();
            if (null != this.mainMenuUI) this.mainMenuUI.gameObject.SetActive(false); // 추가: 스킵 시에도 메인 메뉴를 미리 숨겨야 이중 연출 방지
            
            this.ShowDimmer();

            Sound.PlayBGM(SoundID.MainBGM);

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

        if (false == hasPlayedSplash)
        {
            hasPlayedSplash = true;
            if (null != this.splashScreenUI)
            {
                this.splashScreenUI.gameObject.SetActive(true);
                if (null != this.pressAnyKeyUI) this.pressAnyKeyUI.Hide();
                if (null != this.mainMenuUI) this.mainMenuUI.gameObject.SetActive(false);
                if (null != this.logoAnimUI) this.logoAnimUI.gameObject.SetActive(false);

                // 마지막 페이드아웃 직전에 UI를 미리 켜고, 완료 시 스플래시 자체를 끕니다.
                // 팀 로고가 페이드인되기 시작하는 시점에 메인메뉴 BGM을 재생합니다.
                this.splashScreenUI.PlaySequence(this.OnSplashScreenCompleted, this.PrepareNextUIAfterSplash, this.OnSplashLogoFadeInStart);
            }
            else
            {
                if (null != this.splashScreenUI) this.splashScreenUI.gameObject.SetActive(false);
                this.PrepareNextUIAfterSplash();
                this.OnSplashScreenCompleted();
            }
        }
        else
        {
            // 이미 스플래시를 본 적이 있다면(인게임에서 나왔다면) 스플래시 연출을 스킵하고 바로 Press Any Key로 넘어감
            if (null != this.splashScreenUI) this.splashScreenUI.gameObject.SetActive(false);
            Sound.PlayBGM(SoundID.MainBGM);
            this.PrepareNextUIAfterSplash();
            this.OnSplashScreenCompleted();
        }
    }

    private void OnSplashLogoFadeInStart()
    {
        Sound.PlayBGM(SoundID.MainBGM);
    }

    private void PrepareNextUIAfterSplash()
    {
        // 언어 선택 및 데이터 수집 동의 팝업은 "아직 묻지 않은" 유저에게만 노출한다.
        //
        // 예전에는 조건 없이 매 실행마다 띄웠는데, 선택이 저장되지 않던 시절에는 그럴 수밖에
        // 없었다. 이제 선택이 Settings.json에 남으므로, 한 번 답한 유저에게 다시 묻는 것은
        // 그 답을 무시하는 것과 같다. 마음이 바뀐 유저는 옵션 창에서 언제든 바꿀 수 있다.
        if (null != initialSetupPopup && EDataConsent.NotAsked == SettingsManager.Instance.DataConsent)
        {
            if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
            if (null != logoAnimUI) logoAnimUI.gameObject.SetActive(false);

            initialSetupPopup.Show(OnInitialSetupPopupCompleted);
            return;
        }

        // 시작 시 분기: Press Any Key 화면이 있으면 먼저 띄우고 메인 메뉴 숨김
        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Show();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
            if (null != logoAnimUI) logoAnimUI.gameObject.SetActive(true); // 로고는 항상 먼저 보여야 함
        }
        else
        {
            ShowDimmer(); // 로고 애니메이션(또는 메인메뉴) 시작 시 딤 처리 실행

            if (null != logoAnimUI)
            {
                logoAnimUI.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 초기 설정 팝업이 닫힌 뒤의 화면 전환만 담당합니다.
    ///
    /// 동의 결과 자체는 여기로 오지 않습니다. 팝업이 확인 버튼에서 곧바로
    /// SettingsManager.SetDataConsent로 기록하고, DataConsentGate가 그것을 SDK에 반영합니다.
    /// 예전에는 이 자리에 결과를 실어 나르는 이벤트가 있었지만 구독자가 하나도 없어서
    /// 동의 여부가 조용히 버려졌습니다. 같은 함정을 다시 만들지 않도록 통로를 없앴습니다.
    /// </summary>
    private void OnInitialSetupPopupCompleted()
    {
        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Show();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
            if (null != logoAnimUI) logoAnimUI.gameObject.SetActive(true);
        }
        else
        {
            ShowDimmer();
            if (null != logoAnimUI)
            {
                logoAnimUI.PlayRevealSequence(ShowMainMenu);
            }
            else
            {
                ShowMainMenu();
            }
        }
    }

    private void OnSplashScreenCompleted()
    {
        if (null != splashScreenUI)
        {
            splashScreenUI.gameObject.SetActive(false);
        }

        if (null != initialSetupPopup && true == initialSetupPopup.IsActive)
        {
            return;
        }

        if (null == pressAnyKeyUI)
        {
            if (null != logoAnimUI)
            {
                logoAnimUI.PlayRevealSequence(ShowMainMenu);
            }
            else
            {
                ShowMainMenu();
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
        Sound.PlayUI(SoundID.MainClick);

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

        if (null != otherCanvasGroup)
        {
            otherCanvasGroup.DOFade(1f, 0.5f);
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

        if (null != otherCanvasGroup)
        {
            _seq.Join(otherCanvasGroup.DOFade(0f, 0.5f));
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
            logoAnimUI.SetAlpha(0f);
        }

        if (null != otherCanvasGroup)
        {
            otherCanvasGroup.DOKill();
            otherCanvasGroup.alpha = 0f;
        }

        // 2. 스플래시 스크린(팀 로고) 이후의 초기 시작 연출을 그대로 다시 트리거합니다.
        PrepareNextUIAfterSplash();

        if (null != logoAnimUI)
        {
            logoAnimUI.PlayFadeIn(0.8f);
        }

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

    public void OnCreditButtonClicked()
    {
        if (null != creditUI)
        {
            creditUI.PlayCredit();
        }
    }

    private void HideCredit()
    {
        if (null != mainMenuUI)
        {
            mainMenuUI.ReleaseOptionButtonState(); // Reusing this to reset all buttons
        }
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
