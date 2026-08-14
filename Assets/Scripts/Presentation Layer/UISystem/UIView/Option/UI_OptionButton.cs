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
            
            Color _targetGraphicColor = (EVisualMode.Color == visualMode) ? normalColor : (null != targetGraphic ? targetGraphic.color : Color.white);
            _targetGraphicColor.a = 0.5f;
            Color _targetTextColor = normalTextColor; _targetTextColor.a = 0.5f;
            Color _targetEffectColor = normalEffectColor; _targetEffectColor.a = 0.5f;

            ApplyVisualState(originalScale, _targetGraphicColor, normalSprite, _targetTextColor, _targetEffectColor, false);
        }
        else
        {
            Color _targetGraphicColor = null != targetGraphic ? targetGraphic.color : Color.white;
            _targetGraphicColor.a = 1f;
            Color _targetTextColor = null != buttonText ? buttonText.color : Color.white;
            _targetTextColor.a = 1f;
            Color _targetEffectColor = null != targetEffect ? targetEffect.shadowColor : Color.black;
            _targetEffectColor.a = 1f;

            ApplyVisualState(originalScale, _targetGraphicColor, normalSprite, _targetTextColor, _targetEffectColor, false);
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
            ApplyVisualState(clickScale, clickColor, clickSprite, clickTextColor, clickEffectColor, true);
        }
        else
        {
            ApplyVisualState(hoverScale, hoverColor, hoverSprite, hoverTextColor, hoverEffectColor, true);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (false == isInteractable) return;
        
        KillTween();
        ApplyVisualState(originalScale, normalColor, normalSprite, normalTextColor, normalEffectColor, true);
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        isPointerDown = true;
        if (false == isInteractable) return;
        
        KillTween();
        ApplyVisualState(clickScale, clickColor, clickSprite, clickTextColor, clickEffectColor, true);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        isPointerDown = false;
        if (false == isInteractable) return;
        
        KillTween();
        
        if (true == isHovered)
        {
            ApplyVisualState(hoverScale, hoverColor, hoverSprite, hoverTextColor, hoverEffectColor, true);
        }
        else
        {
            ApplyVisualState(originalScale, normalColor, normalSprite, normalTextColor, normalEffectColor, true);
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
        
        Color _targetGraphicColor = (EVisualMode.Color == visualMode) ? normalColor : (null != targetGraphic ? targetGraphic.color : Color.white);
        _targetGraphicColor.a = (true == isInteractable) ? 1f : 0.5f;
        Color _targetTextColor = normalTextColor; _targetTextColor.a = (true == isInteractable) ? 1f : 0.5f;
        Color _targetEffectColor = normalEffectColor; _targetEffectColor.a = (true == isInteractable) ? 1f : 0.5f;

        ApplyVisualState(originalScale, _targetGraphicColor, normalSprite, _targetTextColor, _targetEffectColor, false);
    }

    private void ApplyVisualState(Vector3 _scale, Color _graphicColor, Sprite _sprite, Color _textColor, Color _effectColor, bool _animated)
    {
        if (true == _animated)
        {
            if (true == enableScaleMotion) scaleTarget.DOScale(_scale, tweenDuration).SetUpdate(true);
            if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.DOColor(_graphicColor, tweenDuration).SetUpdate(true);
            else if (EVisualMode.Sprite == visualMode && null != targetImage && null != _sprite) targetImage.sprite = _sprite;
            
            if (null != buttonText) buttonText.DOColor(_textColor, tweenDuration).SetUpdate(true);
            if (null != targetEffect) DOTween.To(() => targetEffect.shadowColor, x => targetEffect.shadowColor = x, _effectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
        }
        else
        {
            if (true == enableScaleMotion) scaleTarget.localScale = _scale;
            if (EVisualMode.Color == visualMode && null != targetGraphic) targetGraphic.color = _graphicColor;
            else if (EVisualMode.Sprite == visualMode && null != targetImage && null != _sprite) targetImage.sprite = _sprite;
            
            if (null != buttonText) buttonText.color = _textColor;
            if (null != targetEffect) targetEffect.shadowColor = _effectColor;
        }
    }

    private void OnDestroy()
    {
        KillTween();
        onClickAction = null;
    }
}
