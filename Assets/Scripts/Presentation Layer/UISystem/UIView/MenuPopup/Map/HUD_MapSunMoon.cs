using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;
using DG.Tweening;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// HUD에서 해와 달 이미지를 중앙 기준으로 공전시키는 연출을 관리하는 클래스입니다.
    /// 중앙축이 회전하더라도 해와 달 이미지는 항상 정면을 바라보도록 유지합니다.
    /// </summary>
    public class HUD_MapSunMoon : MonoBehaviour
    {
        // //외부 의존성
        [Header("Orbit References")]
        [SerializeField] private RectTransform pivotRect;
        [SerializeField] private CanvasGroup sunGroup;
        [SerializeField] private CanvasGroup moonGroup;
        [SerializeField] private RectTransform sunRect;
        [SerializeField] private RectTransform moonRect;

        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;

        // //내부 의존성
        private bool isInitialized = false;
        private bool isRebound = false;

        // GC Alloc 최적화를 위한 벡터 캐싱
        private static readonly Vector3 punchRotation = new Vector3(0f, 0f, 10f);

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == isInitialized)
                return;

            isInitialized = true;
            motionPlayer?.Play("SunFloating", bReset: true);
            motionPlayer?.Play("Cloude1Floationg", bReset: true);
            motionPlayer?.Play("Cloude2Floationg", bReset: true);
        }

        public void SetRotation(bool _isDay, float _duration)
        {
            if (null == pivotRect)
                return;

            float _targetAngle = _isDay ? 75.0f : 255.0f;

            pivotRect.DOKill();
            pivotRect.DOLocalRotate(new Vector3(0.0f, 0.0f, _targetAngle), _duration, RotateMode.FastBeyond360)
                .OnComplete(ReboundSunMoon);

            if (null != sunGroup)
                sunGroup.DOFade(_isDay ? 1.0f : 0.0f, _duration);

            if (null != moonGroup)
                moonGroup.DOFade(_isDay ? 0.0f : 1.0f, _duration);
        }

        public void SetInitialAlpha(bool _isDay)
        {
            if (null != sunGroup)
                sunGroup.alpha = _isDay ? 1.0f : 0.0f;

            if (null != moonGroup)
                moonGroup.alpha = _isDay ? 0.0f : 1.0f;
        }

        public void PlayOpenAnim()
        {
            if (null == motionPlayer)
                return;

            motionPlayer.Play("SunMoon", bReset: true);
        }

        // //내부 로직

        private void ReboundSunMoon()
        {
            if (null == sunRect || null == moonRect)
                return;

            isRebound = true;

            sunRect.DOKill();
            moonRect.DOKill();

            Sequence _seq = DOTween.Sequence();
            _seq.Join(sunRect.DOPunchRotation(punchRotation, 0.25f));
            _seq.Join(moonRect.DOPunchRotation(punchRotation, 0.25f));
            _seq.OnComplete(ReboundCompleted);
        }

        private void ReboundCompleted() => isRebound = false;

        // //유니티 이벤트 함수

        private void LateUpdate()
        {
            if (!isInitialized || isRebound)
                return;

            if (null != sunRect)
                sunRect.rotation = Quaternion.identity;

            if (null != moonRect)
                moonRect.rotation = Quaternion.identity;
        }
    }
}
