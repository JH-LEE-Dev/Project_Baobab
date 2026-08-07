using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 옵션 창 전용 버튼 클래스입니다. (닫기 버튼, 좌우 화살표 등)
/// 람다를 배제하고 GC 할당이 없는 커스텀 클릭 및 마우스 호버 모션을 지원합니다.
/// </summary>
public class UI_OptionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Component")]
    [SerializeField, Tooltip("크기와 색상이 변형될 대상 이미지 (Raycast 본체와 다를 경우 지정)")] 
    private Graphic targetGraphic;
    
    [SerializeField, Tooltip("버튼에 표시될 텍스트 (선택 사항)")]
    private TMPro.TextMeshProUGUI buttonText;
    
    [SerializeField, Tooltip("그림자 효과 변경을 위한 UIEffect 컴포넌트 (선택 사항)")]
    private Coffee.UIEffects.UIEffect targetEffect;

    public enum EVisualMode { None, Color, Sprite }

    [Header("Motion Settings")]
    [SerializeField, Tooltip("스케일 모션 켜기/끄기")] private bool enableScaleMotion = true;
    [SerializeField, Tooltip("비주얼 연출 모드 선택")] private EVisualMode visualMode = EVisualMode.Color;
    [SerializeField] private Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);
    [SerializeField] private Vector3 clickScale = new Vector3(0.9f, 0.9f, 1f);
    [SerializeField] private float tweenDuration = 0.1f;
    
    [Header("Color Settings (If Visual Mode is Color)")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color clickColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    
    [Header("Sprite Settings (If Visual Mode is Sprite)")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite clickSprite;
    
    [Header("Text Color Settings")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.white;
    [SerializeField] private Color clickTextColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    
    [Header("Effect Settings")]
    [ColorUsage(true, true)] [SerializeField] private Color normalEffectColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color hoverEffectColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color clickEffectColor = Color.black;
    
    private Action onClickAction;
    private SoundID hoverSoundId = SoundID.MainMenuDot01;
    private SoundID clickSoundId = SoundID.OptionClick;
    private bool isInteractable = true;
    
    private bool isHovered = false;
    private bool isPointerDown = false;

    // 초기 상태 캐싱
    private Vector3 originalScale;
    private Transform scaleTarget;
    private Image targetImage;

    private void Awake()
    {
        scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        originalScale = scaleTarget.localScale;
        
        targetImage = targetGraphic as Image;
    }

    public void Initialize(
        Action _onClick,
        SoundID _hoverSoundId = SoundID.MainMenuDot01,
        SoundID _clickSoundId = SoundID.OptionClick)
    {
        onClickAction = _onClick;
        hoverSoundId = _hoverSoundId;
        clickSoundId = _clickSoundId;
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;
        
        if (false == isInteractable)
        {
            KillTween();
            isHovered = false;
            isPointerDown = false;
            
            if (true == enableScaleMotion) scaleTarget.localScale = originalScale;
            if (null != targetGraphic)
            {
                Color _c = EVisualMode.Color == visualMode ? normalColor : targetGraphic.color;
                _c.a = 0.5f;
                targetGraphic.color = _c;
                
                if (EVisualMode.Sprite == visualMode && null != targetImage)
                {
                    if (null != normalSprite) targetImage.sprite = normalSprite;
                }
            }
            if (null != buttonText)
            {
                Color _tc = normalTextColor;
                _tc.a = 0.5f;
                buttonText.color = _tc;
            }
            if (null != targetEffect)
            {
                Color _ec = normalEffectColor;
                _ec.a = 0.5f;
                targetEffect.shadowColor = _ec;
            }
        }
        else
        {
            if (null != targetGraphic)
            {
                Color _c = targetGraphic.color;
                _c.a = 1f;
                targetGraphic.color = _c;
                
                if (EVisualMode.Sprite == visualMode && null != targetImage)
                {
                    if (null != normalSprite) targetImage.sprite = normalSprite;
                }
            }
            if (null != buttonText)
            {
                Color _tc = buttonText.color;
                _tc.a = 1f;
                buttonText.color = _tc;
            }
            if (null != targetEffect)
            {
                Color _ec = targetEffect.shadowColor;
                _ec.a = 1f;
                targetEffect.shadowColor = _ec;
            }
        }
    }

    public void SetText(string _text)
    {
        if (null != buttonText)
        {
            buttonText.text = _text;
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        if (false == isInteractable) return;

        Sound.PlayUI(hoverSoundId);
        
        KillTween();
        if (true == isPointerDown)
        {
            if (true == enableScaleMotion) scaleTarget.DOScale(clickScale, tweenDuration).SetUpdate(true);
            if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(clickColor, tweenDuration).SetUpdate(true);
            else if (EVisualMode.Sprite == visualMode && null != targetImage && null != clickSprite) targetImage.sprite = clickSprite;
            
            if (null != buttonText) buttonText.DOColor(clickTextColor, tweenDuration).SetUpdate(true);
            if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, clickEffectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
        }
        else
        {
            if (true == enableScaleMotion) scaleTarget.DOScale(hoverScale, tweenDuration).SetUpdate(true);
            if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(hoverColor, tweenDuration).SetUpdate(true);
            else if (EVisualMode.Sprite == visualMode && null != targetImage && null != hoverSprite) targetImage.sprite = hoverSprite;
            
            if (null != buttonText) buttonText.DOColor(hoverTextColor, tweenDuration).SetUpdate(true);
            if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, hoverEffectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (false == isInteractable) return;
        
        KillTween();
        if (true == enableScaleMotion) scaleTarget.DOScale(originalScale, tweenDuration).SetUpdate(true);
        if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(normalColor, tweenDuration).SetUpdate(true);
        else if (EVisualMode.Sprite == visualMode && null != targetImage && null != normalSprite) targetImage.sprite = normalSprite;
        
        if (null != buttonText) buttonText.DOColor(normalTextColor, tweenDuration).SetUpdate(true);
        if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, normalEffectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        isPointerDown = true;
        if (false == isInteractable) return;
        
        KillTween();
        if (true == enableScaleMotion) scaleTarget.DOScale(clickScale, tweenDuration).SetUpdate(true);
        if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(clickColor, tweenDuration).SetUpdate(true);
        else if (EVisualMode.Sprite == visualMode && null != targetImage && null != clickSprite) targetImage.sprite = clickSprite;
        
        if (null != buttonText) buttonText.DOColor(clickTextColor, tweenDuration).SetUpdate(true);
        if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, clickEffectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        isPointerDown = false;
        if (false == isInteractable) return;
        
        KillTween();
        
        if (true == isHovered)
        {
            if (true == enableScaleMotion) scaleTarget.DOScale(hoverScale, tweenDuration).SetUpdate(true);
            if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(hoverColor, tweenDuration).SetUpdate(true);
            else if (EVisualMode.Sprite == visualMode && null != targetImage && null != hoverSprite) targetImage.sprite = hoverSprite;
            
            if (null != buttonText) buttonText.DOColor(hoverTextColor, tweenDuration).SetUpdate(true);
            if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, hoverEffectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
        }
        else
        {
            if (true == enableScaleMotion) scaleTarget.DOScale(originalScale, tweenDuration).SetUpdate(true);
            if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(normalColor, tweenDuration).SetUpdate(true);
            else if (EVisualMode.Sprite == visualMode && null != targetImage && null != normalSprite) targetImage.sprite = normalSprite;
            
            if (null != buttonText) buttonText.DOColor(normalTextColor, tweenDuration).SetUpdate(true);
            if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, normalEffectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable) return;

        Sound.PlayUI(clickSoundId);

        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }
    }

    private void KillTween()
    {
        scaleTarget.DOKill();
        if (null != targetGraphic) targetGraphic.DOKill();
        if (null != buttonText) buttonText.DOKill();
        if (null != targetEffect) DOTween.Kill(targetEffect);
    }

    private void OnDisable()
    {
        isHovered = false;
        isPointerDown = false;

        KillTween();
        
        scaleTarget.localScale = originalScale;
        
        if (null != targetGraphic)
        {
            Color _c = EVisualMode.Color == visualMode ? normalColor : targetGraphic.color;
            _c.a = true == isInteractable ? 1f : 0.5f;
            targetGraphic.color = _c;
            
            if (EVisualMode.Sprite == visualMode && null != targetImage && null != normalSprite)
            {
                targetImage.sprite = normalSprite;
            }
        }
        
        if (null != buttonText)
        {
            Color _tc = normalTextColor;
            _tc.a = true == isInteractable ? 1f : 0.5f;
            buttonText.color = _tc;
        }
        
        if (null != targetEffect)
        {
            Color _ec = normalEffectColor;
            _ec.a = true == isInteractable ? 1f : 0.5f;
            targetEffect.shadowColor = _ec;
        }
    }

    private void OnDestroy()
    {
        KillTween();
        onClickAction = null;
    }
}
