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
        [SerializeField] private RectTransform pivotRect;      // 중앙 회전축
        [SerializeField] private Image sunImage;              // 해 이미지
        [SerializeField] private Image moonImage;             // 달 이미지
        [SerializeField] private ObjectMotionPlayer omp;

        // //내부 의존성
        private RectTransform sunRect;
        private RectTransform moonRect;
        private bool isInitialized = false;
        private bool isRebound = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// HUD 요소를 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null != sunImage)
                sunRect = sunImage.GetComponent<RectTransform>();

            if (null != moonImage)
                moonRect = moonImage.GetComponent<RectTransform>();

            isInitialized = true;
        }

        /// <summary>
        /// 낮/밤 상태에 따라 중앙축을 회전시키고 해/달의 알파를 교차 페이드합니다.
        /// </summary>
        public void SetRotation(bool _isDay, float _duration)
        {
            if (null == pivotRect)
                return;

            float _targetAngle = _isDay ? 55f : 235f;

            pivotRect.DOKill();
            pivotRect.DOLocalRotate(new Vector3(0.0f, 0.0f, _targetAngle), _duration, RotateMode.FastBeyond360)
                .OnComplete(ReboundSunMoon);

            // 알파 페이드 연출
            if (null != sunImage)
                sunImage.DOFade(_isDay ? 1.0f : 0.0f, _duration);

            if (null != moonImage)
                moonImage.DOFade(_isDay ? 0.0f : 1.0f, _duration);
        }

        public void PlayOpenAnim()
        {
            if (null == omp)
                return;

            omp.Play("SunMoon", bReset: true);
        }

        private void ReboundSunMoon()
        {
            if (null == sunRect || null == moonRect)
                return;

            isRebound = true;

            sunRect.DOKill();
            moonRect.DOKill();

            Vector3 punchRot = new Vector3(0f, 0f, 10f);

            Sequence seq = DOTween.Sequence();

            seq.Join(sunRect.DOPunchRotation(punchRot, 0.25f));
            seq.Join(moonRect.DOPunchRotation(punchRot, 0.25f));

            seq.OnComplete(ReboundCompleted);
        }

        private void ReboundCompleted()
        {
            isRebound = false;
        }

        /// <summary>
        /// 초기 투명도 설정을 수행합니다.
        /// </summary>
        public void SetInitialAlpha(bool _isDay)
        {
            if (null != sunImage)
            {
                Color _color = sunImage.color;
                _color.a = _isDay ? 1.0f : 0.0f;
                sunImage.color = _color;
            }

            if (null != moonImage)
            {
                Color _color = moonImage.color;
                _color.a = _isDay ? 0.0f : 1.0f;
                moonImage.color = _color;
            }
        }

        // //유니티 이벤트 함수

        private void LateUpdate()
        {
            if (false == isInitialized || true == isRebound)
                return;

            // 중앙축이 회전하더라도 이미지는 월드 기준 정면(회전 0)을 유지하도록 처리
            if (null != sunRect)
                sunRect.rotation = Quaternion.identity;

            if (null != moonRect)
                moonRect.rotation = Quaternion.identity;
        }
    }
}
