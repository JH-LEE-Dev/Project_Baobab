using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 지금 유저가 키보드/마우스를 쓰는지 게임패드를 쓰는지, 그리고 그 패드가 어느 벤더인지를 추적합니다.
///
/// 설계 원칙 세 가지:
/// 1. 어느 쪽 입력도 막지 않는다. 키보드와 패드는 항상 동시에 살아 있고, 여기서 바뀌는 것은
///    "무엇을 화면에 보여줄지"뿐이다. 배타 처리를 하면 왼손 키보드 + 오른손 패드 같은 혼용이 깨진다.
/// 2. 마지막으로 실제 조작한 쪽이 이긴다(last-used-device wins). 싱글플레이 PC 게임의 사실상 표준이다.
/// 3. 노이즈는 조작으로 치지 않는다. 문턱값과 쿨다운의 근거는 InputDeviceSettings 주석 참고.
///
/// 액션(InputAction)이 아니라 장치의 물리 컨트롤을 직접 폴링하는 이유:
/// 어떤 키를 어떤 기능에 쓸지가 아직 정해지지 않았어도 "패드를 만졌다"는 사실 자체는 판별할 수
/// 있어야 하기 때문입니다. 덕분에 패드 바인딩이 하나도 없는 지금 상태에서도 그대로 동작합니다.
///
/// InputSystem.onEvent 대신 폴링을 쓰는 이유:
/// onEvent는 매 입력 이벤트마다 콜백이 돌고 변경된 컨트롤을 열거하려면 할당이 발생하는데,
/// 아래처럼 고정된 컨트롤 목록을 프레임당 한 번 읽으면 할당이 전혀 없습니다.
/// </summary>
public class InputDeviceTracker
{
    /// <summary>사용 중인 장치 종류가 바뀌었을 때 발생합니다.</summary>
    public event Action<EInputDeviceType> DeviceChangedEvent;

    /// <summary>표시할 패드 아이콘 세트가 바뀌었을 때 발생합니다. (패드 교체, 수동 지정 변경)</summary>
    public event Action<EGamepadIconSet> IconSetChangedEvent;

    /// <summary>패드의 물리적 연결/해제가 바뀌었을 때 발생합니다. (true = 하나 이상 연결됨)</summary>
    public event Action<bool> GamepadConnectionChangedEvent;

    //내부 의존성
    private InputDeviceSettings settings;

    private EInputDeviceType currentDevice = EInputDeviceType.KeyboardMouse;
    private EGamepadIconSet detectedIconSet = EGamepadIconSet.Generic;

    private bool bUseIconSetOverride = false;
    private EGamepadIconSet overrideIconSet = EGamepadIconSet.Generic;

    // 같은 패드를 매 프레임 다시 분류하지 않기 위한 참조 비교용 캐시.
    private Gamepad classifiedGamepad;

    private bool bGamepadConnected = false;
    private bool bAnyInputThisFrame = false;

    private float switchCooldownRemain = 0f;
    private float mouseTravelAccum = 0f;
    private float mouseIdleAccum = 0f;

    // 이벤트 구독/해제에 같은 델리게이트 인스턴스를 써야 하므로 캐싱한다. (메서드 그룹 변환은 매번 할당)
    private Action<InputDevice, InputDeviceChange> cachedDeviceChangeHandler;

    public EInputDeviceType CurrentDevice => currentDevice;

    public bool IsGamepadMode => EInputDeviceType.Gamepad == currentDevice;

    /// <summary>패드가 물리적으로 하나 이상 연결되어 있는지입니다. 실제 사용 여부와는 별개입니다.</summary>
    public bool IsGamepadConnected => bGamepadConnected;

    /// <summary>
    /// 화면에 그릴 패드 아이콘 세트입니다. 수동 지정(SetIconSetOverride)이 켜져 있으면 그 값이,
    /// 아니면 자동 판별 결과가 나옵니다.
    /// </summary>
    public EGamepadIconSet CurrentIconSet => bUseIconSetOverride ? overrideIconSet : detectedIconSet;

    /// <summary>수동 지정과 무관한 자동 판별 결과입니다. 옵션 UI에서 "자동 (Xbox)"처럼 보여줄 때 씁니다.</summary>
    public EGamepadIconSet DetectedIconSet => detectedIconSet;

    /// <summary>
    /// 이번 프레임에 키보드/마우스/패드 중 어디서든 "의도적인 조작"이 들어왔는지입니다.
    /// 장치 전환 판정과 같은 문턱값을 쓰므로, 책상 진동으로 인한 마우스 지터나 스틱 드리프트는
    /// 여기서도 입력으로 치지 않습니다. ("아무 키나 누르세요" 화면, 유휴 타이머 해제 등에 씁니다)
    /// </summary>
    public bool AnyInputThisFrame => bAnyInputThisFrame;

    public void Initialize(InputDeviceSettings _settings)
    {
        settings = null != _settings ? _settings : InputDeviceSettings.CreateDefault();

        if (null == cachedDeviceChangeHandler)
        {
            cachedDeviceChangeHandler = OnDeviceChange;
        }

        InputSystem.onDeviceChange -= cachedDeviceChangeHandler;
        InputSystem.onDeviceChange += cachedDeviceChangeHandler;

        // 부팅 시점의 상태를 한 번 반영해 둔다. 이벤트는 "변화"만 알려주므로,
        // 이미 패드를 꽂은 채로 게임을 켠 유저는 이 초기 동기화가 없으면 연결을 놓친다.
        bGamepadConnected = Gamepad.all.Count > 0;
        RefreshIconSet();
    }

    public void Release()
    {
        if (null != cachedDeviceChangeHandler)
        {
            InputSystem.onDeviceChange -= cachedDeviceChangeHandler;
        }
    }

    /// <summary>
    /// 매 프레임 호출합니다. 게임이 일시정지(timeScale = 0)여도 장치 전환은 되어야 하므로
    /// 반드시 unscaled 델타를 넘기세요.
    /// </summary>
    public void Tick(float _unscaledDeltaTime)
    {
        if (null == settings) return;

        if (switchCooldownRemain > 0f)
        {
            switchCooldownRemain -= _unscaledDeltaTime;
        }

        // 패드가 바뀌었으면(교체/추가) 아이콘 세트를 다시 판별한다. 참조 비교라 비용이 없다.
        if (classifiedGamepad != Gamepad.current)
        {
            RefreshIconSet();
        }

        // 두 쪽 모두 폴링한다. 마우스 판정은 누적값을 갱신하는 부작용이 있어서
        // 패드가 활성일 때 건너뛰면 누적치가 낡은 채로 남는다.
        bool bKeyboardMouseActive = PollKeyboardMouseActivity(_unscaledDeltaTime);
        bool bGamepadActive = PollGamepadActivity();

        bAnyInputThisFrame = bKeyboardMouseActive || bGamepadActive;

        // 같은 프레임에 둘 다 들어오면 패드를 우선한다. 패드 조작은 문턱값이 높아
        // 오탐 가능성이 낮은 반면, 마우스는 손이 스치기만 해도 잡히기 때문이다.
        if (true == bGamepadActive)
        {
            TrySwitchDevice(EInputDeviceType.Gamepad, false);
        }
        else if (true == bKeyboardMouseActive)
        {
            TrySwitchDevice(EInputDeviceType.KeyboardMouse, false);
        }
    }

    /// <summary>
    /// 패드 아이콘 표기를 수동으로 고정합니다. 자동 판별은 서드파티 어댑터나 Steam Input의
    /// XInput 위장 때문에 반드시 틀리는 경우가 생기므로, 옵션에서 유저가 직접 고를 수 있어야 합니다.
    /// </summary>
    /// <param name="_bUseOverride">false면 자동 판별로 되돌립니다. (이때 _iconSet은 무시됩니다)</param>
    public void SetIconSetOverride(bool _bUseOverride, EGamepadIconSet _iconSet)
    {
        EGamepadIconSet _before = CurrentIconSet;

        bUseIconSetOverride = _bUseOverride;
        overrideIconSet = _iconSet;

        if (_before != CurrentIconSet)
        {
            IconSetChangedEvent?.Invoke(CurrentIconSet);
        }
    }

    /// <summary>
    /// 장치 종류를 즉시 강제 전환합니다. 쿨다운을 무시합니다.
    /// 연출이나 튜토리얼처럼 "지금은 무조건 이 표기로 보여줘야 하는" 예외 상황용이며,
    /// 평소에는 자동 판별에 맡기세요. (다음 입력이 들어오면 다시 자동으로 바뀝니다)
    /// </summary>
    public void ForceDevice(EInputDeviceType _device)
    {
        TrySwitchDevice(_device, true);
    }

    private void TrySwitchDevice(EInputDeviceType _device, bool _bIgnoreCooldown)
    {
        if (currentDevice == _device) return;
        if (false == _bIgnoreCooldown && switchCooldownRemain > 0f) return;

        currentDevice = _device;

        // ForceDevice는 Initialize 이전에도 호출될 수 있다.
        switchCooldownRemain = null != settings ? settings.switchCooldownSeconds : 0f;

        // 전환 직후에 이전 장치의 누적치가 남아 있으면 곧바로 되돌아가 버린다.
        mouseTravelAccum = 0f;
        mouseIdleAccum = 0f;

        DeviceChangedEvent?.Invoke(currentDevice);
    }

    private bool PollKeyboardMouseActivity(float _unscaledDeltaTime)
    {
        Keyboard _keyboard = Keyboard.current;
        if (null != _keyboard && true == _keyboard.anyKey.wasPressedThisFrame) return true;

        Mouse _mouse = Mouse.current;
        if (null == _mouse) return false;

        if (true == _mouse.leftButton.wasPressedThisFrame) return true;
        if (true == _mouse.rightButton.wasPressedThisFrame) return true;
        if (true == _mouse.middleButton.wasPressedThisFrame) return true;

        if (_mouse.scroll.ReadValue().sqrMagnitude > 0f) return true;

        // 마우스 이동은 단발 델타가 아니라 누적 거리로 판정한다. (InputDeviceSettings 주석 참고)
        float _travel = _mouse.delta.ReadValue().magnitude;

        if (_travel <= 0f)
        {
            mouseIdleAccum += _unscaledDeltaTime;

            if (mouseIdleAccum >= settings.mouseTravelResetSeconds)
            {
                mouseTravelAccum = 0f;
            }

            return false;
        }

        mouseIdleAccum = 0f;
        mouseTravelAccum += _travel;

        if (mouseTravelAccum < settings.mouseTravelThresholdPixels) return false;

        // 한 번 인정했으면 누적치를 비운다. 그러지 않으면 값이 무한정 커지고,
        // 패드로 전환한 직후에도 이미 문턱을 넘은 상태라 곧바로 되돌아간다.
        mouseTravelAccum = 0f;
        return true;
    }

    private bool PollGamepadActivity()
    {
        // Gamepad.current는 Input System이 "가장 최근에 입력이 들어온 패드"로 알아서 갱신하므로,
        // 여러 개가 꽂혀 있어도 싱글플레이에서는 이것만 보면 된다.
        Gamepad _gamepad = Gamepad.current;
        if (null == _gamepad) return false;

        float _stickThresholdSqr = settings.stickActuationThreshold * settings.stickActuationThreshold;

        if (_gamepad.leftStick.ReadValue().sqrMagnitude >= _stickThresholdSqr) return true;
        if (_gamepad.rightStick.ReadValue().sqrMagnitude >= _stickThresholdSqr) return true;

        // 트리거는 아날로그라 눌림 판정 대신 깊이로 본다.
        if (_gamepad.leftTrigger.ReadValue() >= settings.triggerActuationThreshold) return true;
        if (_gamepad.rightTrigger.ReadValue() >= settings.triggerActuationThreshold) return true;

        // 표준 게임패드의 디지털 버튼 전체. 바인딩과 무관하게 "패드를 만졌는지"만 보므로
        // 액션이 하나도 연결되지 않은 지금 상태에서도 그대로 동작한다.
        if (true == _gamepad.buttonSouth.wasPressedThisFrame) return true;
        if (true == _gamepad.buttonEast.wasPressedThisFrame) return true;
        if (true == _gamepad.buttonWest.wasPressedThisFrame) return true;
        if (true == _gamepad.buttonNorth.wasPressedThisFrame) return true;

        if (true == _gamepad.leftShoulder.wasPressedThisFrame) return true;
        if (true == _gamepad.rightShoulder.wasPressedThisFrame) return true;

        if (true == _gamepad.leftStickButton.wasPressedThisFrame) return true;
        if (true == _gamepad.rightStickButton.wasPressedThisFrame) return true;

        if (true == _gamepad.startButton.wasPressedThisFrame) return true;
        if (true == _gamepad.selectButton.wasPressedThisFrame) return true;

        if (true == _gamepad.dpad.up.wasPressedThisFrame) return true;
        if (true == _gamepad.dpad.down.wasPressedThisFrame) return true;
        if (true == _gamepad.dpad.left.wasPressedThisFrame) return true;
        if (true == _gamepad.dpad.right.wasPressedThisFrame) return true;

        return false;
    }

    private void OnDeviceChange(InputDevice _device, InputDeviceChange _change)
    {
        if (false == (_device is Gamepad)) return;

        bool _connected = Gamepad.all.Count > 0;

        if (bGamepadConnected != _connected)
        {
            bGamepadConnected = _connected;
            GamepadConnectionChangedEvent?.Invoke(bGamepadConnected);
        }

        RefreshIconSet();

        // 쓰던 패드가 빠졌는데 계속 패드 표기를 띄워두면, 유저는 키보드로 조작하는데 화면에는
        // 패드 버튼이 나오는 상태가 된다. 쿨다운을 무시하고 즉시 되돌린다.
        if (false == _connected && EInputDeviceType.Gamepad == currentDevice)
        {
            TrySwitchDevice(EInputDeviceType.KeyboardMouse, true);
        }
    }

    private void RefreshIconSet()
    {
        Gamepad _gamepad = Gamepad.current;
        classifiedGamepad = _gamepad;

        EGamepadIconSet _before = CurrentIconSet;

        // 패드가 빠져도 마지막으로 판별한 세트를 유지한다. 무선 패드가 절전으로 잠깐 끊길 때
        // 아이콘이 Generic으로 떨어졌다 돌아오는 깜빡임을 막기 위해서다.
        if (null != _gamepad)
        {
            detectedIconSet = ClassifyGamepad(_gamepad);
        }

        if (_before != CurrentIconSet)
        {
            IconSetChangedEvent?.Invoke(CurrentIconSet);
        }
    }

    /// <summary>
    /// 패드의 레이아웃(있으면)과 제품 문자열(폴백)로 벤더를 판별합니다.
    ///
    /// 구체 타입(XInputController, DualShockGamepad 등)을 직접 참조하지 않고 레이아웃 이름
    /// 문자열로 판별하는 이유: 그 타입들은 플랫폼에 따라 컴파일에서 빠질 수 있지만,
    /// 레이아웃 이름 조회는 어디서나 안전합니다.
    ///
    /// 알려진 한계: Steam Input이 켜져 있으면 DualSense도 XInput 가상 패드로 위장해서
    /// 들어오므로 Xbox로 판별됩니다. 이건 어떤 판별 로직으로도 못 뚫습니다.
    /// 그래서 SetIconSetOverride(수동 지정)가 반드시 함께 있어야 합니다.
    /// </summary>
    private static EGamepadIconSet ClassifyGamepad(Gamepad _gamepad)
    {
        if (null == _gamepad) return EGamepadIconSet.Generic;

        string _layout = _gamepad.layout;

        if (false == string.IsNullOrEmpty(_layout))
        {
            // XInput보다 먼저 검사한다. 벤더 전용 레이아웃이 더 구체적인 정보이기 때문이다.
            if (true == InputSystem.IsFirstLayoutBasedOnSecond(_layout, "DualShockGamepad")) return EGamepadIconSet.PlayStation;
            if (true == InputSystem.IsFirstLayoutBasedOnSecond(_layout, "SwitchProControllerHID")) return EGamepadIconSet.Nintendo;
            if (true == InputSystem.IsFirstLayoutBasedOnSecond(_layout, "XInputController")) return EGamepadIconSet.Xbox;
        }

        EGamepadIconSet _byProduct = ClassifyByDescription(_gamepad.description.product);
        if (EGamepadIconSet.Generic != _byProduct) return _byProduct;

        return ClassifyByDescription(_gamepad.description.manufacturer);
    }

    private static EGamepadIconSet ClassifyByDescription(string _text)
    {
        if (true == string.IsNullOrEmpty(_text)) return EGamepadIconSet.Generic;

        // PlayStation을 먼저 본다. DS4의 제품명이 하필 "Wireless Controller"라서,
        // 뒤로 밀면 다른 규칙에 먼저 걸릴 여지가 생긴다.
        if (true == ContainsIgnoreCase(_text, "dualsense")) return EGamepadIconSet.PlayStation;
        if (true == ContainsIgnoreCase(_text, "dualshock")) return EGamepadIconSet.PlayStation;
        if (true == ContainsIgnoreCase(_text, "playstation")) return EGamepadIconSet.PlayStation;
        if (true == ContainsIgnoreCase(_text, "sony")) return EGamepadIconSet.PlayStation;
        if (true == ContainsIgnoreCase(_text, "wireless controller")) return EGamepadIconSet.PlayStation;

        if (true == ContainsIgnoreCase(_text, "nintendo")) return EGamepadIconSet.Nintendo;
        if (true == ContainsIgnoreCase(_text, "switch")) return EGamepadIconSet.Nintendo;
        if (true == ContainsIgnoreCase(_text, "joy-con")) return EGamepadIconSet.Nintendo;

        if (true == ContainsIgnoreCase(_text, "xbox")) return EGamepadIconSet.Xbox;
        if (true == ContainsIgnoreCase(_text, "xinput")) return EGamepadIconSet.Xbox;
        if (true == ContainsIgnoreCase(_text, "microsoft")) return EGamepadIconSet.Xbox;

        return EGamepadIconSet.Generic;
    }

    // string.Contains(string, StringComparison) 오버로드는 이 런타임에서 쓸 수 없어 IndexOf로 대체한다.
    private static bool ContainsIgnoreCase(string _text, string _keyword)
    {
        return _text.IndexOf(_keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
