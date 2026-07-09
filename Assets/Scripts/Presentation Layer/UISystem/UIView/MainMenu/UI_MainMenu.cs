using UnityEngine;

/// <summary>
/// 메인 메뉴의 실질적인 UI 요소들을 관리하는 스크립트입니다.
/// UIView_MainMenu에 의해 생성되어 소통 창구 역할을 합니다.
/// </summary>
public class UI_MainMenu : MonoBehaviour
{
    // 외부 의존성
    [Header("Main Buttons")]
    [SerializeField] private UI_MainMenuButton newGameButton;
    [SerializeField] private UI_MainMenuButton loadGameButton;
    [SerializeField] private UI_MainMenuButton exitButton;
    
    [Header("Additional Features (Upcoming)")]
    [SerializeField] private UI_MainMenuButton optionButton;
    [SerializeField] private UI_MainMenuButton creditButton;
    [SerializeField] private UI_MainMenuButton languageButton;

    [Header("Localization Settings")]
    [SerializeField] private int mainMenuUIJsonId = 8;
    
    // 내부 의존성
    private UIView_MainMenu parentView;
    private UIViewContext viewCtx;
    
    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIView_MainMenu _parentView, UIViewContext uIViewContext)
    {
        parentView = _parentView;
        viewCtx = uIViewContext;
        
        if (null != newGameButton)
        {
            newGameButton.Initialize(OnNewGameClicked);
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.Initialize(OnLoadGameClicked);
        }
        
        if (null != exitButton)
        {
            exitButton.Initialize(OnExitClicked);
        }
        
        if (null != optionButton)
        {
            optionButton.Initialize(OnOptionClicked);
        }
        
        if (null != creditButton)
        {
            creditButton.Initialize(OnCreditClicked);
        }
        
        if (null != languageButton)
        {
            languageButton.Initialize(OnLanguageClicked);
        }

        SetLocalization();
    }

    public void SetLocalization()
    {
        if (null == viewCtx || null == viewCtx.localizationManager)
            return;

        if (null != newGameButton)
        {
            newGameButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 1));
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 2));
        }
        
        if (null != optionButton)
        {
            optionButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 3));
        }
        
        if (null != creditButton)
        {
            creditButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 4));
        }

        if (null != exitButton)
        {
            exitButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 5));
        }
    }
    
    private void OnNewGameClicked()
    {
        if (null != parentView)
        {
            parentView.OnNewGameStartButton();
        }
    }
    
    private void OnLoadGameClicked()
    {
        if (null != parentView)
        {
            parentView.OnLoadGameButtonClicked();
        }
    }
    
    private void OnExitClicked()
    {
        if (null != parentView)
        {
            parentView.OnExitButtonClicked();
        }
    }
    
    private void OnOptionClicked()
    {
        // TODO: 설정(Option) 기능 연결
        Debug.Log("Option Button Clicked - 기능 연결 필요");
    }
    
    private void OnCreditClicked()
    {
        // TODO: 크레딧(Credit) 기능 연결
        Debug.Log("Credit Button Clicked - 기능 연결 필요");
    }
    
    private void OnLanguageClicked()
    {
        // TODO: 언어설정 기능 연결
        Debug.Log("Language Button Clicked - 기능 연결 필요");
    }
    
    // 유니티 이벤트 함수
    private void OnDestroy()
    {
        if (null != newGameButton)
        {
            newGameButton.Release();
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.Release();
        }
        
        if (null != exitButton)
        {
            exitButton.Release();
        }
        
        if (null != optionButton)
        {
            optionButton.Release();
        }
        
        if (null != creditButton)
        {
            creditButton.Release();
        }
        
        if (null != languageButton)
        {
            languageButton.Release();
        }
    }
}
