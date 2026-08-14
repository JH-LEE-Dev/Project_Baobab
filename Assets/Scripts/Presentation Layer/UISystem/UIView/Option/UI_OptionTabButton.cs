using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 옵션 창의 개별 탭 버튼을 담당합니다. 클로저 할당 방지를 위해 IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler를 직접 구현합니다.
/// </summary>
public class UI_OptionTabButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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

    private void OnDisable()
    {
        isHovered = false;
    }

    // 유니티 이벤트 함수
    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        Sound.PlayUI(SoundID.MainMenuDot01);
        UpdateVisualState();
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        UpdateVisualState();
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        Sound.PlayUI(SoundID.OptionClick);

        if (null != parentGroup)
        {
            parentGroup.OnTabClicked(tabIndex);
        }
    }
}
