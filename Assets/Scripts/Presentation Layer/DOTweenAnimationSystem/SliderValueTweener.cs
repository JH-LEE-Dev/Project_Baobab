using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine.UI;

/// <summary>
/// Slider.DOValue(DOTweenModuleUI)와 똑같은 트윈을 만들되, getter/setter 델리게이트를 슬라이더마다 한 번만 만든다.
///
/// DOValue는 호출마다 슬라이더를 캡처하는 클로저 객체 하나와 델리게이트 두 개를 새로 할당한다. 나무 체력바는
/// 타격마다(충격파 한 번에 화면의 바 수십 개), 인벤토리 용량 바는 습득마다 부르므로 그만큼 쓰레기가 쌓였다.
/// 만드는 트윈은 DOValue와 같다: 같은 getter/setter 동작, SetOptions(snapping: false), SetTarget(슬라이더)
/// (타깃 기준 DOKill도 그대로 동작한다).
/// </summary>
public sealed class SliderValueTweener
{
    private readonly Slider slider;
    private readonly DOGetter<float> getter;
    private readonly DOSetter<float> setter;

    private SliderValueTweener(Slider _slider)
    {
        slider = _slider;
        getter = () => _slider.value;
        setter = x => _slider.value = x;
    }

    /// <summary>
    /// _slider.DOValue(_endValue, _duration)와 같은 트윈을 돌려준다. _cache는 호출부가 슬라이더별로 들고 있는 필드이며,
    /// 비어 있거나 다른 슬라이더용이면 새로 만든다.
    /// </summary>
    public static TweenerCore<float, float, FloatOptions> DOValue(ref SliderValueTweener _cache, Slider _slider, float _endValue, float _duration)
    {
        if (null == _cache || false == ReferenceEquals(_cache.slider, _slider))
            _cache = new SliderValueTweener(_slider);

        TweenerCore<float, float, FloatOptions> t = DOTween.To(_cache.getter, _cache.setter, _endValue, _duration);
        t.SetOptions(false).SetTarget(_slider);
        return t;
    }
}
