using DG.Tweening;
using UnityEngine;
using System;

public class RifleAnimation : MonoBehaviour
{
    // 외부 의존성
    [Header("Recoil Settings")]
    [SerializeField] private float recoilDistance = 0.1f;
    [SerializeField] private float recoilDuration = 0.05f;
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease recoilEase = Ease.OutQuad;
    [SerializeField] private Ease returnEase = Ease.InQuad;

    // 내부 의존성
    private Sequence recoilSequence;
    private Vector3 initialLocalPos;
    private Action onCompleteCallback;

    public void PlayRecoil(Action _onComplete)
    {
        // 초기 위치 저장
        initialLocalPos = transform.localPosition;
        onCompleteCallback = _onComplete;

        KillTweens();

        // 시퀀스를 사용하여 람다 없이 체이닝
        recoilSequence = DOTween.Sequence();
        
        // 1. 뒤로 밀림
        recoilSequence.Append(transform.DOLocalMoveX(initialLocalPos.x - recoilDistance, recoilDuration)
            .SetEase(recoilEase));
            
        // 2. 원래 위치로 복귀
        recoilSequence.Append(transform.DOLocalMoveX(initialLocalPos.x, returnDuration)
            .SetEase(returnEase));

        // 3. 완료 시 기명 메서드 호출
        recoilSequence.OnComplete(NotifyComplete);
    }

    private void NotifyComplete()
    {
        onCompleteCallback?.Invoke();
        onCompleteCallback = null;
    }

    public void KillTweens()
    {
        if (recoilSequence != null && recoilSequence.IsActive())
        {
            recoilSequence.Kill();
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
