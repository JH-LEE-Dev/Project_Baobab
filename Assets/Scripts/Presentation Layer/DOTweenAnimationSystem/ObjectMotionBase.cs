using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public abstract class ObjectMotionBase : MonoBehaviour
    {
        // //외부 의존성
        [SerializeField] protected float publicDuration = 0.5f;
        [SerializeField] protected float publicDelay = 0f;

        // //내부 의존성
        protected Tween currentTween;
        protected UnityAction onStartAction;
        protected UnityAction onCompleteAction;

        public virtual void Play(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete)
        {
            if (null == _targets || 0 == _targets.Count)
                return;

            Sequence _seq = StopAndBinding(_onStart, _onComplete);
            ProcessTargets(_seq, _targets);
            ApplyTweenSettings(_seq);
        }

        public virtual void Stop()
        {
            if (null != currentTween && currentTween.IsActive())
                currentTween.Kill();
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

        protected void InternalOnComplete()
        {
            if (null != onCompleteAction)
                onCompleteAction.Invoke();
        }

        protected virtual void OnDestroy()
        {
            if (null != currentTween && currentTween.IsActive())
                currentTween.Kill();
        }
    }
}