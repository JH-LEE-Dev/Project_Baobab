using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 맵 선택 UI에서 최종 결정을 내리는 확인 버튼 클래스입니다.
    /// 마우스 호버 및 클릭 이벤트를 처리하며 상위 UIView로 이벤트를 전달합니다.
    /// </summary>
    public class HUD_MapSelectorButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // //외부 의존성
        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;
        [SerializeField] private Image buttonImage;

        // //내부 의존성
        private Action onConfirmEvent;
        private Action<RectTransform, Vector2> onHoverEnterEvent;
        private Action onHoverExitEvent;
        private RectTransform rect;
        private MotionEntry enterAnim;
        private MotionEntry exitAnim;
        private MotionEntry clickedAnim;

        private static readonly Color normalColor = Color.white;
        private static readonly Color dimmedColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);

        private string hoverMotionKey;
        private string hoverOffMotionKey;
        private string clickMotionKey;

        private float currentAlpha = 1.0f;
        private bool isDimmed = false;
        private bool isClicked = false;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 버튼을 초기화하고 콜백을 등록합니다.
        /// </summary>
        public void Initialize(Action _onConfirm, string _hoverTag, string _hoverOffTag, string _clickTag, Action<RectTransform, Vector2> _onHoverEnter = null, Action _onHoverExit = null)
        {
            if (true == isInitialized)
                return;

            onConfirmEvent = _onConfirm;
            hoverMotionKey = _hoverTag;
            hoverOffMotionKey = _hoverOffTag;
            clickMotionKey = _clickTag;
            onHoverEnterEvent = _onHoverEnter;
            onHoverExitEvent = _onHoverExit;
            rect = GetComponent<RectTransform>();
            
            isInitialized = true;
        }

        /// <summary>
        /// 버튼의 명암(색상) 및 활성화 상태를 조절합니다.
        /// </summary>
        public void SetDimmed(bool _isDimmed)
        {
            if (null == buttonImage)
                return;

            isDimmed = _isDimmed;

            Color _targetColor = (true == isDimmed) ? dimmedColor : normalColor;
            _targetColor.a = currentAlpha;
            
            buttonImage.color = _targetColor;
            buttonImage.raycastTarget = !isDimmed;
        }

        public void SetAlpha(float _alpha)
        {
            currentAlpha = _alpha;

            if (null == buttonImage)
                return;

            Color _color = buttonImage.color;
            _color.a = currentAlpha;
            buttonImage.color = _color;
        }

        // //Event System 구현부

        public void OnPointerEnter(PointerEventData _eventData)
        {
            if (null == motionPlayer || isDimmed || isClicked)
                return;

            onHoverEnterEvent?.Invoke(rect, rect.rect.size);

            motionPlayer.SettingEntryMotion(clickedAnim, true, true);
            motionPlayer.SettingEntryMotion(exitAnim, true, true);

            enterAnim = motionPlayer.Play(hoverMotionKey, bReset: true);
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            if (null == motionPlayer || isDimmed || isClicked)
                return;

            onHoverExitEvent?.Invoke();

            motionPlayer.SettingEntryMotion(enterAnim, true, true);
            motionPlayer.SettingEntryMotion(clickedAnim, true, true);

            exitAnim = motionPlayer.Play(hoverOffMotionKey, bReset: true);
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (null == motionPlayer || isDimmed)
                return;

            motionPlayer.SettingEntryMotion(enterAnim, true, true);
            motionPlayer.SettingEntryMotion(exitAnim, true, true);
            isClicked = true;

            clickedAnim = motionPlayer.Play(clickMotionKey, bReset: true, _onComplete: UnClicked);

            onConfirmEvent?.Invoke();
        }

        private void UnClicked() => isClicked = false;
    }
}