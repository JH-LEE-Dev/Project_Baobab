using System;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public InputReader inputReader { get; private set; }

    [Header("Input Device")]
    [SerializeField, Tooltip("키보드/마우스 ↔ 패드 자동 전환의 민감도 설정. 비워두면 기본값으로 동작한다.")]
    private InputDeviceSettings deviceSettings;

    // 진동은 InputReader(액션 해석)와 관심사가 달라 InputManager가 직접 소유한다.
    // 필드 초기화로 만들어 두는 이유: Initialize 이전에 ApplyInputSettings가 불려도 안전하도록.
    private readonly GamepadHaptics haptics = new GamepadHaptics();

    /// <summary>
    /// 패드 진동입니다. 무엇이 언제 진동할지는 호출부가 정합니다.
    /// 예: <c>inputManager.Haptics.Play(0.8f, 0.2f, 0.15f)</c>
    /// </summary>
    public GamepadHaptics Haptics => haptics;

    private bool bCursorHoveredOnUI = false;

    // OS 커서를 마지막으로 어떤 상태로 맞춰 놓았는지. Cursor.visible을 매 프레임 대입해도
    // 동작에는 문제가 없지만, 값이 바뀔 때만 건드리는 편이 디버깅할 때 훨씬 읽기 쉽다.
    private bool bCursorVisibleApplied = true;

    // 알트탭 등으로 게임이 포커스를 잃은 상태. 그때는 장치와 무관하게 커서를 돌려준다.
    private bool bApplicationFocused = true;

    public void Initialize()
    {
        inputReader = new InputReader();

        if (inputReader == null)
        {
            Debug.Log("inputReader is null -> InputManager::Initialize");
            return;
        }

        inputReader.Initialize(deviceSettings);

        BindSettings();
    }

    /// <summary>
    /// 저장된 패드 아이콘 표기 설정을 반영하고, 이후 변경도 따라가도록 구독합니다.
    ///
    /// 적용 이벤트는 옵션 창을 닫을 때(또는 실시간 미리보기에서)만 발행되므로, 여기서
    /// 현재 값을 한 번 직접 적용하지 않으면 게임을 껐다 켰을 때 저장된 표기가 무시됩니다.
    /// (AudioManager가 볼륨을 다루는 방식과 동일합니다)
    /// </summary>
    private void BindSettings()
    {
        SettingsManager _settings = SettingsManager.Instance;
        if (null == _settings) return;

        _settings.OnInputSettingsAppliedEvent -= ApplyInputSettings;
        _settings.OnInputSettingsAppliedEvent += ApplyInputSettings;

        ApplyInputSettings(_settings.Current);
    }

    public void ApplyInputSettings(SettingsData _data)
    {
        bool _bUseOverride = ToIconSetOverride(_data.gamepadIconPreference, out EGamepadIconSet _iconSet);
        SetGamepadIconSetOverride(_bUseOverride, _iconSet);

        // 슬라이더는 0~100, 진동 서비스는 0~1 배율을 쓴다.
        haptics.SetStrengthScale(_data.hapticStrength / SettingsData.SLIDER_MAX);
    }

    /// <summary>
    /// 유저 설정(EGamepadIconPreference)을 트래커가 쓰는 형태로 변환합니다.
    /// Auto는 "수동 지정 없음"이므로 false를 반환하며, 이때 _iconSet은 쓰이지 않습니다.
    /// </summary>
    private static bool ToIconSetOverride(EGamepadIconPreference _preference, out EGamepadIconSet _iconSet)
    {
        switch (_preference)
        {
            case EGamepadIconPreference.Xbox: _iconSet = EGamepadIconSet.Xbox; return true;
            case EGamepadIconPreference.PlayStation: _iconSet = EGamepadIconSet.PlayStation; return true;
            case EGamepadIconPreference.Nintendo: _iconSet = EGamepadIconSet.Nintendo; return true;
            case EGamepadIconPreference.Generic: _iconSet = EGamepadIconSet.Generic; return true;

            default:
                _iconSet = EGamepadIconSet.Generic;
                return false;
        }
    }

    public void Release()
    {
        haptics.Release();
        inputReader.Release();
    }

    public void OnDestroy()
    {
        // 종료 중에 Instance 게터를 쓰면 싱글턴이 되살아나므로 HasInstance로 확인한다.
        if (true == SettingsManager.HasInstance)
        {
            SettingsManager.Instance.OnInputSettingsAppliedEvent -= ApplyInputSettings;
        }

        haptics.Release();
        inputReader?.Release();
    }

    private void Update()
    {
        // 일시정지(timeScale = 0) 중에도 옵션 창에서 패드를 만지면 표기가 바뀌어야 하므로 unscaled를 쓴다.
        // 진동도 같은 이유로 unscaled여야 한다. (일시정지 중에 진동이 영원히 안 끝나면 안 된다)
        float _unscaledDeltaTime = Time.unscaledDeltaTime;

        // 커서가 움직이기 전에 이동 가능 영역부터 최신으로 맞춘다. 순서가 뒤바뀌면
        // 해상도가 바뀐 프레임에 커서가 한 번 옛 영역으로 잘린다.
        UpdateVirtualCursorBounds();

        inputReader?.Tick(_unscaledDeltaTime);
        haptics.Tick(_unscaledDeltaTime);

        // Tick 뒤에 부르는 이유: 이번 프레임의 장치 전환 결과를 바로 반영하기 위해서다.
        ApplyCursorVisibility();
    }

    private void OnApplicationFocus(bool _bFocused)
    {
        // 알트탭으로 게임을 벗어났는데 패드가 계속 울리는 것을 막는다.
        haptics.SetApplicationFocus(_bFocused);

        bApplicationFocused = _bFocused;
        ApplyCursorVisibility();
    }

    private void OnApplicationQuit()
    {
        // 진동은 장치 쪽에 남는 상태라 게임이 꺼져도 스스로 멎지 않는다. 반드시 명시적으로 끈다.
        haptics.Release();

        // 에디터에서는 플레이 모드를 나가도 Cursor.visible이 그대로 남아, 패드로 플레이하다
        // 멈추면 에디터 전체에서 커서가 사라진 것처럼 보인다. 빌드에서는 프로세스가 끝나며
        // OS가 알아서 되돌리므로 무해하지만, 개발 중에 겪는 혼란이 크다.
        SetCursorVisible(true);
    }

    // OS 커서

    /// <summary>
    /// 패드를 쓰는 동안 OS 커서를 감춥니다.
    ///
    /// 이게 없으면 패드로 조작하는 내내 화면 한가운데에 쓰지도 않는 커서가 남아 있습니다.
    /// 특히 특성 UI에서 가상 커서가 뜨면 커서가 둘 보이게 됩니다.
    ///
    /// 판단 기준이 IsGamepadConnected(연결 여부)가 아니라 IsGamepadMode(실제 사용 중)인 것이
    /// 중요합니다. 패드를 꽂아둔 채 키보드로 플레이하는 유저가 흔한데, 연결 여부로 판단하면
    /// 그 유저는 커서를 통째로 잃습니다.
    ///
    /// 되돌아오는 경로는 자동입니다. 마우스를 조금(기본 12px) 움직이면 장치가 키보드/마우스로
    /// 바뀌고 커서가 다시 나타납니다. 커서가 숨겨져 있어도 마우스 이동 자체는 그대로 읽히므로,
    /// "커서가 사라져서 되돌릴 방법이 없는" 상태에는 빠지지 않습니다.
    /// </summary>
    private void ApplyCursorVisibility()
    {
        // 포커스를 잃은 동안에는 무조건 돌려준다. 창 모드에서 게임 창 위에 커서가 없으면
        // 유저는 다른 창을 클릭할 수도, 창을 닫을 수도 없다고 느낀다.
        bool _bVisible = (false == bApplicationFocused) || (false == IsGamepadMode);

        SetCursorVisible(_bVisible);
    }

    private void SetCursorVisible(bool _bVisible)
    {
        if (bCursorVisibleApplied == _bVisible) return;

        bCursorVisibleApplied = _bVisible;
        Cursor.visible = _bVisible;
    }

    // 입력 장치 (실제 처리는 inputReader 위임. 변경 알림 이벤트는 다른 입력 이벤트들과 동일하게
    // inputManager.inputReader에서 직접 구독한다)

    /// <summary>지금 유저가 조작에 쓰고 있는 장치입니다. 물리적 연결 여부가 아니라 "마지막으로 실제 입력이 들어온 쪽"입니다.</summary>
    public EInputDeviceType CurrentDevice => null != inputReader ? inputReader.CurrentDevice : EInputDeviceType.KeyboardMouse;

    /// <summary>CurrentDevice == Gamepad 의 편의 표현입니다.</summary>
    public bool IsGamepadMode => null != inputReader && inputReader.IsGamepadMode;

    /// <summary>패드가 물리적으로 하나 이상 연결되어 있는지입니다. 실제 사용 여부와는 별개입니다.</summary>
    public bool IsGamepadConnected => null != inputReader && inputReader.IsGamepadConnected;

    /// <summary>화면에 그릴 패드 아이콘 세트입니다. 수동 지정이 켜져 있으면 그 값, 아니면 자동 판별 결과입니다.</summary>
    public EGamepadIconSet CurrentGamepadIconSet => null != inputReader ? inputReader.CurrentGamepadIconSet : EGamepadIconSet.Generic;

    /// <summary>수동 지정과 무관한 자동 판별 결과입니다. 옵션에서 "자동 (Xbox)"처럼 보여줄 때 씁니다.</summary>
    public EGamepadIconSet DetectedGamepadIconSet => null != inputReader ? inputReader.DetectedGamepadIconSet : EGamepadIconSet.Generic;

    /// <summary>이번 프레임에 어느 장치에서든 조작이 들어왔는지입니다. ("아무 키나 누르세요" 화면 등)</summary>
    public bool AnyInputThisFrame => null != inputReader && inputReader.AnyInputThisFrame;

    public void SetGamepadIconSetOverride(bool _bUseOverride, EGamepadIconSet _iconSet)
    {
        inputReader?.SetGamepadIconSetOverride(_bUseOverride, _iconSet);
    }

    public void ForceInputDevice(EInputDeviceType _device)
    {
        inputReader?.ForceInputDevice(_device);
    }

    public string GetBindingPath(ERebindableAction _action, EInputDeviceType _device)
    {
        return inputReader.GetBindingPath(_action, _device);
    }

    public string GetBindingDisplayString(ERebindableAction _action, EInputDeviceType _device)
    {
        return inputReader.GetBindingDisplayString(_action, _device);
    }

    public string GetBindingPathForCurrentDevice(ERebindableAction _action)
    {
        return inputReader.GetBindingPathForCurrentDevice(_action);
    }

    public string GetBindingDisplayStringForCurrentDevice(ERebindableAction _action)
    {
        return inputReader.GetBindingDisplayStringForCurrentDevice(_action);
    }

    public bool HasBindingFor(ERebindableAction _action, EInputDeviceType _device)
    {
        return inputReader.HasBindingFor(_action, _device);
    }

    public void PauseMove(bool _bPause)
    {
        inputReader.PauseMove(_bPause);
    }

    public void ClearPrevKeyboardInput()
    {
        inputReader.ClearPrevKeyboardInput();
    }

    public void SetCursorHoveredOnUI(bool _bCursorHoveredOnUI)
    {
        bCursorHoveredOnUI = _bCursorHoveredOnUI;
    }

    /// <summary>
    /// 게임플레이 입력을 UI가 가로채고 있는지입니다.
    ///
    /// 이름은 마우스 시절 그대로지만, 이제 "커서가 UI 위에 있다"뿐 아니라 "입력 모드가 UI다"도
    /// 참으로 봅니다. 패드에는 커서가 없어서 앞의 조건만으로는 판단이 안 되기 때문입니다.
    /// 덕분에 호출부(AxeComponent 등)를 고치지 않아도 패드에서 같은 보호가 걸립니다.
    /// </summary>
    public bool IsCursorHoveredOnUI()
    {
        return bCursorHoveredOnUI || EInputMode.UI == CurrentInputMode;
    }

    // 패드 가상 커서

    /// <summary>
    /// 패드용 가상 커서입니다. 마우스가 없는 상태에서 화면의 임의 지점을 가리켜야 할 때 씁니다.
    ///
    /// UI는 여기의 ActiveChangedEvent / MovedEvent를 구독해 커서 이미지를 보이고 옮기면 됩니다.
    /// 그 좌표로 무엇을 집을지(월드 오브젝트 레이캐스트, UI 히트 테스트)는 소비하는 쪽이 정합니다.
    /// 예: <c>inputManager.VirtualCursor.MovedEvent += OnCursorMoved;</c>
    /// </summary>
    public GamepadVirtualCursor VirtualCursor => null != inputReader ? inputReader.VirtualCursor : null;

    /// <summary>
    /// 가상 커서가 필요한 화면이 열렸는지 알려 줍니다. (특성 UI를 열 때 true, 닫을 때 false)
    ///
    /// 요청했다고 반드시 나오는 것은 아닙니다. 마우스를 쓰는 유저에게는 진짜 커서가 이미 있어
    /// 켜지지 않습니다. 창을 열어 둔 채 패드를 잡으면 그때 화면 중앙에 나타납니다.
    /// </summary>
    public void SetVirtualCursorRequested(bool _bRequested)
    {
        inputReader?.SetVirtualCursorRequested(_bRequested);
    }

    /// <summary>커서가 지금 화면에 떠 있는지입니다.</summary>
    public bool IsVirtualCursorActive => null != VirtualCursor && VirtualCursor.IsActive;

    /// <summary>
    /// 커서가 나갈 수 있는 영역을 카메라가 실제로 그리는 사각형으로 맞춥니다.
    ///
    /// 울트라와이드에서는 PixelPerfectCamera의 Pillarbox 때문에 화면 좌우에 검은 띠가 생기고,
    /// 그 띠는 카메라가 그리지 않는 영역이라 커서가 거기까지 나가면 가리킬 대상이 없습니다.
    /// 창 크기나 크롭 설정이 언제든 바뀔 수 있으므로 매 프레임 다시 맞춥니다.
    /// 커서가 꺼져 있을 때도 갱신하는 이유: 켜지는 순간의 "화면 중앙"이 옛 영역 기준으로
    /// 계산되면 커서가 엉뚱한 자리에서 시작합니다. 비용은 Rect 비교 한 번이라 무시할 수준입니다.
    /// </summary>
    private void UpdateVirtualCursorBounds()
    {
        GamepadVirtualCursor _cursor = VirtualCursor;

        if (null == _cursor) return;

        Camera _camera = null != CameraFinder.Instance ? CameraFinder.Instance.PPMainCamera : null;

        if (null == _camera)
        {
            _cursor.ClearBounds();
            return;
        }

        _cursor.SetBounds(_camera.pixelRect);
    }

    // 입력 모드 (게임플레이 ↔ UI)

    /// <summary>지금 입력이 게임플레이로 가는지 UI로 가는지입니다.</summary>
    public EInputMode CurrentInputMode => null != inputReader ? inputReader.CurrentInputMode : EInputMode.Gameplay;

    /// <summary>팝업·메뉴를 열 때 UI로, 닫을 때 Gameplay로 되돌리세요.</summary>
    public void SetInputMode(EInputMode _mode)
    {
        inputReader?.SetInputMode(_mode);
    }

    public void PauseInteractKey(bool _boolean)
    {
        inputReader.PauseInteractKey(_boolean);
    }

    public void PauseESCKey(bool _boolean)
    {
        inputReader.PauseESCKey(_boolean);
    }

    public void PauseInventoryKey(bool _boolean)
    {
        inputReader.PauseInventoryKey(_boolean);
    }

    // 키 리바인딩 (실제 처리는 inputReader 위임, KeyBindingsChangedEvent는 inputManager.inputReader에서 직접 구독)
    public bool IsRebinding => inputReader.IsRebinding;

    public IReadOnlyList<ERebindableAction> GetRebindableActions()
    {
        return inputReader.GetRebindableActions();
    }

    public string GetBindingDisplayString(ERebindableAction _action)
    {
        return inputReader.GetBindingDisplayString(_action);
    }

    public string GetBindingPath(ERebindableAction _action)
    {
        return inputReader.GetBindingPath(_action);
    }

    public bool IsConflicting(ERebindableAction _action)
    {
        return inputReader.IsConflicting(_action);
    }

    public bool IsConflicting(ERebindableAction _action, EInputDeviceType _device)
    {
        return inputReader.IsConflicting(_action, _device);
    }

    /// <summary>중복이 하나라도 있는지입니다. (모든 장치를 통틀어 — 저장 차단 판단용)</summary>
    public bool HasAnyConflict()
    {
        return inputReader.HasAnyConflict();
    }

    /// <summary>지정한 장치 안에 중복이 있는지입니다. (탭별 표시용)</summary>
    public bool HasAnyConflict(EInputDeviceType _device)
    {
        return inputReader.HasAnyConflict(_device);
    }

    /// <summary>그 장치에서 유저가 바꿀 수 있는 항목인지입니다. false면 "변경" 버튼을 비활성화하세요.</summary>
    public bool IsRebindable(ERebindableAction _action, EInputDeviceType _device)
    {
        return inputReader.IsRebindable(_action, _device);
    }

    public void BeginEditSession()
    {
        inputReader.BeginEditSession();
    }

    public void DiscardEditSession()
    {
        inputReader.DiscardEditSession();
    }

    public bool CommitEditSession()
    {
        return inputReader.CommitEditSession();
    }

    public void StartRebind(ERebindableAction _action, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        inputReader.StartRebind(_action, _onFinished);
    }

    public void StartRebind(ERebindableAction _action, EInputDeviceType _device, Action<ERebindResult, ERebindableAction?> _onFinished)
    {
        inputReader.StartRebind(_action, _device, _onFinished);
    }

    public void CancelRebind()
    {
        inputReader.CancelRebind();
    }

    public void ResetBinding(ERebindableAction _action)
    {
        inputReader.ResetBinding(_action);
    }

    public void ResetBinding(ERebindableAction _action, EInputDeviceType _device)
    {
        inputReader.ResetBinding(_action, _device);
    }

    public void ResetAllBindings()
    {
        inputReader.ResetAllBindings();
    }
}
