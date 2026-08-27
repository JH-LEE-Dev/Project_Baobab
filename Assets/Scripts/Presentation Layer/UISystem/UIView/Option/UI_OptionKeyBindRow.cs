using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 옵션 패널 - 컨트롤(조작) 탭의 키보드/마우스 전용 키 변경 행 컴포넌트입니다.
/// 행 자체가 단일 Selectable 버튼으로 작동하며, 마우스 클릭 또는 패드/키보드 선택 후 Submit 시 리바인딩 프롬프트를 엽니다.
/// </summary>
public class UI_OptionKeyBindRow : Selectable,
    IMoveHandler,
    ISubmitHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI actionNameText;     // 액션 한글 이름
    [SerializeField] private Image keyIconImage;                  // 키 아이콘 이미지
    [SerializeField] private TextMeshProUGUI keyFallbackText;    // 아이콘 없을 때 폴백 텍스트

    [Header("Conflict Warning")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color conflictColor = Color.red;

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
    private KeyIconDatabase iconDatabase;
    private Action<ERebindableAction> onRebindRequested;
    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private UI_CustomScroll customScroll;
    private bool isInteractableState = true;

    public ERebindableAction BoundAction => boundAction;
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
        KeyIconDatabase _iconDB,
        Action<ERebindableAction> _onRebind)
    {
        boundAction = _action;
        iconDatabase = _iconDB;
        onRebindRequested = _onRebind;

        if (null != actionNameText)
        {
            actionNameText.text = _label;
        }

        Refresh(_bindingPath, _displayString, _isConflict);
    }

    public void SetInteractable(bool _interactable)
    {
        isInteractableState = _interactable;
        interactable = _interactable;
        if (false == _interactable)
        {
            ApplyFocusVisual(false);
            HideCursor();
        }
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        cursorBoxUI = _cursorBoxUI;
        inputManager = _inputManager;
    }

    public void SetCustomScroll(UI_CustomScroll _customScroll)
    {
        customScroll = _customScroll;
    }

    public void Refresh(string _bindingPath, string _displayString, bool _isConflict)
    {
        Sprite _icon = null;
        if (null != iconDatabase && false == string.IsNullOrEmpty(_bindingPath))
        {
            _icon = iconDatabase.GetIcon(_bindingPath);
        }

        Color _targetColor = true == _isConflict ? conflictColor : normalColor;

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
                keyFallbackText.text = _displayString;
                keyFallbackText.color = _targetColor;
            }
        }
    }

    public void RefreshLabel(string _label)
    {
        if (null != actionNameText)
        {
            actionNameText.text = _label;
        }
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

    public void SetRowFocus(bool _isFocused)
    {
        ApplyFocusVisual(_isFocused);
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

    public bool IsMouseOver()
    {
        if (false == gameObject.activeInHierarchy) return false;
        Vector2 _mousePos = Vector2.zero;
        if (null != Mouse.current)
        {
            _mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            return false;
        }

        Canvas _canvas = GetComponentInParent<Canvas>();
        Camera _cam = (null != _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
        RectTransform _rect = transform as RectTransform;
        if (null == _rect) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            ShowCursor();
            ApplyFocusVisual(true);
            Sound.PlayUI(hoverSoundId);
        }

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
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

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
