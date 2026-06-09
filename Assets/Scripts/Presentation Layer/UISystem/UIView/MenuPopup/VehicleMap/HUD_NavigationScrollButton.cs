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
    private float pressTime = 0.0f;
    private bool isPressed = false;
    private bool isHovered = false;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(Action<bool> _onPressStateChanged)
    {
        onPressStateChangedCallback = _onPressStateChanged;
        onDisappearTweenCompleteCallback = OnDisappearTweenComplete;

        if (null == buttonImage)
            buttonImage = GetComponent<Image>();
    }

    public void PlayDisappearAnimation(TweenCallback _onComplete)
    {
        externalOnCompleteCallback = _onComplete;
        transform.DOScale(Vector3.zero, disappearDuration)
                 .SetEase(disappearEase)
                 .OnComplete(onDisappearTweenCompleteCallback);
    }

    public void PlayAppearAnimation()
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, appearDuration).SetEase(appearEase);
    }

    public void ResetAnimation()
    {
        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;

        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);

        isPressed = false;
        isHovered = false;
        pressTime = 0.0f;
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
            onPressStateChangedCallback?.Invoke(false);
        }

        PlayColorTween(normalColor);
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        isPressed = true;
        pressTime = 0.0f;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = clickColor;

        onPressStateChangedCallback?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        if (true == isPressed)
        {
            isPressed = false;
            onPressStateChangedCallback?.Invoke(false);

            Color _targetColor = true == isHovered ? hoverColor : normalColor;
            PlayColorTween(_targetColor);
        }
    }


    // 내부 로직

    private void OnDisappearTweenComplete()
    {
        gameObject.SetActive(false);
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

    private void Update()
    {
        if (true == isPressed)
        {
            pressTime += Time.deltaTime;
            float _ratio = Mathf.Clamp01(pressTime / maxPressDuration);

            if (null != buttonImage)
            {
                buttonImage.color = Color.Lerp(clickColor, maxPressColor, _ratio);
            }
        }
    }

    private void OnDisable()
    {
        isPressed = false;
        isHovered = false;
        pressTime = 0.0f;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;
    }
}
