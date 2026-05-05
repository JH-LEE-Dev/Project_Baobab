using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UIHoverOffWiggle : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Rotation Settings")]
            public float startAngle = 12f;
            public float angleDamping = 0.62f;
            public int swingCount = 5;
            [Range(0f, 1f)] public float rotationTimeRatio = 1f;
            public Ease rotationEase = Ease.OutSine;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 0.7f;
            forwardDelay = 0f;
            forwardEase = Ease.Unset;
            backwardDuration = 0.5f;
            backwardDelay = 0f;
            backwardEase = Ease.Unset;
            resetOnValidateInPlayMode = true;
            valueSettings = new ValueSettings();
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
            _seq.Join(BuildRotationTween(_rect, _state.localRotation));
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
            _seq.Join(BuildRotationTween(_trans, _state.localRotation));
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
            RestoreCachedState(false);
        }

        protected override void RestoreAfterValidate()
        {
            RestoreMotionState();
        }

        private Tween BuildRotationTween(Transform _target, Vector3 _initialRotation)
        {
            Sequence sequence = DOTween.Sequence();
            float angle = Mathf.Abs(valueSettings.startAngle);
            int swingCount = Mathf.Max(valueSettings.swingCount, 1);
            float rotationDuration = forwardDuration * Mathf.Clamp01(valueSettings.rotationTimeRatio);
            float swingDuration = rotationDuration / (swingCount + 1);

            for (int i = 0; i < swingCount; i++)
            {
                float direction = i % 2 == 0 ? 1f : -1f;
                Vector3 targetRotation = _initialRotation;
                targetRotation.z += angle * direction;

                sequence.Append(
                    _target.DOLocalRotate(targetRotation, swingDuration, RotateMode.Fast)
                           .SetEase(valueSettings.rotationEase));

                angle *= Mathf.Clamp01(valueSettings.angleDamping);
            }

            sequence.Append(
                _target.DOLocalRotate(_initialRotation, swingDuration, RotateMode.Fast)
                       .SetEase(valueSettings.rotationEase));

            return sequence;
        }
    }
}
