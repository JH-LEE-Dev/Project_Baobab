using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class ObjectMotionTemplate : ObjectMotionBase
    {
        // //외부 의존성
        [SerializeField] private Transform startTransform;
        [SerializeField] private Transform targetTransform;

        protected override void OnTransform(Sequence _seq, Transform _trans, Ease _currPublicEase)
        {
            transform.position = startTransform.position;

            _seq.Append(_trans.DOMove(targetTransform.position, forwardDuration));
        }

        protected override void OnSpriteRenderer(Sequence _seq, SpriteRenderer _renderer, Ease _currPublicEase)
        {
            Color alphaZero = new Color(1f, 1f, 1f, 0f);
            _renderer.color = alphaZero;

            _seq.Join(_renderer.DOFade(1f, forwardDuration));
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);
        }       
    }
}