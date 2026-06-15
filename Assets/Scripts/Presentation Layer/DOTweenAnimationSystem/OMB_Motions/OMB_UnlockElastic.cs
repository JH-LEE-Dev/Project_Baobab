using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UnlockElastic : ObjectMotionBase
    {
        // //외부 의존성
        [Header("Squash Settings")]
        [SerializeField] private float squashXScaleMultiplier = 0.1f;
        [SerializeField] private float squashYScaleMultiplier = 1.25f;
        [SerializeField] private float squashDurationRatio = 0.25f;
        [SerializeField] private Ease squashEase = Ease.InQuad;

        [Header("Elastic Settings")]
        [SerializeField] private Ease elasticEase = Ease.OutElastic;
        [SerializeField] private float elasticAmplitude = 1.0f;
        [SerializeField] private float elasticPeriod = 0.3f;


        // //내부 의존성
        // (필요 시 추가)


        // 퍼블릭 초기화 및 제어 메서드
        // (부모 클래스의 Play / PlayBackward 활용)


        // 유니티 이벤트 함수 및 오버라이딩

        protected override void OnTransform(Sequence _seq, Transform _trans, Ease _currPublicEase)
        {
            if (null == _seq || null == _trans)
            {
                return;
            }

            TargetInitialState _state = new TargetInitialState
            {
                transform = _trans,
                localPosition = _trans.localPosition,
                localRotation = _trans.localEulerAngles,
                localScale = _trans.localScale
            };
            stateCache.Add(_state);

            float _totalDuration = forwardDuration;
            float _squashDuration = _totalDuration * squashDurationRatio;
            float _elasticDuration = _totalDuration * (1f - squashDurationRatio);

            Vector3 _initialScale = _state.localScale;
            Vector3 _squashedScale = new Vector3(_initialScale.x * squashXScaleMultiplier, _initialScale.y * squashYScaleMultiplier, _initialScale.z);

            // 1. 가로로 얇아지고 세로로 늘어나는 Squash 단계
            _seq.Append(_trans.DOScale(_squashedScale, _squashDuration).SetEase(squashEase));

            // 2. 원래 크기로 띠용~ 돌아가는 Elastic 단계
            _seq.Append(_trans.DOScale(_initialScale, _elasticDuration).SetEase(elasticEase, elasticAmplitude, elasticPeriod));
        }

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect, Ease _currPublicEase)
        {
            if (null == _seq || null == _rect)
            {
                return;
            }

            TargetInitialState _state = new TargetInitialState
            {
                rectTransform = _rect,
                anchoredPosition = _rect.anchoredPosition,
                localRotation = _rect.localEulerAngles,
                localScale = _rect.localScale
            };
            stateCache.Add(_state);

            float _totalDuration = forwardDuration;
            float _squashDuration = _totalDuration * squashDurationRatio;
            float _elasticDuration = _totalDuration * (1f - squashDurationRatio);

            Vector3 _initialScale = _state.localScale;
            Vector3 _squashedScale = new Vector3(_initialScale.x * squashXScaleMultiplier, _initialScale.y * squashYScaleMultiplier, _initialScale.z);

            // 1. 가로로 얇아지고 세로로 늘어나는 Squash 단계
            _seq.Append(_rect.DOScale(_squashedScale, _squashDuration).SetEase(squashEase));

            // 2. 원래 크기로 띠용~ 돌아가는 Elastic 단계
            _seq.Append(_rect.DOScale(_initialScale, _elasticDuration).SetEase(elasticEase, elasticAmplitude, elasticPeriod));
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);

            if (null != currentTween)
            {
                currentTween.OnKill(RestoreMotionState);
            }
        }

        protected override void InternalOnComplete()
        {
            RestoreMotionState();
            base.InternalOnComplete();
        }

        private void RestoreMotionState()
        {
            RestoreCachedState(true);
        }

        protected override void RestoreAfterValidate()
        {
            RestoreMotionState();
        }
    }
}
