using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using TMPro;


public class UIView_MainMenu : UIView
{
    public event Action NewGameButtonClickedEvent;
    public event Action LoadGameButtonClickedEvent;
    public event Action ExitButtonClickedEvent;
    /// <summary>로그라이크 모드 버튼(기획서 4장 "모드 진입"). 버튼 배치·해금 표시는 UI 담당이 UI_MainMenu에 붙인다.</summary>
    public event Action RoguelikeButtonClickedEvent;
    public event Action<EOptionLanguage> OnLanguageOptionChangedEvent;

    [Header("UI References")]
    [SerializeField] private UI_MainMenu mainMenuUI; // 메인 메뉴
    [SerializeField] private UI_PressAnyKey pressAnyKeyUI; // 아무 키나 누르세요 화면
    [SerializeField] private UI_SplashScreen splashScreenUI; // 스플래시 스크린
    [SerializeField] private UI_LogoAnim logoAnimUI; // 로고 애니메이션 객체
    [SerializeField] private UI_MainMenuBackground backgroundUI; // 동적 배경 관리 객체
    [SerializeField] private CanvasGroup otherCanvasGroup; // 게임 버전 등 기타 UI 최상위 캔버스 그룹
    [SerializeField] private UI_ExternalLinkButton discordButton; // 디스코드 바로가기 버튼

    [Header("Game Version UI")]
    [SerializeField, Tooltip("게임 버전 표시 TextMeshProUGUI (UI_Other > GameVersion > Text (TMP))")]
    private TextMeshProUGUI gameVersionText;
    [SerializeField, Tooltip("데모 버전 표기 포맷")]
    private string demoVersionFormat = "Demo V{0}";
    [SerializeField, Tooltip("정식 버전 표기 포맷")]
    private string releaseVersionFormat = "Release V{0}";

    [Header("Sub Views")]
    [SerializeField] private UI_Option optionUI; // 공용 옵션 UI
    [SerializeField] private UI_Credit creditUI; // 크레딧 UI
    [SerializeField] private UI_InitialSetupPopup initialSetupPopup; // 초기 언어 및 약관 동의 팝업

#if UNITY_EDITOR
    [Header("Editor Debug (빌드에는 포함되지 않음)")]
    [SerializeField, Tooltip("켜면 저장된 동의 상태와 무관하게 에디터 플레이마다 초기 설정(언어/약관 동의) 팝업을 항상 띄운다. 기본값은 켜짐이며, 끄면 일반 유저와 같은 조건(미동의일 때만)으로 동작한다.")]
    private bool bForceShowInitialSetupPopup = true;
#endif

    [Header("Background Overlay")]
    [SerializeField, Tooltip("메인 메뉴 뒤에 깔릴 검은색 셀로판지(Dimmer)")] 
    private Image backgroundDimmer; 
    [SerializeField] private float dimmerTargetAlpha = 0.3f;
    [SerializeField] private float dimmerFadeDuration = 1f;

    [Header("Roguelike Entry")]
    [SerializeField, Tooltip("로그라이크 버튼을 누른 뒤 메인메뉴 화면에 걸리는 디스토션 링 수치(기획서 v2.2 9-1 1단계). 암전 유지·페이드 아웃은 로그라이크 쪽(RoguelikeSystem 프리팹) 값을 쓴다.")]
    private RoguelikeScreenTransitionSettings roguelikeEntryTransition = new RoguelikeScreenTransitionSettings();

    [Header("Exit Animation")]
    [SerializeField] private float exitMoveDuration = 1.8f;
    // MainMenu 모드에서 꺼지는 SkyProduction.prefab의 skyImage 이동 거리(635 → 135)와 동일한 500으로 맞춤
    // (MainMenu가 Sky의 자리를 대신하므로 cloudImage가 아닌 skyImage 기준)
    [SerializeField] private float exitMoveDistance = 500f;
    // GameInstaller.prefab의 SkyCameraProductionManager.moveEase 직렬화 값(10)과 동일
    [SerializeField] private Ease exitMoveEase = (Ease)10;

#if UNITY_EDITOR
    [Header("Debug (빌드에는 포함되지 않음)")]
    [SerializeField, Tooltip("체크하면 에디터 환경에서 스플래시와 로고 연출을 건너뛰고 바로 메인 메뉴를 출력합니다.")]
    private bool skipIntroInEditor = true;
#endif

    // 내부 상태 및 캐시
    private RectTransform rootRectTransform;
    private Vector2 restAnchoredPosition;
    private int mainMenuUIJsonId = 8; // MainMenuUI.json의 ID
    // 로그라이크 진입 디스토션 중(버튼을 누른 뒤 메인메뉴가 내려갈 때까지). 그동안 다른 메뉴 버튼은 무시한다 -
    // 본편 버튼은 누르는 즉시 메뉴를 내리지만 로그라이크는 메뉴를 띄운 채 출렁이게 하므로, 그 사이 이어하기 등이 눌리면 두 흐름이 겹친다.
    private bool bRoguelikeEntryPlaying;
    private IMainMenuSaveSystem saveSystem;

    // 한 번 스플래시를 본 이후(예: 인게임에서 ESC로 메인 메뉴로 돌아왔을 때) 스플래시를 생략하기 위한 정적 변수
    private static bool hasPlayedSplash = false;

    // 캐싱 델리게이트 (Zero GC)
    private Action currentRevealCompleteAction;
    private Action onRevealSequenceCompletedCallback;
    private Action currentExitCompleteAction;
    private TweenCallback onExitAnimationCompleteCallback;
    private Action currentEnterCompleteAction;
    private TweenCallback onEnterAnimationCompleteCallback;
    private TweenCallback invokeNewGameEventCallback;
    private TweenCallback invokeLoadGameEventCallback;
    private TweenCallback invokeRoguelikeEventCallback;
    private Action onOptionUIClosedCallback;
    private Action showMainMenuCallback;
    private Action onSplashScreenCompletedCallback;
    private Action prepareNextUIAfterSplashCallback;
    private Action onSplashLogoFadeInStartCallback;
    private Action onInitialSetupPopupCompletedCallback;
    private Action hideCreditCallback;

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
        return null != saveSystem && true == saveSystem.HasSaveData();
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

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
            if (null == hideCreditCallback) hideCreditCallback = HideCredit;
            creditUI.Initialize(hideCreditCallback, _ctx?.inputManager, _ctx?.depthController);
        }

        if (null != initialSetupPopup)
        {
            initialSetupPopup.Initialize(_ctx?.inputManager, _ctx?.localizationManager, _ctx?.cursorBoxUI, _ctx?.depthController);
        }

        ApplyBuildVariantState();
    }

    /// <summary>
    /// BuildInfo.IsDemo에 따라 로고 데모 뱃지 표시 여부 및 하단 게임 버전 텍스트를 갱신합니다.
    /// Tools > 빌드 메뉴(PlatformBuildModeSwitcher)의 배포 모드 전환 상태와 직접 연동됩니다.
    /// </summary>
    public void ApplyBuildVariantState()
    {
        if (null == gameVersionText && null != otherCanvasGroup)
        {
            gameVersionText = otherCanvasGroup.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (null != gameVersionText)
        {
            string _format = true == BuildInfo.IsDemo ? demoVersionFormat : releaseVersionFormat;
            gameVersionText.text = string.Format(_format, Application.version);
        }

        if (null != logoAnimUI)
        {
            logoAnimUI.SetDemoBadgeActive(true == BuildInfo.IsDemo);
        }
    }

    private void HandleLanguageOptionChanged(EOptionLanguage _lang)
    {
        OnChangedLanguage();
        OnLanguageOptionChangedEvent?.Invoke(_lang);
    }

    /// <summary>
    /// UI_PressAnyKey에서 아무 키 입력이 감지되었을 때 호출됩니다.
    /// </summary>
    public void OnPressAnyKeyCompleted()
    {
        Sound.PlayUI(SoundID.MainClick);

        if (null != splashScreenUI && true == splashScreenUI.gameObject.activeInHierarchy)
        {
            splashScreenUI.gameObject.SetActive(false);
        }

        if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
        
        ShowDimmer(); // 로고 애니메이션(또는 메인메뉴) 시작 시 딤 처리 실행

        if (null == showMainMenuCallback) showMainMenuCallback = ShowMainMenu;

        if (null != logoAnimUI)
        {
            // 로고 애니메이션 실행 후 끝나는 시점에 메인 메뉴 노출
            logoAnimUI.PlayRevealSequence(showMainMenuCallback);
        }
        else
        {
            ShowMainMenu();
        }
    }

    public void OnChangedLanguage()
    {
        if (null != pressAnyKeyUI && null != viewCtx && null != viewCtx.localizationManager)
        {
            pressAnyKeyUI.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 99));
        }

        if (null != mainMenuUI)
        {
            mainMenuUI.SetLocalization();
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

    private void PrepareNextUIAfterSplash()
    {
        // 언어 선택 및 데이터 수집 동의 팝업은 "아직 묻지 않은" 유저에게만 노출한다.
        //
        // 예전에는 조건 없이 매 실행마다 띄웠는데, 선택이 저장되지 않던 시절에는 그럴 수밖에
        // 없었다. 이제 선택이 Settings.json에 남으므로, 한 번 답한 유저에게 다시 묻는 것은
        // 그 답을 무시하는 것과 같다. 마음이 바뀐 유저는 옵션 창에서 언제든 바꿀 수 있다.
        bool bShowInitialSetupPopup = EDataConsent.NotAsked == SettingsManager.Instance.DataConsent;
#if UNITY_EDITOR
        if (true == bForceShowInitialSetupPopup) bShowInitialSetupPopup = true;
#endif

        if (null != initialSetupPopup && true == bShowInitialSetupPopup)
        {
            if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
            if (null != logoAnimUI) logoAnimUI.gameObject.SetActive(false);

            if (null == onInitialSetupPopupCompletedCallback) onInitialSetupPopupCompletedCallback = OnInitialSetupPopupCompleted;
            initialSetupPopup.Show(onInitialSetupPopupCompletedCallback, false);
            return;
        }

        // 시작 시 분기: Press Any Key 화면이 있으면 먼저 띄우고 메인 메뉴 숨김
        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Show(true);
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

    private void OnSplashScreenCompleted()
    {
        if (null != splashScreenUI)
        {
            splashScreenUI.gameObject.SetActive(false);
        }

        if (null != initialSetupPopup && true == initialSetupPopup.gameObject.activeInHierarchy)
        {
            initialSetupPopup.ActivateInput();
            return;
        }

        if (null != pressAnyKeyUI && true == pressAnyKeyUI.gameObject.activeInHierarchy)
        {
            pressAnyKeyUI.ActivateInput();
        }
        else if (null == pressAnyKeyUI)
        {
            if (null == showMainMenuCallback) showMainMenuCallback = ShowMainMenu;
            if (null != logoAnimUI)
            {
                logoAnimUI.PlayRevealSequence(showMainMenuCallback);
            }
            else
            {
                ShowMainMenu();
            }
        }
    }

    private void OnSplashLogoFadeInStart()
    {
        Sound.PlayBGM(SoundID.MainBGM);
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
            if (null == showMainMenuCallback) showMainMenuCallback = ShowMainMenu;
            if (null != logoAnimUI)
            {
                logoAnimUI.PlayRevealSequence(showMainMenuCallback);
            }
            else
            {
                ShowMainMenu();
            }
        }
    }

    public void OnNewGameStartButton()
    {
        if (bRoguelikeEntryPlaying) return;
        if (null == invokeNewGameEventCallback) invokeNewGameEventCallback = InvokeNewGameEvent;
        PlayGameStartSequence(invokeNewGameEventCallback);
    }

    public void OnLoadGameButtonClicked()
    {
        if (bRoguelikeEntryPlaying) return;
        if (null == invokeLoadGameEventCallback) invokeLoadGameEventCallback = InvokeLoadGameEvent;
        PlayGameStartSequence(invokeLoadGameEventCallback);
    }

    /// <summary>
    /// 로그라이크 모드 버튼에서 부른다. 해금 조건(본편 모든 특성 해금 + 엔딩)은 본편 엔딩이 아직 없어
    /// 적용하지 않는다(기획서 9장 - 개발 중에는 상시 활성).
    /// 진입 연출 1~2단계(기획서 4-1): 지금 메인메뉴 화면에 디스토션이 걸리고 검게 덮인다. 검은 화면 아래에서
    /// 새로하기·이어하기와 같은 퇴장 정리(버튼·딤머·로고)를 바로 끝내고 씬을 넘긴다. 검은 화면은 로그라이크 로비가 걷는다.
    /// </summary>
    public void OnRoguelikeButtonClicked()
    {
        if (bRoguelikeEntryPlaying) return;

        bRoguelikeEntryPlaying = true;
        StartCoroutine(RoguelikeEntryRoutine());
    }

    // 디스토션 도중 메인메뉴가 꺼져 코루틴이 끊기면(씬 전환 요청 전) 버튼 잠금을 풀고 덮어 둔 검은 화면도 걷는다 -
    // 씬이 바뀌지 않으니 BootStrap의 ClearIfCovering도 불리지 않는다. 씬 전환 요청 뒤에는 이미 false라 아무것도 하지 않는다.
    private void OnDisable()
    {
        if (false == bRoguelikeEntryPlaying) return;

        bRoguelikeEntryPlaying = false;
        RoguelikeScreenTransition.ClearIfCovering();
    }

    private System.Collections.IEnumerator RoguelikeEntryRoutine()
    {
        // 메인메뉴 BGM은 디스토션 동안 잦아든다(본편 진입 하강이 메인메뉴 BGM을 내리는 것과 같은 역할).
        Sound.FadeOutBGM(Mathf.Max(0.01f, roguelikeEntryTransition.ringSeconds));
        yield return RoguelikeScreenTransition.Instance.DistortToBlack(roguelikeEntryTransition);

        // 본편은 씬이 넘어간 뒤 PlayExitAnimation으로 메인메뉴를 위로 밀어 올린다(구름·하강 연출이 그 위를 덮는다).
        // 로그라이크는 검은 화면 아래라 밀어 올린 끝 자리로 바로 옮긴다 - 돌아갈 때 PlayEnterAnimation이 같은 자리에서 내려온다.
        if (null != rootRectTransform)
        {
            rootRectTransform.DOKill();
            rootRectTransform.anchoredPosition += Vector2.up * exitMoveDistance;
        }

        if (null == invokeRoguelikeEventCallback) invokeRoguelikeEventCallback = InvokeRoguelikeEvent;
        Sequence _seq = PlayGameStartSequence(invokeRoguelikeEventCallback);

        // 메뉴가 내려갔다(PlayGameStartSequence). 씬 전환 요청(Complete → InvokeRoguelikeEvent) 전에 내린다 -
        // 그 안에서 메인메뉴가 꺼지면 OnDisable이 진입 도중으로 오해해 검은 화면을 걷는다.
        bRoguelikeEntryPlaying = false;
        _seq.Complete(true);
    }

    private void InvokeRoguelikeEvent()
    {
        RoguelikeButtonClickedEvent?.Invoke();
    }

    private Sequence PlayGameStartSequence(TweenCallback _onSequenceCompleted)
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

        return _seq;
    }

    public void OnOptionButtonClicked()
    {
        if (bRoguelikeEntryPlaying) return;
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
            mainMenuUI.OnSubViewClosed();
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

    private void OnRevealSequenceCompleted()
    {
        ShowMainMenu();
        currentRevealCompleteAction?.Invoke();
        currentRevealCompleteAction = null;
    }

    // PlayGameStartSequence()의 반대 방향: 씬 진입 후 스플래시 연출(팀 로고) 직후의 연출부터 다시 시작합니다.
    public void PlayButtonsRevealAnimation(Action _onComplete = null)
    {
        ApplyBuildVariantState();

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

        currentRevealCompleteAction = _onComplete;
        if (null == onRevealSequenceCompletedCallback)
        {
            onRevealSequenceCompletedCallback = OnRevealSequenceCompleted;
        }

        if (null == pressAnyKeyUI)
        {
            if (null != logoAnimUI)
            {
                logoAnimUI.PlayRevealSequence(onRevealSequenceCompletedCallback);
            }
            else
            {
                OnRevealSequenceCompleted();
            }
        }
        else
        {
            // pressAnyKeyUI가 활성화되면 사용자가 키를 입력할 때 OnPressAnyKeyCompleted()에서 
            // logoAnimUI.PlayRevealSequence(ShowMainMenu)가 진행되므로 여기선 콜백만 호출합니다.
            pressAnyKeyUI.ActivateInput();
            _onComplete?.Invoke();
            currentRevealCompleteAction = null;
        }
    }

    public void OnCreditButtonClicked()
    {
        if (bRoguelikeEntryPlaying) return;
        if (null != creditUI)
        {
            creditUI.PlayCredit();
        }
    }

    private void HideCredit()
    {
        if (null != mainMenuUI)
        {
            mainMenuUI.OnSubViewClosed();
        }
    }

    public void OnExitButtonClicked()
    {
        if (bRoguelikeEntryPlaying) return;
        ExitButtonClickedEvent?.Invoke();
    }

    // 유니티 및 생명주기 이벤트 함수
    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        ApplyBuildVariantState();

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

        // 최초 1회만 스플래시(팀 로고)를 재생합니다. 복귀 시의 실제 연출은 Bootstrap.SetupMainMenuScene()가
        // 곧이어 호출하는 PlayButtonsRevealAnimation()이 전담하므로, 여기서는 조용히 빠져나갑니다.
        if (true == hasPlayedSplash)
        {
            if (null != splashScreenUI)
            {
                splashScreenUI.gameObject.SetActive(false);
            }
            return;
        }
        hasPlayedSplash = true;

#if UNITY_EDITOR
        if (true == skipIntroInEditor)
        {
            if (null != splashScreenUI) splashScreenUI.gameObject.SetActive(false);
            if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
            
            ShowDimmer();
            Sound.PlayBGM(SoundID.MainBGM);

            if (null == showMainMenuCallback) showMainMenuCallback = ShowMainMenu;

            if (null != logoAnimUI)
            {
                logoAnimUI.gameObject.SetActive(true);
                logoAnimUI.PlayRevealSequence(showMainMenuCallback);
            }
            else
            {
                ShowMainMenu();
            }
            return;
        }
#endif

        if (null != splashScreenUI)
        {
            splashScreenUI.gameObject.SetActive(true);
            if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
            if (null != logoAnimUI) logoAnimUI.gameObject.SetActive(false);

            if (null == onSplashScreenCompletedCallback) onSplashScreenCompletedCallback = OnSplashScreenCompleted;
            if (null == prepareNextUIAfterSplashCallback) prepareNextUIAfterSplashCallback = PrepareNextUIAfterSplash;
            if (null == onSplashLogoFadeInStartCallback) onSplashLogoFadeInStartCallback = OnSplashLogoFadeInStart;

            // 마지막 페이드아웃 직전에 UI를 미리 켜고, 완료 시 스플래시 자체를 끕니다.
            // 팀 로고가 페이드인되기 시작하는 시점에 메인메뉴 BGM을 재생합니다.
            splashScreenUI.PlaySequence(onSplashScreenCompletedCallback, prepareNextUIAfterSplashCallback, onSplashLogoFadeInStartCallback);
        }
        else
        {
            PrepareNextUIAfterSplash();
            OnSplashScreenCompleted();
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    public override void OnDestroy()
    {
        NewGameButtonClickedEvent = null;
        LoadGameButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
        RoguelikeButtonClickedEvent = null;
        OnLanguageOptionChangedEvent = null;

        if (null != viewCtx && null != viewCtx.localizationManager)
        {
            viewCtx.localizationManager.OnLanguageChanged -= OnChangedLanguage;
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
}
