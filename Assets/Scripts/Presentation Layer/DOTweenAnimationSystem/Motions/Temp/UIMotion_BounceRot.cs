using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class UIMotion_BounceRot : ObjectMotionBase
{
    [SerializeField] private Vector3 startAngle;
    [SerializeField] private Vector3 startScale;

    [SerializeField] private Ease easeRot;
    [SerializeField] private Ease easeScale;

    protected override void ApplyTweenSettings(Tween _tween)
    {
        base.ApplyTweenSettings(_tween);
    }

    protected override void OnRectTransform(Sequence _seq, RectTransform _rect, Ease _currPublicEase)
    {
        if (null == _seq || null == _rect)
            return;

        _rect.eulerAngles = startAngle;
        _seq.Append(_rect.DORotate(new Vector3(0f, 0f, 0f), forwardDuration, RotateMode.FastBeyond360))
            .SetEase(easeRot);

        _rect.localScale = startScale;
        _seq.Join(_rect.DOScale(new Vector3(1f, 1f, 1f), forwardDuration))
            .SetEase(easeScale);
    }
}
