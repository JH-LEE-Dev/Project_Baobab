using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UISelectionCursorIdle : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            public float sizeOffset = 1f;
            public Ease idleEase = Ease.Linear;
        }

        private readonly Dictionary<RectTransform, Vector2> sizeCache = new Dictionary<RectTransform, Vector2>();
        private RectTransform currentRectTransform;

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 3f;
            forwardDelay = 0f;
            forwardEase = Ease.Linear;
            backwardDuration = 0.5f;
            backwardDelay = 0f;
            backwardEase = Ease.Linear;
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
            currentRectTransform = _rect;
            _seq.Append(BuildSnappedSizeTween(_rect, _rect.sizeDelta, _rect.sizeDelta + Vector2.one * Mathf.Abs(valueSettings.sizeOffset), forwardDuration * 0.25f));
            _seq.Append(BuildSnappedSizeTween(_rect, _rect.sizeDelta + Vector2.one * Mathf.Abs(valueSettings.sizeOffset), _rect.sizeDelta - Vector2.one * Mathf.Abs(valueSettings.sizeOffset), forwardDuration * 0.5f));
            _seq.Append(BuildSnappedSizeTween(_rect, _rect.sizeDelta - Vector2.one * Mathf.Abs(valueSettings.sizeOffset), _rect.sizeDelta, forwardDuration * 0.25f));
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);

            if (null != currentTween)
            {
                currentTween.SetLoops(-1, LoopType.Restart);
                currentTween.OnKill(RestoreMotionState);
            }
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

        private Tween BuildSnappedSizeTween(RectTransform _target, Vector2 _from, Vector2 _to, float _duration)
        {
            Vector2 from = RoundSize(_from);
            Vector2 to = RoundSize(_to);

            return _target.DOSizeDelta(to, _duration)
                          .From(from)
                          .SetEase(valueSettings.idleEase)
                          .OnUpdate(RoundCurrentRectSize);
        }

        private void RoundCurrentRectSize()
        {
            if (currentRectTransform != null)
                currentRectTransform.sizeDelta = RoundSize(currentRectTransform.sizeDelta);
        }

        private Vector2 RoundSize(Vector2 _size)
        {
            return new Vector2(Mathf.Round(_size.x), Mathf.Round(_size.y));
        }
    }
}
