using System;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Coffee.UIEffects;

/// <summary>
/// ESC 메뉴 전용 커스텀 버튼 컴포넌트입니다.
/// 등장 시 X 확장/Y 압축 상태에서 원복되며,
/// 호버 및 클릭 시 Y 스케일이 살짝 찌부되었다가 뽀잉 원복되는 찰진 모션을 제공합니다.
/// 퇴장 시에는 역순으로 찌부되며 페이드아웃됩니다.
/// </summary>
public class UI_EscapeMenuButton : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI Component References")]
    [SerializeField] private Image raycastImage;
    [SerializeField] private RectTransform motionTarget;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private UIEffect uiEffect;

    [Header("Appear Motion Settings (X확장 + Y압축에서 원복)")]
    [SerializeField] private Vector3 appearStartScale = new Vector3(1.25f, 0.2f, 1f);
    [SerializeField] private float appearDuration = 0.2f;
    [SerializeField] private Ease appearEase = Ease.OutBack;

    [Header("Disappear Motion Settings (되감기 역모션)")]
    [SerializeField] private float disappearDuration = 0.15f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    [Header("Hover & Click Squash Motion Settings (Y찌부 뽀잉)")]
    [SerializeField, Tooltip("호버/클릭 시 찌부되는 Y 스케일 비율 (예: 0.85)")]
    private float squashYScale = 0.85f;
    [SerializeField, Tooltip("찌부 후 뽀잉 원복까지의 전체 시간")]
    private float squashDuration = 0.22f;
    [SerializeField, Range(0.1f, 0.9f), Tooltip("눌리는 시간 비율")]
    private float squashPressRatio = 0.4f;
    [SerializeField] private Ease squashPressEase = Ease.OutQuad;
    [SerializeField] private Ease squashBounceEase = Ease.OutBack;

    [Header("UIEffect HDR Shadow Colors")]
    [ColorUsage(true, true)] [SerializeField] private Color normalShadowColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color hoverShadowColor = new Color(1.5f, 1.5f, 1.5f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color unhoverShadowColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color clickShadowColor = new Color(2f, 1.5f, 0.5f, 1f);
    [SerializeField] private float shadowTweenDuration = 0.15f;
    [SerializeField] private Ease shadowEase = Ease.OutQuad;

    private Action onClickAction;
    private bool isInteractable = true;
    private bool isHovered = false;
    private bool isAppearing = false;

    private Vector3 originalScale = Vector3.one;

    private RectTransform cachedRectTransform;
    private CanvasGroup canvasGroup;
    private Canvas cachedCanvas;

    private DOGetter<Color> getShadowColorDelegate;
    private DOSetter<Color> setShadowColorDelegate;
    private TweenCallback onAppearMotionStartCallback;
    private TweenCallback onAppearCompleteCallback;

    public RectTransform RectTransform
    {
        get
        {
            if (null == cachedRectTransform)
                cachedRectTransform = GetComponent<RectTransform>();
            return cachedRectTransform;
        }
    }

    public CanvasGroup CanvasGroup
    {
        get
        {
            if (null == canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (null == canvasGroup)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }
    }

    public float AppearDuration => appearDuration;
    public float DisappearDuration => disappearDuration;

    private void Awake()
    {
        if (null == cachedRectTransform)
            cachedRectTransform = GetComponent<RectTransform>();

        if (null == motionTarget)
            motionTarget = cachedRectTransform;

        originalScale = motionTarget.localScale;

        if (null == raycastImage)
            raycastImage = GetComponent<Image>();

        if (null == buttonText)
            buttonText = GetComponentInChildren<TMP_Text>();

        if (null == uiEffect)
            uiEffect = GetComponentInChildren<UIEffect>();

        if (null == canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (null == canvasGroup)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        getShadowColorDelegate = GetShadowColor;
        setShadowColorDelegate = SetShadowColor;
        onAppearMotionStartCallback = OnAppearMotionStart;
        onAppearCompleteCallback = OnAppearAnimationComplete;
    }

    private void OnDisable()
    {
        isHovered = false;
        isAppearing = false;

        KillTweens();

        if (null != motionTarget)
        {
            motionTarget.localScale = originalScale;
        }

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 1f;
        }

        if (null != uiEffect)
        {
            uiEffect.shadowColor = normalShadowColor;
        }
    }

    private void OnDestroy()
    {
        KillTweens();
        onClickAction = null;
        getShadowColorDelegate = null;
        setShadowColorDelegate = null;
        onAppearMotionStartCallback = null;
        onAppearCompleteCallback = null;
    }

    public void Initialize(Action _onClick)
    {
        onClickAction = _onClick;
    }

    public void SetText(string _text)
    {
        if (null != buttonText)
        {
            buttonText.text = _text;
        }
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;

        if (false == isInteractable)
        {
            KillTweens();
            isHovered = false;

            if (null != motionTarget)
            {
                motionTarget.localScale = originalScale;
            }

            if (null != uiEffect)
            {
                Color _c = normalShadowColor;
                _c.a = 0.5f;
                uiEffect.shadowColor = _c;
            }

            if (null != CanvasGroup)
            {
                CanvasGroup.alpha = 0.5f;
            }
        }
        else
        {
            if (null != CanvasGroup)
            {
                CanvasGroup.alpha = 1f;
            }

            if (null != uiEffect)
            {
                uiEffect.shadowColor = normalShadowColor;
            }

            if (false == isAppearing)
            {
                CheckCursorHover();
            }
        }
    }

    public void PrepareAppearState()
    {
        KillTweens();
        isInteractable = false;
        isAppearing = true;
        isHovered = false;

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 0f;
        }

        if (null != motionTarget)
        {
            motionTarget.localScale = Vector3.zero;
        }

        if (null != uiEffect)
        {
            uiEffect.shadowColor = normalShadowColor;
        }
    }

    public void PlayAppearAnimation()
    {
        PlayAppearAnimation(0f);
    }

    public void PlayAppearAnimation(float _delay)
    {
        KillTweens();
        isInteractable = false;
        isAppearing = true;
        isHovered = false;

        if (null == motionTarget)
        {
            isAppearing = false;
            isInteractable = true;
            return;
        }

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 0f;
        }
        motionTarget.localScale = Vector3.zero;

        Sequence _appearSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (_delay > 0f)
        {
            _appearSeq.AppendInterval(_delay);
        }

        _appearSeq.AppendCallback(onAppearMotionStartCallback);
        _appearSeq.Append(motionTarget.DOScale(originalScale, appearDuration).SetEase(appearEase));
        _appearSeq.OnComplete(onAppearCompleteCallback);
    }

    private void OnAppearMotionStart()
    {
        isInteractable = true;

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 1f;
        }

        if (null != motionTarget)
        {
            motionTarget.localScale = new Vector3(
                originalScale.x * appearStartScale.x,
                originalScale.y * appearStartScale.y,
                originalScale.z * appearStartScale.z);
        }
    }

    private void OnAppearAnimationComplete()
    {
        isAppearing = false;
        isInteractable = true;
        CheckCursorHover();
    }

    public void PlayDisappearAnimation()
    {
        PlayDisappearAnimation(0f, null);
    }

    /// <summary>
    /// 등장 연출의 역과정으로 찌부 축소되며 페이드아웃되는 퇴장 애니메이션입니다.
    /// </summary>
    public void PlayDisappearAnimation(float _delay, Action _onComplete)
    {
        KillTweens();
        isInteractable = false;
        isAppearing = false;
        isHovered = false;

        if (null == motionTarget)
        {
            if (null != CanvasGroup) CanvasGroup.alpha = 0f;
            if (null != _onComplete) _onComplete.Invoke();
            return;
        }

        Vector3 _squashTargetScale = new Vector3(
            originalScale.x * appearStartScale.x,
            originalScale.y * appearStartScale.y,
            originalScale.z * appearStartScale.z);

        Sequence _disappearSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (_delay > 0f)
        {
            _disappearSeq.AppendInterval(_delay);
        }

        _disappearSeq.Append(motionTarget.DOScale(_squashTargetScale, disappearDuration).SetEase(disappearEase));
        if (null != CanvasGroup)
        {
            _disappearSeq.Join(CanvasGroup.DOFade(0f, disappearDuration * 0.8f).SetEase(Ease.InQuad));
        }

        _disappearSeq.OnComplete(() =>
        {
            if (null != motionTarget)
                motionTarget.localScale = Vector3.zero;

            if (null != CanvasGroup)
                CanvasGroup.alpha = 0f;

            if (null != _onComplete)
                _onComplete.Invoke();
        });
    }

    /// <summary>
    /// 마우스 커서가 이미 버튼 영역에 놓여져 있는지 수동 검사하여 호버 애니메이션을 즉시 트리거합니다.
    /// </summary>
    public void CheckCursorHover()
    {
        if (false == isInteractable || false == gameObject.activeInHierarchy || true == isAppearing)
            return;

        Vector2 _mousePos = Vector2.zero;
        if (null != Mouse.current)
        {
            _mousePos = Mouse.current.position.ReadValue();
        }

        RectTransform _hitRect = null != raycastImage ? raycastImage.rectTransform : RectTransform;
        if (null == _hitRect) return;

        if (null == cachedCanvas)
            cachedCanvas = GetComponentInParent<Canvas>();

        Camera _cam = (null != cachedCanvas && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? cachedCanvas.worldCamera
            : null;

        bool _contains = RectTransformUtility.RectangleContainsScreenPoint(_hitRect, _mousePos, _cam);

        if (true == _contains)
        {
            if (false == isHovered)
            {
                isHovered = true;
                PlayHoverAnimation();
            }
        }
        else
        {
            if (true == isHovered)
            {
                isHovered = false;
                PlayUnhoverAnimation();
            }
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        if (false == isInteractable || true == isAppearing) return;

        PlayHoverAnimation();
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (false == isInteractable || true == isAppearing) return;

        PlayUnhoverAnimation();
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable || true == isAppearing) return;

        PlayClickAnimation();

        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }
    }

    private void PlayHoverAnimation()
    {
        PlaySquashBoingMotion();
        TweenShadowColor(hoverShadowColor, shadowTweenDuration, shadowEase);
    }

    private void PlayUnhoverAnimation()
    {
        if (null != motionTarget)
        {
            motionTarget.DOKill();
            motionTarget.DOScale(originalScale, 0.1f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        TweenShadowColor(unhoverShadowColor, shadowTweenDuration, shadowEase);
    }

    private void PlayClickAnimation()
    {
        PlaySquashBoingMotion();
        TweenShadowColor(clickShadowColor, shadowTweenDuration * 0.5f, Ease.OutQuad);
    }

    /// <summary>
    /// Y 스케일이 살짝 찌부되었다가 뽀잉하며 1로 원복되는 탄성 모션을 재생합니다.
    /// </summary>
    private void PlaySquashBoingMotion()
    {
        if (null == motionTarget) return;

        motionTarget.DOKill();

        float _pressTime = squashDuration * squashPressRatio;
        float _bounceTime = squashDuration * (1f - squashPressRatio);

        Sequence _squashSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _squashSeq.Append(motionTarget.DOScaleY(originalScale.y * squashYScale, _pressTime).SetEase(squashPressEase));
        _squashSeq.Append(motionTarget.DOScaleY(originalScale.y, _bounceTime).SetEase(squashBounceEase));
    }

    private void TweenShadowColor(Color _targetColor, float _duration, Ease _ease)
    {
        if (null == uiEffect) return;

        DOTween.Kill(uiEffect);
        DOTween.To(getShadowColorDelegate, setShadowColorDelegate, _targetColor, _duration)
            .SetEase(_ease)
            .SetUpdate(true)
            .SetTarget(uiEffect);
    }

    private Color GetShadowColor()
    {
        return null != uiEffect ? uiEffect.shadowColor : Color.black;
    }

    private void SetShadowColor(Color _color)
    {
        if (null != uiEffect)
        {
            uiEffect.shadowColor = _color;
        }
    }

    private void KillTweens()
    {
        if (null != motionTarget) motionTarget.DOKill();
        if (null != uiEffect) DOTween.Kill(uiEffect);
    }
}
