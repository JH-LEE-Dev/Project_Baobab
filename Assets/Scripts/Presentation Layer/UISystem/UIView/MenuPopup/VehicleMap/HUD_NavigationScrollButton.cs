using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class HUD_NavigationScrollButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    // 외부 의존성
    [SerializeField] private Image buttonImage;

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

    // 내부 의존성
    private Action<bool> onPressStateChangedCallback;
    private TweenCallback onDisappearTweenCompleteCallback;
    private TweenCallback externalOnCompleteCallback;
    private Tweener colorTween;
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
        if (null != transitionTween && transitionTween.IsActive())
            transitionTween.Kill();

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;

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
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = clickColor;

        if (true == IsTransitioning())
            return;

        isPressed = true;
        
        if (null != buttonImage)
            colorTween = buttonImage.DOColor(maxPressColor, maxPressDuration).SetEase(Ease.Linear);

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
        PlayColorTween(_targetColor);
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

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        colorTween = buttonImage.DOColor(_targetColor, colorDuration).SetEase(Ease.Linear);
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

        if (null != buttonImage)
            buttonImage.color = normalColor;
    }

    private void OnDestroy()
    {
        if (null != transitionTween && true == transitionTween.IsActive())
            transitionTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();
    }
}
