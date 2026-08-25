using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// InputDeviceTracker의 판정 로직 테스트입니다.
///
/// Unity의 InputTestFixture를 쓰지 않는 이유: 그 클래스는 Input System 패키지의 Tests~ 폴더에
/// 있어서 manifest.json에 testables를 추가해야 하는데, 그러면 패키지 자체의 테스트 수백 개가
/// 테스트 러너에 함께 딸려 들어옵니다. 여기서는 합성 디바이스를 직접 추가/제거하는 것으로 충분합니다.
///
/// 대신 전역 InputSystem 상태를 건드리므로, SetUp에서 추가한 장치를 TearDown에서 반드시 되돌립니다.
/// 키보드·마우스도 합성 장치로 덮어써서, 테스트 도중 실제 손 입력이 끼어들어 결과가 흔들리는 것을 막습니다.
///
/// **EditMode에서 검증할 수 없는 것**: 디지털 버튼의 "이번 프레임에 눌림"(wasPressedThisFrame)은
/// 에디터 업데이트에서 항상 false로 나옵니다. 눌림 엣지 집계가 런타임의 프레임 진행에 묶여 있기
/// 때문이며(isPressed는 정상 동작), 플레이 모드에서는 문제가 없습니다.
/// 그래서 여기서는 값 기반으로 읽을 수 있는 것(스틱·트리거·마우스 델타)만 다룹니다.
/// 트래커가 보는 패드 버튼 16개와 키보드 anyKey 경로는 이 테스트의 사각지대이므로,
/// 실제 패드로 한 번은 손으로 확인해야 합니다.
/// </summary>
public class InputDeviceTrackerTests
{
    private readonly List<InputDevice> addedDevices = new List<InputDevice>(8);

    private InputDeviceTracker tracker;
    private InputDeviceSettings settings;

    private Keyboard keyboard;
    private Mouse mouse;

    private const float Dt = 0.016f;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<InputDeviceSettings>();

        // 실제 장치가 current를 차지하지 않도록 합성 키보드/마우스를 만들어 현재 장치로 세운다.
        keyboard = AddDevice<Keyboard>();
        mouse = AddDevice<Mouse>();

        tracker = new InputDeviceTracker();
        tracker.Initialize(settings);
    }

    [TearDown]
    public void TearDown()
    {
        tracker.Release();
        tracker = null;

        for (int i = addedDevices.Count - 1; i >= 0; i--)
        {
            if (null != addedDevices[i] && true == addedDevices[i].added)
            {
                InputSystem.RemoveDevice(addedDevices[i]);
            }
        }

        addedDevices.Clear();

        Object.DestroyImmediate(settings);
        settings = null;
    }

    // 초기 상태

    [Test]
    public void Initialize_StartsOnKeyboardMouse()
    {
        Assert.AreEqual(EInputDeviceType.KeyboardMouse, tracker.CurrentDevice);
        Assert.IsFalse(tracker.IsGamepadMode);
        Assert.IsFalse(tracker.AnyInputThisFrame);
    }

    [Test]
    public void Initialize_WithNullSettings_FallsBackToDefaults()
    {
        InputDeviceTracker _bare = new InputDeviceTracker();
        _bare.Initialize(null);

        // 기본값으로 동작해야 하므로 Tick이 예외 없이 돌아야 한다.
        Assert.DoesNotThrow(() => _bare.Tick(Dt));
        Assert.AreEqual(EInputDeviceType.KeyboardMouse, _bare.CurrentDevice);

        _bare.Release();
    }

    // 벤더 판별
    //
    // 이 테스트가 가장 중요합니다. 판별이 레이아웃 상속에 기대고 있어서,
    // Input System 패키지가 업데이트되며 레이아웃 이름이나 상속 구조가 바뀌면 조용히 깨집니다.

    [TestCase("XInputControllerWindows", EGamepadIconSet.Xbox)]
    [TestCase("DualShock4GamepadHID", EGamepadIconSet.PlayStation)]
    [TestCase("DualSenseGamepadHID", EGamepadIconSet.PlayStation)]
    [TestCase("SwitchProControllerHID", EGamepadIconSet.Nintendo)]
    [TestCase("Gamepad", EGamepadIconSet.Generic)]
    public void DetectedIconSet_MatchesGamepadVendor(string _layout, EGamepadIconSet _expected)
    {
        AddGamepad(_layout);
        tracker.Tick(Dt);

        Assert.AreEqual(_expected, tracker.DetectedIconSet);
    }

    [Test]
    public void DetectedIconSet_SurvivesDisconnect()
    {
        Gamepad _pad = AddGamepad("XInputControllerWindows");
        tracker.Tick(Dt);
        Assert.AreEqual(EGamepadIconSet.Xbox, tracker.DetectedIconSet);

        InputSystem.RemoveDevice(_pad);

        // 무선 패드가 절전으로 잠깐 끊길 때 아이콘이 Generic으로 떨어졌다 돌아오는
        // 깜빡임을 막기 위해, 판별 결과는 연결이 끊겨도 유지되어야 한다.
        Assert.AreEqual(EGamepadIconSet.Xbox, tracker.DetectedIconSet);
    }

    // 아이콘 표기 수동 지정

    [Test]
    public void IconSetOverride_WinsOverDetection_AndAutoRestores()
    {
        AddGamepad("XInputControllerWindows");
        tracker.Tick(Dt);
        Assert.AreEqual(EGamepadIconSet.Xbox, tracker.CurrentIconSet);

        tracker.SetIconSetOverride(true, EGamepadIconSet.PlayStation);
        Assert.AreEqual(EGamepadIconSet.PlayStation, tracker.CurrentIconSet);

        // 수동 지정은 자동 판별 결과를 오염시키지 않는다.
        // (옵션에서 "자동 (Xbox)"처럼 보여주려면 이 값이 살아 있어야 한다)
        Assert.AreEqual(EGamepadIconSet.Xbox, tracker.DetectedIconSet);

        tracker.SetIconSetOverride(false, EGamepadIconSet.Generic);
        Assert.AreEqual(EGamepadIconSet.Xbox, tracker.CurrentIconSet);
    }

    [Test]
    public void IconSetChangedEvent_FiresOnlyWhenResultChanges()
    {
        int _count = 0;
        tracker.IconSetChangedEvent += _s => _count++;

        tracker.SetIconSetOverride(true, EGamepadIconSet.Generic);

        // 판별 결과도 Generic(패드 없음)이라 실제로 보이는 값이 안 바뀌었으므로 발행되지 않아야 한다.
        Assert.AreEqual(0, _count, "표시 결과가 그대로인데 이벤트가 발행되었습니다.");

        tracker.SetIconSetOverride(true, EGamepadIconSet.Nintendo);
        Assert.AreEqual(1, _count);
    }

    // 연결 / 해제

    [Test]
    public void GamepadDisconnect_WhileInGamepadMode_FallsBackImmediately()
    {
        Gamepad _pad = AddGamepad("XInputControllerWindows");
        tracker.Tick(Dt);

        tracker.ForceDevice(EInputDeviceType.Gamepad);
        Assert.IsTrue(tracker.IsGamepadMode);
        Assert.IsTrue(tracker.IsGamepadConnected);

        bool _lastConnected = true;
        tracker.GamepadConnectionChangedEvent += _c => _lastConnected = _c;

        InputSystem.RemoveDevice(_pad);

        // 쿨다운을 무시하고 즉시 되돌아가야 한다. 그러지 않으면 유저는 키보드를 쓰는데
        // 화면에는 패드 버튼이 계속 떠 있는 상태가 된다.
        Assert.AreEqual(EInputDeviceType.KeyboardMouse, tracker.CurrentDevice);
        Assert.IsFalse(tracker.IsGamepadConnected);
        Assert.IsFalse(_lastConnected);
    }

    // 노이즈 문턱값 — 이 시스템의 핵심

    [Test]
    public void Stick_BelowThreshold_DoesNotSwitchDevice()
    {
        Gamepad _pad = AddGamepad("Gamepad");

        SetLeftStick(_pad, new Vector2(0.2f, 0f));
        tracker.Tick(Dt);

        // 스틱 드리프트를 조작으로 오인하면 아이콘이 계속 깜빡인다.
        Assert.AreEqual(EInputDeviceType.KeyboardMouse, tracker.CurrentDevice);
        Assert.IsFalse(tracker.AnyInputThisFrame);
    }

    [Test]
    public void Stick_AboveThreshold_SwitchesToGamepad()
    {
        Gamepad _pad = AddGamepad("Gamepad");

        SetLeftStick(_pad, new Vector2(1f, 0f));
        tracker.Tick(Dt);

        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice);
        Assert.IsTrue(tracker.AnyInputThisFrame);
    }

    [Test]
    public void Trigger_AboveThreshold_SwitchesToGamepad()
    {
        Gamepad _pad = AddGamepad("Gamepad");

        InputSystem.QueueStateEvent(_pad, new GamepadState { rightTrigger = 1f });
        InputSystem.Update();

        tracker.Tick(Dt);

        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice);
    }

    [Test]
    public void SwitchCooldown_BlocksImmediateSwitchBack()
    {
        settings.switchCooldownSeconds = 1f;

        Gamepad _pad = AddGamepad("Gamepad");

        SetLeftStick(_pad, new Vector2(1f, 0f));
        tracker.Tick(Dt);
        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice);

        // 스틱을 놓고 곧바로 마우스를 크게 움직여도 쿨다운 동안에는 되돌아가지 않아야 한다.
        SetLeftStick(_pad, Vector2.zero);
        MoveMouse(new Vector2(40f, 0f));
        tracker.Tick(Dt);

        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice, "쿨다운 중인데 전환되었습니다.");

        // 쿨다운이 지나면 전환된다.
        tracker.Tick(1f);
        MoveMouse(new Vector2(40f, 0f));
        tracker.Tick(Dt);

        Assert.AreEqual(EInputDeviceType.KeyboardMouse, tracker.CurrentDevice);
    }

    [Test]
    public void MouseMicroJitter_DoesNotSwitchDevice()
    {
        settings.switchCooldownSeconds = 0f;
        settings.mouseTravelThresholdPixels = 12f;

        Gamepad _pad = AddGamepad("Gamepad");
        SetLeftStick(_pad, new Vector2(1f, 0f));
        tracker.Tick(Dt);
        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice);

        SetLeftStick(_pad, Vector2.zero);

        // 책상 진동 수준의 1px 흔들림은 누적되더라도 리셋 시간 안에 문턱을 못 넘겨야 한다.
        for (int i = 0; i < 5; i++)
        {
            MoveMouse(new Vector2(1f, 0f));
            tracker.Tick(Dt);
        }

        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice, "마우스 지터로 전환되었습니다.");
    }

    [Test]
    public void MouseDeliberateMove_SwitchesToKeyboardMouse()
    {
        settings.switchCooldownSeconds = 0f;

        Gamepad _pad = AddGamepad("Gamepad");
        SetLeftStick(_pad, new Vector2(1f, 0f));
        tracker.Tick(Dt);
        Assert.AreEqual(EInputDeviceType.Gamepad, tracker.CurrentDevice);

        SetLeftStick(_pad, Vector2.zero);

        MoveMouse(new Vector2(40f, 0f));
        tracker.Tick(Dt);

        Assert.AreEqual(EInputDeviceType.KeyboardMouse, tracker.CurrentDevice);
    }

    [Test]
    public void DeviceChangedEvent_FiresOncePerActualSwitch()
    {
        settings.switchCooldownSeconds = 0f;

        int _count = 0;
        tracker.DeviceChangedEvent += _d => _count++;

        Gamepad _pad = AddGamepad("Gamepad");

        // 스틱을 계속 기울인 채 여러 프레임이 지나도 전환은 한 번뿐이어야 한다.
        SetLeftStick(_pad, new Vector2(1f, 0f));

        for (int i = 0; i < 5; i++)
        {
            tracker.Tick(Dt);
        }

        Assert.AreEqual(1, _count);
    }

    // 헬퍼

    private T AddDevice<T>() where T : InputDevice
    {
        T _device = InputSystem.AddDevice<T>();
        addedDevices.Add(_device);
        _device.MakeCurrent();
        return _device;
    }

    private Gamepad AddGamepad(string _layout)
    {
        Gamepad _pad = (Gamepad)InputSystem.AddDevice(_layout);
        addedDevices.Add(_pad);
        _pad.MakeCurrent();
        return _pad;
    }

    private void SetLeftStick(Gamepad _pad, Vector2 _value)
    {
        InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = _value });
        InputSystem.Update();
    }

    private void MoveMouse(Vector2 _delta)
    {
        InputSystem.QueueDeltaStateEvent(mouse.delta, _delta);
        InputSystem.Update();
    }
}
