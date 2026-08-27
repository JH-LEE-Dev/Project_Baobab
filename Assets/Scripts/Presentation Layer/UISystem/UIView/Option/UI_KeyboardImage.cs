using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 키 아이콘 표시 모드입니다.
/// </summary>
public enum EKeyIconMode
{
    /// <summary>InputManager와 연동하여 리바인딩 가능한 액션 키를 실시간으로 추적 및 표시합니다.</summary>
    RebindableAction = 0,

    /// <summary>리바인딩 목록에 포함되지 않는 고정/기타 키(Ctrl, Shift, Space 등)를 표시합니다.</summary>
    Other = 1,
}

/// <summary>
/// EKeyIconMode.Other 모드에서 사용할 키보드/마우스 키 식별자입니다.
/// </summary>
public enum EOtherKey
{
    None = 0,

    // 특수 및 제어 키
    Ctrl,
    LeftCtrl,
    RightCtrl,
    Shift,
    LeftShift,
    RightShift,
    Alt,
    LeftAlt,
    RightAlt,
    Space,
    Tab,
    Escape,
    Enter,
    NumpadEnter,

    // 방향키 및 네비게이션
    UpArrow,
    DownArrow,
    LeftArrow,
    RightArrow,
    PageUp,
    PageDown,
    Insert,
    End,
    Slash,

    // 마우스
    MouseLeft,
    MouseRight,
    MouseMiddle,
    MouseMove,

    // 알파벳 (A-Z)
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // 숫자 (0-9)
    Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,

    // 기능키 (F1-F12)
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}

/// <summary>
/// EKeyIconMode.Other 모드에서 패드 모드일 때 사용할 게임패드 버튼 식별자입니다.
/// </summary>
public enum EGamepadButton
{
    None = 0,

    // 페이스 버튼 (Xbox / PlayStation)
    Button_A_Cross = 1,       // A (Xbox) / ✕ (PlayStation)
    Button_B_Circle = 2,      // B (Xbox) / ○ (PlayStation)
    Button_X_Square = 3,      // X (Xbox) / □ (PlayStation)
    Button_Y_Triangle = 4,    // Y (Xbox) / △ (PlayStation)

    // 숄더 및 트리거
    LeftShoulder_LB_L1 = 5,   // LB (Xbox) / L1 (PlayStation)
    RightShoulder_RB_R1 = 6,  // RB (Xbox) / R1 (PlayStation)
    LeftTrigger_LT_L2 = 7,    // LT (Xbox) / L2 (PlayStation)
    RightTrigger_RT_R2 = 8,   // RT (Xbox) / R2 (PlayStation)

    // 스틱 전체 및 클릭
    LeftStick = 9,            // 왼쪽 스틱 전체 (Move / PadLStick_Full)
    LeftStick_Click_LS_L3 = 10, // LS / L3 클릭
    RightStick = 11,          // 오른쪽 스틱 전체 (Aim / PadRStick_Full)
    RightStick_Click_RS_R3 = 12, // RS / R3 클릭

    // 스틱 방향별
    LeftStick_Up = 13,
    LeftStick_Down = 14,
    LeftStick_Left = 15,
    LeftStick_Right = 16,
    RightStick_Up = 17,
    RightStick_Down = 18,
    RightStick_Left = 19,
    RightStick_Right = 20,

    // D-Pad (십자키)
    DPad_Full = 21,           // 십자키 전체 (DPad_Full)
    DPad_Up = 22,
    DPad_Down = 23,
    DPad_Left = 24,
    DPad_Right = 25,

    // 메뉴 / 시스템 버튼
    Start_Menu_Options = 26,  // Menu / Start / Options
    Select_View_Share = 27,   // View / Back / Share / Touchpad
}

/// <summary>
/// 특정 키 바인딩 속성(ERebindableAction) 또는 기타 키(EOtherKey/EGamepadButton)를 지정하면 
/// KeyIconDatabase와 연동하여 해당 키에 맞는 키보드/패드 아이콘을 띄워주는 재사용 가능한 UI 컴포넌트입니다.
/// </summary>
public class UI_KeyboardImage : MonoBehaviour
{
    [Header("Mode & Database")]
    [SerializeField, Tooltip("키/패드 아이콘 매핑 데이터베이스 (KeyIconDatabase)")] 
    private KeyIconDatabase keyIconDatabase;
    
    [SerializeField, Tooltip("키 아이콘 표시 모드 (RebindableAction / Other)")] 
    private EKeyIconMode keyIconMode = EKeyIconMode.RebindableAction;

    [Header("Rebindable Action Settings")]
    [SerializeField, Tooltip("RebindableAction 모드일 때 감지할 키 액션")] 
    private ERebindableAction boundAction;

    [Header("Other Mode - Keyboard Settings")]
    [SerializeField, Tooltip("Other 모드(키보드)일 때 표시할 키 선택")]
    private EOtherKey otherKey = EOtherKey.None;

    [Header("Other Mode - Gamepad Settings")]
    [SerializeField, Tooltip("Other 모드(패드)일 때 표시할 게임패드 버튼 선택")]
    private EGamepadButton otherGamepadButton = EGamepadButton.None;

    [Header("UI Component")]
    [SerializeField, Tooltip("스프라이트를 노출할 이미지 컴포넌트")] 
    private Image targetImage;

    private InputManager inputManager;
    private Action cachedRefreshIcon;
    private Action<EInputDeviceType> cachedOnDeviceChanged;
    private Action<EGamepadIconSet> cachedOnIconSetChanged;
    private bool isGamepadMode;

    public EKeyIconMode KeyIconMode => keyIconMode;
    public ERebindableAction BoundAction => boundAction;
    public EOtherKey OtherKey => otherKey;
    public EGamepadButton OtherGamepadButton => otherGamepadButton;
    public bool IsGamepadMode => isGamepadMode;

    private void Awake()
    {
        if (null == targetImage)
        {
            targetImage = GetComponent<Image>();
        }

        EnsureInputManager();
    }

    private void OnEnable()
    {
        EnsureInputManager();

        if (null != inputManager)
        {
            isGamepadMode = inputManager.IsGamepadMode;
        }

        // Other 모드이거나 이미 초기화된 경우 활성화 시 자동 갱신
        if (EKeyIconMode.Other == keyIconMode || null != inputManager)
        {
            RefreshIcon();
        }
    }

    private void EnsureInputManager()
    {
        if (null == inputManager)
        {
            InputManager _found = FindAnyObjectByType<InputManager>();
            if (null != _found)
            {
                Initialize(_found);
            }
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 부모 UI에서 이 컴포넌트를 초기화할 때 호출합니다.
    /// InputManager 참조를 전달받아 키 설정 변경 및 장치 전환 이벤트를 구독합니다.
    /// </summary>
    public void Initialize(InputManager _inputManager)
    {
        UnsubscribeEvents();

        inputManager = _inputManager;
        
        if (null == cachedRefreshIcon)
        {
            cachedRefreshIcon = RefreshIcon;
        }

        if (null == cachedOnDeviceChanged)
        {
            cachedOnDeviceChanged = OnInputDeviceChanged;
        }

        if (null == cachedOnIconSetChanged)
        {
            cachedOnIconSetChanged = OnGamepadIconSetChanged;
        }
        
        // 키 바인딩 및 장치 전환 시 자동 갱신되도록 이벤트 구독
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.KeyBindingsChangedEvent += cachedRefreshIcon;
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnDeviceChanged;
            inputManager.inputReader.GamepadIconSetChangedEvent += cachedOnIconSetChanged;
            isGamepadMode = inputManager.IsGamepadMode;
        }

        RefreshIcon();
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        SetGamepadMode(EInputDeviceType.Gamepad == _device);
    }

    private void OnGamepadIconSetChanged(EGamepadIconSet _iconSet)
    {
        RefreshIcon();
    }

    private void UnsubscribeEvents()
    {
        if (null != inputManager && null != inputManager.inputReader)
        {
            if (null != cachedRefreshIcon)
            {
                inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshIcon;
            }
            if (null != cachedOnDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
            }
            if (null != cachedOnIconSetChanged)
            {
                inputManager.inputReader.GamepadIconSetChangedEvent -= cachedOnIconSetChanged;
            }
        }
    }

    /// <summary>
    /// RebindableAction 모드로 전환하고 특정 액션을 지정합니다.
    /// </summary>
    public void SetAction(ERebindableAction _action)
    {
        keyIconMode = EKeyIconMode.RebindableAction;
        boundAction = _action;
        RefreshIcon();
    }

    /// <summary>
    /// Other 모드로 전환하고 특정 키보드 키를 지정합니다.
    /// </summary>
    public void SetOtherKey(EOtherKey _key)
    {
        keyIconMode = EKeyIconMode.Other;
        otherKey = _key;
        RefreshIcon();
    }

    /// <summary>
    /// Other 모드일 때 사용할 게임패드 버튼을 지정합니다.
    /// </summary>
    public void SetOtherGamepadButton(EGamepadButton _button)
    {
        otherGamepadButton = _button;
        if (true == isGamepadMode)
        {
            RefreshIcon();
        }
    }

    /// <summary>
    /// 패드 모드 여부를 설정합니다. KeyIconDatabase에서 패드/키보드 아이콘을 자동 전환합니다.
    /// </summary>
    public void SetGamepadMode(bool _isGamepad)
    {
        if (isGamepadMode == _isGamepad) return;

        isGamepadMode = _isGamepad;
        RefreshIcon();
    }

    /// <summary>
    /// 현재 모드와 디바이스 상태에 따라 KeyIconDatabase에서 아이콘을 조회하여 갱신합니다.
    /// </summary>
    public void RefreshIcon()
    {
        if (null == targetImage) return;

        if (null == keyIconDatabase)
        {
            targetImage.enabled = false;
            return;
        }

        string _bindingPath = null;

        // 1. 패드 모드
        if (true == isGamepadMode)
        {
            if (EKeyIconMode.RebindableAction == keyIconMode)
            {
                if (null != inputManager)
                {
                    _bindingPath = inputManager.GetBindingPath(boundAction, EInputDeviceType.Gamepad);
                }
            }
            else if (EKeyIconMode.Other == keyIconMode)
            {
                _bindingPath = GetBindingPathForGamepadButton(otherGamepadButton);
            }
        }
        // 2. 키보드/마우스 모드
        else
        {
            if (EKeyIconMode.RebindableAction == keyIconMode)
            {
                if (null != inputManager)
                {
                    _bindingPath = inputManager.GetBindingPath(boundAction, EInputDeviceType.KeyboardMouse);
                }
            }
            else if (EKeyIconMode.Other == keyIconMode)
            {
                _bindingPath = GetBindingPathForOtherKey(otherKey);
            }
        }

        EGamepadIconSet _iconSet = (null != inputManager) ? inputManager.CurrentGamepadIconSet : EGamepadIconSet.Xbox;
        Sprite _icon = keyIconDatabase.GetIcon(_bindingPath, _iconSet);

        if (null != _icon)
        {
            targetImage.sprite = _icon;
            targetImage.enabled = true;
        }
        else
        {
            targetImage.enabled = false;
        }
    }

    /// <summary>
    /// EOtherKey 열거형을 Input System 키보드/마우스 바인딩 경로 문자열로 변환합니다.
    /// </summary>
    public static string GetBindingPathForOtherKey(EOtherKey _key)
    {
        switch (_key)
        {
            case EOtherKey.Ctrl:        return "<Keyboard>/ctrl";
            case EOtherKey.LeftCtrl:    return "<Keyboard>/leftCtrl";
            case EOtherKey.RightCtrl:   return "<Keyboard>/rightCtrl";
            case EOtherKey.Shift:       return "<Keyboard>/shift";
            case EOtherKey.LeftShift:   return "<Keyboard>/leftShift";
            case EOtherKey.RightShift:  return "<Keyboard>/rightShift";
            case EOtherKey.Alt:         return "<Keyboard>/alt";
            case EOtherKey.LeftAlt:     return "<Keyboard>/leftAlt";
            case EOtherKey.RightAlt:    return "<Keyboard>/rightAlt";
            case EOtherKey.Space:       return "<Keyboard>/space";
            case EOtherKey.Tab:         return "<Keyboard>/tab";
            case EOtherKey.Escape:      return "<Keyboard>/escape";
            case EOtherKey.Enter:       return "<Keyboard>/enter";
            case EOtherKey.NumpadEnter: return "<Keyboard>/numpadEnter";

            case EOtherKey.UpArrow:     return "<Keyboard>/upArrow";
            case EOtherKey.DownArrow:   return "<Keyboard>/downArrow";
            case EOtherKey.LeftArrow:   return "<Keyboard>/leftArrow";
            case EOtherKey.RightArrow:  return "<Keyboard>/rightArrow";
            case EOtherKey.PageUp:      return "<Keyboard>/pageUp";
            case EOtherKey.PageDown:    return "<Keyboard>/pageDown";
            case EOtherKey.Insert:      return "<Keyboard>/insert";
            case EOtherKey.End:         return "<Keyboard>/end";
            case EOtherKey.Slash:       return "<Keyboard>/slash";

            case EOtherKey.MouseLeft:   return "<Mouse>/leftButton";
            case EOtherKey.MouseRight:  return "<Mouse>/rightButton";
            case EOtherKey.MouseMiddle: return "<Mouse>/middleButton";
            case EOtherKey.MouseMove:   return "<Mouse>/delta";

            case EOtherKey.A:           return "<Keyboard>/a";
            case EOtherKey.B:           return "<Keyboard>/b";
            case EOtherKey.C:           return "<Keyboard>/c";
            case EOtherKey.D:           return "<Keyboard>/d";
            case EOtherKey.E:           return "<Keyboard>/e";
            case EOtherKey.F:           return "<Keyboard>/f";
            case EOtherKey.G:           return "<Keyboard>/g";
            case EOtherKey.H:           return "<Keyboard>/h";
            case EOtherKey.I:           return "<Keyboard>/i";
            case EOtherKey.J:           return "<Keyboard>/j";
            case EOtherKey.K:           return "<Keyboard>/k";
            case EOtherKey.L:           return "<Keyboard>/l";
            case EOtherKey.M:           return "<Keyboard>/m";
            case EOtherKey.N:           return "<Keyboard>/n";
            case EOtherKey.O:           return "<Keyboard>/o";
            case EOtherKey.P:           return "<Keyboard>/p";
            case EOtherKey.Q:           return "<Keyboard>/q";
            case EOtherKey.R:           return "<Keyboard>/r";
            case EOtherKey.S:           return "<Keyboard>/s";
            case EOtherKey.T:           return "<Keyboard>/t";
            case EOtherKey.U:           return "<Keyboard>/u";
            case EOtherKey.V:           return "<Keyboard>/v";
            case EOtherKey.W:           return "<Keyboard>/w";
            case EOtherKey.X:           return "<Keyboard>/x";
            case EOtherKey.Y:           return "<Keyboard>/y";
            case EOtherKey.Z:           return "<Keyboard>/z";

            case EOtherKey.Num0:        return "<Keyboard>/0";
            case EOtherKey.Num1:        return "<Keyboard>/1";
            case EOtherKey.Num2:        return "<Keyboard>/2";
            case EOtherKey.Num3:        return "<Keyboard>/3";
            case EOtherKey.Num4:        return "<Keyboard>/4";
            case EOtherKey.Num5:        return "<Keyboard>/5";
            case EOtherKey.Num6:        return "<Keyboard>/6";
            case EOtherKey.Num7:        return "<Keyboard>/7";
            case EOtherKey.Num8:        return "<Keyboard>/8";
            case EOtherKey.Num9:        return "<Keyboard>/9";

            case EOtherKey.F1:          return "<Keyboard>/f1";
            case EOtherKey.F2:          return "<Keyboard>/f2";
            case EOtherKey.F3:          return "<Keyboard>/f3";
            case EOtherKey.F4:          return "<Keyboard>/f4";
            case EOtherKey.F5:          return "<Keyboard>/f5";
            case EOtherKey.F6:          return "<Keyboard>/f6";
            case EOtherKey.F7:          return "<Keyboard>/f7";
            case EOtherKey.F8:          return "<Keyboard>/f8";
            case EOtherKey.F9:          return "<Keyboard>/f9";
            case EOtherKey.F10:         return "<Keyboard>/f10";
            case EOtherKey.F11:         return "<Keyboard>/f11";
            case EOtherKey.F12:         return "<Keyboard>/f12";

            default:
                return null;
        }
    }

    /// <summary>
    /// EGamepadButton 열거형을 Input System 게임패드 바인딩 경로 문자열로 변환합니다.
    /// </summary>
    public static string GetBindingPathForGamepadButton(EGamepadButton _button)
    {
        switch (_button)
        {
            case EGamepadButton.Button_A_Cross:       return "<Gamepad>/buttonSouth";
            case EGamepadButton.Button_B_Circle:      return "<Gamepad>/buttonEast";
            case EGamepadButton.Button_X_Square:      return "<Gamepad>/buttonWest";
            case EGamepadButton.Button_Y_Triangle:    return "<Gamepad>/buttonNorth";

            case EGamepadButton.LeftShoulder_LB_L1:   return "<Gamepad>/leftShoulder";
            case EGamepadButton.RightShoulder_RB_R1:  return "<Gamepad>/rightShoulder";
            case EGamepadButton.LeftTrigger_LT_L2:    return "<Gamepad>/leftTrigger";
            case EGamepadButton.RightTrigger_RT_R2:   return "<Gamepad>/rightTrigger";

            case EGamepadButton.LeftStick:            return "<Gamepad>/leftStick";
            case EGamepadButton.LeftStick_Click_LS_L3: return "<Gamepad>/leftStickPress";
            case EGamepadButton.RightStick:           return "<Gamepad>/rightStick";
            case EGamepadButton.RightStick_Click_RS_R3: return "<Gamepad>/rightStickPress";

            case EGamepadButton.LeftStick_Up:         return "<Gamepad>/leftStick/up";
            case EGamepadButton.LeftStick_Down:       return "<Gamepad>/leftStick/down";
            case EGamepadButton.LeftStick_Left:       return "<Gamepad>/leftStick/left";
            case EGamepadButton.LeftStick_Right:      return "<Gamepad>/leftStick/right";

            case EGamepadButton.RightStick_Up:        return "<Gamepad>/rightStick/up";
            case EGamepadButton.RightStick_Down:      return "<Gamepad>/rightStick/down";
            case EGamepadButton.RightStick_Left:      return "<Gamepad>/rightStick/left";
            case EGamepadButton.RightStick_Right:     return "<Gamepad>/rightStick/right";

            case EGamepadButton.DPad_Full:            return "<Gamepad>/dpad";
            case EGamepadButton.DPad_Up:              return "<Gamepad>/dpad/up";
            case EGamepadButton.DPad_Down:            return "<Gamepad>/dpad/down";
            case EGamepadButton.DPad_Left:            return "<Gamepad>/dpad/left";
            case EGamepadButton.DPad_Right:           return "<Gamepad>/dpad/right";

            case EGamepadButton.Start_Menu_Options:   return "<Gamepad>/start";
            case EGamepadButton.Select_View_Share:    return "<Gamepad>/select";

            default:
                return null;
        }
    }
}
