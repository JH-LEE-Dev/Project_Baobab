using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 옵션 창의 개별 탭 버튼을 담당합니다. 클로저 할당 방지를 위해 IPointerClickHandler를 직접 구현합니다.
/// </summary>
public class UI_OptionTabButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    // 외부 컴포넌트 참조
    [Header("Visual Settings")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Text Settings")]
    [SerializeField] private TextMeshProUGUI tabText;
    [SerializeField] private Color normalTextColor = Color.gray;
    [SerializeField] private Color selectedTextColor = Color.white;

    [Header("Shadow Settings")]
    [SerializeField] private Coffee.UIEffects.UIEffect uiEffect;
    [SerializeField, ColorUsage(true, true)] private Color normalOutlineColor = Color.black;
    [SerializeField, ColorUsage(true, true)] private Color selectedOutlineColor = Color.white;

    // 내부 상태
    private UI_OptionTabGroup parentGroup;
    private int tabIndex;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UI_OptionTabGroup _parent, int _index)
    {
        parentGroup = _parent;
        tabIndex = _index;
    }

    public void SetSelected(bool _isSelected)
    {
        if (null != targetImage)
        {
            targetImage.sprite = true == _isSelected ? selectedSprite : normalSprite;
        }

        if (null != tabText)
        {
            tabText.color = true == _isSelected ? selectedTextColor : normalTextColor;
        }

        if (null != uiEffect)
        {
            uiEffect.shadowColor = true == _isSelected ? selectedOutlineColor : normalOutlineColor;
        }
    }

    public void SetText(string _text)
    {
        if (null != tabText)
        {
            tabText.text = _text;
        }
    }

    // 유니티 이벤트 함수
    public void OnPointerEnter(PointerEventData _eventData)
    {
        Sound.PlayUI(SoundID.MainMenuDot01);
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
