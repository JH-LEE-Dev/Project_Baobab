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

        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;

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

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            if (null != sunImage)
                sunRect = sunImage.GetComponent<RectTransform>();

            if (null != moonImage)
                moonRect = moonImage.GetComponent<RectTransform>();

            isInitialized = true;
        }

        /// <summary>
        /// 중앙축의 회전값을 수동으로 설정합니다. (필요 시)
        /// </summary>
        public void SetRotation(float _zAngle, float _duration)
        {
            if (null == pivotRect)
                return;

            pivotRect.DOKill();
            Tween Rot = pivotRect.DOLocalRotate(new Vector3(0.0f, 0.0f, _zAngle), _duration, RotateMode.FastBeyond360);
            Rot.OnComplete(ReboundSunMoon);
        }

        private void ReboundSunMoon()
        {
            if (null != sunRect || null != moonRect)
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
