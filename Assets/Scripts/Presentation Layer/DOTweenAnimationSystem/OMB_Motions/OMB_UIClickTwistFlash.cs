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
            public float squashDuration = 0.045f;
            public float recoilDuration = 0.065f;
            public float restoreDuration = 0.08f;
            public Ease squashEase = Ease.OutQuad;
            public Ease restoreEase = Ease.OutBack;

            [Header("Color Settings")]
            public Color flashColor = Color.white;
            public float flashInDuration = 0.035f;
            public float flashOutDuration = 0.12f;
            public Ease flashEase = Ease.OutQuad;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect)
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

        protected override void OnTransform(Sequence _seq, Transform _trans)
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

        protected override void OnGraphic(Sequence _seq, Graphic _graphic)
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
            ApplyDurationScale(_tween, forwardDuration);
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

            return DOTween.Sequence()
                .Append(_target.DOScale(squashScale, valueSettings.squashDuration).SetEase(valueSettings.squashEase))
                .Append(_target.DOScale(recoilScale, valueSettings.recoilDuration).SetEase(Ease.OutQuad))
                .Append(_target.DOScale(_initialScale, valueSettings.restoreDuration).SetEase(valueSettings.restoreEase));
        }

        private Tween BuildFlashTween(Graphic _graphic, Color _initialColor)
        {
            return DOTween.Sequence()
                .Append(_graphic.DOColor(valueSettings.flashColor, valueSettings.flashInDuration).SetEase(valueSettings.flashEase))
                .Append(_graphic.DOColor(_initialColor, valueSettings.flashOutDuration).SetEase(valueSettings.flashEase));
        }
    }
}
