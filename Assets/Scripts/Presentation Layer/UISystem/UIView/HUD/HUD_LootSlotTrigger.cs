using UnityEngine;
using UnityEngine.EventSystems;
using Coffee.UIEffects;
using DG.Tweening;
using DG.Tweening.Core;

/// <summary>
/// 동적으로 생성된 전리품 슬롯(Image)에 부착되어 마우스 호버 이벤트를 감지하고 툴팁을 띄웁니다.
/// </summary>
public class HUD_LootSlotTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private HUD_LootTooltip tooltipUI;
    private UI_RedDot redDotUI;
    private RectTransform rectTransform;
    private string descriptionText;
    
    private UIEffect outlineEffect;
    private Tween outlineTween;
    private Color originalShadowColor;

    private DOGetter<Color> cachedGetShadowColor;
    private DOSetter<Color> cachedSetShadowColor;

    public void Initialize(HUD_LootTooltip _tooltipUI, UI_RedDot _redDotUI)
    {
        cachedGetShadowColor = GetOutlineShadowColor;
        cachedSetShadowColor = SetOutlineShadowColor;

        tooltipUI = _tooltipUI;
        redDotUI = _redDotUI;
        rectTransform = GetComponent<RectTransform>();
        
        outlineEffect = GetComponent<UIEffect>();
        if (null != outlineEffect)
        {
            originalShadowColor = outlineEffect.shadowColor;
            Color _c = originalShadowColor;
            _c.a = 0f;
            outlineEffect.shadowColor = _c;
        }
    }

    public void SetDescription(string _description)
    {
        descriptionText = _description;
    }

    public void StartPulse()
    {
        if (null == outlineEffect) return;

        if (null != outlineTween && true == outlineTween.IsActive())
        {
            outlineTween.Kill();
        }

        Color _startC = originalShadowColor;
        _startC.a = 0f;
        outlineEffect.shadowColor = _startC;

        Color _endC = originalShadowColor;
        _endC.a = 1f;

        outlineTween = DOTween.To(cachedGetShadowColor, cachedSetShadowColor, _endC, 0.5f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject);
    }

    private void StopPulse()
    {
        if (null == outlineEffect) return;

        if (null != outlineTween && true == outlineTween.IsActive())
        {
            outlineTween.Kill();
        }

        Color _endC = originalShadowColor;
        _endC.a = 0f;

        outlineTween = DOTween.To(cachedGetShadowColor, cachedSetShadowColor, _endC, 0.3f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    private Color GetOutlineShadowColor()
    {
        return null != outlineEffect ? outlineEffect.shadowColor : Color.clear;
    }

    private void SetOutlineShadowColor(Color _color)
    {
        if (null != outlineEffect)
        {
            outlineEffect.shadowColor = _color;
        }
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
        
        StopPulse();
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
        
        if (null != outlineTween && true == outlineTween.IsActive())
        {
            outlineTween.Kill();
        }
    }
}
