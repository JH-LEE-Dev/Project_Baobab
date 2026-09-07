using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세이브 파일을 읽을 수 있는지 확인하는 동안, 그리고 끝내 읽지 못했을 때 띄우는 전체 화면 뷰입니다.
///
/// [이 화면이 존재하는 이유]
/// 세이브 파일이 백신이나 런처에 잠겨 잠깐 안 읽히는 일이 있습니다. 파일 내용은 멀쩡한데도요.
/// 그때 아무 말 없이 메인 메뉴를 띄우면 "이어하기"가 사라져 보이고, 유저는 세이브가 날아간 줄 알고
/// 새 게임을 눌러 멀쩡한 진행도를 스스로 지웁니다. 그걸 막는 것이 이 화면의 전부입니다.
///
/// [그래서 반드시 지켜야 하는 것]
/// 이 화면이 떠 있는 동안 그 뒤로 아무것도 눌리면 안 됩니다. 각 패널은 자기만의 전체 화면 블로커
/// (raycastTarget이 켜진 이미지)를 갖고 있어야 하고, 캔버스 sortingOrder는 메인 메뉴보다 위여야 합니다.
/// ESC로 닫히면 안 되므로 UIView의 bCloseableByESC는 반드시 꺼둔 상태로 두세요.
///
/// [담당 구분]
/// 아래 이벤트 세 개를 올려보내고 ApplyState를 받는 부분은 시스템 담당입니다. 건드리지 마세요.
/// 그 아래 패널/버튼 참조와 OnShow/OnHide 계열 훅은 UI 담당입니다. 자세한 내용은
/// Docs/SaveCheckUI.md 를 보세요.
/// </summary>
public class UIView_SaveCheck : UIView
{
    // // 시스템으로 올라가는 신호 (SaveCheckCoordinator만 구독합니다)

    /// <summary>"다시 시도"를 눌렀습니다.</summary>
    public event Action RetryRequestedEvent;

    /// <summary>"기존 세이브를 포기하고 새로 시작"을 확인 팝업까지 거쳐 최종 확정했습니다.</summary>
    public event Action AbandonConfirmedEvent;

    /// <summary>"게임 종료"를 눌렀습니다.</summary>
    public event Action QuitRequestedEvent;

    // // 여기서부터 UI 담당 영역

    [Header("Panels")]
    [SerializeField, Tooltip("\"세이브 파일을 확인 중입니다...\" 화면. 전체 화면 블로커를 포함해야 합니다.")]
    private GameObject checkingPanel;

    [SerializeField, Tooltip("읽기에 최종 실패했을 때의 화면. 다시 시도 / 새로 시작 / 종료 세 버튼만 둡니다.")]
    private GameObject failedPanel;

    [SerializeField, Tooltip("\"기존 진행도를 덮어씁니다\" 최종 확인 팝업. 되돌릴 수 없는 선택이라 반드시 한 단계 거칩니다.")]
    private GameObject abandonConfirmPanel;

    [Header("Buttons - Failed Panel")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button abandonButton;
    [SerializeField] private Button quitButton;

    [Header("Buttons - Abandon Confirm Panel")]
    [SerializeField] private Button abandonConfirmButton;
    [SerializeField] private Button abandonCancelButton;

    [Header("Behaviour")]
    [SerializeField, Tooltip("확인이 이 시간보다 빨리 끝나면 화면을 아예 띄우지 않습니다. " +
        "정상적인 경우 확인은 한 프레임 안에 끝나므로, 0으로 두면 부팅 때마다 화면이 깜빡입니다.")]
    private float checkingPanelDelaySeconds = 0.3f;

    private ESaveCheckState currentState = ESaveCheckState.NotStarted;
    private float currentElapsedSeconds;
    private bool bAbandonConfirmOpen;

    protected override void Awake()
    {
        base.Awake();

        BindButtons();
        RefreshPanels();
    }

    // // 시스템 담당 영역 (아래는 건드리지 마세요)

    /// <summary>
    /// 확인 상태를 반영합니다. SaveCheckCoordinator가 매 프레임 부릅니다.
    /// 같은 값이 계속 들어오므로, 실제로 바뀐 프레임에만 훅이 불리도록 안에서 걸러냅니다.
    /// </summary>
    public void ApplyState(ESaveCheckState _state, float _elapsedSeconds)
    {
        bool _bStateChanged = (currentState != _state);

        currentState = _state;
        currentElapsedSeconds = _elapsedSeconds;

        // 확인이 끝났으면 열려 있던 확인 팝업도 함께 닫는다.
        // (다시 시도가 그새 성공한 경우다. 포기 여부를 물을 이유가 사라졌다)
        if (_bStateChanged && ESaveCheckState.Failed != _state)
        {
            bAbandonConfirmOpen = false;
        }

        RefreshPanels();
    }

    private void BindButtons()
    {
        BindButton(retryButton, OnRetryClicked);
        BindButton(abandonButton, OnAbandonClicked);
        BindButton(quitButton, OnQuitClicked);
        BindButton(abandonConfirmButton, OnAbandonConfirmClicked);
        BindButton(abandonCancelButton, OnAbandonCancelClicked);
    }

    private static void BindButton(Button _button, UnityEngine.Events.UnityAction _action)
    {
        if (null == _button) return;

        _button.onClick.RemoveListener(_action);
        _button.onClick.AddListener(_action);
    }

    private void OnRetryClicked()
    {
        bAbandonConfirmOpen = false;
        RefreshPanels();

        RetryRequestedEvent?.Invoke();
    }

    // 곧바로 포기시키지 않는다. 한 번 더 묻는 단계가 이 흐름의 유일한 안전장치다.
    private void OnAbandonClicked()
    {
        bAbandonConfirmOpen = true;
        RefreshPanels();

        OnAbandonConfirmOpened();
    }

    private void OnAbandonCancelClicked()
    {
        bAbandonConfirmOpen = false;
        RefreshPanels();

        OnAbandonConfirmClosed();
    }

    private void OnAbandonConfirmClicked()
    {
        bAbandonConfirmOpen = false;
        RefreshPanels();

        AbandonConfirmedEvent?.Invoke();
    }

    private void OnQuitClicked()
    {
        QuitRequestedEvent?.Invoke();
    }

    private void RefreshPanels()
    {
        // 확인이 순식간에 끝나는 정상적인 경우에는 아무것도 띄우지 않는다.
        bool _bShowChecking = (ESaveCheckState.Checking == currentState)
                           && (currentElapsedSeconds >= checkingPanelDelaySeconds);

        bool _bFailed = (ESaveCheckState.Failed == currentState);

        SetPanelActive(checkingPanel, _bShowChecking);
        SetPanelActive(failedPanel, _bFailed && false == bAbandonConfirmOpen);
        SetPanelActive(abandonConfirmPanel, _bFailed && bAbandonConfirmOpen);

        if (_bShowChecking || _bFailed) Show();
        else Hide();
    }

    private static void SetPanelActive(GameObject _panel, bool _bActive)
    {
        if (null == _panel) return;
        if (_panel.activeSelf == _bActive) return;

        _panel.SetActive(_bActive);
    }

    // // 다시 UI 담당 영역 - 연출을 붙이려면 여기를 채우세요

    /// <summary>이 화면이 처음 떠오를 때 한 번 불립니다. 페이드 인 같은 연출 자리입니다.</summary>
    protected override void OnShow() { }

    /// <summary>이 화면이 완전히 사라질 때 한 번 불립니다.</summary>
    protected override void OnHide() { }

    /// <summary>포기 확인 팝업이 열릴 때 불립니다.</summary>
    protected virtual void OnAbandonConfirmOpened() { }

    /// <summary>포기 확인 팝업이 취소로 닫힐 때 불립니다.</summary>
    protected virtual void OnAbandonConfirmClosed() { }
}
