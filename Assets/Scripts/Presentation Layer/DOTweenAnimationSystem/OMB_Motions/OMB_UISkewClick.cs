using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.DOTweenAnimationSystem
{
    /// <summary>
    /// 타겟 오브젝트에 부착된 SkewEffect 컴포넌트를 제어하여 비틀기(Skew) 및 스케일(Scale), 컬러(Color), DOPunchRotation 기반 회전 반동(Rotation) 병렬 트윈 연출을 조립하는 모션 클래스입니다.
    /// </summary>
    public class OMB_UISkewClick : ObjectMotionBase
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Skew Settings")]
            public Vector2 maxSkew = new Vector2(0.25f, -0.1f); // 눌렸을 때 최대 비틀기 세기
            [Range(0f, 1f)] public float skewTimeRatio = 0.2f;    // 비틀어지는 시간 비율
            [Range(0f, 1f)] public float restoreTimeRatio = 0.8f;  // 제자리 복원 시간 비율
            public Ease skewEase = Ease.OutQuad;
            public Ease restoreEase = Ease.OutBack;

            [Header("Scale Settings")]
            public Vector2 targetScale = new Vector2(1.15f, 0.9f); // 눌렸을 때 변화할 X, Y 크기 배율
            public Ease scaleEase = Ease.OutQuad;

            [Header("Color Settings")]
            public Color targetColor = new Color(0.85f, 0.85f, 0.85f, 1f); // 눌렸을 때 바뀔 색상
            public Ease colorEase = Ease.OutQuad;

            [Header("Rotation Wiggle Settings")]
            public float punchRotation = 15f;                    // 펀치 회전의 최대 세기 (Z축 흔들림 각도)
            public int rotationVibrato = 10;                     // 흔들리는 횟수 (진동수)
            [Range(0f, 1f)] public float rotationElasticity = 0.5f; // 감쇄율 (탄성/복원도)
        }

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings = new ValueSettings();

        // Skew 초기 상태 복구 캐시
        private readonly Dictionary<SkewEffect, Vector2> initialSkewMap = new Dictionary<SkewEffect, Vector2>(4);


        // //퍼블릭 초기화 및 제어 메서드 (오버라이드)

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
            _seq.Join(BuildRotationWiggleTween(_rect, _state.localRotation));
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
            _seq.Join(BuildRotationWiggleTween(_trans, _state.localRotation));
        }

        protected override void OnGraphic(Sequence _seq, Graphic _graphic, Ease _currPublicEase)
        {
            if (null == _seq || null == _graphic)
                return;

            TargetInitialState _state = new TargetInitialState
            {
                graphic = _graphic,
                color = _graphic.color
            };

            stateCache.Add(_state);

            _seq.Join(BuildColorTween(_graphic, _state.color));

            SkewEffect _skew = _graphic.GetComponent<SkewEffect>();
            if (null != _skew)
            {
                if (false == initialSkewMap.ContainsKey(_skew))
                    initialSkewMap[_skew] = new Vector2(_skew.SkewX, _skew.SkewY);

                _seq.Join(BuildSkewTween(_skew, initialSkewMap[_skew]));
            }
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);

            if (null != currentTween)
                currentTween.OnKill(RestoreMotionState);
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


        // //내부 로직

        private void RestoreMotionState()
        {
            RestoreCachedState(false);
            
            foreach (KeyValuePair<SkewEffect, Vector2> _pair in initialSkewMap)
            {
                if (null != _pair.Key)
                {
                    _pair.Key.SkewX = _pair.Value.x;
                    _pair.Key.SkewY = _pair.Value.y;
                }
            }
        }

        private Tween BuildScaleTween(Transform _target, Vector3 _initialScale)
        {
            float totalRatio = Mathf.Max(valueSettings.skewTimeRatio + valueSettings.restoreTimeRatio, 0.0001f);
            float skewDuration = forwardDuration * Mathf.Clamp01(valueSettings.skewTimeRatio / totalRatio);
            float restoreDuration = forwardDuration * Mathf.Clamp01(valueSettings.restoreTimeRatio / totalRatio);

            Vector3 targetScale = new Vector3(_initialScale.x * valueSettings.targetScale.x, _initialScale.y * valueSettings.targetScale.y, _initialScale.z);

            Sequence sequence = DOTween.Sequence();
            sequence.Append(_target.DOScale(targetScale, skewDuration).SetEase(valueSettings.scaleEase));
            sequence.Append(_target.DOScale(_initialScale, restoreDuration).SetEase(valueSettings.restoreEase));

            return sequence;
        }

        private Tween BuildRotationWiggleTween(Transform _target, Vector3 _initialRotation)
        {
            float totalDuration = forwardDuration;
            Vector3 punchVector = new Vector3(0f, 0f, valueSettings.punchRotation);

            return _target.DOPunchRotation(punchVector, totalDuration, valueSettings.rotationVibrato, valueSettings.rotationElasticity);
        }

        private Tween BuildColorTween(Graphic _target, Color _initialColor)
        {
            float totalRatio = Mathf.Max(valueSettings.skewTimeRatio + valueSettings.restoreTimeRatio, 0.0001f);
            float skewDuration = forwardDuration * Mathf.Clamp01(valueSettings.skewTimeRatio / totalRatio);
            float restoreDuration = forwardDuration * Mathf.Clamp01(valueSettings.restoreTimeRatio / totalRatio);

            Sequence sequence = DOTween.Sequence();
            sequence.Append(_target.DOColor(valueSettings.targetColor, skewDuration).SetEase(valueSettings.colorEase));
            sequence.Append(_target.DOColor(_initialColor, restoreDuration).SetEase(valueSettings.restoreEase));

            return sequence;
        }

        private Tween BuildSkewTween(SkewEffect _target, Vector2 _initialSkew)
        {
            float totalRatio = Mathf.Max(valueSettings.skewTimeRatio + valueSettings.restoreTimeRatio, 0.0001f);
            float skewDuration = forwardDuration * Mathf.Clamp01(valueSettings.skewTimeRatio / totalRatio);
            float restoreDuration = forwardDuration * Mathf.Clamp01(valueSettings.restoreTimeRatio / totalRatio);

            Sequence sequence = DOTween.Sequence();

            // X축, Y축 비틀기 목표값으로 점진 변화 수행
            sequence.Append(DOTween.To(() => _target.SkewX, _x => _target.SkewX = _x, _initialSkew.x + valueSettings.maxSkew.x, skewDuration).SetEase(valueSettings.skewEase));
            sequence.Join(DOTween.To(() => _target.SkewY, _y => _target.SkewY = _y, _initialSkew.y + valueSettings.maxSkew.y, skewDuration).SetEase(valueSettings.skewEase));

            // 제자리로 원상태 복구 트윈 적용
            sequence.Append(DOTween.To(() => _target.SkewX, _x => _target.SkewX = _x, _initialSkew.x, restoreDuration).SetEase(valueSettings.restoreEase));
            sequence.Join(DOTween.To(() => _target.SkewY, _y => _target.SkewY = _y, _initialSkew.y, restoreDuration).SetEase(valueSettings.restoreEase));

            return sequence;
        }


        // //유니티 이벤트 함수

        private void Reset()
        {
            forwardDuration = 0.35f;
            forwardDelay = 0f;
            forwardEase = Ease.Unset;
            backwardDuration = 0.35f;
            backwardDelay = 0f;
            backwardEase = Ease.Unset;
            resetOnValidateInPlayMode = true;
            valueSettings = new ValueSettings();
        }
    }
}
