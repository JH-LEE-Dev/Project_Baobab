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

    /// <summary>
    /// 패드 조준 스틱의 원시 벡터입니다. (데드존 처리된 -1~1)
    /// 여기서는 화면·월드 좌표를 알지 못하므로 방향만 전달하고, 해석은 소비하는 쪽이 합니다.
    /// </summary>
    public event Action<Vector2> AimEvent;
    public event Action InventoryKeyEvent;

    public event Action MouseClickEvent;
    public event Action MouseReleaseEvent;
    public event Action ESCButtonPressedEvent;
    public event Action InteractionKeyPressedEvent;
    public event Action InteractionKeyCanceledEvent;
    public event Action PotionKeyPressedEvent;

    /// <summary>키 바인딩이 실제로 변경(리바인딩 완료/리셋)될 때 발생합니다. UI가 표시 문자열을 다시 조회하도록 알리는 용도입니다.</summary>
    public event Action KeyBindingsChangedEvent;

    /// <summary>유저가 조작에 쓰는 장치가 키보드/마우스 ↔ 게임패드로 바뀔 때 발생합니다.</summary>
    public event Action<EInputDeviceType> InputDeviceChangedEvent;

    /// <summary>표시할 패드 아이콘 세트가 바뀔 때 발생합니다. (패드 교체, 옵션에서 수동 지정 변경)</summary>
    public event Action<EGamepadIconSet> GamepadIconSetChangedEvent;

    /// <summary>패드가 물리적으로 연결/해제될 때 발생합니다. (true = 하나 이상 연결됨)</summary>
    public event Action<bool> GamepadConnectionChangedEvent;

    /// <summary>입력 모드(게임플레이 ↔ UI)가 바뀔 때 발생합니다.</summary>
    public event Action<EInputMode> InputModeChangedEvent;

    /// <summary>UI의 취소(패드 B/○)입니다. 키보드 ESC는 기존대로 ESCButtonPressedEvent로 옵니다.</summary>
    public event Action UICancelEvent;

    /// <summary>UI 탭 전환입니다. -1 = 왼쪽(LB/PageUp), +1 = 오른쪽(RB/PageDown).</summary>
    public event Action<int> UITabShiftEvent;

    //내부 의존성
    private InputActionSystem actions;

    private InputDeviceTracker deviceTracker;

    // 진동과 같은 이유로 액션 해석과 분리해 둔다. 이 객체는 "화면 좌표 하나"만 책임진다.
    private readonly GamepadVirtualCursor virtualCursor = new GamepadVirtualCursor();

    /// <summary>
    /// 패드용 가상 커서입니다. 마을처럼 화면의 임의 지점을 가리켜야 하는 곳에서 씁니다.
    /// 켜고 끄는 것은 유저(오른쪽 스틱 누르기)가 하고, 쓸 수 있는 상황인지는 게임 쪽이 정합니다.
    /// </summary>
    public GamepadVirtualCursor VirtualCursor => virtualCursor;

    private EInputMode currentInputMode = EInputMode.Gameplay;

    private static readonly ERebindableAction[] rebindableActions = (ERebindableAction[])Enum.GetValues(typeof(ERebindableAction));

    // 장치별로 "그 장치에 실제 바인딩이 있는" 액션만 추려 둔 목록.
    // 리바인딩으로도 액션이 장치를 넘나들 수는 없으므로(StartRebind가 다른 장치의 컨트롤을
    // 제외한다) 한 번 만들면 바뀌지 않는다. 그래서 매번 만들지 않고 캐싱한다.
    private ERebindableAction[] keyboardRebindableActions;
    private ERebindableAction[] gamepadRebindableActions;

    // ESC는 메뉴 토글용으로 예약되어 있어, 다른 액션에 재할당하지 못하게 막는다.
    private const string RESERVED_ESCAPE_PATH = "<Keyboard>/escape";

    // 패드로 리바인딩을 취소하는 버튼. 게임플레이 키 배치와 무관한 UI 관례(뒤로가기 = B/○)이므로
    // 배치가 정해지기 전에도 고정해 둘 수 있다.
    private const string RESERVED_GAMEPAD_CANCEL_PATH = "<Gamepad>/buttonEast";

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

    // 마을에서 던전을 클릭해 선택을 확정한 시점부터 던전 입장 연출이 끝날 때까지 Space(인벤토리)를 막는다.
    private bool bPauseInventory = false;

    public void Initialize(InputDeviceSettings _deviceSettings)
    {
        if (null == deviceTracker)
        {
            deviceTracker = new InputDeviceTracker();

            deviceTracker.DeviceChangedEvent += OnInputDeviceChanged;
            deviceTracker.IconSetChangedEvent += OnGamepadIconSetChanged;
            deviceTracker.GamepadConnectionChangedEvent += OnGamepadConnectionChanged;
        }

        deviceTracker.Initialize(_deviceSettings);

        virtualCursor.Initialize(_deviceSettings);

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

            actions.Normal.Aim.performed += OnAim;
            actions.Normal.Aim.canceled += OnAim;

            actions.Normal.VirtualCursor.performed += OnVirtualCursorToggle;

            actions.UI.Cancel.performed += OnUICancel;
            actions.UI.TabLeft.performed += OnUITabLeft;
            actions.UI.TabRight.performed += OnUITabRight;
        }

        BuildDeviceActionLists();

        actions.Normal.Enable();

        // UI 맵은 항상 켜 둔다. 모드에 따라 껐다 켜는 것이 아니라, 게임플레이 쪽 전달만 막는 방식이다.
        // (탭 전환·취소는 UI가 떠 있을 때만 의미가 있으니 구독자 쪽에서 판단한다)
        actions.UI.Enable();
    }

    public void Release()
    {
        // 리바인딩 도중 씬이 정리되는 상황(예: 옵션 창을 닫지 않고 씬 전환)에서 콜백을 태우지 않고 조용히 정리한다.
        rebindOperation?.Dispose();
        rebindOperation = null;

        // Release가 두 번 불릴 수 있어(InputManager.Release + OnDestroy) 구독 해제는 멱등해야 한다.
        deviceTracker?.Release();
        virtualCursor.Release();

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

        actions.Normal.Aim.performed -= OnAim;
        actions.Normal.Aim.canceled -= OnAim;

        actions.Normal.VirtualCursor.performed -= OnVirtualCursorToggle;

        actions.UI.Cancel.performed -= OnUICancel;
        actions.UI.TabLeft.performed -= OnUITabLeft;
        actions.UI.TabRight.performed -= OnUITabRight;

        actions.UI.Disable();
    }

    /// <summary>
    /// InputManager가 매 프레임 호출합니다. 이벤트로 처리할 수 없는, 상태를 계속 지켜봐야 하는
    /// 것들만 여기서 돕니다. (장치 자동 전환 판정, 가상 커서 이동, 리바인딩 취소 감시)
    /// 일시정지 중에도 전부 동작해야 하므로 unscaled 델타를 넘겨야 합니다.
    /// </summary>
    public void Tick(float _unscaledDeltaTime)
    {
        deviceTracker?.Tick(_unscaledDeltaTime);
        TickVirtualCursor(_unscaledDeltaTime);
        UpdateGamepadRebindCancel();
    }

    /// <summary>
    /// 리바인딩 대기 중 패드로 취소하는 경로입니다.
    ///
    /// Input System의 WithCancelingThrough는 취소 경로를 하나만 받아서 ESC가 이미 차지하고 있습니다.
    /// 그래서 패드 취소는 여기서 직접 감시합니다. 이게 없으면 패드만 쓰는 유저는 키 설정 창에서
    /// 키 입력 대기 상태에 들어간 뒤 빠져나올 방법이 없습니다. (패드 입력은 위에서 제외되어 있어
    /// 아무 버튼을 눌러도 리바인딩이 끝나지 않습니다)
    /// </summary>
    private void UpdateGamepadRebindCancel()
    {
        if (null == rebindOperation) return;

        Gamepad _gamepad = Gamepad.current;
        if (null == _gamepad) return;

        if (true == _gamepad.buttonEast.wasPressedThisFrame)
        {
            CancelRebind();
        }
    }

    // 입력 장치 (키보드/마우스 ↔ 게임패드)

    /// <summary>지금 유저가 조작에 쓰고 있는 장치입니다. 물리적 연결 여부가 아니라 "마지막으로 실제 입력이 들어온 쪽"입니다.</summary>
    public EInputDeviceType CurrentDevice => null != deviceTracker ? deviceTracker.CurrentDevice : EInputDeviceType.KeyboardMouse;

    /// <summary>CurrentDevice == Gamepad 의 편의 표현입니다.</summary>
    public bool IsGamepadMode => null != deviceTracker && deviceTracker.IsGamepadMode;

    /// <summary>패드가 물리적으로 하나 이상 연결되어 있는지입니다. 실제 사용 여부와는 별개입니다.</summary>
    public bool IsGamepadConnected => null != deviceTracker && deviceTracker.IsGamepadConnected;

    /// <summary>화면에 그릴 패드 아이콘 세트입니다. 수동 지정이 켜져 있으면 그 값, 아니면 자동 판별 결과입니다.</summary>
    public EGamepadIconSet CurrentGamepadIconSet => null != deviceTracker ? deviceTracker.CurrentIconSet : EGamepadIconSet.Generic;

    /// <summary>수동 지정과 무관한 자동 판별 결과입니다. 옵션에서 "자동 (Xbox)"처럼 보여줄 때 씁니다.</summary>
    public EGamepadIconSet DetectedGamepadIconSet => null != deviceTracker ? deviceTracker.DetectedIconSet : EGamepadIconSet.Generic;

    /// <summary>
    /// 이번 프레임에 어느 장치에서든 "의도적인 조작"이 들어왔는지입니다.
    /// "아무 키나 누르세요" 화면이나 유휴 타이머 해제처럼, 무슨 키인지는 상관없고
    /// 입력이 있었다는 사실만 필요한 곳에서 씁니다.
    ///
    /// 주의: InputManager.Update에서 갱신되므로 스크립트 실행 순서에 따라 최대 1프레임 늦게
    /// 보일 수 있습니다. 위 용도에서는 문제가 되지 않지만, 프레임 정확도가 필요한 곳에는 쓰지 마세요.
    /// </summary>
    public bool AnyInputThisFrame => null != deviceTracker && deviceTracker.AnyInputThisFrame;

    /// <summary>
    /// 패드 아이콘 표기를 수동으로 고정합니다. _bUseOverride가 false면 자동 판별로 되돌립니다.
    /// (Steam Input이나 서드파티 어댑터 때문에 자동 판별이 틀리는 경우가 반드시 생기므로 필요합니다)
    /// </summary>
    public void SetGamepadIconSetOverride(bool _bUseOverride, EGamepadIconSet _iconSet)
    {
        deviceTracker?.SetIconSetOverride(_bUseOverride, _iconSet);
    }

    /// <summary>
    /// 장치 표기를 즉시 강제 전환합니다. 연출/튜토리얼 같은 예외 상황용이며,
    /// 다음 실제 입력이 들어오면 다시 자동 판별로 돌아갑니다.
    /// </summary>
    public void ForceInputDevice(EInputDeviceType _device)
    {
        deviceTracker?.ForceDevice(_device);
    }

    // 입력 모드 (게임플레이 ↔ UI)

    /// <summary>지금 입력이 게임플레이로 가는지 UI로 가는지입니다.</summary>
    public EInputMode CurrentInputMode => currentInputMode;

    /// <summary>
    /// 입력 모드를 바꿉니다. 팝업·메뉴를 열 때 UI로, 닫을 때 Gameplay로 되돌리세요.
    ///
    /// UI로 바꾸는 순간 이동 입력을 0으로 흘려보냅니다. 그러지 않으면 키를 누른 채 창이 열렸을 때
    /// 캐릭터가 계속 걸어갑니다. (PauseMove와 같은 이유)
    /// </summary>
    public void SetInputMode(EInputMode _mode)
    {
        if (currentInputMode == _mode) return;

        currentInputMode = _mode;

        if (EInputMode.UI == _mode)
        {
            MoveEvent?.Invoke(Vector2.zero);
        }
        else if (false == bPauseMove)
        {
            MoveEvent?.Invoke(keyboardMoveInput);
        }

        InputModeChangedEvent?.Invoke(currentInputMode);
    }

    /// <summary>게임플레이 입력을 지금 전달해도 되는지입니다.</summary>
    private bool CanDispatchGameplay => EInputMode.Gameplay == currentInputMode;

    private void OnUICancel(InputAction.CallbackContext context)
    {
        UICancelEvent?.Invoke();
    }

    private void OnUITabLeft(InputAction.CallbackContext context)
    {
        UITabShiftEvent?.Invoke(-1);
    }

    private void OnUITabRight(InputAction.CallbackContext context)
    {
        UITabShiftEvent?.Invoke(1);
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        InputDeviceChangedEvent?.Invoke(_device);
    }

    private void OnGamepadIconSetChanged(EGamepadIconSet _iconSet)
    {
        GamepadIconSetChangedEvent?.Invoke(_iconSet);
    }

    private void OnGamepadConnectionChanged(bool _bConnected)
    {
        GamepadConnectionChangedEvent?.Invoke(_bConnected);
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

        if (bPauseMove || false == CanDispatchGameplay)
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

    private void OnAim(InputAction.CallbackContext context)
    {
        if (false == CanDispatchGameplay) return;

        // 가상 커서가 떠 있으면 같은 오른쪽 스틱이 커서를 몰고 있다. 그 입력을 조준으로도
        // 흘려보내면 커서를 움직일 때마다 캐릭터가 함께 홱홱 돌아간다.
        // 여기서 막으면 조준은 마지막으로 겨눈 방향에 그대로 멈춰 있는다.
        if (true == virtualCursor.IsActive) return;

        AimEvent?.Invoke(context.ReadValue<Vector2>());
    }

    /// <summary>
    /// 가상 커서 토글(오른쪽 스틱 누르기)입니다.
    ///
    /// 이 액션은 패드에만 바인딩되어 있고 리바인딩 대상(ERebindableAction)에도 넣지 않았습니다.
    /// 키보드/마우스에는 이미 진짜 커서가 있어 대응하는 키가 존재할 이유가 없고,
    /// 대응 키가 없는 항목을 키 설정 화면에 올리면 빈 칸만 보이기 때문입니다.
    /// </summary>
    private void OnVirtualCursorToggle(InputAction.CallbackContext context)
    {
        // UI가 입력을 가져간 동안에는 켜고 끄지 않는다. 팝업 위에서 스틱을 눌렀을 때
        // 뒤에 있는 월드용 커서가 튀어나오면 UI 조작과 뒤엉킨다.
        if (false == CanDispatchGameplay) return;

        // 장치 표기를 즉시 패드로 넘긴다. 이 액션은 패드에만 바인딩되어 있으므로,
        // 눌렸다는 사실 자체가 "지금 패드를 쓰고 있다"는 확실한 증거다.
        //
        // 없으면 생기는 문제: 마우스를 만진 직후 0.3초(전환 쿨다운) 안에 이 버튼을 누르면
        // 장치가 아직 키보드/마우스로 남아 있어서, 켜진 커서가 같은 프레임의 Tick에서
        // 곧바로 스스로 꺼진다. 유저 눈에는 버튼이 씹힌 것으로 보인다.
        ForceInputDevice(EInputDeviceType.Gamepad);

        virtualCursor.Toggle();
    }

    /// <summary>
    /// 가상 커서를 매 프레임 움직입니다.
    ///
    /// 스틱 값을 장치에서 직접 읽지 않고 Aim 액션에서 읽는 이유: 조준 스틱을 다른 컨트롤로
    /// 리바인딩하면 커서도 같은 컨트롤을 따라가야 하기 때문입니다. 액션의 데드존 처리도 함께 받습니다.
    /// </summary>
    private void TickVirtualCursor(float _unscaledDeltaTime)
    {
        if (false == virtualCursor.IsActive) return;
        if (null == actions) return;

        virtualCursor.Tick(_unscaledDeltaTime, actions.Normal.Aim.ReadValue<Vector2>(), IsGamepadMode);
    }

    private void OnMouseMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();

        MouseMoveEvent?.Invoke(move);
    }

    private void OnMouseClick(InputAction.CallbackContext context)
    {
        if (false == CanDispatchGameplay) return;

        MouseClickEvent?.Invoke();
    }

    private void OnMouseReleased(InputAction.CallbackContext context)
    {
        // 떼는 신호는 모드와 무관하게 항상 전달한다.
        // 공격을 누른 상태에서 팝업이 열리면 이 신호가 사라져 공격이 눌린 채로 남는다.
        MouseReleaseEvent?.Invoke();
    }

    private void ClearAllEvent()
    {
        MoveEvent = null;
    }

    private void OnInventoryKeyPressed(InputAction.CallbackContext context)
    {
        if (bPauseInventory)
            return;

        InventoryKeyEvent?.Invoke();
    }

    private void InteractionKeyPressed(InputAction.CallbackContext context)
    {
        if (false == CanDispatchGameplay) return;

        if (0 >= pauseInteractCount)
            InteractionKeyPressedEvent?.Invoke();
    }

    private void InteractionKeyCanceled(InputAction.CallbackContext context)
    {
        // 떼는 신호는 언제나 전달한다. (OnMouseReleased 주석 참고)
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

    public void PauseInventoryKey(bool _boolean)
    {
        bPauseInventory = _boolean;
    }

    public void PotionKeyPressed(InputAction.CallbackContext context)
    {
        if (false == CanDispatchGameplay) return;

        PotionKeyPressedEvent?.Invoke();
    }

    // 키 리바인딩
    public bool IsRebinding => null != rebindOperation;

    /// <summary>
    /// 키 설정 화면에 그릴 액션 목록입니다. (키보드/마우스 기준)
    ///
    /// 인자 없는 다른 API와 마찬가지로 키보드/마우스를 뜻합니다. 패드 탭을 그릴 때는
    /// 반드시 장치 인자 버전을 쓰세요. 두 장치의 항목 수는 서로 다릅니다.
    /// </summary>
    public IReadOnlyList<ERebindableAction> GetRebindableActions()
    {
        return GetRebindableActions(EInputDeviceType.KeyboardMouse);
    }

    /// <summary>
    /// 그 장치에 실제 바인딩이 있는 액션만 순서대로 돌려줍니다.
    ///
    /// 장치마다 목록이 다른 이유: VirtualCursor(마을 가상 커서)는 패드에만 있습니다.
    /// 전체 목록을 그대로 돌면 키보드 탭에 빈 칸 행이 하나 끼어듭니다.
    ///
    /// 행과 액션을 인덱스로 짝지어 쓰는 UI라면, 행을 만들 때와 갱신할 때 **같은 장치의**
    /// 목록을 써야 합니다. 섞으면 라벨과 키가 어긋납니다.
    /// </summary>
    public IReadOnlyList<ERebindableAction> GetRebindableActions(EInputDeviceType _device)
    {
        ERebindableAction[] _list = (EInputDeviceType.Gamepad == _device)
            ? gamepadRebindableActions
            : keyboardRebindableActions;

        // Initialize 이전에 불리면 아직 목록이 없다. 전체를 돌려주는 편이 빈 목록보다 덜 위험하다.
        return null != _list ? _list : (IReadOnlyList<ERebindableAction>)rebindableActions;
    }

    private void BuildDeviceActionLists()
    {
        keyboardRebindableActions = BuildDeviceActionList(EInputDeviceType.KeyboardMouse);
        gamepadRebindableActions = BuildDeviceActionList(EInputDeviceType.Gamepad);
    }

    private ERebindableAction[] BuildDeviceActionList(EInputDeviceType _device)
    {
        int _count = 0;

        for (int i = 0; i < rebindableActions.Length; i++)
        {
            if (true == HasBindingFor(rebindableActions[i], _device)) _count++;
        }

        ERebindableAction[] _list = new ERebindableAction[_count];
        int _index = 0;

        for (int i = 0; i < rebindableActions.Length; i++)
        {
            if (false == HasBindingFor(rebindableActions[i], _device)) continue;

            _list[_index] = rebindableActions[i];
            _index++;
        }

        return _list;
    }

    /// <summary>UI에 표시할 현재 바인딩 문자열입니다. (예: "W", "Left Shift") 키보드/마우스 기준입니다.</summary>
    public string GetBindingDisplayString(ERebindableAction _action)
    {
        return GetBindingDisplayString(_action, EInputDeviceType.KeyboardMouse);
    }

    /// <summary>현재 바인딩된 컨트롤의 원본 경로입니다. (예: "&lt;Keyboard&gt;/w") 아이콘 스프라이트 매핑용으로, 표시 문자열과 달리 로케일에 흔들리지 않습니다.</summary>
    public string GetBindingPath(ERebindableAction _action)
    {
        return GetBindingPath(_action, EInputDeviceType.KeyboardMouse);
    }

    // 장치별 바인딩 조회
    //
    // 위의 인자 하나짜리 GetBindingDisplayString / GetBindingPath는 "항상 키보드/마우스 바인딩"을
    // 돌려줍니다. 키 설정 화면은 어떤 장치를 쓰고 있든 키보드 배치를 보여줘야 하므로 그대로 둡니다.
    // 화면에 띄우는 조작 안내(HUD 프롬프트 등)처럼 현재 장치를 따라가야 하는 곳만
    // 아래의 장치 인자 버전을 쓰세요.

    /// <summary>
    /// 지정한 장치에 해당하는 바인딩 경로입니다. 그 장치용 바인딩이 아직 없으면 null입니다.
    /// (패드 바인딩을 .inputactions에 추가하기 전에는 Gamepad로 조회하면 항상 null입니다)
    /// </summary>
    public string GetBindingPath(ERebindableAction _action, EInputDeviceType _device)
    {
        if (false == TryGetBindingTarget(_action, _device, out InputAction _inputAction, out int _bindingIndex)) return null;

        return _inputAction.bindings[_bindingIndex].effectivePath;
    }

    /// <summary>
    /// 지정한 장치에 해당하는 표시 문자열입니다. 그 장치용 바인딩이 아직 없으면 빈 문자열입니다.
    /// </summary>
    public string GetBindingDisplayString(ERebindableAction _action, EInputDeviceType _device)
    {
        if (false == TryGetBindingTarget(_action, _device, out InputAction _inputAction, out int _bindingIndex)) return string.Empty;

        return _inputAction.GetBindingDisplayString(_bindingIndex);
    }

    /// <summary>현재 사용 중인 장치 기준의 바인딩 경로입니다. 해당 장치용 바인딩이 없으면 null입니다.</summary>
    public string GetBindingPathForCurrentDevice(ERebindableAction _action)
    {
        return GetBindingPath(_action, CurrentDevice);
    }

    /// <summary>현재 사용 중인 장치 기준의 표시 문자열입니다. 해당 장치용 바인딩이 없으면 빈 문자열입니다.</summary>
    public string GetBindingDisplayStringForCurrentDevice(ERebindableAction _action)
    {
        return GetBindingDisplayString(_action, CurrentDevice);
    }

    /// <summary>
    /// 지정한 액션에 그 장치용 바인딩이 존재하는지입니다.
    /// UI는 이 값이 false면 프롬프트 아이콘을 숨기는 식으로 쓰면 됩니다.
    /// (예: 마우스 조준처럼 패드에 대응 바인딩이 없는 액션)
    /// </summary>
    public bool HasBindingFor(ERebindableAction _action, EInputDeviceType _device)
    {
        return TryGetBindingTarget(_action, _device, out _, out _);
    }

    /// <summary>
    /// 액션과 장치로 실제 바인딩 인덱스를 찾습니다.
    ///
    /// 키보드/마우스는 GetBindingTarget이 갖고 있는 고정 인덱스를 그대로 씁니다.
    /// 패드는 바인딩이 어떤 순서로 추가될지 알 수 없으므로 인덱스를 하드코딩하지 않고
    /// 액션의 바인딩 목록을 훑어서 찾습니다. 덕분에 나중에 .inputactions에 패드 바인딩을
    /// 어떤 순서로 넣어도 이 코드는 손댈 필요가 없습니다.
    ///
    /// 단, 키보드 쪽 인덱스는 여전히 하드코딩이므로 패드 바인딩은 반드시 기존 바인딩
    /// **뒤에 추가**해야 합니다. 중간에 끼워 넣으면 키보드 인덱스가 밀려 키 설정 화면이 깨집니다.
    /// </summary>
    private bool TryGetBindingTarget(ERebindableAction _action, EInputDeviceType _device, out InputAction _inputAction, out int _bindingIndex)
    {
        GetBindingTarget(_action, out _inputAction, out int _keyboardIndex);

        if (EInputDeviceType.Gamepad != _device)
        {
            _bindingIndex = _keyboardIndex;

            // 패드에만 있는 액션(VirtualCursor)은 이 자리에 패드 경로가 들어 있다.
            // 그대로 돌려주면 키 설정 화면의 키보드 탭에 "Right Stick Press" 같은 행이 뜨고,
            // 거기서 변경을 누르면 패드 바인딩이 키보드 키로 덮어써진다.
            return false == IsGamepadBindingPath(_inputAction.bindings[_keyboardIndex].effectivePath);
        }

        // Move처럼 컴포지트의 한 파트를 가리키는 액션이면, 같은 파트 이름을 가진 패드 바인딩을 먼저 찾는다.
        // (D-Pad를 2DVector 컴포지트로 구성한 경우가 여기에 걸린다)
        string _partName = _inputAction.bindings[_keyboardIndex].isPartOfComposite
            ? _inputAction.bindings[_keyboardIndex].name
            : null;

        int _fallbackIndex = -1;

        for (int i = 0; i < _inputAction.bindings.Count; i++)
        {
            InputBinding _binding = _inputAction.bindings[i];

            if (true == _binding.isComposite) continue;
            if (false == IsGamepadBindingPath(_binding.effectivePath)) continue;

            if (null != _partName)
            {
                if (true == _binding.isPartOfComposite
                    && true == string.Equals(_binding.name, _partName, StringComparison.OrdinalIgnoreCase))
                {
                    _bindingIndex = i;
                    return true;
                }

                // 스틱 하나를 Move에 직접 물린 경우엔 파트가 없다. 컴포지트 파트를 못 찾았을 때의 대안으로 남겨둔다.
                if (false == _binding.isPartOfComposite && _fallbackIndex < 0)
                {
                    _fallbackIndex = i;
                }

                continue;
            }

            _bindingIndex = i;
            return true;
        }

        if (_fallbackIndex >= 0)
        {
            _bindingIndex = _fallbackIndex;
            return true;
        }

        _bindingIndex = -1;
        return false;
    }

    /// <summary>
    /// 바인딩 경로가 게임패드 계열 장치를 가리키는지입니다.
    /// 문자열 앞부분을 직접 비교하지 않고 레이아웃 상속을 확인하므로,
    /// &lt;Gamepad&gt; 뿐 아니라 &lt;XInputController&gt;, &lt;DualShockGamepad&gt; 같은
    /// 구체 레이아웃으로 바인딩해도 올바르게 판정됩니다.
    /// </summary>
    private static bool IsGamepadBindingPath(string _path)
    {
        if (true == string.IsNullOrEmpty(_path)) return false;

        string _layout = InputControlPath.TryGetDeviceLayout(_path);
        if (true == string.IsNullOrEmpty(_layout)) return false;

        return InputSystem.IsFirstLayoutBasedOnSecond(_layout, "Gamepad");
    }

    /// <summary>지정한 액션이 다른 리바인딩 가능한 액션과 같은 키를 쓰고 있는지 여부입니다.</summary>
    public bool IsConflicting(ERebindableAction _action)
    {
        return IsConflicting(_action, EInputDeviceType.KeyboardMouse);
    }

    /// <summary>
    /// 지정한 장치 안에서 이 액션이 다른 액션과 같은 입력을 쓰고 있는지 여부입니다.
    ///
    /// 장치를 넘나드는 비교는 하지 않습니다. 키보드의 E와 패드의 A는 서로 다른 장치의
    /// 입력이라 동시에 눌릴 일이 없으므로 충돌이 아닙니다.
    /// </summary>
    public bool IsConflicting(ERebindableAction _action, EInputDeviceType _device)
    {
        // 유저가 바꿀 수 없는 항목은 충돌을 만들 수도 없다.
        //
        // 이 검사가 없으면 패드에서 MoveUp/Down/Left/Right가 전부 같은 leftStick 바인딩을
        // 가리키기 때문에 서로를 중복으로 신고하고, HasAnyConflict가 영원히 true가 되어
        // 키 설정 저장이 통째로 막힌다.
        if (false == IsRebindable(_action, _device)) return false;

        if (false == TryGetBindingTarget(_action, _device, out InputAction _inputAction, out int _bindingIndex)) return false;

        string _path = _inputAction.bindings[_bindingIndex].effectivePath;
        return TryFindConflict(_action, _device, _path, out _);
    }

    /// <summary>
    /// 중복된 입력이 하나라도 있는지 여부입니다. (모든 장치를 통틀어)
    ///
    /// 저장 차단 판단에 쓰이므로 일부러 장치를 가리지 않습니다. 키보드 탭만 보고 저장을 허용하면
    /// 패드 쪽 중복이 그대로 파일에 기록되어 버립니다.
    /// 특정 탭의 표시용으로는 장치 인자 버전을 쓰세요.
    /// </summary>
    public bool HasAnyConflict()
    {
        return HasAnyConflict(EInputDeviceType.KeyboardMouse) || HasAnyConflict(EInputDeviceType.Gamepad);
    }

    /// <summary>지정한 장치 안에 중복된 입력이 하나라도 있는지 여부입니다.</summary>
    public bool HasAnyConflict(EInputDeviceType _device)
    {
        for (int i = 0; i < rebindableActions.Length; i++)
        {
            if (true == IsConflicting(rebindableActions[i], _device)) return true;
        }

        return false;
    }

    /// <summary>
    /// 지정한 액션을 그 장치에서 유저가 바꿀 수 있는지 여부입니다.
    /// UI는 false인 항목의 "변경" 버튼을 비활성화하면 됩니다. (표시는 그대로 하세요)
    /// </summary>
    public bool IsRebindable(ERebindableAction _action, EInputDeviceType _device)
    {
        // 그 장치에 바인딩이 없으면 바꿀 대상 자체가 없다. (패드 전용 항목의 키보드 탭 등)
        if (EInputDeviceType.Gamepad != _device) return HasBindingFor(_action, _device);

        if (false == GamepadDefaultBindings.IsRebindableOnGamepad(_action)) return false;

        // 패드 바인딩이 없는 액션은 바꿀 대상 자체가 없다.
        return HasBindingFor(_action, EInputDeviceType.Gamepad);
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
        StartRebind(_action, EInputDeviceType.KeyboardMouse, _onFinished);
    }

    /// <summary>
    /// 지정한 장치의 바인딩을 다시 잡습니다.
    /// 바꿀 수 없는 조합(IsRebindable == false)이면 아무 일도 하지 않고 Canceled로 통지합니다.
    /// </summary>
    public void StartRebind(ERebindableAction _action, EInputDeviceType _device, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        if (null != rebindOperation)
        {
            CancelRebind();
        }

        if (false == IsRebindable(_action, _device)
            || false == TryGetBindingTarget(_action, _device, out InputAction _inputAction, out int _bindingIndex))
        {
            _onFinished?.Invoke(ERebindResult.Canceled, null);
            return;
        }

        _inputAction.Disable();

        InputActionRebindingExtensions.RebindingOperation _rebind = _inputAction.PerformInteractiveRebinding(_bindingIndex)
            .WithCancelingThrough(RESERVED_ESCAPE_PATH);

        if (EInputDeviceType.Gamepad == _device)
        {
            // 패드 바인딩을 잡는 중에는 키보드/마우스가 끼어들면 안 된다.
            _rebind = _rebind
                .WithControlsExcluding("Keyboard")
                .WithControlsExcluding("Mouse")
                // B/○는 취소 전용이라 새 바인딩으로 잡히면 안 된다. 잡히는 순간
                // 그 유저는 다음부터 리바인딩 대기에서 빠져나올 수단을 잃는다.
                .WithControlsExcluding(RESERVED_GAMEPAD_CANCEL_PATH);
        }
        else
        {
            // Attack은 기본값 자체가 마우스 좌클릭이라 마우스 버튼도 허용해야 한다.
            // 그 외 액션은 키보드 전용이므로, 마우스 이동/클릭이 실수로 잡히지 않도록 제외한다.
            if (ERebindableAction.Attack != _action)
            {
                _rebind = _rebind.WithControlsExcluding("Mouse");
            }

            // 여기서 대상으로 넘기는 인덱스는 키보드/마우스 쪽이다. 패드를 제외하지 않으면
            // 유저가 패드 버튼을 눌렀을 때 그 키보드 바인딩이 패드 경로로 덮어써져,
            // 해당 기능의 키보드 키가 조용히 사라진다.
            _rebind = _rebind.WithControlsExcluding("Gamepad");
        }

        InputActionRebindingExtensions.RebindingOperation _operation = _rebind
            .OnComplete(_op => CompleteRebind(_action, _device, _inputAction, _bindingIndex, _onFinished))
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

    private void CompleteRebind(ERebindableAction _action, EInputDeviceType _device, InputAction _inputAction, int _bindingIndex, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        DisposeRebindOperation();
        _inputAction.Enable();

        string _newPath = _inputAction.bindings[_bindingIndex].effectivePath;

        KeyBindingsChangedEvent?.Invoke();

        if (true == TryFindConflict(_action, _device, _newPath, out ERebindableAction _conflict))
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

    private bool TryFindConflict(ERebindableAction _self, EInputDeviceType _device, string _path, out ERebindableAction _conflict)
    {
        if (true == string.IsNullOrEmpty(_path))
        {
            _conflict = default;
            return false;
        }

        for (int i = 0; i < rebindableActions.Length; i++)
        {
            ERebindableAction _candidate = rebindableActions[i];
            if (_candidate == _self) continue;

            // 바꿀 수 없는 항목은 충돌 상대가 되지 않는다. (위 IsConflicting의 주석 참고)
            if (false == IsRebindable(_candidate, _device)) continue;

            // 그 장치에 바인딩이 없는 액션은 비교 대상이 아니다.
            if (false == TryGetBindingTarget(_candidate, _device, out InputAction _candidateAction, out int _candidateIndex)) continue;

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
        ResetBinding(_action, EInputDeviceType.KeyboardMouse);
    }

    /// <summary>지정한 장치의 바인딩만 기본값으로 되돌립니다. (편집 세션 내 변경일 뿐, 저장은 CommitEditSession에서)</summary>
    public void ResetBinding(ERebindableAction _action, EInputDeviceType _device)
    {
        if (false == TryGetBindingTarget(_action, _device, out InputAction _inputAction, out int _bindingIndex)) return;

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
            case ERebindableAction.Attack: _inputAction = actions.Normal.Click; _bindingIndex = 0; break;
            case ERebindableAction.PotionKey: _inputAction = actions.Normal.PotionKey; _bindingIndex = 0; break;

            // 패드 전용이라 여기서 돌려주는 인덱스 0도 패드 바인딩이다.
            // TryGetBindingTarget이 "키보드 자리에 패드 경로가 있으면 없는 것으로 친다"로
            // 걸러내므로, 키보드 쪽 조회에는 이 값이 새어 나가지 않는다.
            case ERebindableAction.VirtualCursor: _inputAction = actions.Normal.VirtualCursor; _bindingIndex = 0; break;

            default: throw new ArgumentOutOfRangeException(nameof(_action), _action, null);
        }
    }
}
