using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader
{
    //이벤트
    public event Action<Vector2> MoveEvent;
    public event Action MoveTriggerEvent;
    public event Action<Vector2> MouseMoveEvent;
    public event Action InventoryKeyEvent;

    public event Action MouseClickEvent;
    public event Action MouseReleaseEvent;
    public event Action ESCButtonPressedEvent;
    public event Action InteractionKeyPressedEvent;
    public event Action InteractionKeyCanceledEvent;
    public event Action PotionKeyPressedEvent;

    /// <summary>키 바인딩이 실제로 변경(리바인딩 완료/리셋)될 때 발생합니다. UI가 표시 문자열을 다시 조회하도록 알리는 용도입니다.</summary>
    public event Action KeyBindingsChangedEvent;

    //내부 의존성
    private InputActionSystem actions;

    private static readonly ERebindableAction[] rebindableActions = (ERebindableAction[])Enum.GetValues(typeof(ERebindableAction));

    // ESC는 메뉴 토글용으로 예약되어 있어, 다른 액션에 재할당하지 못하게 막는다.
    private const string RESERVED_ESCAPE_PATH = "<Keyboard>/escape";

    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    // 키 설정 UI가 열려 있는 동안의 편집 세션 스냅샷. "취소"로 닫으면 이 시점으로 되돌린다.
    // null이면 편집 세션이 시작되지 않은 것이다.
    private string editSessionSnapshotJson;

    private bool bPauseMove = false;

    private Vector2 keyboardMoveInput;

    // 단순 bool이 아닌 카운터인 이유: 던전 진입 연출(TownProductionManager.StartSkyProduction 등)과
    // 내비게이션 팝업 해금 연출(GameplayUICoordinator) 등 서로 다른 시스템이 겹치는 타이밍에 각자
    // Pause(true/false)를 걸 수 있는데, bool이면 한쪽이 먼저 false를 걸어 다른 쪽이 아직 막아야 하는
    // 구간에서 조기 해제되어 버린다.
    private int pauseInteractCount = 0;

    private bool bPauseESC = false;

    public void Initialize()
    {
        if (actions == null)
        {
            actions = new InputActionSystem();

            LoadKeyBindings();

            actions.Normal.ESC.performed += OnESCButtonPressed;

            // Move 액션 바인딩 추가
            actions.Normal.Move.performed += OnMove;
            actions.Normal.Move.canceled += OnMove;
            actions.Normal.Mouse.performed += OnMouseMove;
            actions.Normal.Click.performed += OnMouseClick;
            actions.Normal.Click.canceled += OnMouseReleased;
            actions.Normal.Inventory.performed += OnInventoryKeyPressed;
            actions.Normal.Interaction.performed += InteractionKeyPressed;
            actions.Normal.Interaction.canceled += InteractionKeyCanceled;
            actions.Normal.PotionKey.performed += PotionKeyPressed;
        }

        actions.Normal.Enable();
    }

    public void Release()
    {
        // 리바인딩 도중 씬이 정리되는 상황(예: 옵션 창을 닫지 않고 씬 전환)에서 콜백을 태우지 않고 조용히 정리한다.
        rebindOperation?.Dispose();
        rebindOperation = null;

        actions.Normal.Disable();

        actions.Normal.ESC.performed -= OnESCButtonPressed;

        // Move 액션 바인딩 해제
        actions.Normal.Move.performed -= OnMove;
        actions.Normal.Move.canceled -= OnMove;
        actions.Normal.Mouse.performed -= OnMouseMove;
        actions.Normal.Click.performed -= OnMouseClick;
        actions.Normal.Click.canceled -= OnMouseReleased;
        actions.Normal.Inventory.performed -= OnInventoryKeyPressed;
        actions.Normal.Interaction.performed -= InteractionKeyPressed;
        actions.Normal.Interaction.canceled -= InteractionKeyCanceled;
        actions.Normal.PotionKey.performed -= PotionKeyPressed;
    }

    public void PauseMove(bool _bPause)
    {
        bPauseMove = _bPause;

        if (_bPause == true)
        {
            MoveEvent?.Invoke(Vector2.zero);
        }
        else
        {
            MoveEvent?.Invoke(keyboardMoveInput);
        }
    }

    public void ClearPrevKeyboardInput()
    {
        keyboardMoveInput = Vector2.zero;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        keyboardMoveInput = context.ReadValue<Vector2>();

        if (bPauseMove)
        {
            MoveEvent?.Invoke(Vector2.zero);
            return;
        }

        MoveTriggerEvent?.Invoke();
        MoveEvent?.Invoke(keyboardMoveInput);
    }

    private void OnESCButtonPressed(InputAction.CallbackContext context)
    {
        if (LoadingManager.Instance != null && LoadingManager.Instance.IsLoading)
            return;

        if (bPauseESC)
            return;

        ESCButtonPressedEvent?.Invoke();
    }

    private void OnMouseMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();

        MouseMoveEvent?.Invoke(move);
    }

    private void OnMouseClick(InputAction.CallbackContext context)
    {
        MouseClickEvent?.Invoke();
    }

    private void OnMouseReleased(InputAction.CallbackContext context)
    {
        MouseReleaseEvent?.Invoke();
    }

    private void ClearAllEvent()
    {
        MoveEvent = null;
    }

    private void OnInventoryKeyPressed(InputAction.CallbackContext context)
    {
        InventoryKeyEvent?.Invoke();
    }

    private void InteractionKeyPressed(InputAction.CallbackContext context)
    {
        if (0 >= pauseInteractCount)
            InteractionKeyPressedEvent?.Invoke();
    }

    private void InteractionKeyCanceled(InputAction.CallbackContext context)
    {
        InteractionKeyCanceledEvent?.Invoke();
    }

    public void PauseMouse(bool _boolean)
    {
        if (_boolean == true)
        {
            actions.Normal.Mouse.performed -= OnMouseMove;
            actions.Normal.Click.performed -= OnMouseClick;
            actions.Normal.Click.canceled -= OnMouseReleased;
        }
        else
        {
            actions.Normal.Mouse.performed -= OnMouseMove;
            actions.Normal.Click.performed -= OnMouseClick;
            actions.Normal.Click.canceled -= OnMouseReleased;

            actions.Normal.Mouse.performed += OnMouseMove;
            actions.Normal.Click.performed += OnMouseClick;
            actions.Normal.Click.canceled += OnMouseReleased;
        }
    }

    public void PauseInteractKey(bool _boolean)
    {
        if (true == _boolean)
        {
            pauseInteractCount++;
        }
        else if (0 < pauseInteractCount)
        {
            pauseInteractCount--;
        }
    }

    public void PauseESCKey(bool _boolean)
    {
        bPauseESC = _boolean;
    }

    public void PotionKeyPressed(InputAction.CallbackContext context)
    {
        PotionKeyPressedEvent?.Invoke();
    }

    // 키 리바인딩
    public bool IsRebinding => null != rebindOperation;

    public IReadOnlyList<ERebindableAction> GetRebindableActions()
    {
        return rebindableActions;
    }

    /// <summary>UI에 표시할 현재 바인딩 문자열입니다. (예: "W", "Left Shift")</summary>
    public string GetBindingDisplayString(ERebindableAction _action)
    {
        GetBindingTarget(_action, out InputAction _inputAction, out int _bindingIndex);
        return _inputAction.GetBindingDisplayString(_bindingIndex);
    }

    /// <summary>현재 바인딩된 컨트롤의 원본 경로입니다. (예: "&lt;Keyboard&gt;/w") 아이콘 스프라이트 매핑용으로, 표시 문자열과 달리 로케일에 흔들리지 않습니다.</summary>
    public string GetBindingPath(ERebindableAction _action)
    {
        GetBindingTarget(_action, out InputAction _inputAction, out int _bindingIndex);
        return _inputAction.bindings[_bindingIndex].effectivePath;
    }

    /// <summary>지정한 액션이 다른 리바인딩 가능한 액션과 같은 키를 쓰고 있는지 여부입니다.</summary>
    public bool IsConflicting(ERebindableAction _action)
    {
        GetBindingTarget(_action, out InputAction _inputAction, out int _bindingIndex);
        string _path = _inputAction.bindings[_bindingIndex].effectivePath;
        return TryFindConflict(_action, _path, out _);
    }

    /// <summary>편집 세션 내에 중복된 키가 하나라도 있는지 여부입니다. 저장 버튼 활성화 여부 판단에 씁니다.</summary>
    public bool HasAnyConflict()
    {
        for (int i = 0; i < rebindableActions.Length; i++)
        {
            if (true == IsConflicting(rebindableActions[i])) return true;
        }

        return false;
    }

    /// <summary>
    /// 키 설정 UI를 열 때 호출합니다. 이 시점의 바인딩 상태를 스냅샷으로 남겨,
    /// 저장하지 않고 닫았을 때 DiscardEditSession으로 되돌릴 수 있게 합니다.
    /// </summary>
    public void BeginEditSession()
    {
        editSessionSnapshotJson = actions.asset.SaveBindingOverridesAsJson();
    }

    /// <summary>
    /// 편집 세션 동안의 변경분을 모두 버리고 BeginEditSession 시점 상태로 되돌립니다. ("취소"/저장 없이 닫기)
    /// </summary>
    public void DiscardEditSession()
    {
        if (null == editSessionSnapshotJson) return;

        actions.asset.RemoveAllBindingOverrides();
        actions.asset.LoadBindingOverridesFromJson(editSessionSnapshotJson);
        editSessionSnapshotJson = null;

        KeyBindingsChangedEvent?.Invoke();
    }

    /// <summary>
    /// 편집 세션 동안의 변경분을 파일에 실제로 기록합니다. ("저장" 버튼)
    /// 중복된 키가 남아 있으면 저장하지 않고 false를 반환합니다. (UI는 HasAnyConflict로 버튼 자체를 미리 비활성화해야 함)
    /// </summary>
    public bool CommitEditSession()
    {
        if (true == HasAnyConflict()) return false;

        string _json = actions.asset.SaveBindingOverridesAsJson();
        KeyBindingRepository.Save(_json);

        // 저장 시점을 새 기준점으로 삼는다. 저장 후 추가로 리바인딩하다가 취소해도
        // "마지막 저장 상태"로는 돌아가야 하므로, 스냅샷을 비우지 않고 갱신한다.
        editSessionSnapshotJson = _json;

        return true;
    }

    /// <summary>
    /// 지정한 액션의 키 입력을 기다리기 시작합니다. 완료/취소/중복 여부는 _onFinished로 통지됩니다.
    /// Duplicate여도 키는 그대로 적용되며(편집 세션 동안은 중복 허용), UI는 경고만 표시하면 됩니다.
    /// 리바인딩 중에는 대상 액션이 비활성화되어 게임플레이에 반영되지 않습니다.
    /// </summary>
    public void StartRebind(ERebindableAction _action, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        if (null != rebindOperation)
        {
            CancelRebind();
        }

        GetBindingTarget(_action, out InputAction _inputAction, out int _bindingIndex);

        _inputAction.Disable();

        InputActionRebindingExtensions.RebindingOperation _operation = _inputAction.PerformInteractiveRebinding(_bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough(RESERVED_ESCAPE_PATH)
            .OnComplete(_op => CompleteRebind(_action, _inputAction, _bindingIndex, _onFinished))
            .OnCancel(_op => CancelRebindInternal(_inputAction, _onFinished))
            .Start();

        // 극히 드물게 Start() 호출 도중 동기적으로 완료/취소되어(예: 리바인딩을 시작한 프레임에
        // 이미 매칭되는 컨트롤이 눌려 있는 경우) OnComplete/OnCancel이 먼저 실행돼 버릴 수 있다.
        // 그 경우 rebindOperation은 이미 콜백에서 정리(Dispose)되었으므로, 아래에서 되살리지 않는다.
        if (true == _operation.completed || true == _operation.canceled)
        {
            _operation.Dispose();
            return;
        }

        rebindOperation = _operation;
    }

    /// <summary>
    /// 진행 중인 리바인딩을 취소합니다. 컨트롤이 아직 확정되지 않았으므로 원래 키는 그대로 유지됩니다.
    /// </summary>
    public void CancelRebind()
    {
        rebindOperation?.Cancel();
    }

    private void CompleteRebind(ERebindableAction _action, InputAction _inputAction, int _bindingIndex, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        DisposeRebindOperation();
        _inputAction.Enable();

        string _newPath = _inputAction.bindings[_bindingIndex].effectivePath;

        KeyBindingsChangedEvent?.Invoke();

        if (true == TryFindConflict(_action, _newPath, out ERebindableAction _conflict))
        {
            _onFinished?.Invoke(ERebindResult.Duplicate, _conflict);
            return;
        }

        _onFinished?.Invoke(ERebindResult.Success, null);
    }

    private void CancelRebindInternal(InputAction _inputAction, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        DisposeRebindOperation();
        _inputAction.Enable();
        _onFinished?.Invoke(ERebindResult.Canceled, null);
    }

    private void DisposeRebindOperation()
    {
        rebindOperation?.Dispose();
        rebindOperation = null;
    }

    private bool TryFindConflict(ERebindableAction _self, string _path, out ERebindableAction _conflict)
    {
        for (int i = 0; i < rebindableActions.Length; i++)
        {
            ERebindableAction _candidate = rebindableActions[i];
            if (_candidate == _self) continue;

            GetBindingTarget(_candidate, out InputAction _candidateAction, out int _candidateIndex);
            string _candidatePath = _candidateAction.bindings[_candidateIndex].effectivePath;

            if (true == string.Equals(_candidatePath, _path, StringComparison.OrdinalIgnoreCase))
            {
                _conflict = _candidate;
                return true;
            }
        }

        _conflict = default;
        return false;
    }

    /// <summary>지정한 액션을 기본 바인딩으로 되돌립니다. (편집 세션 내 변경일 뿐, 저장은 CommitEditSession에서 이뤄집니다)</summary>
    public void ResetBinding(ERebindableAction _action)
    {
        GetBindingTarget(_action, out InputAction _inputAction, out int _bindingIndex);
        _inputAction.RemoveBindingOverride(_bindingIndex);

        KeyBindingsChangedEvent?.Invoke();
    }

    /// <summary>모든 리바인딩 가능한 액션을 기본 바인딩으로 되돌립니다. (편집 세션 내 변경일 뿐, 저장은 CommitEditSession에서 이뤄집니다)</summary>
    public void ResetAllBindings()
    {
        actions.asset.RemoveAllBindingOverrides();

        KeyBindingsChangedEvent?.Invoke();
    }

    /// <summary>부팅 시 파일에 저장된 바인딩 오버라이드를 불러옵니다.</summary>
    private void LoadKeyBindings()
    {
        if (true == KeyBindingRepository.TryLoad(out string _json))
        {
            actions.asset.LoadBindingOverridesFromJson(_json);
        }
    }

    /// <summary>
    /// ERebindableAction을 실제 InputAction과 바인딩 인덱스로 변환합니다.
    /// Move는 2DVector 컴포지트라 파트 인덱스(1~4)로, 나머지는 단일 바인딩(0)으로 접근합니다.
    /// </summary>
    private void GetBindingTarget(ERebindableAction _action, out InputAction _inputAction, out int _bindingIndex)
    {
        switch (_action)
        {
            case ERebindableAction.MoveUp: _inputAction = actions.Normal.Move; _bindingIndex = 1; break;
            case ERebindableAction.MoveDown: _inputAction = actions.Normal.Move; _bindingIndex = 2; break;
            case ERebindableAction.MoveLeft: _inputAction = actions.Normal.Move; _bindingIndex = 3; break;
            case ERebindableAction.MoveRight: _inputAction = actions.Normal.Move; _bindingIndex = 4; break;
            case ERebindableAction.Inventory: _inputAction = actions.Normal.Inventory; _bindingIndex = 0; break;
            case ERebindableAction.Interaction: _inputAction = actions.Normal.Interaction; _bindingIndex = 0; break;
            case ERebindableAction.SwitchMode: _inputAction = actions.Normal.SwitchMode; _bindingIndex = 0; break;
            case ERebindableAction.AxeMode: _inputAction = actions.Normal.AxeMode; _bindingIndex = 0; break;
            case ERebindableAction.RifleMode: _inputAction = actions.Normal.RifleMode; _bindingIndex = 0; break;
            case ERebindableAction.Reload: _inputAction = actions.Normal.Reload; _bindingIndex = 0; break;
            case ERebindableAction.AimCorrection: _inputAction = actions.Normal.AimCorrection; _bindingIndex = 0; break;
            case ERebindableAction.PotionKey: _inputAction = actions.Normal.PotionKey; _bindingIndex = 0; break;
            default: throw new ArgumentOutOfRangeException(nameof(_action), _action, null);
        }
    }
}
