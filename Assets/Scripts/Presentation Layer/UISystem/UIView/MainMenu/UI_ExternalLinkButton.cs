using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using DG.Tweening.Core;
using Coffee.UIEffects;
using PresentationLayer.UISystem;

public enum EExternalLinkVisualEffectMode
{
    ImageColor,   // Image 자체의 color(틴트)를 변경하여 연출
    OutlineShadow // UIEffect의 ShadowColor(아웃라인)를 변경하여 연출
}

public class UI_ExternalLinkButton : Selectable, ISubmitHandler, IPointerClickHandler
{
    [Header("Visual Effect Settings")]
    [SerializeField, Tooltip("호버 및 클릭 시의 시각 효과 연출 방식 (이미지 색상 틴트 vs UIEffect 아웃라인)")]
    private EExternalLinkVisualEffectMode effectMode = EExternalLinkVisualEffectMode.ImageColor;
    [SerializeField, Tooltip("색상 변화 연출을 적용할 시각적 이미지 (레이캐스트 전용 이미지와 분리)")]
    private Image targetVisualImage;
    [SerializeField, Tooltip("아웃라인 연출에 사용할 UIEffect 컴포넌트 (OutlineShadow 모드 전용)")]
    private UIEffect targetUIEffect;

    [Header("Link Settings")]
    [SerializeField, Tooltip("클릭 시 이동할 외부 링크 URL (디스코드, 웹사이트 등)")]
    private string targetUrl = "https://discord.gg/your_invite_link";

    [Header("Color Settings")]
    [SerializeField, Tooltip("기본 상태 색상")]
    private Color normalColor = Color.white;
    [SerializeField, Tooltip("마우스 오버(Hover) 상태 색상")]
    private Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField, Tooltip("클릭(Click) 상태 색상")]
    private Color clickColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("Animation Settings")]
    [SerializeField, Tooltip("색상 전환 연출 시간")]
    private float transitionDuration = 0.15f;
    [SerializeField, Tooltip("색상 전환 연출 이즈(Ease)")]
    private Ease transitionEase = Ease.OutQuad;

    [Header("Cursor Settings")]
    [SerializeField, Tooltip("커서 박스 크기를 수동으로 직접 지정할지 여부")]
    private bool useCustomCursorSize = false;
    [SerializeField, Tooltip("수동 지정할 커서 박스 크기")]
    private Vector2 customCursorSize = new Vector2(40f, 40f);
    [SerializeField, Tooltip("커서 박스 여백 (패딩)")]
    private Vector2 cursorPadding = new Vector2(10f, 10f);
    [SerializeField, Tooltip("커서 박스 위치 오프셋")]
    private Vector2 cursorOffset = Vector2.zero;

    private Tween visualTween;
    private bool isHovered = false;
    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;

    private DOGetter<Color> getShadowColor;
    private DOSetter<Color> setShadowColor;

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        transition = Transition.None;
    }
#endif

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        EnsureDelegates();
        ApplyInitialVisualState();
    }

    private void EnsureDelegates()
    {
        if (null == targetVisualImage)
        {
            targetVisualImage = GetComponent<Image>();
        }

        if (null == targetUIEffect && null != targetVisualImage)
        {
            targetUIEffect = targetVisualImage.GetComponent<UIEffect>();
        }

        if (null == getShadowColor)
        {
            getShadowColor = GetShadowColor;
        }

        if (null == setShadowColor)
        {
            setShadowColor = SetShadowColor;
        }
    }

    private void ApplyInitialVisualState()
    {
        EnsureDelegates();

        if (EExternalLinkVisualEffectMode.OutlineShadow == effectMode)
        {
            if (null != targetUIEffect)
            {
                targetUIEffect.shadowColor = normalColor;
            }
            if (null != targetVisualImage)
            {
                targetVisualImage.color = Color.white;
            }
        }
        else
        {
            if (null != targetVisualImage)
            {
                targetVisualImage.color = normalColor;
            }
        }
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        cursorBoxUI = _cursorBoxUI;
        inputManager = _inputManager;
    }

    public void SetUrl(string _url)
    {
        targetUrl = _url;
    }

    public void ShowCursor()
    {
        if (null == cursorBoxUI) return;
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        RectTransform _targetRect = (null != targetVisualImage) ? targetVisualImage.rectTransform : (transform as RectTransform);
        if (null != _targetRect)
        {
            Vector2 _size = useCustomCursorSize
                ? customCursorSize
                : Vector2.Scale(_targetRect.rect.size, new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y))) + cursorPadding;

            cursorBoxUI.Show(_targetRect, _size, cursorOffset, CursorMotionSettings.Subtle);
        }
    }

    public void HideCursor()
    {
        if (null == cursorBoxUI) return;

        RectTransform _targetRect = (null != targetVisualImage) ? targetVisualImage.rectTransform : (transform as RectTransform);
        if (null != _targetRect)
        {
            cursorBoxUI.Hide(_targetRect);
        }
        else
        {
            cursorBoxUI.Hide();
        }
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);

        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        isHovered = true;
        PlayVisualTween(hoverColor);
        Sound.PlayUI(SoundID.MainMenuDot01);
        ShowCursor();
    }

    public bool IsHovered => isHovered;

    public void FocusButton(bool _playSound = true)
    {
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        isHovered = true;
        PlayVisualTween(hoverColor);
        if (true == _playSound)
        {
            Sound.PlayUI(SoundID.MainMenuDot01);
        }

        if (null != EventSystem.current)
        {
            if (EventSystem.current.currentSelectedGameObject != gameObject)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        ShowCursor();
    }

    public void UnfocusButton()
    {
        isHovered = false;
        PlayVisualTween(normalColor);
        HideCursor();
    }

    public void UnfocusButtonImmediate()
    {
        isHovered = false;
        KillVisualTween();
        SetVisualColorImmediate(normalColor);
        HideCursor();
    }

    public void ForceHover(bool _playSound = false)
    {
        isHovered = true;
        PlayVisualTween(hoverColor);
        if (true == _playSound)
        {
            Sound.PlayUI(SoundID.MainMenuDot01);
        }
        ShowCursor();
    }

    public void ForceUnhover()
    {
        UnfocusButton();
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);

        isHovered = false;
        PlayVisualTween(normalColor);
        HideCursor();
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        ExecuteClick();
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        ExecuteClick();

        if (null == inputManager || false == inputManager.IsGamepadMode)
        {
            HideCursor();
        }
    }

    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = true;
        PlayVisualTween(hoverColor);
        Sound.PlayUI(SoundID.MainMenuDot01);
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = false;
        PlayVisualTween(normalColor);
        HideCursor();
    }

    public void ExecuteClick()
    {
        Sound.PlayUI(SoundID.OptionClick);

        if (false == string.IsNullOrEmpty(targetUrl))
        {
            Application.OpenURL(targetUrl);
        }

        // 클릭 효과 연출: 순간적으로 ClickColor를 적용한 후 다시 원래 타겟 컬러로 DOTween 복귀
        KillVisualTween();

        SetVisualColorImmediate(clickColor);

        Color _targetColor = (true == isHovered) ? hoverColor : normalColor;
        PlayVisualTween(_targetColor);
    }

    private void PlayVisualTween(Color _targetColor)
    {
        KillVisualTween();
        EnsureDelegates();

        if (EExternalLinkVisualEffectMode.OutlineShadow == effectMode)
        {
            if (null != targetUIEffect && null != getShadowColor && null != setShadowColor)
            {
                visualTween = DOTween.To(getShadowColor, setShadowColor, _targetColor, transitionDuration)
                                     .SetEase(transitionEase)
                                     .SetTarget(targetUIEffect);
            }
        }
        else
        {
            if (null != targetVisualImage)
            {
                visualTween = targetVisualImage.DOColor(_targetColor, transitionDuration)
                                               .SetEase(transitionEase);
            }
        }
    }

    private void SetVisualColorImmediate(Color _color)
    {
        EnsureDelegates();

        if (EExternalLinkVisualEffectMode.OutlineShadow == effectMode)
        {
            if (null != targetUIEffect)
            {
                targetUIEffect.shadowColor = _color;
            }
        }
        else
        {
            if (null != targetVisualImage)
            {
                targetVisualImage.color = _color;
            }
        }
    }

    private void KillVisualTween()
    {
        if (null != visualTween && true == visualTween.IsActive())
        {
            visualTween.Kill();
            visualTween = null;
        }
    }

    private Color GetShadowColor() => (null != targetUIEffect) ? targetUIEffect.shadowColor : normalColor;
    private void SetShadowColor(Color _c) { if (null != targetUIEffect) targetUIEffect.shadowColor = _c; }

    public bool IsMouseOver()
    {
        if (false == gameObject.activeInHierarchy) return false;

        RectTransform _rect = transform as RectTransform;
        if (null == _rect) return false;

        Vector2 _mousePos = Vector2.zero;
        if (null != Mouse.current)
        {
            _mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            _mousePos = Input.mousePosition;
        }

        Canvas _canvas = GetComponentInParent<Canvas>();
        Camera _cam = (null != _canvas && RenderMode.ScreenSpaceOverlay != _canvas.renderMode) ? _canvas.worldCamera : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        HideCursor();
        KillVisualTween();
        ApplyInitialVisualState();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        KillVisualTween();
    }
}
