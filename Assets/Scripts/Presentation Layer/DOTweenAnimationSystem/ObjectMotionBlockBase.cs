using System;
using DG.Tweening;
using UnityEngine;

namespace Presentation.UISystem.Animation
{
    /// <summary>
    /// 모든 모션 블록의 최상위 추상 클래스
    /// </summary>
    public abstract class ObjectMotionBlockBase : MonoBehaviour
    {
        // //외부 의존성
        [Header("Block Configuration")]
        [SerializeField] protected string motionTag; // 이 모션을 호출할 고유 태그

        // //내부 의존성
        public string MotionTag => motionTag;

        // //퍼블릭 제어 메서드

        /// <summary>
        /// 실제 DOTween 시퀀스를 생성하는 추상 메서드입니다.
        /// </summary>
        public abstract Sequence BuildSequence(
            ObjectMotionPlayer _player, 
            Action<int, string> _onStepStart, 
            Action<int, string> _onStepComplete);

        // //보조 메서드 (중복 구현 방지)

        protected Tween CreateScale(ObjectMotionPlayer _player, Vector3 _target, float _duration, Ease _ease = Ease.OutQuad)
        {
            return _player.TargetTransform.DOScale(_target, _duration).SetEase(_ease);
        }

        protected Tween CreateFade(ObjectMotionPlayer _player, float _target, float _duration, Ease _ease = Ease.Linear)
        {
            if (_player.TargetCanvasGroup != null) 
                return _player.TargetCanvasGroup.DOFade(_target, _duration).SetEase(_ease);
            
            if (_player.TargetSpriteRenderer != null) 
                return _player.TargetSpriteRenderer.DOFade(_target, _duration).SetEase(_ease);
            
            return null;
        }

        /// <summary>
        /// 람다 할당 없이 단계별 콜백을 안전하게 바인딩합니다. (Zero-GC)
        /// </summary>
        protected void BindStep(ObjectMotionPlayer _player, Tween _tween, int _index, string _stepName, Action<int, string> _onStart, Action<int, string> _onComplete)
        {
            if (_tween == null || _player == null) 
                return;

            if (_onStart != null)
                _tween.OnStart(_player.GetStepCallback(_index, _stepName, _onStart));

            if (_onComplete != null)
                _tween.OnComplete(_player.GetStepCallback(_index, _stepName, _onComplete));
        }
    }
}