using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class UIMotion_GravityRot : ObjectMotionBase
    {
        // //외부 의존성
        [SerializeField] private float jumpPower = 150f;
        [SerializeField] private float jumpRangeX = 100f;
        [SerializeField] private float jumpRangeY = -300f;
        [SerializeField] private float rotationAngle = 720f;
        [SerializeField] private Ease jumpEase = Ease.Linear;
        [SerializeField] private Ease rotationEase = Ease.OutQuad;
        [SerializeField] private Ease fadeEase = Ease.InQuint;

        // //내부 의존성

        // //퍼블릭 초기화 및 제어 메서드

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect)
        {
            if (null == _seq)
                return;

            if (null == _rect)
                return;

            TargetInitialState _state = new TargetInitialState();
            _state.rectTransform = _rect;
            _state.anchoredPosition = _rect.anchoredPosition;
            _state.localRotation = _rect.localEulerAngles;
            _state.localScale = _rect.localScale;
            stateCache.Add(_state);

            Vector2 _startPos = _rect.anchoredPosition;
            Vector2 _endPos = new Vector2(
                _startPos.x + Random.Range(jumpRangeX * 0.5f, jumpRangeX),
                _startPos.y + jumpRangeY
            );

            _seq.Join(_rect.DOJumpAnchorPos(_endPos, jumpPower, 1, publicDuration)
                .SetEase(jumpEase));

            _seq.Join(_rect.DORotate(new Vector3(0f, 0f, rotationAngle), publicDuration, RotateMode.FastBeyond360)
                .SetEase(rotationEase));

            _seq.Join(_rect.DOScale(Vector3.zero, publicDuration)
                .SetEase(fadeEase));
        }

        protected override void OnTransform(Sequence _seq, Transform _trans)
        {
            if (null == _seq)
                return;

            if (null == _trans)
                return;

            TargetInitialState _state = new TargetInitialState();
            _state.transform = _trans;
            _state.localPosition = _trans.localPosition;
            _state.localRotation = _trans.localEulerAngles;
            _state.localScale = _trans.localScale;
            stateCache.Add(_state);

            Vector3 _startPos = _trans.localPosition;
            Vector3 _endPos = new Vector3(
                _startPos.x + Random.Range(jumpRangeX * 0.5f, jumpRangeX),
                _startPos.y + jumpRangeY,
                _startPos.z
            );

            _seq.Join(_trans.DOLocalJump(_endPos, jumpPower, 1, publicDuration)
                .SetEase(jumpEase));

            _seq.Join(_trans.DOLocalRotate(new Vector3(0f, 0f, rotationAngle), publicDuration, RotateMode.FastBeyond360)
                .SetEase(rotationEase));

            _seq.Join(_trans.DOScale(Vector3.zero, publicDuration)
                .SetEase(fadeEase));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group)
        {
            if (null == _seq)
                return;

            if (null == _group)
                return;

            TargetInitialState _state = new TargetInitialState();
            _state.canvasGroup = _group;
            _state.alpha = _group.alpha;
            stateCache.Add(_state);

            _seq.Join(_group.DOFade(0f, publicDuration)
                .SetEase(fadeEase));
        }

        protected override void OnGraphic(Sequence _seq, Graphic _graphic)
        {
            if (null == _seq)
                return;

            if (null == _graphic)
                return;

            TargetInitialState _state = new TargetInitialState();
            _state.graphic = _graphic;
            _state.color = _graphic.color;
            stateCache.Add(_state);

            _seq.Join(_graphic.DOFade(0f, publicDuration)
                .SetEase(fadeEase));
        }

        protected override void InternalOnComplete()
        {
            // 탄피 모션은 재사용을 위해 리셋이 필요하므로 자식에서 명시적 호출
            ResetToInitialState();

            base.InternalOnComplete();
        }

        // //유니티 이벤트 함수
    }
}
