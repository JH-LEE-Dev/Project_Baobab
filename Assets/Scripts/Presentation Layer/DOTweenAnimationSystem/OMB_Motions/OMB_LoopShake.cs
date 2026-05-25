using UnityEngine;
using DG.Tweening;

using PresentationLayer.DOTweenAnimationSystem;

public class OMB_LoopShake : ObjectMotionBase
{
    [Header("Tremble Settings")]
    [Tooltip("위치 떨림의 강도입니다. 0이면 위치는 떨리지 않습니다.")]
    [SerializeField] private float positionStrength = 5f;

    [Tooltip("회전 떨림의 강도입니다 (Z축 기준). 0이면 회전하지 않습니다.")]
    [SerializeField] private float rotationStrength = 2f;

    [Tooltip("떨림의 진동 수입니다. 높을수록 더 빠르고 잘게 떨립니다.")]
    [SerializeField] private int vibrato = 20;

    [Tooltip("떨림의 무작위성(0~180)입니다. 90이 기본값입니다.")]
    [SerializeField] private float randomness = 90f;

    protected override void OnRectTransform(Sequence _seq, RectTransform _rect, Ease _currPublicEase)
    {
        if (null == _seq || null == _rect)
            return;

        // 1. 초기 상태 캐싱 로직 추가
        TargetInitialState _state = new TargetInitialState
        {
            rectTransform = _rect,
            anchoredPosition = _rect.anchoredPosition,
            localRotation = _rect.localEulerAngles,
            localScale = _rect.localScale
        };

        stateCache.Add(_state);

        // 2. 애니메이션 적용
        if (positionStrength > 0)
        {
            _seq.Join(_rect.DOShakeAnchorPos(forwardDuration, positionStrength, vibrato, randomness, false, false)
                          .SetEase(_currPublicEase));
        }

        if (rotationStrength > 0)
        {
            _seq.Join(_rect.DOShakeRotation(forwardDuration, new Vector3(0, 0, rotationStrength), vibrato, randomness, false)
                          .SetEase(_currPublicEase));
        }
    }

    protected override void OnTransform(Sequence _seq, Transform _trans, Ease _currPublicEase)
    {
        if (null == _seq || null == _trans)
            return;

        // 1. 초기 상태 캐싱 로직 추가
        TargetInitialState _state = new TargetInitialState
        {
            transform = _trans,
            localPosition = _trans.localPosition,
            localRotation = _trans.localEulerAngles,
            localScale = _trans.localScale
        };

        stateCache.Add(_state);

        // 2. 애니메이션 적용
        if (positionStrength > 0)
        {
            _seq.Join(_trans.DOShakePosition(forwardDuration, positionStrength, vibrato, randomness, false, false)
                            .SetEase(_currPublicEase));
        }

        if (rotationStrength > 0)
        {
            _seq.Join(_trans.DOShakeRotation(forwardDuration, new Vector3(0, 0, rotationStrength), vibrato, randomness, false)
                            .SetEase(_currPublicEase));
        }
    }

    protected override void ApplyTweenSettings(Tween _tween)
    {
        base.ApplyTweenSettings(_tween);

        // 내구도가 바닥일 때 멈추지 않고 계속 부들부들 떨리도록 무한 루프 적용
        if (currentTween != null)
            currentTween.SetLoops(-1, LoopType.Restart);
    }

    protected override void ApplyBackwardTweenSettings(Tween _tween)
    {
        base.ApplyBackwardTweenSettings(_tween);

        if (currentTween != null)
            currentTween.SetLoops(-1, LoopType.Restart);
    }
}
