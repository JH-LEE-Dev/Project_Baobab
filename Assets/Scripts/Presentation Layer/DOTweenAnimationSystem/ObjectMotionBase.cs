using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public abstract class ObjectMotionBase : MonoBehaviour
    {
        // //구조체 정의
        protected class TargetInitialState
        {
            public RectTransform rectTransform;
            public Transform transform;
            public CanvasGroup canvasGroup;
            public Graphic graphic;

            public Vector2 anchoredPosition;
            public Vector3 localPosition;
            public Vector3 localRotation;
            public Vector3 localScale;
            public float alpha;
            public Color color;
        }

        // //외부 의존성
        [Header("Duration Settings")]
        [SerializeField] protected float forwardDuration = 0.5f;
        [SerializeField] protected float forwardDelay = 0f;
        [SerializeField] protected Ease forwardEase = Ease.Unset;

        [SerializeField] protected float backwardDuration = 0.5f;
        [SerializeField] protected float backwardDelay = 0f;
        [SerializeField] protected Ease backwardEase = Ease.Unset;

        [Header("Debug Settings")]
        [SerializeField] protected bool resetOnValidateInPlayMode = true;

        // //내부 의존성
        protected Tween currentTween;
        protected UnityAction onStartAction;
        protected UnityAction onCompleteAction;

        protected List<TargetInitialState> stateCache = new List<TargetInitialState>(4);

        public virtual bool Play(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete, bool bReset)
        {
            if (null == _targets || 0 == _targets.Count)
                return false;

            if (true == bReset)
                ResetToInitialState();

            stateCache.Clear();
            Sequence _seq = StopAndBinding(_onStart, _onComplete);
            ProcessTargets(_seq, _targets, forwardEase);
            ApplyTweenSettings(_seq);

            return true;
        }

        public virtual bool PlayBackward(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete, bool bReset)
        {
            if (null == _targets || 0 == _targets.Count)
                return false;

            if (true == bReset)
                ResetToInitialState();

            stateCache.Clear();
            Sequence _seq = StopAndBinding(_onStart, _onComplete);
            ProcessTargets(_seq, _targets, backwardEase);
            ApplyBackwardTweenSettings(_seq);

            return true;
        }

        public virtual void Stop()
        {
            if (null != currentTween && currentTween.IsActive())
                currentTween.Kill();
        }

        public bool IsPlaying()
        {
            return null != currentTween && currentTween.IsActive() && currentTween.IsPlaying();
        }

        public void Skip(bool _isCallback)
        {
            if (null != currentTween && currentTween.IsActive())
                currentTween.Complete(_isCallback);
        }

        public void SetRuntimeSettings(float _duration, float _delay)
        {
            forwardDuration = _duration;
            forwardDelay = _delay;
        }

        protected virtual Sequence StopAndBinding(UnityAction _onStart, UnityAction _onComplete)
        {
            Stop();
            onStartAction = _onStart;
            onCompleteAction = _onComplete;

            return DOTween.Sequence();
        }

        protected virtual void ApplyTweenSettings(Tween _tween)
        {
            if (null == _tween)
                return;

            currentTween = _tween;
            currentTween.SetDelay(forwardDelay)
                        .OnStart(InternalOnStart)
                        .OnComplete(InternalOnComplete);
        }

        protected virtual void ApplyBackwardTweenSettings(Tween _tween)
        {
            if (null == _tween)
                return;

            currentTween = _tween;
            currentTween.SetDelay(backwardDelay)
                        .OnStart(InternalOnStart)
                        .OnComplete(InternalOnComplete);
        }

        protected void ProcessTargets(Sequence _seq, List<MotionTarget> _targets, Ease _currentEase)
        {
            if (null == _targets)
                return;

            for (int i = 0; i < _targets.Count; i++)
            {
                MotionTarget _target = _targets[i];
                if (null == _target)
                    continue;

                if (null != _target.rectTransform) 
                    OnRectTransform(_seq, _target.rectTransform, _currentEase);
                else if (null != _target.transform) 
                    OnTransform(_seq, _target.transform, _currentEase);

                if (null != _target.canvasGroup) 
                    OnCanvasGroup(_seq, _target.canvasGroup, _currentEase);
                if (null != _target.spriteRenderer) 
                    OnSpriteRenderer(_seq, _target.spriteRenderer, _currentEase);
                if (null != _target.uiGraphic) 
                    OnGraphic(_seq, _target.uiGraphic, _currentEase);
            }
        }

        protected virtual void OnRectTransform(Sequence _seq, RectTransform _rect, Ease _currPublicEase) { }
        protected virtual void OnTransform(Sequence _seq, Transform _trans, Ease _currPublicEase) { }
        protected virtual void OnCanvasGroup(Sequence _seq, CanvasGroup _group, Ease _currPublicEase) { }
        protected virtual void OnSpriteRenderer(Sequence _seq, SpriteRenderer _renderer, Ease _currPublicEase) { }
        protected virtual void OnGraphic(Sequence _seq, Graphic _graphic, Ease _currPublicEase) { }

        protected virtual void InternalOnStart()
        {
            if (null != onStartAction)
                onStartAction.Invoke();
        }

        protected virtual void InternalOnComplete()
        {
            if (null != onCompleteAction)
                onCompleteAction.Invoke();
        }

        protected virtual void OnValidate()
        {
            if (false == Application.isPlaying)
                return;

            if (false == resetOnValidateInPlayMode)
                return;

            Stop();
            RestoreAfterValidate();
        }

        protected virtual void RestoreAfterValidate()
        {
            RestoreCachedState();
        }

        protected void ApplyDurationScale(Tween _tween, float _targetDuration)
        {
            if (null == _tween || _targetDuration <= 0f)
                return;

            float currentDuration = _tween.Duration(false);
            if (currentDuration <= 0f)
                return;

            _tween.timeScale = currentDuration / _targetDuration;
        }

        public void ResetToInitialState()
        {
            if (0 == stateCache.Count)
                return;

            Stop();
            RestoreCachedState();
        }

        protected void RestoreCachedState(bool _restorePosition = true)
        {
            for (int i = 0; i < stateCache.Count; i++)
            {
                TargetInitialState _state = stateCache[i];

                if (null != _state.rectTransform)
                {
                    if (true == _restorePosition)
                        _state.rectTransform.anchoredPosition = _state.anchoredPosition;

                    _state.rectTransform.localEulerAngles = _state.localRotation;
                    _state.rectTransform.localScale = _state.localScale;
                }
                else if (null != _state.transform)
                {
                    if (true == _restorePosition)
                        _state.transform.localPosition = _state.localPosition;

                    _state.transform.localEulerAngles = _state.localRotation;
                    _state.transform.localScale = _state.localScale;
                }

                if (null != _state.canvasGroup)
                {
                    _state.canvasGroup.alpha = _state.alpha;
                }

                if (null != _state.graphic)
                {
                    _state.graphic.color = _state.color;
                }
            }
        }

        protected virtual void OnDestroy()
        {
            if (null != currentTween && currentTween.IsActive())
                currentTween.Kill();
        }
    }
}
