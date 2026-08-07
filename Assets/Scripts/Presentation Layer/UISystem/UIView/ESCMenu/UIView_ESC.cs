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

    /// <summary>
    /// 연출 재생 중 키보드 동작(ESC 등) 잠금/해제 요청 이벤트 (true = 잠금, false = 해제)
    /// </summary>
    public event Action<bool> UIInputLockChangedEvent;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiPrefab;
    [SerializeField] private UI_EscapeMenu escapeMenu;

    [Header("Sub Views")]
    [SerializeField] private UI_Option optionUI; // 인스펙터 바인딩 지원

    [Header("Input Lock Settings")]
    [SerializeField, Tooltip("1. ESC 메뉴 패널 등장 연출 중 키보드 입력 Lock 여부")]
    private bool lockOnMenuAppear = true;

    [SerializeField, Tooltip("2. ESC 메뉴 -> 옵션 패널 전환 연출 중 키보드 입력 Lock 여부")]
    private bool lockOnTransitionToOption = true;

    [SerializeField, Tooltip("3. 옵션 패널 -> ESC 메뉴 복귀 연출 중 키보드 입력 Lock 여부")]
    private bool lockOnTransitionFromOption = true;

    private Action cachedCloseProductionFinished;
    private Action cachedOptionMenuCloseCompleted;
    private Action cachedOptionClosed;
    private Action cachedMenuAppearCompleted;
    private Action cachedReturnFromOptionCompleted;

    private bool isClosing = false;
    private bool isOpeningOption = false;

    public bool IsOptionOpen => (null != optionUI && true == optionUI.gameObject.activeInHierarchy) || true == isOpeningOption;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        cachedCloseProductionFinished = OnCloseProductionFinished;
        cachedOptionMenuCloseCompleted = OnOptionMenuCloseCompleted;
        cachedOptionClosed = OnOptionClosed;
        cachedMenuAppearCompleted = OnMenuAppearCompleted;
        cachedReturnFromOptionCompleted = OnReturnFromOptionCompleted;

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
        UIInputLockChangedEvent = null;

        cachedCloseProductionFinished = null;
        cachedOptionMenuCloseCompleted = null;
        cachedOptionClosed = null;
        cachedMenuAppearCompleted = null;
        cachedReturnFromOptionCompleted = null;

        base.OnDestroy();
    }

    public override void Show()
    {
        isClosing = false;
        isOpeningOption = false;
        base.Show();
    }

    public void ShowPauseMenu()
    {
        if (null != escapeMenu)
            escapeMenu.SetSoundsEnabled(true);

        Show();
    }

    public override void Hide()
    {
        if (false == IsVisible || true == isClosing || true == isOpeningOption) return;

        if (null != optionUI && true == optionUI.gameObject.activeInHierarchy)
        {
            optionUI.Hide();
        }

        if (null != escapeMenu && true == gameObject.activeInHierarchy)
        {
            isClosing = true;

            if (null == cachedCloseProductionFinished)
                cachedCloseProductionFinished = OnCloseProductionFinished;

            escapeMenu.PlayCloseProduction(cachedCloseProductionFinished);
        }
        else
        {
            OnCloseProductionFinished();
        }
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        if (null != escapeMenu)
        {
            // 1. ESC 메뉴 패널 등장 시작 시 Lock
            if (true == lockOnMenuAppear)
            {
                DispatchInputLock(true);
            }

            if (null == cachedMenuAppearCompleted)
            {
                cachedMenuAppearCompleted = OnMenuAppearCompleted;
            }

            escapeMenu.PlayOpenProduction(cachedMenuAppearCompleted);
        }
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    private void OnMenuAppearCompleted()
    {
        // 1-1. ESC 메뉴 패널 등장 연출 종료 시 Unlock
        if (true == lockOnMenuAppear)
        {
            DispatchInputLock(false);
        }
    }

    private void OnCloseProductionFinished()
    {
        isClosing = false;
        base.Hide();
        gameObject.SetActive(false);

        if (null != escapeMenu)
            escapeMenu.SetSoundsEnabled(false);
    }

    public void OnResumeButtonClicked()
    {
        if (true == isClosing || true == isOpeningOption) return;

        Hide();
        if (null != ResumeButtonClickedEvent)
        {
            ResumeButtonClickedEvent.Invoke();
        }
    }

    public void OnOptionButtonClicked()
    {
        if (true == isClosing || true == isOpeningOption) return;

        isOpeningOption = true;

        // 2. ESC Menu -> Option 전환 연출 시작 시 Lock
        if (true == lockOnTransitionToOption)
        {
            DispatchInputLock(true);
        }

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
        if (true == isOpeningOption) return;

        if (null != optionUI && true == optionUI.gameObject.activeInHierarchy)
        {
            optionUI.Hide();
        }
    }

    private void OnOptionMenuCloseCompleted()
    {
        isOpeningOption = false;

        if (null != optionUI)
        {
            if (null == cachedOptionClosed)
                cachedOptionClosed = OnOptionClosed;

            optionUI.Show(cachedOptionClosed);
        }

        // 2-1. Option Panel 등장(오픈) 완료 시 Unlock
        if (true == lockOnTransitionToOption)
        {
            DispatchInputLock(false);
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
            // 3. Option -> ESC Menu 복귀 연출 시작 시 Lock
            if (true == lockOnTransitionFromOption)
            {
                DispatchInputLock(true);
            }

            if (null == cachedReturnFromOptionCompleted)
            {
                cachedReturnFromOptionCompleted = OnReturnFromOptionCompleted;
            }

            escapeMenu.PlayOpenProduction(cachedReturnFromOptionCompleted);
        }
    }

    private void OnReturnFromOptionCompleted()
    {
        // 3-1. ESC Menu 복귀 연출 완료 시 Unlock
        if (true == lockOnTransitionFromOption)
        {
            DispatchInputLock(false);
        }
    }

    private void DispatchInputLock(bool _isLocked)
    {
        if (null != UIInputLockChangedEvent)
        {
            UIInputLockChangedEvent.Invoke(_isLocked);
        }
    }

    public void OnGoToMainMenuButtonClicked()
    {
        if (true == isClosing || true == isOpeningOption) return;

        if (null != GoToMainMenuButtonClickedEvent)
        {
            GoToMainMenuButtonClickedEvent.Invoke();
        }
    }

    public void OnExitButtonClicked()
    {
        if (true == isClosing || true == isOpeningOption) return;

        if (null != ExitButtonClickedEvent)
        {
            ExitButtonClickedEvent.Invoke();
        }
    }

    public void OnSaveGameButton()
    {
        if (true == isClosing || true == isOpeningOption) return;

        if (null != SaveGameButtonClickedEvent)
        {
            SaveGameButtonClickedEvent.Invoke();
        }
    }
}
