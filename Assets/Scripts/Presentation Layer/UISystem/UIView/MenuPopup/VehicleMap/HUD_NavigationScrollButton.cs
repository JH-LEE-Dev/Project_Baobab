using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class HUD_NavigationScrollButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    // 외부 의존성
    [SerializeField] private Image buttonImage;

    [Header("Disappear Config")]
    [SerializeField] private float disappearDuration = 0.2f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    [Header("Appear Config")]
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private Ease appearEase = Ease.OutBack;

    // 내부 의존성
    private Action<bool> onPressStateChangedCallback;
    private TweenCallback onDisappearTweenCompleteCallback;
    private TweenCallback externalOnCompleteCallback;
    private bool isPressed = false;

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

    private void OnDisappearTweenComplete()
    {
        gameObject.SetActive(false);
        externalOnCompleteCallback?.Invoke();
        externalOnCompleteCallback = null;
    }

    public void PlayAppearAnimation()
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, appearDuration).SetEase(appearEase);
    }

    public void ResetAnimation()
    {
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);
    }

    // Event System 구현부
    public void OnPointerDown(PointerEventData _eventData)
    {
        isPressed = true;
        onPressStateChangedCallback?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        if (true == isPressed)
        {
            isPressed = false;
            onPressStateChangedCallback?.Invoke(false);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (true == isPressed)
        {
            isPressed = false;
            onPressStateChangedCallback?.Invoke(false);
        }
    }
}
