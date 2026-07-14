using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 옵션 창의 개별 탭 버튼을 담당합니다. 클로저 할당 방지를 위해 IPointerClickHandler를 직접 구현합니다.
/// </summary>
public class UI_OptionTabButton : MonoBehaviour, IPointerClickHandler
{
    // 외부 컴포넌트 참조
    [Header("Visual Settings")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color selectedColor = Color.white;

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
        if (null != targetGraphic)
        {
            targetGraphic.color = true == _isSelected ? selectedColor : normalColor;
        }
    }

    // 유니티 이벤트 함수
    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null != parentGroup)
        {
            parentGroup.OnTabClicked(tabIndex);
        }
    }
}
