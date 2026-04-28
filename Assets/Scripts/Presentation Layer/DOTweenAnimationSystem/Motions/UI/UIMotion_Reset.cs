using UnityEngine;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// 오프셋 위치에서 시작하여 원래 자리로 되돌리며 나타나게 하는 재입고 연출 모션 클래스입니다.
    /// </summary>
    public class UIMotion_Reset : ObjectMotionBase
    {
        // //외부 의존성
        [SerializeField] private Vector2 startOffsetPos = new Vector2(0f, -5f);
        [SerializeField] private Ease easeType = Ease.Linear;

        // //내부 로직

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect)
        {
            if (null == _rect)
                return;

            // 시작할 때 오프셋 위치로 순간 이동
            _rect.anchoredPosition = startOffsetPos;

            // 오프셋 위치에서 원래 위치(Vector2.zero)로 복구
            _seq.Append(_rect.DOAnchorPos(Vector2.zero, publicDuration).SetEase(easeType));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group)
        {
            if (null == _group)
                return;

            // 시작할 때 투명하게 설정
            _group.alpha = 0f;

            // 투명한 상태에서 원래 상태(1.0)로 복구
            _seq.Join(_group.DOFade(1f, publicDuration).SetEase(easeType));
        }
    }
}
