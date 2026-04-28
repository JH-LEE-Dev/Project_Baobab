using UnityEngine;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// 아이템 획득이나 추가 시 '뽕' 하고 나타나는 찰진 느낌의 연출을 담당하는 모션 클래스입니다.
    /// 연출 반복 시 회전값이 누적되지 않도록 초기화 로직이 포함되어 있습니다.
    /// </summary>
    public class UIMotion_Pop : ObjectMotionBase
    {
        // //외부 의존성
        [Header("Scale Settings")]
        [SerializeField] private float startScale = 0.5f;
        [SerializeField] private float targetScale = 1f;
        [SerializeField] private Ease scaleEase = Ease.OutBack;

        [Header("Rotation Settings")]
        [SerializeField] private Vector3 punchRotation = new Vector3(0f, 0f, 15f);
        [SerializeField] private int punchVibrato = 10;
        [SerializeField] private float punchElasticity = 1f;

        // //내부 로직

        protected override void OnRectTransform(Sequence _seq, RectTransform _rect)
        {
            if (null == _rect)
                return;

            // [핵심] 기울어짐 누적 방지: 시작 시 회전값과 크기를 즉시 초기화
            _rect.localEulerAngles = Vector3.zero;
            _rect.localScale = Vector3.one * startScale;

            // 1. 크기 연출
            _seq.Append(_rect.DOScale(targetScale, publicDuration).SetEase(scaleEase));

            // 2. 회전 연출: 초기화된 zero 지점을 기준으로 흔들림
            _seq.Join(_rect.DOPunchRotation(punchRotation, publicDuration, punchVibrato, punchElasticity));
        }

        protected override void OnCanvasGroup(Sequence _seq, CanvasGroup _group)
        {
            if (null == _group)
                return;

            // 알파 초기화 및 페이드인
            _group.alpha = 0f;
            _seq.Join(_group.DOFade(1f, publicDuration * 0.5f));
        }
    }
}
