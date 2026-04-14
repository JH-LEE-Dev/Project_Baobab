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
    private Action onSwingComplete;
    private Action onReturnComplete;

    public void PlaySwing(Action _onComplete)
    {
        // 1. 초기 회전 상태 저장
        initialLocalRot = transform.localRotation;
        onSwingComplete = _onComplete;

        KillTweens();

        // 2. 휘두르기 회전 시작
        rotateTween = transform.DOLocalRotate(new Vector3(0, 0, endRotationZ), swingDuration, RotateMode.LocalAxisAdd)
            .SetEase(swingEase)
            .OnComplete(NotifySwingComplete);
    }

    public void PlayReturn(Action _onComplete)
    {
        onReturnComplete = _onComplete;

        // 3. 원래 회전으로 복귀 시작
        rotateTween = transform.DOLocalRotateQuaternion(initialLocalRot, returnDuration)
            .SetEase(returnEase)
            .OnComplete(NotifyReturnComplete);
    }

    private void NotifySwingComplete()
    {
        onSwingComplete?.Invoke();
        onSwingComplete = null;
    }

    private void NotifyReturnComplete()
    {
        onReturnComplete?.Invoke();
        onReturnComplete = null;
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
