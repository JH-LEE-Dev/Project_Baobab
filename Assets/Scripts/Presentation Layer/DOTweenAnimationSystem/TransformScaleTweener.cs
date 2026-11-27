using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

/// <summary>
/// Transform.DOScale(ShortcutExtensions)과 똑같은 트윈을 만들되, getter/setter 델리게이트를 트랜스폼마다 한 번만 만든다.
/// (SliderValueTweener와 같은 방식)
///
/// DOScale은 호출마다 트랜스폼을 캡처하는 클로저 객체 하나와 델리게이트 두 개를 새로 할당한다. 인벤토리 용량바의
/// 획득 연출과 빨간 점 연출은 원목을 주울 때마다 부르므로 그만큼 쓰레기가 쌓였다.
/// 만드는 트윈은 DOScale과 같다: 같은 getter/setter 동작(localScale), SetTarget(트랜스폼).
/// </summary>
public sealed class TransformScaleTweener
{
    private readonly Transform target;
    private readonly DOGetter<Vector3> getter;
    private readonly DOSetter<Vector3> setter;

    private TransformScaleTweener(Transform _target)
    {
        target = _target;
        getter = () => _target.localScale;
        setter = x => _target.localScale = x;
    }

    /// <summary>
    /// _target.DOScale(_endValue, _duration)과 같은 트윈을 돌려준다. _cache는 호출부가 트랜스폼별로 들고 있는 필드이며,
    /// 비어 있거나 다른 트랜스폼용이면 새로 만든다.
    /// </summary>
    public static TweenerCore<Vector3, Vector3, VectorOptions> DOScale(ref TransformScaleTweener _cache, Transform _target, Vector3 _endValue, float _duration)
    {
        if (null == _cache || false == ReferenceEquals(_cache.target, _target))
            _cache = new TransformScaleTweener(_target);

        TweenerCore<Vector3, Vector3, VectorOptions> t = DOTween.To(_cache.getter, _cache.setter, _endValue, _duration);
        t.SetTarget(_target);
        return t;
    }
}
