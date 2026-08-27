using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 - 컨트롤(조작) 탭의 게임패드 전용 키 변경 행 컴포넌트입니다.
/// 행 자체가 단일 Selectable 버튼으로 작동하며, 패드로 선택 후 A 버튼(Submit) 클릭 시 리바인딩 프롬프트를 엽니다.
/// </summary>
public class UI_OptionGamepadKeyBindRow : Selectable,
    IMoveHandler,
    ISubmitHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI actionNameText;     // 액션 한글 이름
    [SerializeField] private Image keyIconImage;                  // 패드 버튼 아이콘 이미지
    [SerializeField] private TextMeshProUGUI keyFallbackText;    // 아이콘 없을 때 폴백 텍스트
    [SerializeField] private GameObject lockIndicator;           // 리바인드 불가(이동 등) 잠금 표시 UI

    [Header("Conflict Warning")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color conflictColor = Color.red;
    [SerializeField] private Color disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    [Header("Focus Visual Settings")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Color normalBgColor = Color.white;
    [SerializeField] private Color hoverBgColor = Color.white;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.white;

    [Header("Cursor Settings")]
    [SerializeField] private Vector2 cursorPadding = new Vector2(10f, 10f);
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Header("Sound Settings")]
    [SerializeField] private SoundID hoverSoundId = SoundID.MainMenuDot01;
    [SerializeField] private SoundID clickSoundId = SoundID.OptionClick;

    private ERebindableAction boundAction;
    private bool isRebindable = true;
    private KeyIconDatabase iconDatabase;
    private EGamepadIconSet iconSet = EGamepadIconSet.Xbox;
    private Action<ERebindableAction> onRebindRequested;
    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private UI_CustomScroll customScroll;
    private bool isInteractableState = true;

    public ERebindableAction BoundAction => boundAction;
    public bool IsRebindable => isRebindable;
    public new bool IsInteractable => isInteractableState && interactable;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        if (null == backgroundImage)
        {
            backgroundImage = GetComponent<Image>();
            if (null == backgroundImage)
            {
                backgroundImage = GetComponentInChildren<Image>();
            }
        }
        if (null != backgroundImage && null == normalSprite)
        {
            normalSprite = backgroundImage.sprite;
        }
    }

    public void Initialize(
        ERebindableAction _action,
        string _label,
        string _bindingPath,
        string _displayString,
        bool _isConflict,
        bool _isRebindable,
        KeyIconDatabase _iconDB,
        Action<ERebindableAction> _onRebind,
        EGamepadIconSet _iconSet = EGamepadIconSet.Xbox)
    {
        boundAction = _action;
        iconDatabase = _iconDB;
        onRebindRequested = _onRebind;
        iconSet = _iconSet;

        if (null != actionNameText)
        {
            actionNameText.text = _label;
        }

        Refresh(_bindingPath, _displayString, _isConflict, _isRebindable, _iconSet);
    }

    public void Refresh(string _bindingPath, string _displayString, bool _isConflict, bool _isRebindable = true, EGamepadIconSet? _iconSet = null)
    {
        if (null != _iconSet)
        {
            iconSet = _iconSet.Value;
        }
        isRebindable = _isRebindable;

        if (null != lockIndicator)
        {
            lockIndicator.SetActive(false == _isRebindable);
        }

        Sprite _icon = null;
        if (null != iconDatabase && false == string.IsNullOrEmpty(_bindingPath))
        {
            _icon = iconDatabase.GetIcon(_bindingPath, iconSet);
        }

        Color _targetColor = true == _isConflict ? conflictColor : (false == isRebindable ? disabledColor : normalColor);

        if (null != _icon)
        {
            if (null != keyIconImage)
            {
                keyIconImage.sprite = _icon;
                keyIconImage.enabled = true;
                keyIconImage.color = _targetColor;
            }
            if (null != keyFallbackText)
            {
                keyFallbackText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (null != keyIconImage)
            {
                keyIconImage.enabled = false;
            }
            if (null != keyFallbackText)
            {
                keyFallbackText.gameObject.SetActive(true);
                keyFallbackText.text = FormatGamepadDisplayString(_bindingPath, _displayString);
                keyFallbackText.color = _targetColor;
            }
        }
    }

    private string FormatGamepadDisplayString(string _bindingPath, string _rawDisplayString)
    {
        if (false == string.IsNullOrWhiteSpace(_rawDisplayString))
        {
            return _rawDisplayString;
        }

        if (true == string.IsNullOrEmpty(_bindingPath)) return string.Empty;

        if (true == _bindingPath.Contains("buttonSouth")) return "A";
        if (true == _bindingPath.Contains("buttonEast")) return "B";
        if (true == _bindingPath.Contains("buttonWest")) return "X";
        if (true == _bindingPath.Contains("buttonNorth")) return "Y";
        if (true == _bindingPath.Contains("leftTrigger")) return "LT";
        if (true == _bindingPath.Contains("rightTrigger")) return "RT";
        if (true == _bindingPath.Contains("leftShoulder")) return "LB";
        if (true == _bindingPath.Contains("rightShoulder")) return "RB";
        if (true == _bindingPath.Contains("leftStickPress")) return "LS";
        if (true == _bindingPath.Contains("rightStickPress")) return "RS";
        if (true == _bindingPath.Contains("leftStick")) return "Left Stick";
        if (true == _bindingPath.Contains("rightStick")) return "Right Stick";
        if (true == _bindingPath.Contains("dpad/up")) return "D-Up";
        if (true == _bindingPath.Contains("dpad/down")) return "D-Down";
        if (true == _bindingPath.Contains("dpad/left")) return "D-Left";
        if (true == _bindingPath.Contains("dpad/right")) return "D-Right";
        if (true == _bindingPath.Contains("start")) return "Start";
        if (true == _bindingPath.Contains("select")) return "Select";

        return _bindingPath.Replace("<Gamepad>/", "");
    }

    public void RefreshLabel(string _label)
    {
        if (null != actionNameText)
        {
            actionNameText.text = _label;
        }
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        cursorBoxUI = _cursorBoxUI;
        inputManager = _inputManager;
    }

    public void SetCustomScroll(UI_CustomScroll _scroll)
    {
        customScroll = _scroll;
    }

    public void ApplyFocusVisual(bool _isFocused)
    {
        if (null != backgroundImage)
        {
            if (true == _isFocused && null != hoverSprite)
            {
                backgroundImage.sprite = hoverSprite;
            }
            else if (null != normalSprite)
            {
                backgroundImage.sprite = normalSprite;
            }
            backgroundImage.color = (true == _isFocused) ? hoverBgColor : normalBgColor;
        }

        if (null != actionNameText)
        {
            actionNameText.color = (true == _isFocused) ? hoverTextColor : normalTextColor;
        }
    }

    public void ShowCursor()
    {
        if (null == cursorBoxUI) return;
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        RectTransform _targetRect = transform as RectTransform;
        if (null != _targetRect)
        {
            Vector2 _size = _targetRect.rect.size + cursorPadding;
            cursorBoxUI.Show(_targetRect, _size, cursorOffset, CursorMotionSettings.RowSubtle);
        }
    }

    public void HideCursor()
    {
        if (null == cursorBoxUI) return;
        RectTransform _targetRect = transform as RectTransform;
        if (null != _targetRect)
        {
            cursorBoxUI.Hide(_targetRect);
        }
        else
        {
            cursorBoxUI.Hide();
        }
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            ShowCursor();
            ApplyFocusVisual(true);
        }
        Sound.PlayUI(hoverSoundId);

        if (null == customScroll)
        {
            customScroll = GetComponentInParent<UI_CustomScroll>();
        }
        if (null != customScroll)
        {
            customScroll.EnsureVisible(transform as RectTransform);
        }
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        ApplyFocusVisual(false);
        HideCursor();
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        ApplyFocusVisual(true);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        ApplyFocusVisual(false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ExecuteRebindRequest();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ExecuteRebindRequest();
    }

    private void ExecuteRebindRequest()
    {
        if (false == IsInteractable) return;
        if (false == isRebindable) return;

        Sound.PlayUI(clickSoundId);

        if (null != onRebindRequested)
        {
            onRebindRequested.Invoke(boundAction);
        }
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (false == IsInteractable) return;

        if (MoveDirection.Left == eventData.moveDir || MoveDirection.Right == eventData.moveDir)
        {
            eventData.Use();
            return;
        }

        base.OnMove(eventData);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ApplyFocusVisual(false);
        HideCursor();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        onRebindRequested = null;
        cursorBoxUI = null;
        inputManager = null;
        customScroll = null;
    }
}
