using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System;
using UnityEngine.InputSystem;

/// <summary>
/// 옵션 창 전용 버튼 클래스입니다. (닫기 버튼, 좌우 화살표 등)
/// 람다를 배제하고 GC 할당이 없는 커스텀 클릭 및 마우스 호버 모션을 지원합니다.
/// </summary>
public class UI_OptionButton : Selectable,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISubmitHandler
{
    [Header("UI Component")]
    [SerializeField, Tooltip("크기와 색상이 변형될 대상 이미지 (Raycast 본체와 다를 경우 지정)")] 
    private new Graphic targetGraphic;
    
    [SerializeField, Tooltip("버튼에 표시될 텍스트 (선택 사항)")]
    private TMPro.TextMeshProUGUI buttonText;
    
    [SerializeField, Tooltip("그림자 효과 변경을 위한 UIEffect 컴포넌트 (선택 사항)")]
    private Coffee.UIEffects.UIEffect targetEffect;

    [SerializeField, Tooltip("게임패드 전용 핫키 안내 아이콘 이미지 (선택 사항)")]
    private Image shortcutIconImage;

    public void SetShortcutIcon(Sprite _icon, bool _show)
    {
        if (null == shortcutIconImage) return;
        if (null != _icon) shortcutIconImage.sprite = _icon;
        shortcutIconImage.gameObject.SetActive(_show && null != _icon);
    }

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
    
    public enum EArrowDirection { None, Left, Right }

    private Action onClickAction;
    public event Action<bool> OnFocusChanged;
    private SoundID hoverSoundId = SoundID.MainMenuDot01;
    private SoundID clickSoundId = SoundID.OptionClick;
    private bool isInteractable = true;
    
    private bool isHovered = false;
    private bool isPointerDown = false;

    private UI_OptionButton pairedArrowButton;
    private EArrowDirection arrowDirection = EArrowDirection.None;
    private UI_CustomScroll customScroll;

    // 초기 상태 캐싱
    private Vector3 originalScale;
    private Transform scaleTarget;
    private Image targetImage;

    // GC Alloc 방지용 델리게이트 캐싱
    private DG.Tweening.Core.DOGetter<Color> getShadowColor;
    private DG.Tweening.Core.DOSetter<Color> setShadowColor;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        originalScale = scaleTarget.localScale;
        
        targetImage = targetGraphic as Image;

        getShadowColor = GetShadowColor;
        setShadowColor = SetShadowColor;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        isHovered = false;
        isPointerDown = false;

        KillTween();
        HideCursor();
        OnFocusChanged?.Invoke(false);

        Color _targetGraphicColor = (EVisualMode.Color == visualMode) ? normalColor : (null != targetGraphic ? targetGraphic.color : Color.white);
        _targetGraphicColor.a = (true == isInteractable) ? 1f : 0.5f;
        Color _targetTextColor = normalTextColor; _targetTextColor.a = (true == isInteractable) ? 1f : 0.5f;
        Color _targetEffectColor = normalEffectColor; _targetEffectColor.a = (true == isInteractable) ? 1f : 0.5f;

        ApplyVisualState(originalScale, _targetGraphicColor, normalSprite, _targetTextColor, _targetEffectColor, false);
    }

    public void SetPairedButton(UI_OptionButton _pair, EArrowDirection _direction)
    {
        pairedArrowButton = _pair;
        arrowDirection = _direction;
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

    public new bool IsInteractable => isInteractable && interactable;

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;
        interactable = _isInteractable;
        
        if (false == isInteractable)
        {
            KillTween();
            isHovered = false;
            isPointerDown = false;
            HideCursor();
            
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

    [Header("Cursor Settings")]
    [SerializeField] private Vector2 cursorPadding = new Vector2(2f, 2f);
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
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        RectTransform _targetRect = (null != targetGraphic) ? targetGraphic.rectTransform : (transform as RectTransform);
        if (null != _targetRect)
        {
            Vector2 _size = _targetRect.rect.size;
            if (null != buttonText)
            {
                _size.x = Mathf.Max(_size.x, buttonText.rectTransform.rect.size.x, buttonText.preferredWidth);
                _size.y = Mathf.Max(_size.y, buttonText.rectTransform.rect.size.y, buttonText.preferredHeight);
            }
            _size += cursorPadding;
            cursorBoxUI.Show(_targetRect, _size, cursorOffset, CursorMotionSettings.RowSubtle);
        }
    }

    public void HideCursor()
    {
        if (null == cursorBoxUI) return;
        RectTransform _targetRect = (null != targetGraphic) ? targetGraphic.rectTransform : (transform as RectTransform);
        if (null != _targetRect)
        {
            cursorBoxUI.Hide(_targetRect);
        }
        else
        {
            cursorBoxUI.Hide();
        }
    }

    public void SetText(string _text)
    {
        if (null != buttonText)
        {
            buttonText.text = _text;
        }
    }

    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

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

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = false;
        if (false == isInteractable) return;
        
        KillTween();
        ApplyVisualState(originalScale, normalColor, normalSprite, normalTextColor, normalEffectColor, true);

        HideCursor();
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
        RectTransform _rect = (null != targetGraphic) ? targetGraphic.rectTransform : (transform as RectTransform);
        if (null == _rect) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    public void ForceUnhover()
    {
        isHovered = false;
        isPointerDown = false;
        KillTween();
        ApplyVisualState(originalScale, normalColor, normalSprite, normalTextColor, normalEffectColor, false);
        HideCursor();
        OnFocusChanged?.Invoke(false);
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        if (null == inputManager || false == inputManager.IsGamepadMode) return;
        if (false == isInteractable) return;

        if (true == isHovered)
        {
            ShowCursor();
            return;
        }

        isHovered = true;
        Sound.PlayUI(hoverSoundId);
        
        KillTween();
        ApplyVisualState(hoverScale, hoverColor, hoverSprite, hoverTextColor, hoverEffectColor, true);

        if (null == customScroll)
        {
            customScroll = GetComponentInParent<UI_CustomScroll>();
        }
        if (null != customScroll)
        {
            customScroll.EnsureVisible(transform as RectTransform);
        }

        ShowCursor();
        OnFocusChanged?.Invoke(true);
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);
        if (null == inputManager || false == inputManager.IsGamepadMode) return;
        if (false == isInteractable) return;
        
        isHovered = false;
        KillTween();
        ApplyVisualState(originalScale, normalColor, normalSprite, normalTextColor, normalEffectColor, true);

        HideCursor();
        OnFocusChanged?.Invoke(false);
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        if (false == isInteractable) return;

        Sound.PlayUI(clickSoundId);
        PlayClickFeedback();

        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }
    }

    public override void OnMove(AxisEventData _eventData)
    {
        if (EArrowDirection.Left == arrowDirection)
        {
            if (MoveDirection.Left == _eventData.moveDir)
            {
                // < 버튼에서 Left 입력 ➔ 값 감소 / 이전 선택지 실행 + 클릭 피드백
                if (true == isInteractable)
                {
                    Sound.PlayUI(clickSoundId);
                    PlayClickFeedback();
                    onClickAction?.Invoke();
                }
                _eventData.Use();
                return;
            }
            else if (MoveDirection.Right == _eventData.moveDir)
            {
                // < 버튼에서 Right 입력 ➔ > 버튼으로 포커스 이동
                if (null != pairedArrowButton && true == pairedArrowButton.gameObject.activeInHierarchy)
                {
                    EventSystem.current?.SetSelectedGameObject(pairedArrowButton.gameObject);
                }
                _eventData.Use();
                return;
            }
        }
        else if (EArrowDirection.Right == arrowDirection)
        {
            if (MoveDirection.Right == _eventData.moveDir)
            {
                // > 버튼에서 Right 입력 ➔ 값 증가 / 다음 선택지 실행 + 클릭 피드백
                if (true == isInteractable)
                {
                    Sound.PlayUI(clickSoundId);
                    PlayClickFeedback();
                    onClickAction?.Invoke();
                }
                _eventData.Use();
                return;
            }
            else if (MoveDirection.Left == _eventData.moveDir)
            {
                // > 버튼에서 Left 입력 ➔ < 버튼으로 포커스 이동
                if (null != pairedArrowButton && true == pairedArrowButton.gameObject.activeInHierarchy)
                {
                    EventSystem.current?.SetSelectedGameObject(pairedArrowButton.gameObject);
                }
                _eventData.Use();
                return;
            }
        }

        // Up, Down 및 일반 버튼은 기본 Selectable 네비게이션으로 행 간 이동
        base.OnMove(_eventData);
    }

    public void PlayClickFeedback()
    {
        KillTween();
        ApplyVisualState(clickScale, clickColor, clickSprite, clickTextColor, clickEffectColor, false);
        if (true == isHovered)
        {
            ApplyVisualState(hoverScale, hoverColor, hoverSprite, hoverTextColor, hoverEffectColor, true);
        }
        else
        {
            ApplyVisualState(originalScale, normalColor, normalSprite, normalTextColor, normalEffectColor, true);
        }
    }

    public override void OnPointerDown(PointerEventData _eventData)
    {
        base.OnPointerDown(_eventData);
        isPointerDown = true;
        if (false == isInteractable) return;
        
        KillTween();
        ApplyVisualState(clickScale, clickColor, clickSprite, clickTextColor, clickEffectColor, true);
    }

    public override void OnPointerUp(PointerEventData _eventData)
    {
        base.OnPointerUp(_eventData);
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
        PlayClickFeedback();

        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }

        if (null == inputManager || false == inputManager.IsGamepadMode)
        {
            HideCursor();
        }
    }

    private void KillTween()
    {
        scaleTarget.DOKill();
        if (null != targetGraphic) targetGraphic.DOKill();
        if (null != buttonText) buttonText.DOKill();
        if (null != targetEffect) DOTween.Kill(targetEffect);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        isPointerDown = false;

        KillTween();
        HideCursor();
        OnFocusChanged?.Invoke(false);
        
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
            if (null != targetEffect && null != getShadowColor && null != setShadowColor)
            {
                DOTween.To(getShadowColor, setShadowColor, _effectColor, tweenDuration).SetUpdate(true).SetTarget(targetEffect);
            }
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

    private Color GetShadowColor()
    {
        return null != targetEffect ? targetEffect.shadowColor : Color.black;
    }

    private void SetShadowColor(Color _color)
    {
        if (null != targetEffect)
        {
            targetEffect.shadowColor = _color;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        KillTween();
        onClickAction = null;
        getShadowColor = null;
        setShadowColor = null;
    }
}
