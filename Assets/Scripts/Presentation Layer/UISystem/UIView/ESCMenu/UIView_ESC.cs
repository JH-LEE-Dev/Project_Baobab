using System;
using UnityEngine;

/// <summary>
/// ESC 메뉴의 최상위 UIView 컴포넌트입니다.
/// 유니티 기본 Button 대신 UI_EscapeMenu 컴포넌트와 연동하여 애니메이션 및 이벤트를 중계합니다.
/// </summary>
public class UIView_ESC : UIView
{
    public event Action ResumeButtonClickedEvent;
    public event Action OptionButtonClickedEvent;
    public event Action GoToMainMenuButtonClickedEvent;
    public event Action ExitButtonClickedEvent;
    public event Action SaveGameButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiPrefab;
    [SerializeField] private UI_EscapeMenu escapeMenu;

    private Action cachedCloseProductionFinished;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null != uiPrefab)
        {
            Transform _parent = null != uiRoot ? uiRoot : transform;
            GameObject _instance = Instantiate(uiPrefab, _parent);
            if (null == escapeMenu)
            {
                escapeMenu = _instance.GetComponentInChildren<UI_EscapeMenu>(true);
            }
        }

        if (null == escapeMenu)
        {
            escapeMenu = GetComponentInChildren<UI_EscapeMenu>(true);
        }

        if (null != escapeMenu)
        {
            escapeMenu.Initialize(
                _ctx?.localizationManager,
                OnResumeButtonClicked,
                OnOptionButtonClicked,
                OnGoToMainMenuButtonClicked,
                OnExitButtonClicked);
        }
    }

    public override void OnDestroy()
    {
        ResumeButtonClickedEvent = null;
        OptionButtonClickedEvent = null;
        GoToMainMenuButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
        SaveGameButtonClickedEvent = null;
        cachedCloseProductionFinished = null;

        base.OnDestroy();
    }

    public override void Hide()
    {
        if (false == IsVisible) return;

        if (null != escapeMenu)
        {
            if (null == cachedCloseProductionFinished)
                cachedCloseProductionFinished = OnCloseProductionFinished;

            escapeMenu.PlayCloseProduction(cachedCloseProductionFinished);
        }
        else
        {
            base.Hide();
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        if (null != escapeMenu)
        {
            escapeMenu.PlayOpenProduction();
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    private void OnCloseProductionFinished()
    {
        base.Hide();
    }

    public void OnResumeButtonClicked()
    {
        Hide();
        if (null != ResumeButtonClickedEvent)
        {
            ResumeButtonClickedEvent.Invoke();
        }
    }

    public void OnOptionButtonClicked()
    {
        if (null != OptionButtonClickedEvent)
        {
            OptionButtonClickedEvent.Invoke();
        }
    }

    public void OnGoToMainMenuButtonClicked()
    {
        if (null != GoToMainMenuButtonClickedEvent)
        {
            GoToMainMenuButtonClickedEvent.Invoke();
        }
    }

    public void OnExitButtonClicked()
    {
        if (null != ExitButtonClickedEvent)
        {
            ExitButtonClickedEvent.Invoke();
        }
    }

    public void OnSaveGameButton()
    {
        if (null != SaveGameButtonClickedEvent)
        {
            SaveGameButtonClickedEvent.Invoke();
        }
    }
}
