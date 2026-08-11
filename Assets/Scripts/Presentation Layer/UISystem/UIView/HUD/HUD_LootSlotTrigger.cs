using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 동적으로 생성된 전리품 슬롯(Image)에 부착되어 마우스 호버 이벤트를 감지하고 툴팁을 띄웁니다.
/// </summary>
public class HUD_LootSlotTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private HUD_LootTooltip tooltipUI;
    private UI_RedDot redDotUI;
    private RectTransform rectTransform;
    private string descriptionText;

    public void Initialize(HUD_LootTooltip _tooltipUI, UI_RedDot _redDotUI)
    {
        tooltipUI = _tooltipUI;
        redDotUI = _redDotUI;
        rectTransform = GetComponent<RectTransform>();
    }

    public void SetDescription(string _description)
    {
        descriptionText = _description;
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (null != tooltipUI && false == string.IsNullOrEmpty(descriptionText))
        {
            tooltipUI.ShowTooltip(rectTransform, descriptionText);
        }

        if (null != redDotUI)
        {
            redDotUI.Deactivate();
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null != tooltipUI)
        {
            tooltipUI.HideTooltip();
        }
    }
    
    private void OnDisable()
    {
        // 슬롯이 비활성화될 때 툴팁도 함께 숨김 처리
        if (null != tooltipUI)
        {
            tooltipUI.HideTooltip();
        }
    }
}
