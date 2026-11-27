using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;

namespace PresentationLayer.DOTweenAnimationSystem
{
    [System.Serializable]
    public struct MotionKeyEvent
    {
        public float triggerTime;
        public string eventTag;
    }

    public abstract class ObjectMotionBase : MonoBehaviour
    {
        // //구조체 정의
        protected class TargetInitialState
        {
            public RectTransform rectTransform;
            public Transform transform;
            public CanvasGroup canvasGroup;
            public SpriteRenderer spriteRenderer;
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

        [Header("Keyframe Events")]
        [SerializeField] protected List<MotionKeyEvent> keyEvents = new List<MotionKeyEvent>();

        // //내부 의존성
        protected Tween currentTween;
        protected UnityAction onStartAction;
        protected UnityAction onCompleteAction;
        protected System.Action<string> currentKeyEventAction;

        protected List<TargetInitialState> stateCache = new List<TargetInitialState>(4);

        public virtual bool Play(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete, bool bReset)
        {
            if (null == _targets || 0 == _targets.Count)
            {
                return false;
            }

            if (true == bReset)
            {
                ResetToInitialState();
            }

            stateCache.Clear();
            Sequence _seq = StopAndBinding(_onStart, _onComplete);
            ProcessTargets(_seq, _targets, forwardEase);
            ApplyTweenSettings(_seq);

            return true;
        }

        public virtual bool PlayBackward(List<MotionTarget> _targets, UnityAction _onStart, UnityAction _onComplete, bool bReset)
        {
            if (null == _targets || 0 == _targets.Count)
            {
                return false;
            }

            if (true == bReset)
            {
                ResetToInitialState();
            }

            stateCache.Clear();
            Sequence _seq = StopAndBinding(_onStart, _onComplete);
            ProcessTargets(_seq, _targets, backwardEase);
            ApplyBackwardTweenSettings(_seq);

            return true;
        }

        public virtual void Stop()
        {
            if (true == IsCurrentTweenAlive())
                currentTween.Kill();
        }

        public bool IsPlaying()
        {
            return true == IsCurrentTweenAlive() && currentTween.IsPlaying();
        }

        public void Skip(bool _isCallback)
        {
            if (true == IsCurrentTweenAlive())
                currentTween.Complete(_isCallback);
        }

        // currentTween이 살아 있고 아직 이 모션의 것인지. 모션이 만든 트윈에는 id로 자신을 달아 둔다(ApplyTweenSettings).
        // 일부 모션(OMB_UIClickTwist)은 트윈을 재활용(SetRecyclable)하는데, 재활용 트윈은 죽은 뒤 다른 곳의 트윈으로 다시
        // 쓰이므로 들고 있던 참조가 남의 트윈일 수 있다. 재활용하지 않는 모션에서는 예전의 IsActive 검사와 결과가 같다.
        protected bool IsCurrentTweenAlive()
        {
            return null != currentTween && currentTween.IsActive() && ReferenceEquals(currentTween.id, this);
        }

        public void SetDelayForward(float _delay) => forwardDelay = _delay;
        public void SetDelayBackward(float _delay) => backwardDelay = _delay;

        public void SetDurationForward(float _duration) => forwardDuration = _duration;
        public void SetDurationBackward(float _duration) => backwardDuration = _duration;

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
            {
                return;
            }

            currentTween = _tween;

            Sequence _seq = _tween as Sequence;
            if (null != _seq)
            {
                BindKeyEvents(_seq, currentKeyEventAction);
            }

            EnsureCallbacks();
            currentTween.SetId(this)
                        .SetDelay(forwardDelay)
                        .OnStart(cachedInternalOnStart)
                        .OnComplete(cachedInternalOnComplete);
        }

        // InternalOnStart/InternalOnComplete를 메서드 그룹으로 넘기면 재생마다 델리게이트가 새로 할당된다. 한 번만 만들어 둔다
        // (가상 함수라 만들 때의 인스턴스 타입으로 묶이며, 매번 새로 만들던 것과 부르는 함수가 같다).
        private TweenCallback cachedInternalOnStart;
        private TweenCallback cachedInternalOnComplete;

        private void EnsureCallbacks()
        {
            if (null != cachedInternalOnStart)
                return;

            cachedInternalOnStart = InternalOnStart;
            cachedInternalOnComplete = InternalOnComplete;
        }

        protected virtual void ApplyBackwardTweenSettings(Tween _tween)
        {
            if (null == _tween)
            {
                return;
            }

            currentTween = _tween;

            Sequence _seq = _tween as Sequence;
            if (null != _seq)
            {
                BindKeyEvents(_seq, currentKeyEventAction);
            }

            EnsureCallbacks();
            currentTween.SetId(this)
                        .SetDelay(backwardDelay)
                        .OnStart(cachedInternalOnStart)
                        .OnComplete(cachedInternalOnComplete);
        }

        protected void ProcessTargets(Sequence _seq, List<MotionTarget> _targets, Ease _currentEase)
        {
            if (null == _targets)
                return;

            for (int _i = 0; _i < _targets.Count; _i++)
            {
                MotionTarget _target = _targets[_i];
                if (null == _target)
                    continue;

                // 트랜스폼 애니메이션 (위치/회전/크기 등)
                if (null != _target.rectTransform) 
                    OnRectTransform(_seq, _target.rectTransform, _currentEase);
                else if (null != _target.transform) 
                    OnTransform(_seq, _target.transform, _currentEase);

                // 시각적 애니메이션 (알파/컬러 등) - 병행 실행 허용
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

        protected void RestoreCachedState(bool _restorePosition = true, bool _restoreAlpha = true)
        {
            for (int _i = 0; _i < stateCache.Count; _i++)
            {
                TargetInitialState _state = stateCache[_i];

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

                if (true == _restoreAlpha)
                {
                    if (null != _state.canvasGroup)
                        _state.canvasGroup.alpha = _state.alpha;

                    if (null != _state.spriteRenderer)
                        _state.spriteRenderer.color = _state.color;

                    if (null != _state.graphic)
                        _state.graphic.color = _state.color;
                }
            }
        }

        protected virtual void OnDestroy()
        {
            if (true == IsCurrentTweenAlive())
                currentTween.Kill();
        }

        public void SetKeyEventAction(System.Action<string> _onKeyEvent)
        {
            currentKeyEventAction = _onKeyEvent;
        }

        protected void BindKeyEvents(Sequence _seq, System.Action<string> _onKeyEvent)
        {
            if (null == _onKeyEvent || null == keyEvents || 0 == keyEvents.Count)
            {
                return;
            }

            for (int _i = 0; _i < keyEvents.Count; _i++)
            {
                MotionKeyEvent _keyEvent = keyEvents[_i];
                if (true == string.IsNullOrEmpty(_keyEvent.eventTag))
                {
                    continue;
                }

                float _time = _keyEvent.triggerTime;
                string _tag = _keyEvent.eventTag;

                _seq.InsertCallback(_time, () =>
                {
                    _onKeyEvent.Invoke(_tag);
                });
            }
        }
    }
}
