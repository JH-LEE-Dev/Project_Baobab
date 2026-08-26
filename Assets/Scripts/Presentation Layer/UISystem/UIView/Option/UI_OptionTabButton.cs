using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 옵션 창의 개별 탭 버튼을 담당합니다. 클로저 할당 방지를 위해 IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler를 직접 구현합니다.
/// </summary>
public class UI_OptionTabButton : Selectable, IPointerClickHandler, ISubmitHandler
{
    // 외부 컴포넌트 참조
    [Header("Visual Settings")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoveredSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Text Settings")]
    [SerializeField] private TextMeshProUGUI tabText;
    [SerializeField] private Color normalTextColor = Color.gray;
    [SerializeField] private Color hoveredTextColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.white;

    [Header("Shadow Settings")]
    [SerializeField] private Coffee.UIEffects.UIEffect uiEffect;
    [SerializeField, ColorUsage(true, true)] private Color normalOutlineColor = Color.black;
    [SerializeField, ColorUsage(true, true)] private Color hoveredOutlineColor = Color.black;
    [SerializeField, ColorUsage(true, true)] private Color selectedOutlineColor = Color.white;

    // 내부 상태
    private UI_OptionTabGroup parentGroup;
    private int tabIndex;
    private bool isSelected;
    private bool isHovered;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UI_OptionTabGroup _parent, int _index)
    {
        parentGroup = _parent;
        tabIndex = _index;
    }

    public void SetSelected(bool _isSelected)
    {
        isSelected = _isSelected;
        UpdateVisualState();
    }

    public void SetText(string _text)
    {
        if (null != tabText)
        {
            tabText.text = _text;
        }
    }

    private void UpdateVisualState()
    {
        if (true == isSelected)
        {
            if (null != targetImage && null != selectedSprite)
            {
                targetImage.sprite = selectedSprite;
            }

            if (null != tabText)
            {
                tabText.color = selectedTextColor;
            }

            if (null != uiEffect)
            {
                uiEffect.shadowColor = selectedOutlineColor;
            }
        }
        else if (true == isHovered)
        {
            if (null != targetImage)
            {
                targetImage.sprite = null != hoveredSprite ? hoveredSprite : normalSprite;
            }

            if (null != tabText)
            {
                tabText.color = hoveredTextColor;
            }

            if (null != uiEffect)
            {
                uiEffect.shadowColor = hoveredOutlineColor;
            }
        }
        else
        {
            if (null != targetImage && null != normalSprite)
            {
                targetImage.sprite = normalSprite;
            }

            if (null != tabText)
            {
                tabText.color = normalTextColor;
            }

            if (null != uiEffect)
            {
                uiEffect.shadowColor = normalOutlineColor;
            }
        }
    }

    [Header("Cursor Settings")]
    [SerializeField] private Vector2 cursorPadding = new Vector2(10f, 10f);
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;

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
            cursorBoxUI.Show(_targetRect, _size, cursorOffset, CursorMotionSettings.Subtle);
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

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        HideCursor();
    }

    // 유니티 이벤트 함수
    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        isHovered = true;
        Sound.PlayUI(SoundID.MainMenuDot01);
        UpdateVisualState();
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        isHovered = false;
        UpdateVisualState();
        HideCursor();
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        isHovered = true;
        UpdateVisualState();
        ShowCursor();

        if (null != parentGroup)
        {
            parentGroup.OnTabClicked(tabIndex);
        }
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);
        isHovered = false;
        UpdateVisualState();
        HideCursor();
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        OnPointerClick(null);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null != parentGroup)
        {
            parentGroup.OnTabClicked(tabIndex);
        }
    }
}
