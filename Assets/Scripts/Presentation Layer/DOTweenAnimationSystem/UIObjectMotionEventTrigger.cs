using UnityEngine;
using UnityEngine.EventSystems;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class UIObjectMotionEventTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Motion Player")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;

        [Header("Motion Tags")]
        [SerializeField] private string hoverMotionTag = "Hover";
        [SerializeField] private string clickMotionTag = "Click";

        [Header("Play Settings")]
        [SerializeField] private bool resetOnPlay = true;
        [SerializeField] private float hoverReplayCooldown = 0.15f;
        [SerializeField] private bool stopHoverBeforeClick = true;

        private bool isPointerInside;
        private float nextHoverPlayTime;

        private void Awake()
        {
            if (null != motionPlayer)
                return;

            motionPlayer = GetComponent<ObjectMotionPlayer>();
            if (null == motionPlayer)
                motionPlayer = GetComponentInChildren<ObjectMotionPlayer>(true);
        }

        public void OnPointerEnter(PointerEventData _eventData)
        {
            if (true == isPointerInside)
                return;

            isPointerInside = true;
            PlayHoverMotion();
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            if (true == IsPointerInsideRoot(_eventData))
                return;

            isPointerInside = false;
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (null != _eventData && _eventData.button != PointerEventData.InputButton.Left)
                return;

            PlayClickMotion();
        }

        public void PlayHoverMotion()
        {
            if (null == motionPlayer || string.IsNullOrEmpty(hoverMotionTag))
                return;

            if (Time.unscaledTime < nextHoverPlayTime)
                return;

            if (motionPlayer.IsPlaying(hoverMotionTag))
                return;

            nextHoverPlayTime = Time.unscaledTime + hoverReplayCooldown;
            motionPlayer.Play(hoverMotionTag, bReset: resetOnPlay);
        }

        public void PlayClickMotion()
        {
            if (null == motionPlayer || string.IsNullOrEmpty(clickMotionTag))
                return;

            if (true == stopHoverBeforeClick && string.IsNullOrEmpty(hoverMotionTag) == false)
                motionPlayer.Stop(hoverMotionTag);

            nextHoverPlayTime = Time.unscaledTime + hoverReplayCooldown;
            motionPlayer.Play(clickMotionTag, bReset: resetOnPlay);
        }

        private bool IsPointerInsideRoot(PointerEventData _eventData)
        {
            if (null == _eventData)
                return false;

            RectTransform rectTransform = transform as RectTransform;
            if (null == rectTransform)
                return false;

            Camera eventCamera = _eventData.enterEventCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, _eventData.position, eventCamera);
        }
    }
}
