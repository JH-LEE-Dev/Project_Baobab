using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 개별 총알 아이콘의 시각적 상태와 연출을 관리하는 컴포넌트입니다.
    /// </summary>
    public class HUD_BulletIcon : MonoBehaviour
    {
        // //외부 의존성
        [SerializeField] private Image emptyImage;         // 배경 (빈 탄약)
        [SerializeField] private Image filledImage;        // 전경 (차 있는 탄약)
        [SerializeField] private GameObject outline;

        [SerializeField] private ObjectMotionPlayer motionPlayer; // 연출용 플레이어

        // //내부 의존성
        private bool isCurrentlyFilled = true;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize(bool _startFilled)
        {
            isCurrentlyFilled = _startFilled;

            if (null != filledImage)
                filledImage.gameObject.SetActive(isCurrentlyFilled);
        }

        /// <summary>
        /// 총알의 상태를 설정합니다.
        /// </summary>
        /// <param name="_isFilled">채워짐 여부</param>
        /// <param name="_shouldAnimate">연출 재생 여부</param>
        public void SetState(bool _isFilled, bool _shouldAnimate)
        {
            if (_isFilled == isCurrentlyFilled)
                return;

            isCurrentlyFilled = _isFilled;

            if (false == _isFilled && true == _shouldAnimate)
                PlayEjectMotion();
            else
                ResetToFilled();
        }

        private void PlayEjectMotion()
        {
            if (null == motionPlayer)
            {
                if (null != filledImage)
                    filledImage.gameObject.SetActive(false);
                
                return;
            }

            // 탄피 배출 연출 재생
            motionPlayer.Play("Eject", null, OnEjectComplete);
        }

        private void OnEjectComplete()
        {
            if (null != filledImage)
                filledImage.gameObject.SetActive(false);
        }

        private void ResetToFilled()
        {
            if (null == filledImage)
                return;

            filledImage.gameObject.SetActive(true);
            
            if (null != motionPlayer)
                motionPlayer.Play("Reset");
        }

        public void SetActive(bool _isActive)
        {
            if (null == outline)
                return;

            outline.SetActive(_isActive);
        }
    }
}
