using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_AbsoluteMove : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Relative Offset Settings")]
            public Vector2 startOffset;
            public Vector2 endOffset;

            [Header("Fade Settings")]
            public bool useFade = true;
            public float startAlpha = 0.0f;
            public float endAlpha = 1.0f;
            public Ease fadeEase = Ease.OutExpo;
        }

        private enum PlayDirection
        {
            Forward,
            Backward
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private PlayDirection currentDirection = PlayDirection.Forward;

        public override bool Play(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete, bool _bReset)
        {
            currentDirection = PlayDirection.Forward;

            if (!base.Play(_targets, _onStart, _onComplete, _bReset))
                return false;

            return true;
        }

        public override bool PlayBackward(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete, bool _bReset)
        {
            currentDirection = PlayDirection.Backward;

            if (!base.PlayBackward(_targets, _onStart, _onComplete, _bReset))
                return false;

            return true;
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

            Debug.Log(_state.anchoredPosition);

            stateCache.Add(_state);

            Vector2 _startPos;
            Vector2 _endPos;
            float _duration;

            if (PlayDirection.Forward == currentDirection)
            {
                _startPos = _state.anchoredPosition + valueSettings.startOffset;
                _endPos = _state.anchoredPosition + valueSettings.endOffset;
                _duration = forwardDuration;
            }
            else
            {
                _startPos = _state.anchoredPosition + valueSettings.endOffset;
                _endPos = _state.anchoredPosition + valueSettings.startOffset;
                _duration = backwardDuration;
            }

            _rect.anchoredPosition = _startPos;

            _seq.Join(
                _rect.DOAnchorPos(_endPos, _duration)
                     .SetEase(_currPublicEase)
            );
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

            Vector3 _startPos;
            Vector3 _endPos;
            float _duration;

            Vector3 _startOffset = new Vector3(valueSettings.startOffset.x, valueSettings.startOffset.y, 0f);
            Vector3 _endOffset = new Vector3(valueSettings.endOffset.x, valueSettings.endOffset.y, 0f);

            if (PlayDirection.Forward == currentDirection)
            {
                _startPos = _state.localPosition + _startOffset;
                _endPos = _state.localPosition + _endOffset;
                _duration = forwardDuration;
            }
            else
            {
                _startPos = _state.localPosition + _endOffset;
                _endPos = _state.localPosition + _startOffset;
                _duration = backwardDuration;
            }

            _trans.localPosition = _startPos;

            _seq.Join(
                _trans.DOLocalMove(_endPos, _duration)
                      .SetEase(_currPublicEase)
            );
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group, Ease _currPublicEase)
        {
            if (null == _seq || null == _group)
                return;

            if (false == valueSettings.useFade)
                return;

            TargetInitialState _state = new TargetInitialState
            {
                canvasGroup = _group,
                alpha = _group.alpha
            };

            stateCache.Add(_state);

            float _startAlpha;
            float _endAlpha;
            float _duration;

            if (PlayDirection.Forward == currentDirection)
            {
                _startAlpha = valueSettings.startAlpha;
                _endAlpha = valueSettings.endAlpha;
                _duration = forwardDuration;
            }
            else
            {
                _startAlpha = valueSettings.endAlpha;
                _endAlpha = valueSettings.startAlpha;
                _duration = backwardDuration;
            }

            _group.alpha = _startAlpha;

            _seq.Join(
                _group.DOFade(_endAlpha, _duration)
                      .SetEase(valueSettings.fadeEase)
            );
        }

        protected override void OnGraphic(Sequence _seq, Graphic _graphic, Ease _currPublicEase)
        {
            if (null == _seq || null == _graphic)
                return;

            if (false == valueSettings.useFade)
                return;

            TargetInitialState _state = new TargetInitialState
            {
                graphic = _graphic,
                color = _graphic.color
            };

            stateCache.Add(_state);

            Color _startColor = _graphic.color;
            Color _endColor = _graphic.color;
            float _duration;

            if (PlayDirection.Forward == currentDirection)
            {
                _startColor.a = valueSettings.startAlpha;
                _endColor.a = valueSettings.endAlpha;
                _duration = forwardDuration;
            }
            else
            {
                _startColor.a = valueSettings.endAlpha;
                _endColor.a = valueSettings.startAlpha;
                _duration = backwardDuration;
            }

            _graphic.color = _startColor;

            _seq.Join(
                _graphic.DOColor(_endColor, _duration)
                        .SetEase(valueSettings.fadeEase)
            );
        }

        protected override void InternalOnComplete()
        {
            base.InternalOnComplete();
        }
    }

}