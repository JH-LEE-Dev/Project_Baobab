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
    [SerializeField] private UI_WarningPopup warningPopup;

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

    // 지금 이 뷰가 입력 잠금을 쥐고 있는지.
    //
    // 잠금을 푸는 일은 등장/퇴장 연출의 완료 콜백(OnMenuAppearCompleted 등)이 담당하는데, 그 연출이
    // 끝나기 전에 다른 연출이 시작되면 UI_EscapeMenu.KillProductionSequences()가 진행 중인 시퀀스를
    // Kill()로 죽인다. DOTween의 Kill()은 OnComplete를 발동시키지 않으므로 그 콜백은 통째로 사라지고,
    // 결과적으로 잠금이 영구히 남아 ESC가 죽는다. (패드로 ESC 메뉴를 열자마자 B로 닫으면 재현)
    //
    // 그래서 "연출이 끝났는가"에 의존하지 않고 소유 여부를 직접 들고 있다가, 상태가 바뀌는 모든
    // 지점에서 ReleaseInputLock()으로 확실히 반납한다.
    private bool isInputCurrentlyLocked = false;
    // 설치 시점(GameplayUIInstaller)에도 UIManager.Open()이 Show()를 호출했다가 곧바로 Hide()하는데,
    // 이때의 Hide는 닫기 연출을 거치는 비동기라 OnHide가 몇 프레임 뒤에 온다. 그 사이 오디오가
    // 먹먹해지고 루프가 꺼지는 게 메인 메뉴에서 실제로 들리므로, 진짜 일시정지(ShowPauseMenu)로
    // 열린 경우에만 오디오를 건드린다. escapeMenu.SetSoundsEnabled와 같은 취지의 플래그다.
    private bool isPauseShow = false;

    // 이 메뉴를 열기 직전의 입력 모드. 닫을 때 Gameplay를 박는 대신 이 값으로 되돌린다.
    //
    // 입력 모드는 스택이 아니라 단일 값이라, 이미 UI 모드를 쥐고 있는 창(결과창 등) 위로 이 메뉴가
    // 열렸다가 닫히면 그 창이 아직 UI 모드여야 하는데도 Gameplay로 풀려버린다.
    // 실제로 탈진 사망 결과창에서 그 경로가 열려 있었다.
    private EInputMode inputModeBeforeShow = EInputMode.Gameplay;

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

            if (null == warningPopup)
            {
                warningPopup = _instance.GetComponentInChildren<UI_WarningPopup>(true);
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

        if (null == warningPopup)
        {
            warningPopup = GetComponentInChildren<UI_WarningPopup>(true);
        }

        if (null != warningPopup)
        {
            warningPopup.Initialize(_ctx);
        }

        if (null != escapeMenu)
        {
            escapeMenu.Initialize(
                _ctx?.localizationManager,
                OnResumeButtonClicked,
                OnOptionButtonClicked,
                OnGoToMainMenuButtonClicked,
                OnExitButtonClicked,
                warningPopup,
                _ctx?.inputManager);
        }

        if (null != optionUI)
        {
            optionUI.Initialize(_ctx);
        }
    }

    public override void OnDestroy()
    {
        // 잠금을 쥔 채로 파괴되면(씬 전환 등) InputManager는 살아남아 그 잠금이 다음 씬까지 따라간다.
        // 반드시 UIInputLockChangedEvent를 비우기 전에 반납해야 통지가 실제로 전달된다.
        ReleaseInputLock();

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
        // 직전 퇴장 연출이 완료 콜백을 남긴 채 교체될 수 있으므로, 새 연출을 시작하기 전에 반납한다.
        ReleaseInputLock();

        isClosing = false;
        isOpeningOption = false;
        base.Show();
    }

    public void ShowPauseMenu()
    {
        if (null != escapeMenu)
            escapeMenu.SetSoundsEnabled(true);

        isPauseShow = true;
        Show();
    }

    public override void Hide()
    {
        if (false == IsVisible || true == isClosing || true == isOpeningOption) return;

        // 아래 PlayCloseProduction()이 아직 재생 중인 등장 연출을 죽이면서 그 완료 콜백(=잠금 해제)을
        // 함께 없앤다. 그러기 전에 여기서 확실히 반납한다.
        ReleaseInputLock();

        if (null != optionUI && true == optionUI.gameObject.activeInHierarchy)
        {
            optionUI.Hide();
        }

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.HideImmediately();
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

    public void HideImmediately()
    {
        ReleaseInputLock();

        isClosing = false;
        isOpeningOption = false;

        if (null != optionUI && true == optionUI.gameObject.activeInHierarchy)
        {
            optionUI.Hide();
        }

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.HideImmediately();
        }

        base.Hide();
        gameObject.SetActive(false);

        if (null != escapeMenu)
            escapeMenu.SetSoundsEnabled(false);
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        // 반드시 UI로 바꾸기 전에 읽는다.
        inputModeBeforeShow = viewCtx?.inputManager?.CurrentInputMode ?? EInputMode.Gameplay;
        viewCtx?.inputManager?.SetInputMode(EInputMode.UI);

        // ESC 메뉴 = 일시정지. BGM을 먹먹하게 하고, 게임플레이 사운드는 통째로 죽여
        // BGM과 이 메뉴의 조작음만 들리게 한다.
        if (true == isPauseShow)
        {
            Sound.RequestAudioDuck();
            Sound.SetGameplayAudioMuted(true);
        }

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
        // 어떤 경로로 닫히든 마지막 안전망. (Hide/HideImmediately/OnCloseProductionFinished가 이미
        // 반납했다면 멱등 가드에 걸려 아무 일도 하지 않는다)
        ReleaseInputLock();

        // Gameplay를 박지 않고 열기 직전 값으로 되돌린다. (inputModeBeforeShow 참고)
        // 원래 Gameplay였다면 결과는 종전과 완전히 동일하다.
        viewCtx?.inputManager?.SetInputMode(inputModeBeforeShow);

        base.OnHide();

        if (true == isPauseShow)
        {
            isPauseShow = false;
            Sound.ReleaseAudioDuck();
            Sound.SetGameplayAudioMuted(false);
        }
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
        ReleaseInputLock();

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

        if (null != escapeMenu)
        {
            escapeMenu.SelectOptionButton();
        }
    }

    private void DispatchInputLock(bool _isLocked)
    {
        // 같은 상태를 두 번 통지하지 않는다. 중복 잠금은 해제 한 번으로 안 풀리는 사고를,
        // 중복 해제는 다른 시스템의 잠금까지 건드리는 사고를 만든다.
        if (_isLocked == isInputCurrentlyLocked) return;

        isInputCurrentlyLocked = _isLocked;

        if (null != UIInputLockChangedEvent)
        {
            UIInputLockChangedEvent.Invoke(_isLocked);
        }
    }

    /// <summary>
    /// 쥐고 있던 입력 잠금을 반납합니다. 연출이 중간에 교체되어 완료 콜백이 유실되더라도 잠금이
    /// 남지 않도록, 상태가 바뀌는 모든 지점에서 호출합니다. 쥔 것이 없으면 아무 일도 하지 않습니다.
    /// </summary>
    private void ReleaseInputLock()
    {
        DispatchInputLock(false);
    }

    public void OnGoToMainMenuButtonClicked()
    {
        if (true == isClosing || true == isOpeningOption) return;

        HideImmediately();

        if (null != GoToMainMenuButtonClickedEvent)
        {
            GoToMainMenuButtonClickedEvent.Invoke();
        }
    }

    public void OnExitButtonClicked()
    {
        if (true == isClosing || true == isOpeningOption) return;

        HideImmediately();

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
