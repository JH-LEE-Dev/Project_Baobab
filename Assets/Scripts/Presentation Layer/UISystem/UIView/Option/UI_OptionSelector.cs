using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using DG.Tweening;

/// <summary>
/// 언어, 화면 모드 등 좌우 버튼으로 단순 선택지를 바꾸는 옵션 항목의 UI입니다.
/// 행(Row) 자체가 Selectable로 동작하여 게임패드 포커스 및 좌우 조작을 처리합니다.
/// </summary>
public class UI_OptionSelector : Selectable, IMoveHandler
{
    // 외부 컴포넌트 참조
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private UI_OptionButton leftArrowButton;
    [SerializeField] private UI_OptionButton rightArrowButton;

    public UI_OptionButton LeftArrowButton => leftArrowButton;
    public UI_OptionButton RightArrowButton => rightArrowButton;

    [Header("Focus Visual Settings")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.white;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.white;

    [Header("Cursor Settings")]
    [SerializeField] private Vector2 cursorPadding = new Vector2(10f, 6f);
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Header("Switch Feedback")]
    [SerializeField] private Color switchFlashColor = new Color(1.0f, 0.835f, 0.31f, 1.0f); // 값이 바뀔 때 번쩍이는 색 (#FFD54F)
    [SerializeField] private float switchSlideDistance = 12f;   // 값 텍스트가 들어오는 거리(px). 정수 스냅으로 움직여 픽셀이 뭉개지지 않는다
    [SerializeField] private float switchNudgeDistance = 3f;    // 누른 화살표가 바깥으로 밀리는 거리(px)

    // 내부 상태
    private Action onLeftClicked;
    private Action onRightClicked;
    private Action cachedHandleLeft;
    private Action cachedHandleRight;
    private int pendingDirection = 0;
    private Sequence switchFeedbackTween;
    private RectTransform valueRect;
    private RectTransform leftArrowRect;
    private RectTransform rightArrowRect;
    private Vector2 leftArrowBasePosition;
    private Vector2 rightArrowBasePosition;
    private Vector2 switchBasePosition;
    private Color switchBaseColor;
    private bool hasSwitchBase = false;
    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private UI_CustomScroll customScroll;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        if (null == backgroundImage)
        {
            backgroundImage = GetComponent<Image>();
        }
        if (null != backgroundImage && null == normalSprite)
        {
            normalSprite = backgroundImage.sprite;
        }
    }

    public void ApplyFocusVisual(bool _isFocused)
    {
        if (true == _isFocused)
        {
            if (null == inputManager || false == inputManager.IsGamepadMode) return;
        }

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
            backgroundImage.color = (true == _isFocused) ? hoverColor : normalColor;
        }

        if (null != titleText)
        {
            titleText.color = (true == _isFocused) ? hoverTextColor : normalTextColor;
        }
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(string _title, string _initialValue, Action _onLeft, Action _onRight)
    {
        onLeftClicked = _onLeft;
        onRightClicked = _onRight;

        // 화살표 콜백을 감싸 입력 방향을 기록한다. 콜백 안에서 UpdateValue가 불리면 그 방향으로 연출한다.
        if (null == cachedHandleLeft) cachedHandleLeft = HandleLeftClicked;
        if (null == cachedHandleRight) cachedHandleRight = HandleRightClicked;
        if (null != valueText) valueRect = valueText.rectTransform;
        if (null == leftArrowRect && null != leftArrowButton)
        {
            leftArrowRect = leftArrowButton.transform as RectTransform;
            leftArrowBasePosition = leftArrowRect.anchoredPosition;
        }
        if (null == rightArrowRect && null != rightArrowButton)
        {
            rightArrowRect = rightArrowButton.transform as RectTransform;
            rightArrowBasePosition = rightArrowRect.anchoredPosition;
        }

        if (null != leftArrowButton)
        {
            leftArrowButton.Initialize(cachedHandleLeft);
            Navigation _noneNav = new Navigation();
            _noneNav.mode = Navigation.Mode.None;
            leftArrowButton.navigation = _noneNav;
        }
        if (null != rightArrowButton)
        {
            rightArrowButton.Initialize(cachedHandleRight);
            Navigation _noneNav = new Navigation();
            _noneNav.mode = Navigation.Mode.None;
            rightArrowButton.navigation = _noneNav;
        }

        if (null != titleText)
        {
            titleText.text = _title;
        }

        UpdateValue(_initialValue);
    }

    public void SetCustomScroll(UI_CustomScroll _scroll)
    {
        customScroll = _scroll;
    }

    public void UpdateValue(string _value)
    {
        if (null != valueText)
        {
            bool _changed = false == string.Equals(valueText.text, _value);
            valueText.text = _value;

            if (0 != pendingDirection && true == _changed)
            {
                PlayValueFeedback(pendingDirection);
            }
        }
    }

    private void HandleLeftClicked()
    {
        InvokeWithDirection(onLeftClicked, -1);
    }

    private void HandleRightClicked()
    {
        InvokeWithDirection(onRightClicked, 1);
    }

    private void InvokeWithDirection(Action _callback, int _direction)
    {
        pendingDirection = _direction;
        PlayArrowNudge(_direction);

        _callback?.Invoke();

        pendingDirection = 0;
    }

    /// <summary>진행 중인 연출을 멈추고 값 텍스트와 화살표를 기준 위치로 되돌립니다. (연타해도 최종 모습이 항상 같도록)</summary>
    private void ResetSwitchFeedback()
    {
        if (null != switchFeedbackTween && true == switchFeedbackTween.IsActive())
        {
            switchFeedbackTween.Kill();
        }

        switchFeedbackTween = null;

        if (true == hasSwitchBase && null != valueText && null != valueRect)
        {
            valueRect.anchoredPosition = switchBasePosition;
            valueText.color = switchBaseColor;
        }

        hasSwitchBase = false;

        if (null != leftArrowRect)
        {
            leftArrowRect.DOKill();
            leftArrowRect.anchoredPosition = leftArrowBasePosition;
        }

        if (null != rightArrowRect)
        {
            rightArrowRect.DOKill();
            rightArrowRect.anchoredPosition = rightArrowBasePosition;
        }
    }

    /// <summary>
    /// 값 텍스트가 누른 방향에서 정수 픽셀로 미끄러져 들어오며 금색에서 원래 색으로 돌아옵니다.
    /// 텍스트에는 스케일과 알파 페이드를 쓰지 않습니다. (픽셀 폰트가 뭉개지고, 빠르게 넘길 때 화면이 깜빡임)
    /// </summary>
    private void PlayValueFeedback(int _direction)
    {
        if (null == valueRect || false == valueText.gameObject.activeInHierarchy) return;

        // 이전 연출이 도중에 끊기면 값이 어긋난 상태이므로, 끊기기 전의 기준값으로 먼저 되돌린 뒤 시작한다.
        if (null != switchFeedbackTween && true == switchFeedbackTween.IsActive())
        {
            ResetSwitchFeedback();
        }

        Color _baseColor = valueText.color;
        Vector2 _basePos = valueRect.anchoredPosition;
        switchBaseColor = _baseColor;
        switchBasePosition = _basePos;
        hasSwitchBase = true;

        Color _from = switchFlashColor;
        _from.a = _baseColor.a;

        valueRect.anchoredPosition = new Vector2(_basePos.x + _direction * Mathf.Round(switchSlideDistance), _basePos.y);
        valueText.color = _from;

        Sequence _seq = DOTween.Sequence().SetUpdate(true);
        _seq.Join(valueRect.DOAnchorPosX(_basePos.x, 0.14f, true).SetEase(Ease.OutBack));
        _seq.Join(valueText.DOColor(_baseColor, 0.22f).SetEase(Ease.OutQuad));
        switchFeedbackTween = _seq;
    }

    /// <summary>누른 쪽 화살표가 바깥으로 튕겼다 돌아옵니다.</summary>
    private void PlayArrowNudge(int _direction)
    {
        RectTransform _arrow = (0 < _direction) ? rightArrowRect : leftArrowRect;
        if (null == _arrow || false == _arrow.gameObject.activeInHierarchy) return;

        _arrow.DOKill();
        Vector2 _base = (0 < _direction) ? rightArrowBasePosition : leftArrowBasePosition;
        _arrow.anchoredPosition = new Vector2(_base.x + _direction * Mathf.Round(switchNudgeDistance), _base.y);
        _arrow.DOAnchorPosX(_base.x, 0.1f, true).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public new bool IsInteractable => interactable && ((null != leftArrowButton && true == leftArrowButton.IsInteractable) || (null != rightArrowButton && true == rightArrowButton.IsInteractable));

    public void SetInteractable(bool _isInteractable)
    {
        interactable = _isInteractable;

        if (null != leftArrowButton) leftArrowButton.SetInteractable(_isInteractable);
        if (null != rightArrowButton) rightArrowButton.SetInteractable(_isInteractable);
        
        // 시각적 피드백 처리 (알파값 조절 등)
        if (null != valueText)
        {
            Color _color = valueText.color;
            _color.a = true == _isInteractable ? 1f : 0.5f;
            valueText.color = _color;
        }
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        cursorBoxUI = _cursorBoxUI;
        inputManager = _inputManager;
    }

    public void ShowCursor()
    {
        if (null == cursorBoxUI) return;
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

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
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        ShowCursor();
        ApplyFocusVisual(true);
        Sound.PlayUI(SoundID.MainMenuDot01);

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
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        ApplyFocusVisual(false);
        HideCursor();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ResetSwitchFeedback();
        ApplyFocusVisual(false);
        HideCursor();
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (false == IsInteractable) return;

        if (MoveDirection.Left == eventData.moveDir)
        {
            if (null != leftArrowButton)
            {
                leftArrowButton.OnPointerClick(null);
            }
            else
            {
                onLeftClicked?.Invoke();
                Sound.PlayUI(SoundID.OptionClick);
            }
            eventData.Use();
            return;
        }
        else if (MoveDirection.Right == eventData.moveDir)
        {
            if (null != rightArrowButton)
            {
                rightArrowButton.OnPointerClick(null);
            }
            else
            {
                onRightClicked?.Invoke();
                Sound.PlayUI(SoundID.OptionClick);
            }
            eventData.Use();
            return;
        }

        base.OnMove(eventData);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ResetSwitchFeedback();
        onLeftClicked = null;
        onRightClicked = null;
        cachedHandleLeft = null;
        cachedHandleRight = null;
        cursorBoxUI = null;
        inputManager = null;
        customScroll = null;
    }
}
