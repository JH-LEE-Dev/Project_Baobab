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

    // 알파벳 (A-Z)
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // 숫자 (0-9)
    Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,

    // 기능키 (F1-F12)
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // 직접 바인딩 경로 입력
    Custom,
}

/// <summary>
/// EKeyIconMode.Other 모드에서 패드 모드일 때 사용할 게임패드 버튼 식별자입니다.
/// </summary>
public enum EGamepadButton
{
    None = 0,

    // 페이스 버튼 (A/B/X/Y or Cross/Circle/Square/Triangle)
    ButtonSouth,      // A (Xbox) / Cross (PlayStation) / B (Nintendo)
    ButtonEast,       // B (Xbox) / Circle (PlayStation) / A (Nintendo)
    ButtonWest,       // X (Xbox) / Square (PlayStation) / Y (Nintendo)
    ButtonNorth,      // Y (Xbox) / Triangle (PlayStation) / X (Nintendo)

    // 숄더 및 트리거
    LeftShoulder,     // LB / L1
    RightShoulder,    // RB / R1
    LeftTrigger,      // LT / L2
    RightTrigger,     // RT / R2

    // 스틱 클릭
    LeftStick,        // L3 / LS Click
    RightStick,       // R3 / RS Click

    // D-Pad (방향키)
    DPadUp,           // D-Pad Up
    DPadDown,         // D-Pad Down
    DPadLeft,         // D-Pad Left
    DPadRight,        // D-Pad Right

    // 메뉴 / 시스템 버튼
    Start,            // Menu / Start / Options
    Select,           // View / Back / Share / Touchpad

    // 직접 바인딩 경로 입력
    Custom,
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

    [SerializeField, Tooltip("EOtherKey.Custom 선택 시 사용할 직접 바인딩 경로 (예: <Keyboard>/c)")]
    private string customKeyboardPath;

    [Header("Other Mode - Gamepad Settings")]
    [SerializeField, Tooltip("Other 모드(패드)일 때 표시할 게임패드 버튼 선택")]
    private EGamepadButton otherGamepadButton = EGamepadButton.None;

    [SerializeField, Tooltip("EGamepadButton.Custom 선택 시 사용할 직접 바인딩 경로 (예: <Gamepad>/rightTrigger)")]
    private string customGamepadPath;

    [Header("UI Component")]
    [SerializeField, Tooltip("스프라이트를 노출할 이미지 컴포넌트")] 
    private Image targetImage;

    private InputManager inputManager;
    private Action cachedRefreshIcon;
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
    }

    private void OnEnable()
    {
        // Other 모드이거나 이미 초기화된 경우 활성화 시 자동 갱신
        if (EKeyIconMode.Other == keyIconMode || null != inputManager)
        {
            RefreshIcon();
        }
    }

    /// <summary>
    /// 부모 UI에서 이 컴포넌트를 초기화할 때 호출합니다.
    /// InputManager 참조를 전달받아 키 설정 변경 이벤트를 구독합니다.
    /// </summary>
    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        
        if (null == cachedRefreshIcon)
        {
            cachedRefreshIcon = RefreshIcon;
        }
        
        // 키 바인딩이 변경될 때마다 자동 갱신되도록 이벤트 구독
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshIcon;
            inputManager.inputReader.KeyBindingsChangedEvent += cachedRefreshIcon;
        }

        RefreshIcon();
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
    /// Other 모드로 전환하고 커스텀 키보드 바인딩 경로를 지정합니다.
    /// </summary>
    public void SetCustomKeyboardPath(string _path)
    {
        keyIconMode = EKeyIconMode.Other;
        otherKey = EOtherKey.Custom;
        customKeyboardPath = _path;
        RefreshIcon();
    }

    /// <summary>
    /// Other 모드로 전환하고 커스텀 게임패드 바인딩 경로를 지정합니다.
    /// </summary>
    public void SetCustomGamepadPath(string _path)
    {
        otherGamepadButton = EGamepadButton.Custom;
        customGamepadPath = _path;
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
                    // 향후 InputManager에 패드 바인딩 조회가 추가되면 해당 경로 활용, 현재는 기본 바인딩 조회
                    _bindingPath = inputManager.GetBindingPath(boundAction);
                }
            }
            else if (EKeyIconMode.Other == keyIconMode)
            {
                if (EGamepadButton.Custom == otherGamepadButton)
                {
                    _bindingPath = customGamepadPath;
                }
                else
                {
                    _bindingPath = GetBindingPathForGamepadButton(otherGamepadButton);
                }
            }
        }
        // 2. 키보드/마우스 모드
        else
        {
            if (EKeyIconMode.RebindableAction == keyIconMode)
            {
                if (null != inputManager)
                {
                    _bindingPath = inputManager.GetBindingPath(boundAction);
                }
            }
            else if (EKeyIconMode.Other == keyIconMode)
            {
                if (EOtherKey.Custom == otherKey)
                {
                    _bindingPath = customKeyboardPath;
                }
                else
                {
                    _bindingPath = GetBindingPathForOtherKey(otherKey);
                }
            }
        }

        Sprite _icon = keyIconDatabase.GetIcon(_bindingPath);

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
            case EGamepadButton.ButtonSouth:    return "<Gamepad>/buttonSouth";
            case EGamepadButton.ButtonEast:     return "<Gamepad>/buttonEast";
            case EGamepadButton.ButtonWest:     return "<Gamepad>/buttonWest";
            case EGamepadButton.ButtonNorth:    return "<Gamepad>/buttonNorth";

            case EGamepadButton.LeftShoulder:   return "<Gamepad>/leftShoulder";
            case EGamepadButton.RightShoulder:  return "<Gamepad>/rightShoulder";
            case EGamepadButton.LeftTrigger:    return "<Gamepad>/leftTrigger";
            case EGamepadButton.RightTrigger:   return "<Gamepad>/rightTrigger";

            case EGamepadButton.LeftStick:      return "<Gamepad>/leftStickPress";
            case EGamepadButton.RightStick:     return "<Gamepad>/rightStickPress";

            case EGamepadButton.DPadUp:         return "<Gamepad>/dpad/up";
            case EGamepadButton.DPadDown:       return "<Gamepad>/dpad/down";
            case EGamepadButton.DPadLeft:       return "<Gamepad>/dpad/left";
            case EGamepadButton.DPadRight:      return "<Gamepad>/dpad/right";

            case EGamepadButton.Start:          return "<Gamepad>/start";
            case EGamepadButton.Select:         return "<Gamepad>/select";

            default:
                return null;
        }
    }

    private void OnDestroy()
    {
        if (null != inputManager && null != inputManager.inputReader && null != cachedRefreshIcon)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshIcon;
        }
    }
}
