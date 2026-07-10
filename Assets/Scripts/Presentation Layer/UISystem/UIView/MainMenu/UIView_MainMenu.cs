using UnityEngine;
using System;


public class UIView_MainMenu : UIView
{
    public event Action NewGameButtonClickedEvent;
    public event Action LoadGameButtonClickedEvent;
    public event Action ExitButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private UI_MainMenu mainMenuUI; // 메인 메뉴
    [SerializeField] private UI_PressAnyKey pressAnyKeyUI; // 아무 키나 누르세요 화면

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        // 프리팹을 인스턴스화하지 않고, 이미 바인딩된 컴포넌트를 바로 초기화
        if (null != mainMenuUI)
        {
            mainMenuUI.Initialize(this, _ctx);
        }

        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Initialize(this);
        }
    }

    public override void OnDestroy()
    {
        NewGameButtonClickedEvent = null;
        LoadGameButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        // 시작 시 분기: Press Any Key 화면이 있으면 먼저 띄우고 메인 메뉴 숨김
        if (null != pressAnyKeyUI)
        {
            pressAnyKeyUI.Show();
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(false);
        }
        else
        {
            if (null != mainMenuUI) mainMenuUI.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// UI_PressAnyKey에서 아무 키 입력이 감지되었을 때 호출됩니다.
    /// </summary>
    public void OnPressAnyKeyCompleted()
    {
        if (null != pressAnyKeyUI) pressAnyKeyUI.Hide();
        
        if (null != mainMenuUI) 
        {
            mainMenuUI.gameObject.SetActive(true);
            // (선택) 여기서 MainMenu가 나타날 때 DOTween 페이드인 연출을 추가할 수 있습니다.
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    public void OnNewGameStartButton()
    {
        NewGameButtonClickedEvent?.Invoke();
    }

    public void OnLoadGameButtonClicked()
    {
        LoadGameButtonClickedEvent?.Invoke();
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
