using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UISelectionCursorShow : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Size Settings")]
            public float shrinkSizeScale = 0.8f;
            [Range(0f, 1f)] public float shrinkTimeRatio = 0.08f;
            [Range(0f, 1f)] public float restoreTimeRatio = 0.12f;
            public Ease sizeEase = Ease.OutBack;

            [Header("Rotation Settings")]
            public float startAngle = 20f;
            public float angleDamping = 0.62f;
            public int swingCount = 5;
            [Range(0f, 1f)] public float rotationTimeRatio = 0.8f;
            public Ease rotationEase = Ease.OutSine;
        }

        private readonly Dictionary<RectTransform, Vector2> sizeCache = new Dictionary<RectTransform, Vector2>();

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

            TargetInitialState state = new TargetInitialState
            {
                rectTransform = _rect,
                anchoredPosition = _rect.anchoredPosition,
                localRotation = _rect.localEulerAngles,
                localScale = _rect.localScale
            };

            stateCache.Add(state);
            sizeCache.Clear();
            sizeCache[_rect] = _rect.sizeDelta;
            _seq.Join(BuildSizeTween(_rect, _rect.sizeDelta));
            _seq.Join(BuildRotationTween(_rect, state.localRotation));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group, Ease _currPublicEase)
        {
            if (null == _group)
                return;

            _group.alpha = 1f;
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

        protected override void RestoreAfterValidate()
        {
            RestoreMotionState();
        }

        private void RestoreMotionState()
        {
            RestoreCachedState(false);

            foreach (KeyValuePair<RectTransform, Vector2> pair in sizeCache)
            {
                if (pair.Key != null)
                    pair.Key.sizeDelta = pair.Value;
            }
        }

        private Tween BuildSizeTween(RectTransform _target, Vector2 _initialSize)
        {
            Vector2 shrinkSize = _initialSize * valueSettings.shrinkSizeScale;
            float shrinkDuration = forwardDuration * Mathf.Clamp01(valueSettings.shrinkTimeRatio);
            float restoreDuration = forwardDuration * Mathf.Clamp01(valueSettings.restoreTimeRatio);

            return DOTween.Sequence()
                .Append(_target.DOSizeDelta(shrinkSize, shrinkDuration).SetEase(Ease.OutQuad))
                .Append(_target.DOSizeDelta(_initialSize, restoreDuration).SetEase(valueSettings.sizeEase));
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
                float direction = i % 2 == 0 ? -1f : 1f;
                Vector3 targetRotation = _initialRotation;
                targetRotation.z += angle * direction;

                sequence.Append(_target.DOLocalRotate(targetRotation, swingDuration, RotateMode.Fast).SetEase(valueSettings.rotationEase));
                angle *= Mathf.Clamp01(valueSettings.angleDamping);
            }

            sequence.Append(_target.DOLocalRotate(_initialRotation, swingDuration, RotateMode.Fast).SetEase(valueSettings.rotationEase));
            return sequence;
        }
    }
}
