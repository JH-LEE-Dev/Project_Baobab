using System;
using UnityEngine;
using UnityEngine.EventSystems;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 맵 선택 UI에서 최종 결정을 내리는 확인 버튼 클래스입니다.
    /// 마우스 호버 및 클릭 이벤트를 처리하며 상위 UIView로 이벤트를 전달합니다.
    /// </summary>
    public class HUD_MapSelectButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
        // //외부 의존성
        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;

        // //내부 의존성
        private Action onConfirmEvent;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 버튼을 초기화하고 콜백을 등록합니다.
        /// </summary>
        public void Initialize(Action _onConfirm)
        {
            if (true == isInitialized)
                return;

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            onConfirmEvent = _onConfirm;
            isInitialized = true;
        }

        // //Event System 구현부

        public void OnPointerEnter(PointerEventData _eventData)
        {
            motionPlayer?.Play("Hover");
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            motionPlayer?.Play("Click");
            onConfirmEvent?.Invoke();
        }

        // //유니티 이벤트 함수

        private void Awake()
        {
            if (false == isInitialized)
                Initialize(null);
        }
    }
}
