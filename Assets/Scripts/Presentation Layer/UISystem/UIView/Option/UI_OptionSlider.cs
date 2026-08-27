using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 볼륨, 밝기 등 연속적인 범위의 값을 좌우 버튼 및 슬라이더 드래그로 조절하는 옵션 항목의 UI입니다.
/// 행(Row) 자체가 Selectable로 동작하여 게임패드 포커스 및 좌우 미세 조절을 처리합니다.
/// </summary>
public class UI_OptionSlider : Selectable, IMoveHandler
{
    // 외부 컴포넌트 참조
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Slider slider;
    [SerializeField] private UI_OptionButton leftArrowButton;
    [SerializeField] private UI_OptionButton rightArrowButton;

    public UI_OptionButton LeftArrowButton => leftArrowButton;
    public UI_OptionButton RightArrowButton => rightArrowButton;

    [Header("Settings")]
    [SerializeField] private float stepValue = 5f; // 좌우 버튼 클릭 시 변하는 양
    [SerializeField] private string valueFormat = "{0}%"; // 값 표기 형식

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

    // 내부 상태
    private Action<float> onValueChanged;
    private Action onLeftClicked;
    private Action onRightClicked;
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
            if (null == backgroundImage)
            {
                backgroundImage = GetComponentInChildren<Image>();
            }
        }
        if (null != backgroundImage && null == normalSprite)
        {
            normalSprite = backgroundImage.sprite;
        }

        if (null != slider)
        {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
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
    public void Initialize(string _title, float _initialValue, float _minValue, float _maxValue, Action<float> _onValueChanged)
    {
        onValueChanged = _onValueChanged;
        
        if (null == onLeftClicked) onLeftClicked = OnLeftButtonClicked;
        if (null == onRightClicked) onRightClicked = OnRightButtonClicked;

        if (null != leftArrowButton)
        {
            leftArrowButton.Initialize(onLeftClicked);
            Navigation _noneNav = new Navigation();
            _noneNav.mode = Navigation.Mode.None;
            leftArrowButton.navigation = _noneNav;
        }
        if (null != rightArrowButton)
        {
            rightArrowButton.Initialize(onRightClicked);
            Navigation _noneNav = new Navigation();
            _noneNav.mode = Navigation.Mode.None;
            rightArrowButton.navigation = _noneNav;
        }

        if (null != slider)
        {
            slider.minValue = _minValue;
            slider.maxValue = _maxValue;
            slider.value = _initialValue;
            Navigation _noneNav = new Navigation();
            _noneNav.mode = Navigation.Mode.None;
            slider.navigation = _noneNav;
        }

        if (null != titleText)
        {
            titleText.text = _title;
        }

        UpdateValueDisplay(_initialValue);
    }

    public void SetCustomScroll(UI_CustomScroll _scroll)
    {
        customScroll = _scroll;
    }

    public void UpdateValue(float _value)
    {
        if (null != slider)
        {
            slider.value = _value;
        }
        else
        {
            UpdateValueDisplay(_value);
        }
    }

    private void UpdateValueDisplay(float _value)
    {
        if (null != valueText)
        {
            valueText.SetText(valueFormat, Mathf.RoundToInt(_value));
        }
    }

    public new bool IsInteractable => interactable && ((null != leftArrowButton && true == leftArrowButton.IsInteractable) || (null != rightArrowButton && true == rightArrowButton.IsInteractable) || (null != slider && true == slider.interactable));

    public void SetInteractable(bool _isInteractable)
    {
        interactable = _isInteractable;

        if (null != slider) slider.interactable = _isInteractable;
        if (null != leftArrowButton) leftArrowButton.SetInteractable(_isInteractable);
        if (null != rightArrowButton) rightArrowButton.SetInteractable(_isInteractable);

        if (null != valueText)
        {
            Color _color = valueText.color;
            _color.a = true == _isInteractable ? 1f : 0.5f;
            valueText.color = _color;
        }
    }

    private void OnSliderValueChanged(float _newValue)
    {
        UpdateValueDisplay(_newValue);

        if (null != onValueChanged)
        {
            onValueChanged.Invoke(_newValue);
        }
    }

    private bool ChangeSliderValue(float _delta)
    {
        if (null == slider) return false;
        float _oldValue = slider.value;
        float _newValue = Mathf.Clamp(slider.value + _delta, slider.minValue, slider.maxValue);
        if (Mathf.Approximately(_oldValue, _newValue)) return false;
        slider.value = _newValue;
        return true;
    }

    private void OnLeftButtonClicked()
    {
        ChangeSliderValue(-stepValue);
    }

    private void OnRightButtonClicked()
    {
        ChangeSliderValue(stepValue);
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        cursorBoxUI = _cursorBoxUI;
        inputManager = _inputManager;
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
        ApplyFocusVisual(false);
        HideCursor();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
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
                if (true == ChangeSliderValue(-stepValue))
                {
                    Sound.PlayUI(SoundID.OptionClick);
                }
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
                if (true == ChangeSliderValue(stepValue))
                {
                    Sound.PlayUI(SoundID.OptionClick);
                }
            }
            eventData.Use();
            return;
        }

        base.OnMove(eventData);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (null != slider)
        {
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
        onValueChanged = null;
        onLeftClicked = null;
        onRightClicked = null;
        cursorBoxUI = null;
        inputManager = null;
        customScroll = null;
    }
}
