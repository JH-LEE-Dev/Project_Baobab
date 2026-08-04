using System;
using UnityEngine;

/// <summary>
/// ESC 메뉴의 최상위 UIView 컴포넌트입니다.
/// 유니티 기본 Button 대신 UI_EscapeMenu 컴포넌트와 연동하여 애니메이션 및 이벤트를 중계하며,
/// 옵션 버튼 클릭 시 되감기 역모션 후 UI_Option을 열고, 옵션 창이 닫히면 ESC 메뉴를 다시 복원합니다.
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

    [Header("Sub Views")]
    [SerializeField] private UI_Option optionUI; // 인스펙터 바인딩 지원

    private Action cachedCloseProductionFinished;
    private Action cachedOptionMenuCloseCompleted;
    private Action cachedOptionClosed;

    public bool IsOptionOpen => null != optionUI && true == optionUI.gameObject.activeInHierarchy;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        cachedCloseProductionFinished = OnCloseProductionFinished;
        cachedOptionMenuCloseCompleted = OnOptionMenuCloseCompleted;
        cachedOptionClosed = OnOptionClosed;

        if (null != uiPrefab)
        {
            Transform _parent = null != uiRoot ? uiRoot : transform;
            GameObject _instance = Instantiate(uiPrefab, _parent);
            if (null == escapeMenu)
            {
                escapeMenu = _instance.GetComponentInChildren<UI_EscapeMenu>(true);
            }

            if (null == optionUI)
            {
                optionUI = _instance.GetComponentInChildren<UI_Option>(true);
            }
        }

        if (null == escapeMenu)
        {
            escapeMenu = GetComponentInChildren<UI_EscapeMenu>(true);
        }

        if (null == optionUI)
        {
            optionUI = GetComponentInChildren<UI_Option>(true);
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

        if (null != optionUI)
        {
            optionUI.Initialize(_ctx);
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
        cachedOptionMenuCloseCompleted = null;
        cachedOptionClosed = null;

        base.OnDestroy();
    }

    public override void Hide()
    {
        if (false == IsVisible) return;

        base.Hide();

        if (null != optionUI && true == optionUI.gameObject.activeInHierarchy)
        {
            optionUI.Hide();
        }

        if (null != escapeMenu)
        {
            if (null == cachedCloseProductionFinished)
                cachedCloseProductionFinished = OnCloseProductionFinished;

            escapeMenu.PlayCloseProduction(cachedCloseProductionFinished);
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
        gameObject.SetActive(false);
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
        if (null != escapeMenu)
        {
            if (null == cachedOptionMenuCloseCompleted)
                cachedOptionMenuCloseCompleted = OnOptionMenuCloseCompleted;

            escapeMenu.PlayCloseProduction(cachedOptionMenuCloseCompleted);
        }
        else
        {
            OnOptionMenuCloseCompleted();
        }
    }

    public void CloseOption()
    {
        if (null != optionUI && true == optionUI.gameObject.activeInHierarchy)
        {
            optionUI.Hide();
        }
    }

    private void OnOptionMenuCloseCompleted()
    {
        if (null != optionUI)
        {
            if (null == cachedOptionClosed)
                cachedOptionClosed = OnOptionClosed;

            optionUI.Show(cachedOptionClosed);
        }

        if (null != OptionButtonClickedEvent)
        {
            OptionButtonClickedEvent.Invoke();
        }
    }

    private void OnOptionClosed()
    {
        if (false == IsVisible) return;

        if (null != escapeMenu)
        {
            escapeMenu.PlayOpenProduction();
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
