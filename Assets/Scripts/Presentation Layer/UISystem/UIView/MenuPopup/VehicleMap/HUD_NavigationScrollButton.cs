using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Coffee.UIEffects;

public class HUD_NavigationScrollButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    // 외부 의존성
    [SerializeField] private Image buttonImage;
    [SerializeField] private Image outlineImage;

    [Header("Disappear Config")]
    [SerializeField] private float disappearDuration = 0.2f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    [Header("Appear Config")]
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private Ease appearEase = Ease.OutBack;

    [Header("Color Config")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.green;
    [SerializeField] private Color clickColor = Color.yellow;
    [SerializeField] private Color maxPressColor = Color.red;
    [SerializeField] private float colorDuration = 0.2f;
    [SerializeField] private float maxPressDuration = 1.5f;

    [Header("Outline Color Config")]
    [SerializeField] private Color outlineNormalColor = Color.white;
    [SerializeField] private Color outlineHoverColor = Color.green;
    [SerializeField] private Color outlineClickColor = Color.yellow;
    [SerializeField] private Color outlineMaxPressColor = Color.red;

    [Header("UI Effect Outline Intensity Settings")]
    [SerializeField] private UIEffect uiEffect;
    [Range(0f, 1f)] [SerializeField] private float outlineNormalIntensity = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float outlineHoverIntensity = 1f;
    [Range(0f, 1f)] [SerializeField] private float outlineClickIntensity = 1f;
    [Range(0f, 1f)] [SerializeField] private float outlineMaxPressIntensity = 1f;

    // 내부 의존성
    private Action<bool> onPressStateChangedCallback;
    private TweenCallback onDisappearTweenCompleteCallback;
    private TweenCallback externalOnCompleteCallback;
    private Tweener colorTween;
    private Tweener outlineColorTween;
    private Tweener shadowAlphaTween;
    private Tween transitionTween;
    private bool isPressed = false;
    private bool isHovered = false;
    [SerializeField] private CanvasGroup canvasGroup;


    // 퍼블릭 초기화 및 제어 메서드

    private CanvasGroup GetCanvasGroup()
    {
        if (null == canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        return canvasGroup;
    }

    public void SetVisibility(bool _visible)
    {
        CanvasGroup _cg = GetCanvasGroup();
        _cg.alpha = true == _visible ? 1f : 0f;
        _cg.blocksRaycasts = _visible;
        _cg.interactable = _visible;

        if (false == _visible)
            CleanupOnHide();
    }

    public void Initialize(Action<bool> _onPressStateChanged)
    {
        onPressStateChangedCallback = _onPressStateChanged;
        onDisappearTweenCompleteCallback = OnDisappearTweenComplete;

        if (null == buttonImage)
            buttonImage = GetComponent<Image>();
    }

    public void PlayDisappearAnimation(TweenCallback _onComplete)
    {
        if (null != transitionTween && transitionTween.IsActive())
            transitionTween.Kill();

        externalOnCompleteCallback = _onComplete;
        transitionTween = transform.DOScale(Vector3.zero, disappearDuration)
                                   .SetEase(disappearEase)
                                   .OnComplete(onDisappearTweenCompleteCallback);
    }

    public void PlayAppearAnimation()
    {
        if (null != transitionTween && transitionTween.IsActive())
            transitionTween.Kill();

        SetVisibility(true);
        transform.localScale = Vector3.zero;
        transitionTween = transform.DOScale(Vector3.one, appearDuration).SetEase(appearEase);
    }

    public void ResetAnimation()
    {
        if (null != transitionTween && true == transitionTween.IsActive())
            transitionTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;

        if (null != outlineImage)
            outlineImage.color = outlineNormalColor;

        if (null != uiEffect)
            uiEffect.shadowColorAlpha = outlineNormalIntensity;

        transform.localScale = Vector3.zero;
        SetVisibility(true);

        isPressed = false;
        isHovered = false;
    }


    // Event System 구현부

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;

        if (false == isPressed)
        {
            PlayColorTween(hoverColor);
            PlayOutlineColorTween(outlineHoverColor);
            PlayShadowAlphaTween(outlineHoverIntensity);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;

        if (true == isPressed)
        {
            isPressed = false;
            if (false == IsTransitioning())
                onPressStateChangedCallback?.Invoke(false);
        }

        PlayColorTween(normalColor);
        PlayOutlineColorTween(outlineNormalColor);
        PlayShadowAlphaTween(outlineNormalIntensity);
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        if (null != buttonImage)
            buttonImage.color = clickColor;

        if (null != outlineImage)
            outlineImage.color = outlineClickColor;

        if (null != uiEffect)
            uiEffect.shadowColorAlpha = outlineClickIntensity;

        if (true == IsTransitioning())
            return;

        isPressed = true;
        
        if (null != buttonImage)
            colorTween = buttonImage.DOColor(maxPressColor, maxPressDuration).SetEase(Ease.Linear);

        if (null != outlineImage)
            outlineColorTween = outlineImage.DOColor(outlineMaxPressColor, maxPressDuration).SetEase(Ease.Linear);

        if (null != uiEffect)
            shadowAlphaTween = DOTween.To(GetShadowColorAlpha, SetShadowColorAlpha, outlineMaxPressIntensity, maxPressDuration).SetEase(Ease.Linear);

        onPressStateChangedCallback?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        if (true == isPressed)
        {
            isPressed = false;
            if (false == IsTransitioning())
                onPressStateChangedCallback?.Invoke(false);
        }

        Color _targetColor = true == isHovered ? hoverColor : normalColor;
        Color _targetOutlineColor = true == isHovered ? outlineHoverColor : outlineNormalColor;
        float _targetIntensity = true == isHovered ? outlineHoverIntensity : outlineNormalIntensity;

        PlayColorTween(_targetColor);
        PlayOutlineColorTween(_targetOutlineColor);
        PlayShadowAlphaTween(_targetIntensity);
    }


    // 내부 로직

    private bool IsTransitioning()
    {
        if (null != transitionTween && true == transitionTween.IsActive())
            return true;

        return false;
    }

    private void OnDisappearTweenComplete()
    {
        SetVisibility(false);
        externalOnCompleteCallback?.Invoke();
        externalOnCompleteCallback = null;
    }

    private void PlayColorTween(Color _targetColor)
    {
        if (null == buttonImage)
            return;

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        colorTween = buttonImage.DOColor(_targetColor, colorDuration).SetEase(Ease.Linear);
    }

    private void PlayOutlineColorTween(Color _targetColor)
    {
        if (null == outlineImage)
            return;

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        outlineColorTween = outlineImage.DOColor(_targetColor, colorDuration).SetEase(Ease.Linear);
    }

    private float GetShadowColorAlpha()
    {
        if (null != uiEffect)
            return uiEffect.shadowColorAlpha;
        return 0f;
    }

    private void SetShadowColorAlpha(float _value)
    {
        if (null != uiEffect)
            uiEffect.shadowColorAlpha = _value;
    }

    private void PlayShadowAlphaTween(float _targetIntensity)
    {
        if (null == uiEffect)
            return;

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        shadowAlphaTween = DOTween.To(GetShadowColorAlpha, SetShadowColorAlpha, _targetIntensity, colorDuration).SetEase(Ease.Linear);
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        CleanupOnHide();
    }

    private void CleanupOnHide()
    {
        isPressed = false;
        isHovered = false;

        if (null != transitionTween && true == transitionTween.IsActive())
            transitionTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;

        if (null != outlineImage)
            outlineImage.color = outlineNormalColor;

        if (null != uiEffect)
            uiEffect.shadowColorAlpha = outlineNormalIntensity;
    }

    private void OnDestroy()
    {
        if (null != transitionTween && true == transitionTween.IsActive())
            transitionTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();
    }
}
