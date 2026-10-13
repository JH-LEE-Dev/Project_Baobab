using System;
using System.Collections.Generic;

/// <summary>
/// 로그라이크의 전체 화면 UI(카드 선택·결과·순위표·보유 카드) 공통 동작.
///
/// 열릴 때 입력 모드를 UI로 바꾸고 이동을 잠근다. 닫힐 때의 처리는 RoguelikeModalInput이 맡는다 -
/// 열린 모달이 하나도 남지 않았을 때만, 그 순간 로그라이크 시스템이 원하는 조작 상태로 되돌린다.
/// (예전처럼 모달마다 "열기 직전 값"을 저장해 되돌리면, 열린 순서와 다르게 닫히거나 시스템이 그 사이 조작을
///  바꾼 경우 낡은 값이 덮어써진다 - 전투 중 이동 잠금, 카드 선택 중 Gameplay 입력 모드)
///
/// [UI 담당 구현 지점] 상속한 뷰의 OnShowContent/OnHideContent에 등장·퇴장 연출을 넣는다.
/// 게임패드 기본 선택(EventSystem.SetSelectedGameObject)과 CursorBox(viewCtx.cursorBoxUI) 연결도 각 뷰가 한다.
/// </summary>
public abstract class UIView_RoguelikeModalBase : UIView
{
    /// <summary>화면이 닫혔다(어떤 경로로든 - 닫기 버튼·ESC·시스템). 상속한 뷰가 OnHideContent를 덮어써도 빠지지 않는다.</summary>
    public event Action HiddenEvent;

    protected sealed override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        RoguelikeModalInput.NotifyOpened(this, viewCtx?.inputManager);

        OnShowContent();
    }

    protected sealed override void OnHide()
    {
        base.OnHide();

        OnHideContent();

        RoguelikeModalInput.NotifyClosed(this, viewCtx?.inputManager);

        gameObject.SetActive(false);

        HiddenEvent?.Invoke();
    }

    /// <summary>[UI 담당] 화면이 켜진 직후. 등장 연출·기본 선택을 여기서 한다.</summary>
    protected virtual void OnShowContent() { }

    /// <summary>[UI 담당] 화면이 꺼지기 직전.</summary>
    protected virtual void OnHideContent() { }

    public override void OnDestroy()
    {
        // 열린 채로 파괴되면(인스톨러 해제) 목록에서만 뺀다. 입력은 건드리지 않는다 - 곧 다음 씬이 자기 상태를 건다.
        HiddenEvent = null;
        RoguelikeModalInput.Forget(this);
        base.OnDestroy();
    }
}

/// <summary>
/// 로그라이크 전체 화면 UI가 함께 쓰는 입력 잠금.
///
///   - 모달이 하나라도 열려 있으면: 입력 모드 UI, 이동 잠금.
///   - 마지막 모달이 닫히면: 로그라이크 시스템이 알려 준 조작 상태(SetGameplayControl)로 맞춘다.
///     입력 모드는 첫 모달이 열리기 직전 값으로 되돌린다(ESC 메뉴 등 다른 UI의 모드를 덮지 않기 위해).
///   - 모달이 열려 있는 동안 시스템이 조작을 허용하면, 그 적용은 마지막 모달이 닫힐 때로 미룬다.
///
/// 본편에서는 쓰이지 않는다(로그라이크 뷰와 RoguelikeSystem만 부른다).
/// </summary>
public static class RoguelikeModalInput
{
    private static readonly List<UIView_RoguelikeModalBase> openModals = new List<UIView_RoguelikeModalBase>(4);
    private static EInputMode inputModeBeforeFirstModal = EInputMode.Gameplay;
    private static bool bGameplayControlEnabled;

    public static bool HasOpenModal => openModals.Count > 0;

    /// <summary>
    /// 로그라이크 시스템의 조작 허용/잠금. 모달이 없으면 바로 적용하고, 있으면 마지막 모달이 닫힐 때 적용한다.
    /// _input이 null이면 상태만 기록한다(쓰러짐처럼 다른 시스템이 이미 입력을 잠근 경우).
    /// </summary>
    public static void SetGameplayControl(bool _bEnabled, InputManager _input)
    {
        bGameplayControlEnabled = _bEnabled;

        if (openModals.Count > 0 || null == _input) return;

        if (_bEnabled)
        {
            _input.SetInputMode(EInputMode.Gameplay);
            _input.PauseMove(false);
        }
        else
        {
            _input.PauseMove(true);
        }
    }

    /// <summary>세션 시작·끝. 열린 모달 목록과 조작 상태를 비운다.</summary>
    public static void Reset()
    {
        openModals.Clear();
        inputModeBeforeFirstModal = EInputMode.Gameplay;
        bGameplayControlEnabled = false;
    }

    public static void NotifyOpened(UIView_RoguelikeModalBase _view, InputManager _input)
    {
        if (null == _view || openModals.Contains(_view)) return;

        if (0 == openModals.Count)
        {
            inputModeBeforeFirstModal = null != _input ? _input.CurrentInputMode : EInputMode.Gameplay;
        }

        openModals.Add(_view);

        if (null == _input) return;
        _input.SetInputMode(EInputMode.UI);
        _input.PauseMove(true);
    }

    public static void NotifyClosed(UIView_RoguelikeModalBase _view, InputManager _input)
    {
        if (false == openModals.Remove(_view)) return;
        if (openModals.Count > 0 || null == _input) return;

        _input.SetInputMode(bGameplayControlEnabled ? EInputMode.Gameplay : inputModeBeforeFirstModal);
        _input.PauseMove(false == bGameplayControlEnabled);
    }

    public static void Forget(UIView_RoguelikeModalBase _view)
    {
        openModals.Remove(_view);
    }
}
