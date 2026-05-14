using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UIToolTipShow : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            public float startOffsetY = -12f;
            public float startAngle = 8f;
            public float angleDamping = 0.25f;
            public int swingCount = 3;
            public Ease moveEase = Ease.OutQuad;
            public Ease rotationEase = Ease.OutSine;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 0.55f;
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
            _rect.anchoredPosition = state.anchoredPosition + Vector2.up * valueSettings.startOffsetY;
            _rect.localEulerAngles = Vector3.zero;
            _seq.Join(_rect.DOAnchorPos(state.anchoredPosition, forwardDuration).SetEase(valueSettings.moveEase));
            _seq.Join(BuildRotationTween(_rect));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group, Ease _currPublicEase)
        {
            if (_seq == null || _group == null)
                return;

            TargetInitialState state = new TargetInitialState
            {
                canvasGroup = _group,
                alpha = _group.alpha
            };

            stateCache.Add(state);
            _group.alpha = 0f;
            _seq.Join(_group.DOFade(1f, forwardDuration).SetEase(valueSettings.moveEase));
        }

        private Tween BuildRotationTween(RectTransform _rect)
        {
            Sequence sequence = DOTween.Sequence();
            float angle = Mathf.Abs(valueSettings.startAngle);
            int swingCount = Mathf.Max(valueSettings.swingCount, 1);
            float swingDuration = forwardDuration / (swingCount + 1);

            for (int i = 0; i < swingCount; i++)
            {
                float direction = i % 2 == 0 ? -1f : 1f;
                Vector3 targetRotation = Vector3.forward * angle * direction;
                sequence.Append(_rect.DOLocalRotate(targetRotation, swingDuration, RotateMode.Fast).SetEase(valueSettings.rotationEase));
                angle *= Mathf.Clamp01(valueSettings.angleDamping);
            }

            sequence.Append(_rect.DOLocalRotate(Vector3.zero, swingDuration, RotateMode.Fast).SetEase(valueSettings.rotationEase));
            return sequence;
        }
    }
}
