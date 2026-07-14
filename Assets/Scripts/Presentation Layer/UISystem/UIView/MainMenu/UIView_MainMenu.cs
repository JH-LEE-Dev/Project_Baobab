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
        PlayGameStartSequence(() => NewGameButtonClickedEvent?.Invoke());
    }

    public void OnLoadGameButtonClicked()
    {
        PlayGameStartSequence(() => LoadGameButtonClickedEvent?.Invoke());
    }

    private void PlayGameStartSequence(Action _onComplete)
    {
        Sequence _seq = DOTween.Sequence();

        // 1. 딤머를 알파 0으로 서서히 없앰 (속도는 설정된 dimmerFadeDuration 값 활용)
        if (backgroundDimmer != null)
        {
            _seq.Append(backgroundDimmer.DOFade(0f, dimmerFadeDuration));
        }

        // 2. 딤머 페이드 아웃 완료 후 로고도 0.5초 동안 알파 0으로 페이드 아웃
        if (logoAnimUI != null)
        {
            _seq.AppendCallback(() => logoAnimUI.PlayFadeOut(0.5f));
            _seq.AppendInterval(0.5f);
        }

        // 3. 연출이 모두 끝나면 게임 시작(이벤트 발생)
        _seq.OnComplete(() => _onComplete?.Invoke());
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
