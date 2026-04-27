using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 장비 HUD 아이템의 공통 기능을 관리하는 추상 클래스.
    /// 활성화 상태에 따른 시각적 토글 및 어둡게 처리(Dimming) 기능을 포함합니다.
    /// </summary>
    public abstract class HUD_EquipmentItem : MonoBehaviour
    {
        // //외부 의존성
        [Header("Common Visual Components")]
        [SerializeField] protected GameObject outlineObject;      // 활성화 시 표시될 테두리
        [SerializeField] private GameObject shadowObject;       // 활성화 시 표시될 그림자
        
        [Header("Inactive Visual Settings")]
        [SerializeField] private CanvasGroup canvasGroup;       // 알파 조절용 컴포넌트
        [SerializeField] private Graphic[] colorTargets;      // 어둡게 처리할 대상 그래픽 배열
        [SerializeField] private float inactiveAlpha = 0.5f;    // 비활성 시 알파값
        [SerializeField] private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1.0f); // 비활성 시 어두운 색상

        [Header("Animation Settings")]
        [SerializeField] protected ObjectMotionPlayer motionPlayer;
        [SerializeField] protected string activateMotionTag = "Activate";
        [SerializeField] protected string deactivateMotionTag = "Deactivate";

        // //내부 의존성
        protected bool isActive = false;

        // //퍼블릭 초기화 및 제어 메서드

        public virtual void Initialize()
        {
            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            if (null == canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void SetActivate(bool _isActive)
        {
            isActive = _isActive;

            UpdateVisuals();
            PlayStatusMotion();
        }

        /// <summary>
        /// 상태에 따라 테두리, 그림자, 알파, 색상을 모두 업데이트합니다.
        /// </summary>
        protected virtual void UpdateVisuals()
        {
            // 1. 오브젝트 활성화/비활성화 제어
            if (null != outlineObject)
                outlineObject.SetActive(isActive);

            if (null != shadowObject)
                shadowObject.SetActive(isActive);

            // 2. 알파 및 색상 처리 (어둡게 만들기)
            UpdateStatusVisuals();
        }

        /// <summary>
        /// 활성/비활성 상태에 따른 알파 및 색상 값을 적용합니다.
        /// </summary>
        private void UpdateStatusVisuals()
        {
            float _targetAlpha = isActive ? 1.0f : inactiveAlpha;
            Color _targetColor = isActive ? Color.white : inactiveColor;

            // 알파 적용
            if (null != canvasGroup)
                canvasGroup.alpha = _targetAlpha;

            // 색상(어둡게) 적용
            if (null == colorTargets)
                return;

            for (int i = 0; i < colorTargets.Length; i++)
            {
                if (null == colorTargets[i])
                    continue;

                colorTargets[i].color = _targetColor;
            }
        }

        protected virtual void PlayStatusMotion()
        {
            if (null == motionPlayer)
                return;

            string _tag = isActive ? activateMotionTag : deactivateMotionTag;
            motionPlayer.Play(_tag);
        }

        // //유니티 이벤트 함수
    }
}
