using System;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core.Easing;

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

    // 본편 성장 곡선(AnimateSaplingVE). 웨이브 클리어 역성장이 같은 곡선을 거꾸로 돌린다.
    private const float GrowDuration = 1.0f;
    private const float GrowAmplitude = 1.7f;
    private const float GrowPeriod = 0.3f;

    // [로그라이크] 웨이브 클리어로 사라지기 전 크기. RestoreScaleForRun이 되돌린다.
    private Vector3 scaleBeforeVanish = Vector3.one;
    private bool bVanishedForRun;

    // [로그라이크] 역성장은 성장 곡선의 앞 0.6초만 거꾸로 돈다. 그 뒤는 3% 미만으로 잔잔히 떨리는 꼬리라
    // 거꾸로 돌리면 한참 멈춰 있다가 움직이는 것처럼 보인다. 묘목 단계가 전체 시간에서 차지하는 비율(본편은 두 단계 모두 1초).
    private const float ReverseGrowSpan = 0.6f;
    private const float RevertSaplingRatio = 0.5f;

    // 잘라 낸 꼬리 지점의 진행값. 역성장이 0에서 시작하도록 빼 준다(그대로 두면 첫 프레임에 1.6%쯤 튄다).
    private static readonly float ReverseGrowStartOffset = 1f - EaseManager.Evaluate(Ease.OutElastic, null, ReverseGrowSpan, GrowDuration, GrowAmplitude, GrowPeriod);
    private static readonly EaseFunction ReverseGrowEase = EvaluateReverseGrow;

    // [로그라이크] 나무가 다 줄어든 순간(묘목 그림으로 바뀔 때) 부를 콜백과 이어지는 묘목 단계 길이.
    private Action revertShrunkCallback;
    private float revertSaplingSeconds;
    private TweenCallback onRevertTreeShrunk;

    public void Initialize(Transform _saplingTransform)
    {
        saplingTransform = _saplingTransform;
        startScaleVector = new Vector3(startScale.x, startScale.y, 1f);
        zeroScaleVector = new Vector3(0f, 0f, 1f);
        targetScaleVector = new Vector3(targetScale.x, targetScale.y, 1f);
        onRevertTreeShrunk = OnRevertTreeShrunk;
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
        scaleTween = saplingTransform.DOScale(targetScaleVector, GrowDuration)
            .SetEase(Ease.OutElastic, GrowAmplitude, GrowPeriod);

        if (bScalePeakFired) return;

        // OutElastic은 목표 스케일을 넘겨 한 번 크게 부푼 뒤 수렴하므로, 그 첫 정점이 곧 스케일 최대 시점이다.
        // 혹시 오버슈트가 없는 이징으로 바뀌더라도 트윈 완료 시점에 늦게라도 한 번은 발동하도록 보강한다.
        scaleTween.OnUpdate(CheckScalePeak).OnComplete(FireScalePeak);
    }

    /// <summary>
    /// [로그라이크 전용] 웨이브 클리어: 본편 성장을 거꾸로 돌린다(기획서 3-1 "필드의 나무가 묘목으로 되돌아가며 사라지는 연출").
    /// 나무가 출렁이며 부풀었다가 묘목 크기로 쏙 줄어들고(나무 성장의 역재생), 그 순간 _onShrunk가 그림을 묘목으로 바꾼다.
    /// 이어서 묘목이 같은 식으로 출렁이다 0으로 줄어 사라진다(묘목 등장의 역재생). 전체 길이는 _seconds 그대로다
    /// (웨이브 흐름의 대기 시간과 맞물려 있다). 사라진 크기는 RestoreScaleForRun이 되돌린다. 본편에서는 호출되지 않는다.
    /// </summary>
    public void AnimateReverseGrowAndVanishForRun(float _seconds, Action _onShrunk)
    {
        if (saplingTransform == null)
        {
            _onShrunk?.Invoke();
            return;
        }

        // 막 자라는 중(성장 트윈 도중)이면 지금 크기는 중간값이다. 트윈이 수렴할 크기(targetScaleVector)를 원래 크기로 친다.
        // 줄어드는 연출은 지금 크기에서 그대로 이어서 시작한다. 트윈을 죽이면 IsActive가 꺼지므로 죽이기 전에 본다.
        bool bGrowing = null != scaleTween && scaleTween.IsActive() && scaleTween.IsPlaying() && scaleTweenTarget == saplingTransform;

        KillScaleTweenOn(saplingTransform);
        scalePeakCallback = null;
        bScalePeakFired = true;

        if (false == bVanishedForRun)
        {
            scaleBeforeVanish = bGrowing ? targetScaleVector : saplingTransform.localScale;
            bVanishedForRun = true;
        }

        float duration = GetRevertDuration(_seconds);
        revertSaplingSeconds = duration * RevertSaplingRatio;
        revertShrunkCallback = _onShrunk;

        // 나무 단계: 본편에서 나무는 startScale에서 탄성으로 자라 나온다. 그 곡선을 거꾸로 따라 startScale까지 줄어든다.
        scaleTweenTarget = saplingTransform;
        scaleTween = saplingTransform.DOScale(startScaleVector, GetRevertShrinkSeconds(_seconds))
            .SetEase(ReverseGrowEase)
            .OnComplete(onRevertTreeShrunk);
    }

    private static float GetRevertDuration(float _seconds) => Mathf.Max(0.05f, _seconds);

    /// <summary>[로그라이크 전용] AnimateReverseGrowAndVanishForRun(_seconds)에서 나무가 다 줄어 묘목 그림으로 바뀌기까지의 시간(초).</summary>
    public static float GetRevertShrinkSeconds(float _seconds)
    {
        return GetRevertDuration(_seconds) * (1f - RevertSaplingRatio);
    }

    private void OnRevertTreeShrunk()
    {
        // 본편 성장 순간의 역순: 다 자란 묘목(원래 크기)이 작은 나무로 바뀌었으니, 거꾸로 작은 나무가 원래 크기의 묘목으로 바뀐다.
        Action callback = revertShrunkCallback;
        revertShrunkCallback = null;
        callback?.Invoke();

        // 묘목 단계: 본편에서 묘목은 0에서 탄성으로 튀어나온다. 그 곡선을 거꾸로 따라 0으로 줄어 사라진다.
        saplingTransform.localScale = scaleBeforeVanish;
        scaleTweenTarget = saplingTransform;
        scaleTween = saplingTransform.DOScale(zeroScaleVector, revertSaplingSeconds).SetEase(ReverseGrowEase);
    }

    // 본편 성장 곡선(OutElastic)을 시간축으로 뒤집는다. 트윈 길이가 달라도 같은 모양이 되도록 성장 1초 기준으로 계산한다.
    private static float EvaluateReverseGrow(float _time, float _duration, float _overshootOrAmplitude, float _period)
    {
        float t = _duration > 0f ? Mathf.Clamp01(_time / _duration) : 1f;
        float reversed = 1f - EaseManager.Evaluate(Ease.OutElastic, null, (1f - t) * ReverseGrowSpan, GrowDuration, GrowAmplitude, GrowPeriod);
        return reversed - ReverseGrowStartOffset * (1f - t);
    }

    /// <summary>[로그라이크 전용] AnimateReverseGrowAndVanishForRun으로 사라진 나무를 원래 크기로 되돌린다(풀로 돌아가기 전에).</summary>
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
