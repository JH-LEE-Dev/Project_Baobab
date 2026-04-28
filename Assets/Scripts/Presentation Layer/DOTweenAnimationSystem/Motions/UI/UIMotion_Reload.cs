using UnityEngine;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// 재장전 시 UI 요소가 모여서 사라진 후, 원래 위치로 복구시켜 다음 연출을 준비하는 모션 클래스입니다.
    /// </summary>
    public class UIMotion_Reload : ObjectMotionBase
    {
        // //외부 의존성
        [SerializeField] private Vector2 gatherAnchorPos = new Vector2(0f, -18f);
        [SerializeField] private float gatherAlpha = 0f;
        [SerializeField] private Ease easeType = Ease.OutBack;

        // //내부 의존성
        private RectTransform targetRect;
        private CanvasGroup targetGroup;

        // //내부 로직

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect)
        {
            if (null == _rect)
                return;

            targetRect = _rect;
            _seq.Append(_rect.DOAnchorPos(gatherAnchorPos, publicDuration).SetEase(easeType));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group)
        {
            if (null == _group)
                return;

            targetGroup = _group;
            _seq.Join(_group.DOFade(gatherAlpha, publicDuration).SetEase(easeType));
        }

        protected override void InternalOnComplete()
        {
            // 애니메이션 종료 후 위치만 원래 슬롯(0,0)으로 복구시켜 둡니다. (알파는 0)
            if (null != targetRect)
                targetRect.anchoredPosition = Vector2.zero;

            if (null != targetGroup)
                targetGroup.alpha = 0f;

            base.InternalOnComplete();
        }
    }
}
