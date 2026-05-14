using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UIToolTipClick : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            public Vector2 squashScale = new Vector2(1.2f, 0.8f);
            public Vector2 recoilScale = new Vector2(0.9f, 1.1f);
            [Range(1, 5)] public int bounceCount = 1;
            [Range(0f, 1f)] public float bounceDamping = 0.9f;
            [Range(0f, 1f)] public float squashTimeRatio = 0.15f;
            [Range(0f, 1f)] public float recoilTimeRatio = 0.2f;
            [Range(0f, 1f)] public float restoreTimeRatio = 0.4f;
            public Ease squashEase = Ease.OutQuad;
            public Ease restoreEase = Ease.OutBack;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 0.25f;
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
            if (_seq == null || _rect == null)
                return;

            TargetInitialState state = new TargetInitialState
            {
                rectTransform = _rect,
                anchoredPosition = _rect.anchoredPosition,
                localRotation = _rect.localEulerAngles,
                localScale = _rect.localScale
            };

            stateCache.Add(state);
            _rect.localEulerAngles = Vector3.zero;
            _seq.Join(BuildScaleTween(_rect, state.localScale));
        }

        private Tween BuildScaleTween(Transform _target, Vector3 _initialScale)
        {
            Vector3 squashScale = new Vector3(
                _initialScale.x * valueSettings.squashScale.x,
                _initialScale.y * valueSettings.squashScale.y,
                _initialScale.z);

            Vector3 recoilScale = new Vector3(
                _initialScale.x * valueSettings.recoilScale.x,
                _initialScale.y * valueSettings.recoilScale.y,
                _initialScale.z);

            int bounceCount = Mathf.Max(valueSettings.bounceCount, 1);
            float cycleRatio = valueSettings.squashTimeRatio + valueSettings.recoilTimeRatio;
            float totalRatio = Mathf.Max((cycleRatio * bounceCount) + valueSettings.restoreTimeRatio, 0.0001f);
            float squashDuration = forwardDuration * Mathf.Clamp01(valueSettings.squashTimeRatio / totalRatio);
            float recoilDuration = forwardDuration * Mathf.Clamp01(valueSettings.recoilTimeRatio / totalRatio);
            float restoreDuration = forwardDuration * Mathf.Clamp01(valueSettings.restoreTimeRatio / totalRatio);

            Sequence sequence = DOTween.Sequence();
            float intensity = 1f;

            for (int i = 0; i < bounceCount; i++)
            {
                Vector3 dampedSquashScale = Vector3.Lerp(_initialScale, squashScale, intensity);
                Vector3 dampedRecoilScale = Vector3.Lerp(_initialScale, recoilScale, intensity);

                sequence.Append(_target.DOScale(dampedSquashScale, squashDuration).SetEase(valueSettings.squashEase));
                sequence.Append(_target.DOScale(dampedRecoilScale, recoilDuration).SetEase(Ease.OutQuad));

                intensity *= Mathf.Clamp01(valueSettings.bounceDamping);
            }

            sequence.Append(_target.DOScale(_initialScale, restoreDuration).SetEase(valueSettings.restoreEase));
            return sequence;
        }
    }
}
