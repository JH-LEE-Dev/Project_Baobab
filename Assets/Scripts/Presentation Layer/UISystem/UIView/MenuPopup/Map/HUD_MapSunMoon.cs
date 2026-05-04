using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

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

        // //내부 의존성
        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;

        private RectTransform sunRect;
        private RectTransform moonRect;
        private bool isInitialized = false;

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
        /// 특정 모션 태그를 통해 애니메이션(공전, 알파 등)을 재생합니다.
        /// </summary>
        public void PlayMotion(string _motionTag)
        {
            if (null == motionPlayer)
                return;

            motionPlayer.Play(_motionTag);
        }

        /// <summary>
        /// 중앙축의 회전값을 수동으로 설정합니다. (필요 시)
        /// </summary>
        public void SetRotation(float _zAngle)
        {
            if (null == pivotRect)
                return;

            pivotRect.localRotation = Quaternion.Euler(0.0f, 0.0f, _zAngle);
        }

        private void LateUpdate()
        {
            if (false == isInitialized)
                return;

            // 중앙축이 회전하더라도 이미지는 월드 기준 정면(회전 0)을 유지하도록 처리
            if (null != sunRect)
                sunRect.rotation = Quaternion.identity;

            if (null != moonRect)
                moonRect.rotation = Quaternion.identity;
        }
    }
}
