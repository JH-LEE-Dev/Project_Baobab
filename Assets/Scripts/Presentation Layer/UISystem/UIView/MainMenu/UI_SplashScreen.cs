using System;
using UnityEngine;
using DG.Tweening;

[Serializable]
public struct SplashSequenceItem
{
    public CanvasGroup targetGroup;
    public float fadeInDuration;
    public Ease fadeInEase;
    public float holdDuration;
    public float fadeOutDuration;
    public Ease fadeOutEase;
}

public class UI_SplashScreen : MonoBehaviour
{
    //내부 의존성
    [SerializeField] private CanvasGroup splashBackgroundGroup; // 스플래시 스크린 전용 배경
    [SerializeField] private float backgroundFadeOutDelay = 0.5f; // 마지막 시퀀스 페이드아웃 후 대기 시간 (N초)
    [SerializeField] private float backgroundFadeOutDuration = 1f; // 배경 페이드아웃 소요 시간
    [SerializeField] private SplashSequenceItem[] sequences;
    
    private Action onSequenceComplete;
    private Action currentBeforeLastFadeOut;
    private TweenCallback onBeforeLastFadeOutCallback;
    private Sequence currentSequence;

    public void PlaySequence(Action _onComplete, Action _onBeforeLastFadeOut = null)
    {
        if (null == onBeforeLastFadeOutCallback) onBeforeLastFadeOutCallback = InvokeBeforeLastFadeOut;
        currentBeforeLastFadeOut = _onBeforeLastFadeOut;

        this.onSequenceComplete = _onComplete;
        
        if (null == this.sequences || 0 == this.sequences.Length)
        {
            InvokeBeforeLastFadeOut();
            this.OnSequenceFinished();
            return;
        }

        // 스플래시 전용 배경이 있다면 검은색(알파 1)으로 초기화 후 활성화
        if (null != this.splashBackgroundGroup)
        {
            this.splashBackgroundGroup.alpha = 1f;
            this.splashBackgroundGroup.gameObject.SetActive(true);
        }

        // 모든 타겟을 우선 투명하게 초기화 및 활성화
        for (int i = 0; i < this.sequences.Length; ++i)
        {
            if (null != this.sequences[i].targetGroup)
            {
                this.sequences[i].targetGroup.alpha = 0f;
                this.sequences[i].targetGroup.gameObject.SetActive(true);
                this.sequences[i].targetGroup.interactable = false;
                this.sequences[i].targetGroup.blocksRaycasts = false;
            }
        }

        this.currentSequence = DOTween.Sequence();

        for (int i = 0; i < this.sequences.Length; ++i)
        {
            // 구조체를 ref나 in으로 받거나 단순 복사 사용 (여기서는 구조체 복사지만 참조형인 CanvasGroup이 있으므로 괜찮음)
            SplashSequenceItem _item = this.sequences[i];
            
            if (null != _item.targetGroup)
            {
                this.currentSequence.Append(_item.targetGroup.DOFade(1f, _item.fadeInDuration).SetEase(_item.fadeInEase));
                this.currentSequence.AppendInterval(_item.holdDuration);
                
                var _fadeOutTween = _item.targetGroup.DOFade(0f, _item.fadeOutDuration).SetEase(_item.fadeOutEase);

                // 마지막 시퀀스 요소인 경우, 객체 페이드아웃 완료 후 설정한 시간(N초) 대기 후 배경 페이드 아웃
                if (this.sequences.Length - 1 == i && null != this.splashBackgroundGroup)
                {
                    this.currentSequence.Append(_fadeOutTween);
                    this.currentSequence.AppendInterval(this.backgroundFadeOutDelay);

                    if (null != currentBeforeLastFadeOut)
                    {
                        this.currentSequence.AppendCallback(onBeforeLastFadeOutCallback);
                    }
                    this.currentSequence.Append(this.splashBackgroundGroup.DOFade(0f, this.backgroundFadeOutDuration).SetEase(Ease.OutQuad));
                }
                else
                {
                    this.currentSequence.Append(_fadeOutTween);
                }
            }
        }

        this.currentSequence.OnComplete(this.OnSequenceFinished);
    }

    private void InvokeBeforeLastFadeOut()
    {
        currentBeforeLastFadeOut?.Invoke();
    }

    private void OnSequenceFinished()
    {
        // 최적화를 위해 클로저를 피하고 완료 시점에 모든 타겟을 비활성화
        if (null != this.sequences)
        {
            for (int i = 0; i < this.sequences.Length; ++i)
            {
                if (null != this.sequences[i].targetGroup)
                {
                    this.sequences[i].targetGroup.gameObject.SetActive(false);
                }
            }
        }

        if (null != this.splashBackgroundGroup)
        {
            this.splashBackgroundGroup.gameObject.SetActive(false);
        }

        if (null != this.onSequenceComplete)
        {
            this.onSequenceComplete.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (null != this.currentSequence)
        {
            this.currentSequence.Kill();
        }
    }
}
