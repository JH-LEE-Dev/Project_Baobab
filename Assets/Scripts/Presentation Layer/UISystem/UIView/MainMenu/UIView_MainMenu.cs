using UnityEngine;
using System;


public class UIView_MainMenu : UIView
{
    public event Action NewGameButtonClickedEvent;
    public event Action LoadGameButtonClickedEvent;
    public event Action ExitButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private UI_MainMenu mainMenuUI; // 씬 내부에 이미 배치된 UI_MainMenu 컴포넌트를 직접 할당

    private IMainMenuSaveSystem saveSystem;

    public void DependencyInjection(IMainMenuSaveSystem _saveSystem)
    {
        saveSystem = _saveSystem;
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        // 프리팹을 인스턴스화하지 않고, 이미 바인딩된 컴포넌트를 바로 초기화
        if (null != mainMenuUI)
        {
            mainMenuUI.Initialize(this, _ctx);
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
