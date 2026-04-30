using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class UIMotionTemplate : ObjectMotionBase
    {
        [SerializeField] private RectTransform startTransform;
        [SerializeField] private RectTransform targetTransform;

        [Header("Animation Easing")]
        [SerializeField] private Ease moveEase = Ease.Linear;
        [SerializeField] private Ease colorEase = Ease.Linear;


        protected override void OnRectTransform(Sequence _seq, RectTransform _rect)
        {
            _rect.anchoredPosition = startTransform.anchoredPosition;

            _seq.Append(_rect.DOAnchorPos(targetTransform.anchoredPosition, forwardDuration))
                .SetEase(moveEase);
        }

        protected override void OnGraphic(Sequence _seq, Graphic _graphic)
        {
            Color color = new Color(1f, 1f, 1f, 0f);
            _graphic.color = color;

            _seq.Join(_graphic.DOFade(1f, forwardDuration))
                .SetEase(colorEase);
        }

        protected override void ApplyTweenSettings(Tween _tween)
        {
            base.ApplyTweenSettings(_tween);
        }
    }
}