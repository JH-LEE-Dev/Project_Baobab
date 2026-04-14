using DG.Tweening;
using UnityEngine;
using System;

public class AxeAnimation : MonoBehaviour
{
    // 외부 의존성
    [Header("Swing Settings")]
    [SerializeField] private float swingDuration = 0.05f;
    [SerializeField] private float endRotationZ = -90f;
    [SerializeField] private Ease swingEase = Ease.InCubic;

    [Header("Return Settings")]
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease returnEase = Ease.OutQuad;

    // 내부 의존성
    private Tween rotateTween;
    private Quaternion initialLocalRot;
    private Action onCompleteCallback;

    public void PlaySwing(Action _onComplete)
    {
        // 1. 초기 회전 상태 저장
        initialLocalRot = transform.localRotation;
        onCompleteCallback = _onComplete;

        KillTweens();

        // 2. 휘두르기 회전 시작 (람다 대신 기명 메서드 호출)
        rotateTween = transform.DOLocalRotate(new Vector3(0, 0, endRotationZ), swingDuration, RotateMode.LocalAxisAdd)
            .SetEase(swingEase)
            .OnComplete(NotifyComplete);
    }

    public void PlayReturn(Action _onComplete)
    {
        onCompleteCallback = _onComplete;

        // 3. 원래 회전으로 복귀 시작 (람다 대신 기명 메서드 호출)
        rotateTween = transform.DOLocalRotateQuaternion(initialLocalRot, returnDuration)
            .SetEase(returnEase)
            .OnComplete(NotifyComplete);
    }

    private void NotifyComplete()
    {
        onCompleteCallback?.Invoke();
        onCompleteCallback = null;
    }

    public void KillTweens()
    {
        if (null != rotateTween && rotateTween.IsActive()) rotateTween.Kill();
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
