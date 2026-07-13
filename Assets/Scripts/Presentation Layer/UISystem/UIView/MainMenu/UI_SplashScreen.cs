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
    [SerializeField] private SplashSequenceItem[] sequences;
    
    private Action onSequenceComplete;
    private Sequence currentSequence;

    public void PlaySequence(Action _onComplete)
    {
        this.onSequenceComplete = _onComplete;
        
        if (null == this.sequences || 0 == this.sequences.Length)
        {
            this.OnSequenceFinished();
            return;
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
            SplashSequenceItem item = this.sequences[i];
            
            if (null != item.targetGroup)
            {
                this.currentSequence.Append(item.targetGroup.DOFade(1f, item.fadeInDuration).SetEase(item.fadeInEase));
                this.currentSequence.AppendInterval(item.holdDuration);
                this.currentSequence.Append(item.targetGroup.DOFade(0f, item.fadeOutDuration).SetEase(item.fadeOutEase));
            }
        }

        this.currentSequence.OnComplete(this.OnSequenceFinished);
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
