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
        private Vector2 currentBaseSize;
        private Vector2 currentExpandedSize;
        private Vector2 currentContractedSize;

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

            currentBaseSize = RoundSize(_rect.sizeDelta);
            float sizeDeltaOffset = Mathf.Abs(valueSettings.sizeOffset) * 2f;
            currentExpandedSize = RoundSize(currentBaseSize + Vector2.one * sizeDeltaOffset);
            currentContractedSize = RoundSize(currentBaseSize - Vector2.one * sizeDeltaOffset);

            float stepDuration = Mathf.Max(forwardDuration / 4f, 0.0001f);
            _seq.AppendCallback(SetExpandedSize);
            _seq.AppendInterval(stepDuration);
            _seq.AppendCallback(SetBaseSize);
            _seq.AppendInterval(stepDuration);
            _seq.AppendCallback(SetContractedSize);
            _seq.AppendInterval(stepDuration);
            _seq.AppendCallback(SetBaseSize);
            _seq.AppendInterval(stepDuration);
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

        private void SetExpandedSize()
        {
            ApplyCurrentSize(currentExpandedSize);
        }

        private void SetBaseSize()
        {
            ApplyCurrentSize(currentBaseSize);
        }

        private void SetContractedSize()
        {
            ApplyCurrentSize(currentContractedSize);
        }

        private void ApplyCurrentSize(Vector2 _size)
        {
            if (currentRectTransform != null)
                currentRectTransform.sizeDelta = _size;
        }

        private Vector2 RoundSize(Vector2 _size)
        {
            return new Vector2(Mathf.Round(_size.x), Mathf.Round(_size.y));
        }
    }
}
