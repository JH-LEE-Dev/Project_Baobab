using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UIToolTipHide : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            public float endOffsetY = -10f;
            public Ease hideEase = Ease.InOutSine;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 0.18f;
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
            _seq.Join(_rect.DOAnchorPos(state.anchoredPosition + Vector2.up * valueSettings.endOffsetY, forwardDuration).SetEase(valueSettings.hideEase));
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
            _seq.Join(_group.DOFade(0f, forwardDuration).SetEase(valueSettings.hideEase));
        }
    }
}
