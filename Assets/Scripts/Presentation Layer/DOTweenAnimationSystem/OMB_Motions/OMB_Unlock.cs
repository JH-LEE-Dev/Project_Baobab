using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_Unlock : ObjectMotionBase
    {
        // //외부 의존성
        [Header("Shrink & Shake Settings")]
        [SerializeField] private float shrinkScaleMultiplier = 0.7f;
        [SerializeField] private float shrinkDurationRatio = 0.8f;
        [SerializeField] private float shakeStrength = 10f;
        [SerializeField] private int shakeVibrato = 20;

        [Header("Burst Expand Settings")]
        [SerializeField] private float expandScaleMultiplier = 1.15f;
        [SerializeField] private Ease expandEase = Ease.OutBack;

        // //내부 의존성
        // (필요 시 추가)


        // 퍼블릭 초기화 및 제어 메서드
        // (ObjectMotionBase의 Play 및 PlayBackward 활용)


        // 유니티 이벤트 함수 및 오버라이딩

        protected override void OnTransform(Sequence _seq, Transform _trans, Ease _currPublicEase)
        {
            if (null == _seq || null == _trans)
                return;

            TargetInitialState _state = new TargetInitialState
            {
                transform = _trans,
                localPosition = _trans.localPosition,
                localRotation = _trans.localEulerAngles,
                localScale = _trans.localScale
            };
            stateCache.Add(_state);

            float _totalDuration = forwardDuration;
            float _shrinkDuration = _totalDuration * shrinkDurationRatio;
            float _expandDuration = _totalDuration * (1f - shrinkDurationRatio);

            Vector3 _initialScale = _state.localScale;

            // 1. 축소 및 진동
            _seq.Append(_trans.DOScale(_initialScale * shrinkScaleMultiplier, _shrinkDuration).SetEase(Ease.InOutQuad));
            _seq.Join(_trans.DOShakePosition(_shrinkDuration, _trans.right * shakeStrength, shakeVibrato, 90f, false, false));

            // 2. 쫙 확장 후 원래 크기로 복귀
            _seq.Append(_trans.DOScale(_initialScale * expandScaleMultiplier, _expandDuration * 0.4f));
            _seq.Append(_trans.DOScale(_initialScale, _expandDuration * 0.6f).SetEase(expandEase));
        }

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect, Ease _currPublicEase)
        {
            if (null == _seq || null == _rect)
                return;

            TargetInitialState _state = new TargetInitialState
            {
                rectTransform = _rect,
                anchoredPosition = _rect.anchoredPosition,
                localRotation = _rect.localEulerAngles,
                localScale = _rect.localScale
            };
            stateCache.Add(_state);

            float _totalDuration = forwardDuration;
            float _shrinkDuration = _totalDuration * shrinkDurationRatio;
            float _expandDuration = _totalDuration * (1f - shrinkDurationRatio);

            Vector3 _initialScale = _state.localScale;

            // 1. 축소 및 진동
            _seq.Append(_rect.DOScale(_initialScale * shrinkScaleMultiplier, _shrinkDuration).SetEase(Ease.InOutQuad));
            _seq.Join(_rect.DOShakeAnchorPos(_shrinkDuration, shakeStrength, shakeVibrato, 90f, false, false));

            // 2. 쫙 확장 후 원래 크기로 복귀
            _seq.Append(_rect.DOScale(_initialScale * expandScaleMultiplier, _expandDuration * 0.4f));
            _seq.Append(_rect.DOScale(_initialScale, _expandDuration * 0.6f).SetEase(expandEase));
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);

            if (null != currentTween)
                currentTween.OnKill(RestoreMotionState);
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
