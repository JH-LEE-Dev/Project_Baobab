using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UINonClickShake : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Position Settings")]
            public float startOffsetX = 6f;
            public float offsetDamping = 0.58f;
            public int shakeCount = 6;
            [Range(0f, 1f)] public float positionTimeRatio = 0.78f;
            public Ease positionEase = Ease.OutSine;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

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
            _seq.Join(BuildRectPositionTween(_rect, _state.anchoredPosition));
        }

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
            _seq.Join(BuildTransformPositionTween(_trans, _state.localPosition));
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
            RestoreCachedState();
        }

        protected override void RestoreAfterValidate()
        {
            RestoreMotionState();
        }

        private Tween BuildRectPositionTween(RectTransform _target, Vector2 _initialPosition)
        {
            Sequence sequence = DOTween.Sequence();
            float offset = Mathf.Abs(valueSettings.startOffsetX);
            int shakeCount = Mathf.Max(valueSettings.shakeCount, 1);
            float positionDuration = forwardDuration * Mathf.Clamp01(valueSettings.positionTimeRatio);
            float shakeDuration = positionDuration / (shakeCount + 1);

            for (int i = 0; i < shakeCount; i++)
            {
                float direction = i % 2 == 0 ? -1f : 1f;
                Vector2 targetPosition = _initialPosition + Vector2.right * offset * direction;
                sequence.Append(_target.DOAnchorPos(targetPosition, shakeDuration).SetEase(valueSettings.positionEase));
                offset *= Mathf.Clamp01(valueSettings.offsetDamping);
            }

            sequence.Append(_target.DOAnchorPos(_initialPosition, shakeDuration).SetEase(valueSettings.positionEase));
            return sequence;
        }

        private Tween BuildTransformPositionTween(Transform _target, Vector3 _initialPosition)
        {
            Sequence sequence = DOTween.Sequence();
            float offset = Mathf.Abs(valueSettings.startOffsetX);
            int shakeCount = Mathf.Max(valueSettings.shakeCount, 1);
            float positionDuration = forwardDuration * Mathf.Clamp01(valueSettings.positionTimeRatio);
            float shakeDuration = positionDuration / (shakeCount + 1);

            for (int i = 0; i < shakeCount; i++)
            {
                float direction = i % 2 == 0 ? -1f : 1f;
                Vector3 targetPosition = _initialPosition + Vector3.right * offset * direction;
                sequence.Append(_target.DOLocalMove(targetPosition, shakeDuration).SetEase(valueSettings.positionEase));
                offset *= Mathf.Clamp01(valueSettings.offsetDamping);
            }

            sequence.Append(_target.DOLocalMove(_initialPosition, shakeDuration).SetEase(valueSettings.positionEase));
            return sequence;
        }

    }
}
