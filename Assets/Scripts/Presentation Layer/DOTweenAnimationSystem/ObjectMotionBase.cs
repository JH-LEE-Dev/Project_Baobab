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
        protected struct TargetInitialState
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
        [SerializeField] protected float publicDuration = 0.5f;
        [SerializeField] protected float publicDelay = 0f;

        // //내부 의존성
        protected Tween currentTween;
        protected UnityAction onStartAction;
        protected UnityAction onCompleteAction;
        protected List<TargetInitialState> stateCache = new List<TargetInitialState>(4);

        public virtual void Play(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete)
        {
            if (null == _targets || 0 == _targets.Count)
                return;

            stateCache.Clear();
            Sequence _seq = StopAndBinding(_onStart, _onComplete);
            ProcessTargets(_seq, _targets);
            ApplyTweenSettings(_seq);
        }

        public virtual void Stop()
        {
            if (null != currentTween && currentTween.IsActive())
                currentTween.Kill();
        }

        public void SetRuntimeSettings(float _duration, float _delay)
        {
            publicDuration = _duration;
            publicDelay = _delay;
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
            currentTween.SetDelay(publicDelay)
                        .OnStart(InternalOnStart)
                        .OnComplete(InternalOnComplete);
        }

        protected void ProcessTargets(Sequence _seq, List<MotionTarget> _targets)
        {
            if (null == _targets)
                return;

            for (int i = 0; i < _targets.Count; i++)
            {
                MotionTarget _target = _targets[i];
                if (null == _target)
                    continue;

                if (null != _target.rectTransform) 
                    OnRectTransform(_seq, _target.rectTransform);
                else if (null != _target.transform) 
                    OnTransform(_seq, _target.transform);

                if (null != _target.canvasGroup) 
                    OnCanvasGroup(_seq, _target.canvasGroup);
                if (null != _target.spriteRenderer) 
                    OnSpriteRenderer(_seq, _target.spriteRenderer);
                if (null != _target.uiGraphic) 
                    OnGraphic(_seq, _target.uiGraphic);
            }
        }

        protected virtual void OnRectTransform(Sequence _seq, RectTransform _rect) { }
        protected virtual void OnTransform(Sequence _seq, Transform _trans) { }
        protected virtual void OnCanvasGroup(Sequence _seq, CanvasGroup _group) { }
        protected virtual void OnSpriteRenderer(Sequence _seq, SpriteRenderer _renderer) { }
        protected virtual void OnGraphic(Sequence _seq, Graphic _graphic) { }

        protected void InternalOnStart()
        {
            if (null != onStartAction)
                onStartAction.Invoke();
        }

        protected virtual void InternalOnComplete()
        {
            if (null != onCompleteAction)
                onCompleteAction.Invoke();

            // 메모리 누수 방지를 위해 리셋 여부와 관계없이 캐시는 비워줌
            stateCache.Clear();
        }

        protected void ResetToInitialState()
        {
            if (0 == stateCache.Count)
                return;

            for (int i = 0; i < stateCache.Count; i++)
            {
                TargetInitialState _state = stateCache[i];

                if (null != _state.rectTransform)
                {
                    _state.rectTransform.anchoredPosition = _state.anchoredPosition;
                    _state.rectTransform.localEulerAngles = _state.localRotation;
                    _state.rectTransform.localScale = _state.localScale;
                }
                else if (null != _state.transform)
                {
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
