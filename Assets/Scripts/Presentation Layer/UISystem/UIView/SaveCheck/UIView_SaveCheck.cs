using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 세이브 파일을 읽을 수 있는지 확인하는 동안(Checking), 그리고 끝내 읽지 못했을 때(Failed)
/// UI_WarningPopup을 띄워 유저에게 재시도 또는 진행 여부를 묻는 뷰입니다.
///
/// [동작 구조]
/// - Checking 상태: 100% 불투명 검은색 배경(checkingPanel)만 노출되어 뒷배경을 완벽히 차단.
/// - Failed 상태: checkingPanel 유지 상태에서 내장된 UI_WarningPopup을 호출.
///   - Depth 1 (실패 안내): "저장 데이터를 불러오지 못했습니다.\n일시적인 오류일 수 있습니다. 다시 불러오시겠습니까?"
///     - [Confirm (OK.png)]: 재시도(Retry) -> RetryRequestedEvent 호출.
///     - [Cancel (Cancel.png)]: 1뎁스 닫힘 연출(0.25초) 후 2뎁스 자동 호출.
///   - Depth 2 (진행 재확인): "저장 데이터를 불러오지 않고 계속할까요?\n이후 새 게임을 시작하면 기존 진행 데이터가 덮어쓰일 수 있습니다."
///     - [Confirm (OK.png)]: 진행(Abandon) -> AbandonConfirmedEvent 호출.
///     - [Cancel (Cancel.png)]: 2뎁스 닫힘 연출(0.25초) 후 1뎁스 자동 복귀.
/// - CursorBox & Gamepad:
///   - 캔버스 내에 자체 배치된 UIView_CursorBox를 UI_WarningPopup에 연결하여
///     부팅 극초기(메인메뉴 생성 전)에도 버튼 호버 커서박스 및 패드 첫 포커싱 완벽 지원.
/// </summary>
public class UIView_SaveCheck : UIView
{
    // // 시스템 이벤트
    public event Action RetryRequestedEvent;
    public event Action AbandonConfirmedEvent;

    // // 인스펙터 직렬화 필드 (내부 의존성 및 UI 컴포넌트)
    [Header("Panels & Popups")]
    [SerializeField, Tooltip("\"세이브 파일을 확인 중입니다...\" 검은색 블로커 패널")]
    private GameObject checkingPanel;

    [SerializeField, Tooltip("경고/확인 팝업 컴포넌트")]
    private UI_WarningPopup warningPopup;

    [SerializeField, Tooltip("부팅 극초기 커서 표시를 위한 CursorBox")]
    private UIView_CursorBox cursorBox;

    [Header("Behaviour")]
    [SerializeField, Tooltip("확인이 이 시간보다 빨리 끝나면 화면을 아예 띄우지 않습니다.")]
    private float checkingPanelDelaySeconds = 0.3f;

    [Header("Messages")]
    [SerializeField, TextArea(2, 4)]
    private string depth1FailedMessage = "저장 데이터를 불러오지 못했습니다.\n일시적인 오류일 수 있습니다. 다시 불러오시겠습니까?";

    [SerializeField, TextArea(2, 4)]
    private string depth2AbandonMessage = "저장 데이터를 불러오지 않고 계속할까요?\n이후 새 게임을 시작하면 기존 진행 데이터가 덮어쓰일 수 있습니다.";

    // // 외부 의존성
    private InputManager inputManager;
    private LocalizationManager localizationManager;

    // // 상태 변수
    private ESaveCheckState currentState = ESaveCheckState.NotStarted;
    private float currentElapsedSeconds;
    private bool bHasShownFailedPopup;
    private Tween delayedDepthTransitionTween;

    // // 캐시된 델리게이트 (GC Zero)
    private Action cachedOnRetryClicked;
    private Action cachedOnDepth1CancelClicked;
    private Action cachedOnAbandonConfirmed;
    private Action cachedOnDepth2CancelClicked;
    private TweenCallback cachedShowAbandonConfirmDepth2;
    private TweenCallback cachedShowFailedDepth1;

    // // 1. 퍼블릭 초기화 및 제어 메서드
    public void InitializeDependencies(InputManager _inputManager, LocalizationManager _localizationManager)
    {
        inputManager = _inputManager;
        localizationManager = _localizationManager;
        SetupWarningPopup();
    }

    public void InitializeInput(InputManager _inputManager)
    {
        InitializeDependencies(_inputManager, null);
    }

    public void ApplyState(ESaveCheckState _newState, float _elapsedSeconds)
    {
        currentState = _newState;
        currentElapsedSeconds = _elapsedSeconds;

        switch (currentState)
        {
            case ESaveCheckState.NotStarted:
                HideAll();
                bHasShownFailedPopup = false;
                break;

            case ESaveCheckState.Checking:
                if (checkingPanelDelaySeconds <= currentElapsedSeconds)
                {
                    if (null != checkingPanel && false == checkingPanel.activeSelf)
                    {
                        checkingPanel.SetActive(true);
                    }
                }
                bHasShownFailedPopup = false;
                break;

            case ESaveCheckState.Ready:
                HideAll();
                bHasShownFailedPopup = false;
                break;

            case ESaveCheckState.Failed:
                if (null != checkingPanel && false == checkingPanel.activeSelf)
                {
                    checkingPanel.SetActive(true);
                }

                if (false == bHasShownFailedPopup)
                {
                    bHasShownFailedPopup = true;
                    ShowFailedDepth1();
                }
                break;
        }
    }

    // // 2. 일반 비즈니스 로직 및 내부 메서드
    private void SetupWarningPopup()
    {
        if (null == warningPopup)
        {
            return;
        }

        if (null != cursorBox)
        {
            cursorBox.gameObject.SetActive(true);
            warningPopup.SetCursorBoxUI(cursorBox);
        }

        if (null != inputManager)
        {
            warningPopup.Initialize(inputManager, cursorBox);
        }
    }

    private void ShowFailedDepth1()
    {
        KillDelayedTween();

        if (null == warningPopup)
        {
            return;
        }

        SetupWarningPopup();

        string _displayMsg = null != localizationManager ? localizationManager.GetText("FailedMessage") : null;
        if (true == string.IsNullOrEmpty(_displayMsg))
        {
            _displayMsg = depth1FailedMessage;
        }

        warningPopup.ShowWarning(
            _displayMsg,
            _onConfirm: cachedOnRetryClicked,
            _onCancel: cachedOnDepth1CancelClicked,
            _openSoundId: SoundID.ResultUIOpen,
            _closeSoundId: SoundID.ResultUIClose,
            _hoverSoundId: SoundID.ResultUIHover);
    }

    private void OnDepth1CancelClicked()
    {
        KillDelayedTween();
        float _delay = null != warningPopup ? warningPopup.AnimationDuration : 0.25f;
        delayedDepthTransitionTween = DOVirtual.DelayedCall(_delay, cachedShowAbandonConfirmDepth2).SetLink(gameObject);
    }

    private void ShowAbandonConfirmDepth2()
    {
        KillDelayedTween();

        if (null == warningPopup)
        {
            return;
        }

        SetupWarningPopup();

        string _displayMsg = null != localizationManager ? localizationManager.GetText("AbandonMessage") : null;
        if (true == string.IsNullOrEmpty(_displayMsg))
        {
            _displayMsg = depth2AbandonMessage;
        }

        warningPopup.ShowWarning(
            _displayMsg,
            _onConfirm: cachedOnAbandonConfirmed,
            _onCancel: cachedOnDepth2CancelClicked,
            _openSoundId: SoundID.ResultUIOpen,
            _closeSoundId: SoundID.ResultUIClose,
            _hoverSoundId: SoundID.ResultUIHover);
    }

    private void OnDepth2CancelClicked()
    {
        KillDelayedTween();
        float _delay = null != warningPopup ? warningPopup.AnimationDuration : 0.25f;
        delayedDepthTransitionTween = DOVirtual.DelayedCall(_delay, cachedShowFailedDepth1).SetLink(gameObject);
    }

    private void OnRetryClicked()
    {
        KillDelayedTween();
        RetryRequestedEvent?.Invoke();
    }

    private void OnAbandonConfirmed()
    {
        KillDelayedTween();
        AbandonConfirmedEvent?.Invoke();
    }

    private void HideAll()
    {
        KillDelayedTween();

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.HideImmediately();
        }

        if (null != checkingPanel)
        {
            checkingPanel.SetActive(false);
        }
    }

    private void KillDelayedTween()
    {
        if (null != delayedDepthTransitionTween)
        {
            delayedDepthTransitionTween.Kill();
            delayedDepthTransitionTween = null;
        }
    }

    // // 3. 유니티 라이프사이클 이벤트 함수 (SW_Rules에 따라 최하단 배치)
    protected override void Awake()
    {
        base.Awake();

        cachedOnRetryClicked = OnRetryClicked;
        cachedOnDepth1CancelClicked = OnDepth1CancelClicked;
        cachedOnAbandonConfirmed = OnAbandonConfirmed;
        cachedOnDepth2CancelClicked = OnDepth2CancelClicked;
        cachedShowAbandonConfirmDepth2 = ShowAbandonConfirmDepth2;
        cachedShowFailedDepth1 = ShowFailedDepth1;

        if (null != checkingPanel)
        {
            checkingPanel.SetActive(false);
        }

        if (null != warningPopup)
        {
            warningPopup.gameObject.SetActive(false);
        }
    }

    protected virtual void OnDisable()
    {
        KillDelayedTween();
    }

    public override void OnDestroy()
    {
        KillDelayedTween();

        cachedOnRetryClicked = null;
        cachedOnDepth1CancelClicked = null;
        cachedOnAbandonConfirmed = null;
        cachedOnDepth2CancelClicked = null;
        cachedShowAbandonConfirmDepth2 = null;
        cachedShowFailedDepth1 = null;

        inputManager = null;
        localizationManager = null;

        base.OnDestroy();
    }
}
