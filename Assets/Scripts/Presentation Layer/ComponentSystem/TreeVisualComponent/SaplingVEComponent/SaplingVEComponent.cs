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

    // 마지막으로 건 스케일 트윈과 그 대상. KillScaleTweenOn 참조.
    private Tween scaleTween;
    private Transform scaleTweenTarget;

    // [로그라이크] 웨이브 클리어로 사라지기 전 크기. RestoreScaleForRun이 되돌린다.
    private Vector3 scaleBeforeVanish = Vector3.one;
    private bool bVanishedForRun;

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

        KillScaleTweenOn(saplingTransform);

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
        scaleTweenTarget = saplingTransform;
        scaleTween = saplingTransform.DOScale(targetScaleVector, 1.0f)
            .SetEase(Ease.OutElastic, 1.7f, 0.3f);

        if (bScalePeakFired) return;

        // OutElastic은 목표 스케일을 넘겨 한 번 크게 부푼 뒤 수렴하므로, 그 첫 정점이 곧 스케일 최대 시점이다.
        // 혹시 오버슈트가 없는 이징으로 바뀌더라도 트윈 완료 시점에 늦게라도 한 번은 발동하도록 보강한다.
        scaleTween.OnUpdate(CheckScalePeak).OnComplete(FireScalePeak);
    }

    /// <summary>
    /// [로그라이크 전용] 웨이브 클리어: 묘목으로 돌아간 나무가 본편 묘목 등장처럼 톡 튀었다가 작아지며 사라진다
    /// (기획서 3-1 "필드의 나무가 묘목으로 되돌아가며 사라지는 연출"). 사라진 크기는 RestoreScaleForRun이 되돌린다.
    /// 본편에서는 호출되지 않는다.
    /// </summary>
    public void AnimateRevertAndVanishForRun(float _seconds)
    {
        if (saplingTransform == null) return;

        // 막 자라는 중(성장 트윈 도중)이면 지금 크기는 중간값이다. 트윈이 수렴할 크기(targetScaleVector)를 원래 크기로 친다.
        // 트윈을 죽이면 IsActive가 꺼지므로 죽이기 전에 본다.
        bool bGrowing = null != scaleTween && scaleTween.IsActive() && scaleTween.IsPlaying() && scaleTweenTarget == saplingTransform;

        KillScaleTweenOn(saplingTransform);
        scalePeakCallback = null;
        bScalePeakFired = true;

        if (false == bVanishedForRun)
        {
            scaleBeforeVanish = bGrowing ? targetScaleVector : saplingTransform.localScale;
            bVanishedForRun = true;
        }

        float duration = Mathf.Max(0.05f, _seconds);
        float pop = duration * 0.45f;

        // 묘목 그림으로 바뀌는 순간 작게 튀어 오른 뒤(묘목 등장과 같은 탄성) 0으로 줄어든다.
        saplingTransform.localScale = scaleBeforeVanish * 0.6f;
        Sequence seq = DOTween.Sequence();
        seq.Append(saplingTransform.DOScale(scaleBeforeVanish, pop).SetEase(Ease.OutBack, 2.5f));
        seq.Append(saplingTransform.DOScale(zeroScaleVector, duration - pop).SetEase(Ease.InBack, 1.5f));

        scaleTweenTarget = saplingTransform;
        scaleTween = seq;
    }

    /// <summary>[로그라이크 전용] AnimateRevertAndVanishForRun으로 사라진 나무를 원래 크기로 되돌린다(풀로 돌아가기 전에).</summary>
    public void RestoreScaleForRun()
    {
        if (false == bVanishedForRun || saplingTransform == null) return;

        KillScaleTweenOn(saplingTransform);
        saplingTransform.localScale = scaleBeforeVanish;
        bVanishedForRun = false;
    }

    /// <summary>
    /// _target.DOKill()과 같은 일을 DOTween 활성 트윈 전체 탐색 없이 한다. 나무 transform에 트윈을 거는 곳은
    /// 이 컴포넌트뿐이고 새 트윈을 걸기 전에 항상 이전 것을 먼저 죽이므로, 살아 있을 수 있는 건 마지막 트윈 하나다.
    /// 마지막 트윈을 다른 대상에 걸었던 경우(Initialize가 대상을 바꾼 경우)에만 예전처럼 타깃 기준으로 죽인다.
    /// </summary>
    public void KillScaleTweenOn(Transform _target)
    {
        if (_target == null) return;

        if (scaleTweenTarget != null && scaleTweenTarget != _target)
        {
            _target.DOKill();
            return;
        }

        TreeVisualComponent.KillLastTween(scaleTween, _target, false); // 예전: _target.DOKill()
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
