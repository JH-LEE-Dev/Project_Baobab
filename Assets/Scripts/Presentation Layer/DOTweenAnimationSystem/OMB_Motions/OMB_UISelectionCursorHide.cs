using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UISelectionCursorHide : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            public float expandSizeOffset = 10f;
            public Ease hideEase = Ease.OutQuad;
        }

        private readonly Dictionary<RectTransform, Vector2> sizeCache = new Dictionary<RectTransform, Vector2>();

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 0.15f;
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
            Vector2 expandedSize = _rect.sizeDelta + Vector2.one * Mathf.Abs(valueSettings.expandSizeOffset);
            _seq.Join(_rect.DOSizeDelta(expandedSize, forwardDuration).SetEase(valueSettings.hideEase));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group, Ease _currPublicEase)
        {
            if (null == _seq || null == _group)
                return;

            _seq.Join(_group.DOFade(0f, forwardDuration).SetEase(valueSettings.hideEase));
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);

            if (null != currentTween)
                currentTween.OnKill(RestoreCachedStateOnKill);
        }

        protected override void RestoreAfterValidate()
        {
            RestoreCachedStateOnKill();
        }

        private void RestoreCachedStateOnKill()
        {
            RestoreCachedState(false);

            foreach (KeyValuePair<RectTransform, Vector2> pair in sizeCache)
            {
                if (pair.Key != null)
                    pair.Key.sizeDelta = pair.Value;
            }
        }
    }
}
