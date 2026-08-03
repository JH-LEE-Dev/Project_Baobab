using System;
using UnityEngine;
using DG.Tweening;

public class SaplingVEComponent : MonoBehaviour
{
    private Transform saplingTransform;

    [SerializeField] private Vector2 startScale;
    [SerializeField] private Vector2 targetScale;

    private Vector3 startScaleVector;
    private Vector3 zeroScaleVector;
    private Vector3 targetScaleVector;

    // 스케일이 최대가 되는 순간(OutElastic의 첫 정점)에 한 번만 호출되는 콜백
    private Action scalePeakCallback;
    private float prevScaleX;
    private bool bScalePeakFired = true;

    public void Initialize(Transform _saplingTransform)
    {
        saplingTransform = _saplingTransform;
        startScaleVector = new Vector3(startScale.x, startScale.y, 1f);
        zeroScaleVector = new Vector3(0f, 0f, 1f);
        targetScaleVector = new Vector3(targetScale.x, targetScale.y, 1f);
    }

    public void AnimateSaplingVE(bool _bIsSapling, Action _onScalePeak = null)
    {
        if (saplingTransform == null) return;

        saplingTransform.DOKill();

        // 이전 트윈이 중간에 죽었을 수 있으므로 콜백 상태는 매번 새로 세팅한다.
        scalePeakCallback = _onScalePeak;
        bScalePeakFired = _onScalePeak == null;

        if (_bIsSapling == false)
            saplingTransform.localScale = startScaleVector;
        else
            saplingTransform.localScale = zeroScaleVector;

        prevScaleX = saplingTransform.localScale.x;

        // 더욱 극적인 스프링 효과를 위해 Amplitude(진동 폭)를 크게 높이고 Period(진동 주기)를 조절
        // 1.0초 동안 매우 탄력 있게 튕기며 목표 스케일로 수렴
        Tween scaleTween = saplingTransform.DOScale(targetScaleVector, 1.0f)
            .SetEase(Ease.OutElastic, 1.7f, 0.3f);

        if (bScalePeakFired) return;

        // OutElastic은 목표 스케일을 넘겨 한 번 크게 부푼 뒤 수렴하므로, 그 첫 정점이 곧 스케일 최대 시점이다.
        // 혹시 오버슈트가 없는 이징으로 바뀌더라도 트윈 완료 시점에 늦게라도 한 번은 발동하도록 보강한다.
        scaleTween.OnUpdate(CheckScalePeak).OnComplete(FireScalePeak);
    }

    private void CheckScalePeak()
    {
        if (bScalePeakFired) return;

        float currentScaleX = saplingTransform.localScale.x;

        if (currentScaleX < prevScaleX)
        {
            FireScalePeak();
            return;
        }

        prevScaleX = currentScaleX;
    }

    private void FireScalePeak()
    {
        if (bScalePeakFired) return;

        bScalePeakFired = true;

        Action callback = scalePeakCallback;
        scalePeakCallback = null;
        callback?.Invoke();
    }
}
