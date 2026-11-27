using DG.Tweening;
using UnityEngine;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class OMB_UIClickTwist : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Scale Settings")]
            public Vector2 squashScale = new Vector2(1.4f, 0.7f);
            public Vector2 recoilScale = new Vector2(0.8f, 1.3f);
            [Range(1, 5)] public int bounceCount = 2;
            [Range(0f, 1f)] public float bounceDamping = 0.25f;
            [Range(0f, 1f)] public float squashTimeRatio = 0.15f;
            [Range(0f, 1f)] public float recoilTimeRatio = 0.2f;
            [Range(0f, 1f)] public float restoreTimeRatio = 0.4f;
            public Ease squashEase = Ease.OutQuad;
            public Ease restoreEase = Ease.OutBack;
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        private void Reset()
        {
            forwardDuration = 0.45f;
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

            TargetInitialState _state = new TargetInitialState
            {
                rectTransform = _rect,
                anchoredPosition = _rect.anchoredPosition,
                localRotation = _rect.localEulerAngles,
                localScale = _rect.localScale
            };

            stateCache.Add(_state);
            _seq.Join(BuildScaleTween(_rect, _state.localScale));
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
            _seq.Join(BuildScaleTween(_trans, _state.localScale));
        }

        // 이 모션은 인벤토리 칸이 원목을 받을 때마다 재생된다(숲에서 프레임당 한 번 이상). 재생마다 새로 만들던 시퀀스 2개와
        // 크기 트윈(튀는 횟수 × 2 + 1개), 콜백 델리게이트가 쓰레기로 쌓였다. 시퀀스와 트윈을 재활용(SetRecyclable)하고
        // 콜백은 한 번만 만들어 둔다. 만드는 연출은 예전과 같다.
        // 재활용 트윈은 죽은 뒤 다른 곳의 트윈으로 다시 쓰이므로, currentTween은 ObjectMotionBase.IsCurrentTweenAlive로만 건드린다.
        protected override Sequence StopAndBinding(UnityEngine.Events.UnityAction _onStart, UnityEngine.Events.UnityAction _onComplete)
        {
            return base.StopAndBinding(_onStart, _onComplete).SetRecyclable(true);
        }

        private TweenCallback cachedRestoreMotionState;

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);

            if (null != currentTween)
            {
                if (null == cachedRestoreMotionState)
                    cachedRestoreMotionState = RestoreMotionState;

                currentTween.OnKill(cachedRestoreMotionState);
            }
        }

        protected override void InternalOnComplete()
        {
            RestoreMotionState();
            base.InternalOnComplete();
        }

        protected override void RestoreAfterValidate()
        {
            RestoreMotionState();
        }

        private void RestoreMotionState()
        {
            RestoreCachedState(false);
        }

        // DOScale 본문(DOTween.To(getter, setter, 끝값, 시간).SetTarget(대상))과 같은 트윈을 만들되, 재생마다 단계별로
        // getter/setter 대리자를 새로 만들지 않도록 마지막 대상의 것을 재사용한다(대상이 바뀌면 다시 만든다).
        private Transform scaleTweenTarget;
        private DG.Tweening.Core.DOGetter<Vector3> scaleTweenGetter;
        private DG.Tweening.Core.DOSetter<Vector3> scaleTweenSetter;

        private DG.Tweening.Core.TweenerCore<Vector3, Vector3, DG.Tweening.Plugins.Options.VectorOptions> ScaleTween(Transform _target, Vector3 _endValue, float _duration)
        {
            if (scaleTweenTarget != _target || null == scaleTweenGetter)
            {
                Transform target = _target;
                scaleTweenGetter = () => target.localScale;
                scaleTweenSetter = x => target.localScale = x;
                scaleTweenTarget = _target;
            }

            var t = DOTween.To(scaleTweenGetter, scaleTweenSetter, _endValue, _duration);
            t.SetTarget(_target);
            t.SetRecyclable(true);
            return t;
        }

        private Tween BuildScaleTween(Transform _target, Vector3 _initialScale)
        {
            Vector3 squashScale = new Vector3(
                _initialScale.x * valueSettings.squashScale.x,
                _initialScale.y * valueSettings.squashScale.y,
                _initialScale.z);

            Vector3 recoilScale = new Vector3(
                _initialScale.x * valueSettings.recoilScale.x,
                _initialScale.y * valueSettings.recoilScale.y,
                _initialScale.z);

            int bounceCount = Mathf.Max(valueSettings.bounceCount, 1);
            float cycleRatio = valueSettings.squashTimeRatio + valueSettings.recoilTimeRatio;
            float totalRatio = Mathf.Max((cycleRatio * bounceCount) + valueSettings.restoreTimeRatio, 0.0001f);
            float squashDuration = forwardDuration * Mathf.Clamp01(valueSettings.squashTimeRatio / totalRatio);
            float recoilDuration = forwardDuration * Mathf.Clamp01(valueSettings.recoilTimeRatio / totalRatio);
            float restoreDuration = forwardDuration * Mathf.Clamp01(valueSettings.restoreTimeRatio / totalRatio);

            Sequence sequence = DOTween.Sequence().SetRecyclable(true);
            float intensity = 1f;

            for (int i = 0; i < bounceCount; i++)
            {
                Vector3 dampedSquashScale = Vector3.Lerp(_initialScale, squashScale, intensity);
                Vector3 dampedRecoilScale = Vector3.Lerp(_initialScale, recoilScale, intensity);

                sequence.Append(ScaleTween(_target, dampedSquashScale, squashDuration).SetEase(valueSettings.squashEase));
                sequence.Append(ScaleTween(_target, dampedRecoilScale, recoilDuration).SetEase(Ease.OutQuad));

                intensity *= Mathf.Clamp01(valueSettings.bounceDamping);
            }

            sequence.Append(ScaleTween(_target, _initialScale, restoreDuration).SetEase(valueSettings.restoreEase));
            return sequence;
        }
    }
}
