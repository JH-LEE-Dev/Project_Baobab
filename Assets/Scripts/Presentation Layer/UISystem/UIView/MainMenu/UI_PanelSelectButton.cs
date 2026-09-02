using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;

/// <summary>
/// 패널/모달 박스 내부에서 여러 개의 선택지를 나열할 때 사용하는 공용 사각 프레임 선택 버튼 컴포넌트입니다.
/// 언어 선택, 모드 선택 등에서 사용되며, 키보드/마우스 및 게임패드 CursorBox를 완벽하게 지원합니다.
/// </summary>
public class UI_PanelSelectButton : Selectable,
    IPointerClickHandler,
    ISubmitHandler
{
    public event Action<UI_PanelSelectButton> OnClickedEvent;

    // 외부 컴포넌트
    [Header("UI Components")]
    [SerializeField] private Graphic targetGraphicOverride;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Image selectIndicator;

    [Header("Visual Colors")]
    [SerializeField] private Color normalBgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color hoverBgColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color selectedBgColor = new Color(0.2f, 0.5f, 0.8f, 1f);
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.yellow;
    [SerializeField] private Color selectedTextColor = Color.white;

    [Header("Motion Settings")]
    [SerializeField] private float colorTweenDuration = 0.15f;
    [SerializeField] private float scalePunchStrength = 0.08f;
    [SerializeField] private float scalePunchDuration = 0.2f;

    [Header("Cursor Settings")]
    [SerializeField] private RectTransform cursorTargetTransform;
    [SerializeField] private Vector2 cursorPadding = new Vector2(8f, 6f);
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    // 내부 상태
    private bool isHovered = false;
    private bool isPointerHovered = false;
    private bool isSelectedState = false;
    private Tween colorTween;
    private Tween punchTween;
    private RectTransform cachedRectTransform;
    private Canvas cachedCanvas;

    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private Action onClickCallback;
    private EOptionLanguage boundLanguage = EOptionLanguage.Korean;

    public RectTransform CachedRectTransform
    {
        get
        {
            if (null == cachedRectTransform) cachedRectTransform = GetComponent<RectTransform>();
            return cachedRectTransform;
        }
    }

    public EOptionLanguage BoundLanguage => boundLanguage;
    public bool IsSelectedState => isSelectedState;
    public bool IsHovered => isHovered;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;
        if (null == targetGraphicOverride)
        {
            targetGraphicOverride = targetGraphic;
        }
    }

    public void Initialize(InputManager _inputManager, ICursorBoxUI _cursorBoxUI, Action _onClickCallback = null)
    {
        inputManager = _inputManager;
        cursorBoxUI = _cursorBoxUI;
        onClickCallback = _onClickCallback;

        ApplyVisualState(false);
    }

    public void SetBoundLanguage(EOptionLanguage _lang, string _displayText)
    {
        boundLanguage = _lang;
        if (null != buttonText)
        {
            buttonText.text = _displayText;
        }
    }

    public void SetText(string _text)
    {
        if (null != buttonText)
        {
            buttonText.text = _text;
        }
    }

    public void SetSelected(bool _isSelected)
    {
        isSelectedState = _isSelected;
        if (null != selectIndicator)
        {
            selectIndicator.gameObject.SetActive(_isSelected);
        }
        ApplyVisualState(false);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == IsInteractable() || false == gameObject.activeInHierarchy) return;

        ExecuteClick();
        if (null != EventSystem.current && null != inputManager && false == inputManager.IsGamepadMode)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        if (false == IsInteractable() || false == gameObject.activeInHierarchy) return;

        ExecuteClick();
    }

    public void ExecuteClick()
    {
        Sound.PlayUI(SoundID.OptionClick);
        PlayClickMotion();

        if (null != onClickCallback)
        {
            onClickCallback.Invoke();
        }

        OnClickedEvent?.Invoke(this);
    }

    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = true;
        isPointerHovered = true;
        Sound.PlayUI(SoundID.MainMenuDot01);
        ApplyVisualState(true);
        ShowCursor();
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = false;
        isPointerHovered = false;
        ApplyVisualState(true);
        HideCursor();
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);

        isHovered = true;
        Sound.PlayUI(SoundID.MainMenuDot01);
        ApplyVisualState(true);
        ShowCursor();
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);

        isHovered = false;
        ApplyVisualState(true);
        HideCursor();
    }
    public void ForceHover(bool _playAudio = false)
    {
        isHovered = true;
        if (true == _playAudio)
        {
            Sound.PlayUI(SoundID.MainMenuDot01);
        }
        ApplyVisualState(false);
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            ShowCursor();
        }
    }

    public void ForceUnhover()
    {
        isHovered = false;
        isPointerHovered = false;
        ApplyVisualState(false);
        HideCursor();
    }

    public bool IsMouseOver()
    {
        if (false == IsInteractable() || false == gameObject.activeInHierarchy) return false;
        if (true == isPointerHovered) return true;

        RectTransform _rect = CachedRectTransform;
        if (null == _rect) return false;

        Vector2 _mousePos = Vector2.zero;
        if (null != Mouse.current)
        {
            _mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            _mousePos = Input.mousePosition;
        }

        if (null == cachedCanvas)
        {
            cachedCanvas = GetComponentInParent<Canvas>();
        }

        Camera _cam = (null != cachedCanvas && RenderMode.ScreenSpaceOverlay != cachedCanvas.renderMode)
            ? cachedCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    private void ApplyVisualState(bool _animated)
    {
        Color _targetBg = true == isSelectedState ? selectedBgColor : (true == isHovered ? hoverBgColor : normalBgColor);
        Color _targetText = true == isSelectedState ? selectedTextColor : (true == isHovered ? hoverTextColor : normalTextColor);

        Graphic _bg = (null != targetGraphicOverride) ? targetGraphicOverride : targetGraphic;

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
            colorTween = null;
        }

        if (true == _animated)
        {
            Sequence _seq = DOTween.Sequence();
            if (null != _bg)
            {
                _seq.Join(_bg.DOColor(_targetBg, colorTweenDuration).SetEase(Ease.OutQuad).SetTarget(_bg));
            }
            if (null != buttonText)
            {
                _seq.Join(buttonText.DOColor(_targetText, colorTweenDuration).SetEase(Ease.OutQuad).SetTarget(buttonText));
            }
            _seq.SetTarget(this);
            colorTween = _seq;
        }
        else
        {
            if (null != _bg) _bg.color = _targetBg;
            if (null != buttonText) buttonText.color = _targetText;
        }
    }

    private void PlayClickMotion()
    {
        if (null != punchTween && true == punchTween.IsActive())
        {
            punchTween.Kill();
            punchTween = null;
        }

        transform.localScale = Vector3.one;
        punchTween = transform.DOPunchScale(new Vector3(scalePunchStrength, scalePunchStrength, 0f), scalePunchDuration, 10, 1f)
            .SetEase(Ease.OutQuad)
            .SetTarget(transform);
    }

    private void ShowCursor()
    {
        if (null == cursorBoxUI) return;

        RectTransform _target = (null != cursorTargetTransform) ? cursorTargetTransform : CachedRectTransform;
        if (null == _target) return;

        Vector2 _size = _target.rect.size + cursorPadding;
        cursorBoxUI.Show(_target, _size, cursorOffset, CursorMotionSettings.RowSubtle);
    }

    private void HideCursor()
    {
        if (null == cursorBoxUI) return;

        RectTransform _target = (null != cursorTargetTransform) ? cursorTargetTransform : CachedRectTransform;
        if (null != _target)
        {
            cursorBoxUI.Hide(_target);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        isPointerHovered = false;
        if (null != colorTween && true == colorTween.IsActive()) colorTween.Kill();
        if (null != punchTween && true == punchTween.IsActive()) punchTween.Kill();
        HideCursor();
    }
}
