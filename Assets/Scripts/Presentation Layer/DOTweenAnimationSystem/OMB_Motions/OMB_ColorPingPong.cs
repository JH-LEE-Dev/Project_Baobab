using UnityEngine;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine.UI;

public class OMB_ColorPingPong : ObjectMotionBase
{
    [Header("Color Settings")]
    [Tooltip("시작 색상 (A)")]
    [SerializeField] private Color colorA = Color.white;

    [Tooltip("목표 색상 (B)")]
    [SerializeField] private Color colorB = Color.red;

    protected override void OnSpriteRenderer(Sequence _seq, SpriteRenderer _renderer, Ease _currPublicEase)
    {
        if (null == _seq || null == _renderer)
            return;

        // 1. 초기 상태 캐싱
        TargetInitialState _state = new TargetInitialState
        {
            spriteRenderer = _renderer,
            color = _renderer.color
        };
        stateCache.Add(_state);

        // 2. 초기 색상을 A 컬러로 세팅
        _renderer.color = colorA;

        // 3. A 컬러에서 B 컬러로 변하는 애니메이션 적용
        _seq.Join(
            _renderer.DOColor(colorB, forwardDuration)
                     .SetEase(_currPublicEase)
        );
    }

    protected override void OnGraphic(Sequence _seq, Graphic _graphic, Ease _currPublicEase)
    {
        if (null == _seq || null == _graphic)
            return;

        // 1. 초기 상태 캐싱
        TargetInitialState _state = new TargetInitialState
        {
            graphic = _graphic,
            color = _graphic.color
        };
        stateCache.Add(_state);

        // 2. 초기 색상을 A 컬러로 세팅
        _graphic.color = colorA;

        // 3. A 컬러에서 B 컬러로 변하는 애니메이션 적용
        _seq.Join(
            _graphic.DOColor(colorB, forwardDuration)
                    .SetEase(_currPublicEase)
        );
    }

    protected override void ApplyTweenSettings(Tween _tween)
    {
        base.ApplyTweenSettings(_tween);

        // A -> B 로 변한 뒤, 다시 B -> A 로 자연스럽게 돌아가도록 Yoyo 루프 적용 (무한 루프)
        if (currentTween != null)
            currentTween.SetLoops(-1, LoopType.Yoyo);
    }

    protected override void ApplyBackwardTweenSettings(Tween _tween)
    {
        base.ApplyBackwardTweenSettings(_tween);

        // PlayBackward 시에도 동일하게 Yoyo 효과 적용
        if (currentTween != null)
            currentTween.SetLoops(-1, LoopType.Yoyo);
    }
}
