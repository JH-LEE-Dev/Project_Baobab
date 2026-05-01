using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UIClickTwistFlash : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Scale Settings")]
            public Vector2 squashScale = new Vector2(1.24f, 0.74f);
            public Vector2 recoilScale = new Vector2(0.88f, 1.08f);
            [Range(1, 5)] public int bounceCount = 3;
            [Range(0f, 1f)] public float bounceDamping = 0.55f;
            [Range(0f, 1f)] public float squashTimeRatio = 0.28f;
            [Range(0f, 1f)] public float recoilTimeRatio = 0.32f;
            [Range(0f, 1f)] public float restoreTimeRatio = 0.12f;
            public Ease squashEase = Ease.OutQuad;
            public Ease restoreEase = Ease.OutBack;

            [Header("Color Settings")]
            public Color flashColor = Color.white;
            [Range(0f, 1f)] public float flashInTimeRatio = 0.22f;
            [Range(0f, 1f)] public float flashOutTimeRatio = 0.78f;
            public Ease flashEase = Ease.OutQuad;
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
            _seq.Join(BuildScaleTween(_rect, _state.localScale));
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
            _seq.Join(BuildScaleTween(_trans, _state.localScale));
        }

        protected override void OnGraphic(Sequence _seq, Graphic _graphic, Ease _currPublicEase)
        {
            if (null == _seq || null == _graphic)
                return;

            TargetInitialState _state = new TargetInitialState
            {
                graphic = _graphic,
                color = _graphic.color
            };

            stateCache.Add(_state);
            _seq.Join(BuildFlashTween(_graphic, _state.color));
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

        private Tween BuildFlashTween(Graphic _graphic, Color _initialColor)
        {
            float totalRatio = Mathf.Max(
                valueSettings.flashInTimeRatio + valueSettings.flashOutTimeRatio,
                0.0001f);
            float flashInDuration = forwardDuration * Mathf.Clamp01(valueSettings.flashInTimeRatio / totalRatio);
            float flashOutDuration = forwardDuration * Mathf.Clamp01(valueSettings.flashOutTimeRatio / totalRatio);

            return DOTween.Sequence()
                .Append(_graphic.DOColor(valueSettings.flashColor, flashInDuration).SetEase(valueSettings.flashEase))
                .Append(_graphic.DOColor(_initialColor, flashOutDuration).SetEase(valueSettings.flashEase));
        }
    }
}
