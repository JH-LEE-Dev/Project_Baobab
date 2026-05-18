using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 맵 선택 UI를 닫는 기능을 수행하는 버튼 클래스입니다.
    /// </summary>
    public class HUD_MapExitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,  IPointerClickHandler
    {
        // //외부 의존성
        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;
        [SerializeField] private Image buttonImage;

        // //내부 의존성
        private Action onExitEvent;
        private bool isInitialized = false;

        private MotionEntry enterAnim;
        private MotionEntry exitAnim;
        private MotionEntry clickedAnim;

        private static readonly Color normalColor = Color.white;
        private static readonly Color dimmedColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);

        private static readonly string hoverMotionKey = "ExitWiggle";
        private static readonly string hoverOffMotionKey = "ExitOffWiggle";

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 버튼을 초기화하고 콜백을 등록합니다.
        /// </summary>
        public void Initialize(Action _onExit)
        {
            if (true == isInitialized)
                return;

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            if (null == buttonImage)
                buttonImage = GetComponent<Image>();

            onExitEvent = _onExit;
            isInitialized = true;
        }

        /// <summary>
        /// 버튼의 명암(색상) 및 활성화 상태를 조절합니다.
        /// </summary>
        public void SetDimmed(bool _isDimmed)
        {
            if (null == buttonImage)
                return;

            buttonImage.color = (true == _isDimmed) ? dimmedColor : normalColor;
            buttonImage.raycastTarget = (false == _isDimmed);
        }

        // //Event System 구현부

        public void OnPointerEnter(PointerEventData _eventData)
        {
            if (null == motionPlayer)
                return;

            motionPlayer.SettingEntryMotion(clickedAnim, true, true);
            motionPlayer.SettingEntryMotion(exitAnim, true, true);

            enterAnim = motionPlayer.Play(hoverMotionKey, bReset: true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (null == motionPlayer)
                return;

            motionPlayer.SettingEntryMotion(enterAnim, true, true);
            motionPlayer.SettingEntryMotion(clickedAnim, true, true);

            exitAnim = motionPlayer.Play(hoverOffMotionKey,  bReset: true);
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            onExitEvent?.Invoke();
            //return;

            //if (null != motionPlayer)
            //{
            //    motionPlayer.SettingEntryMotion(enterAnim, true, true);
            //    motionPlayer.SettingEntryMotion(exitAnim, true, true);

            //    clickedAnim = motionPlayer.Play(clickMotionKey, bReset: true, _onComplete: callBack);
            //}
        }

        private void callBack() => onExitEvent?.Invoke();
    }
}
