using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// 재장전 시 각 총알들이 특정 수렴 지점(탄창 등)으로 모여들며 사라지는 연출을 담당하는 컴포넌트입니다.
    /// </summary>
    public class UIMotion_Reload : MonoBehaviour
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Duration Settings")]
            public float duration = 0.2f;

            [Header("Movement Settings")]
            public Vector2 targetOffset = new Vector2(0f, -30f); // 수렴할 목표 지점 오프셋
            public Ease moveEase = Ease.InQuint;

            [Header("Fade Settings")]
            public float fadeDuration = 0.15f;
            public Ease fadeEase = Ease.InQuad;
        }

        private struct InitialState
        {
            public Vector2 position;
            public Vector3 rotation;
            public Vector3 scale;
            public float alpha;
        }

        // //외부 의존성
        [Header("Target Settings")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private CanvasGroup targetCanvasGroup;

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings;

        // //내부 의존성
        private InitialState initialState;
        private Sequence currentSequence;
        private UnityAction onCompleteCallback;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            CaptureInitialState();
        }

        /// <summary>
        /// 탄환이 특정 지점으로 모여드는 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="_delay">순차적 연출을 위한 대기 시간</param>
        /// <param name="_onComplete">완료 후 호출될 콜백</param>
        public void Play(float _delay, UnityAction _onComplete = null)
        {
            if (null == targetRect)
                return;

            onCompleteCallback = _onComplete;

            Stop();
            CaptureInitialState();

            // 시작 상태: 현재 원래 위치 및 투명도 유지
            targetRect.anchoredPosition = initialState.position;
            if (null != targetCanvasGroup)
                targetCanvasGroup.alpha = 1f;

            currentSequence = DOTween.Sequence();

            // 목표 지점 계산
            Vector2 _targetPos = initialState.position + valueSettings.targetOffset;

            // 1. 특정 지점으로 이동 (Move to Target Position)
            currentSequence.Append(targetRect.DOAnchorPos(_targetPos, valueSettings.duration)
                .SetEase(valueSettings.moveEase));

            // 2. 사라지기 (Fade Out)
            if (null != targetCanvasGroup)
                currentSequence.Join(targetCanvasGroup.DOFade(0f, valueSettings.fadeDuration)
                    .SetEase(valueSettings.fadeEase));

            currentSequence.SetDelay(_delay);
            currentSequence.OnComplete(HandleComplete);
        }

        public void Stop()
        {
            if (null != currentSequence && true == currentSequence.IsActive())
                currentSequence.Kill();
        }

        public void ResetToInitialState()
        {
            if (null == targetRect)
                return;

            targetRect.anchoredPosition = initialState.position;
            targetRect.localEulerAngles = initialState.rotation;
            targetRect.localScale = initialState.scale;

            if (null != targetCanvasGroup)
                targetCanvasGroup.alpha = initialState.alpha;
        }

        // //유니티 이벤트 함수

        private void OnDestroy()
        {
            Stop();
        }

        // //내부 로직 메서드

        private void CaptureInitialState()
        {
            if (null == targetRect)
                return;

            initialState.position = targetRect.anchoredPosition;
            initialState.rotation = targetRect.localEulerAngles;
            initialState.scale = targetRect.localScale;
        }

        private void HandleComplete()
        {
            // 연출 완료 후 상태 복구 (다음 연출을 위해 제자리로)
            ResetToInitialState();

            if (null != onCompleteCallback)
                onCompleteCallback.Invoke();
        }
    }
}
