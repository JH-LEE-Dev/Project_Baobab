using UnityEngine;
using DG.Tweening;
using System;

/// <summary>
/// 메인 메뉴 화면에서 로고 전용 애니메이션을 독립적으로 담당하는 스크립트입니다.
/// </summary>
public class UI_LogoAnim : MonoBehaviour
{
    [Header("Logo Target")]
    [SerializeField, Tooltip("애니메이션을 적용할 로고의 RectTransform")] 
    private RectTransform logoTransform;

    [Header("Fade Settings")]
    [SerializeField, Tooltip("로고 페이드아웃을 위한 CanvasGroup (없으면 자동 추가됨)")]
    private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float moveDistanceY = 200f; // 위로 이동할 거리
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private Ease moveEase = Ease.OutBack; // 찰진 튕김 효과 (뽀잉)

    private Vector2 initialPosition;

    private Action currentRevealComplete;
    private TweenCallback onRevealCompleteCallback;

    private bool isInitialPositionSet = false;

    public void Initialize()
    {
        if (null == onRevealCompleteCallback) onRevealCompleteCallback = OnRevealComplete;
        if (null == onFadeOutCompleteCallback) onFadeOutCompleteCallback = OnFadeOutComplete;

        if (null != logoTransform && !isInitialPositionSet)
        {
            initialPosition = logoTransform.anchoredPosition;
            isInitialPositionSet = true;
        }

        if (null == canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (null == canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnRevealComplete()
    {
        currentRevealComplete?.Invoke();
        currentRevealComplete = null;
    }

    /// <summary>
    /// 로고를 원래 시작 위치로 즉시 되돌립니다.
    /// </summary>
    public void ResetToInitialState()
    {
        if (null != logoTransform)
        {
            logoTransform.DOKill();
            if (isInitialPositionSet)
            {
                logoTransform.anchoredPosition = initialPosition;
            }
        }
    }

    /// <summary>
    /// 로고가 위로 튕겨 올라가는 모션을 실행합니다.
    /// </summary>
    /// <param name="onComplete">모션이 완전히 끝난 뒤 호출될 콜백</param>
    public void PlayRevealSequence(Action _onComplete = null)
    {
        currentRevealComplete = _onComplete;

        if (null != logoTransform)
        {
            logoTransform.DOKill();
            
            if (!isInitialPositionSet)
            {
                initialPosition = logoTransform.anchoredPosition;
                isInitialPositionSet = true;
            }
            else
            {
                logoTransform.anchoredPosition = initialPosition;
            }

            // 목표 위치 설정
            Vector2 targetPos = initialPosition + new Vector2(0f, moveDistanceY);

            // 부드럽고 찰지게 이동 후 콜백 실행
            logoTransform.DOAnchorPos(targetPos, moveDuration)
                .SetEase(moveEase)
                .OnComplete(onRevealCompleteCallback);
        }
        else
        {
            // 로고가 없다면 즉시 콜백 실행
            _onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 로고를 서서히 투명하게 만듭니다.
    /// </summary>
    private Action currentFadeOutComplete;
    private TweenCallback onFadeOutCompleteCallback;

    private void OnFadeOutComplete()
    {
        currentFadeOutComplete?.Invoke();
        currentFadeOutComplete = null;
    }

    public void PlayFadeOut(float _duration, Action _onComplete = null)
    {
        currentFadeOutComplete = _onComplete;
        if (null != canvasGroup)
        {
            canvasGroup.DOFade(0f, _duration).OnComplete(onFadeOutCompleteCallback);
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (null != logoTransform)
        {
            logoTransform.DOKill();
        }
    }
}
