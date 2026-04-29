using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// 총알이 충전될 때 살짝 퍼졌다가 제자리로 모이는 "촤라락" 연출을 담당하는 컴포넌트입니다.
    /// CanvasGroup을 통해 알파를 제어하며, 멈춤 없이 부드럽게 연결됩니다.
    /// </summary>
    public class UIMotion_Reset : MonoBehaviour
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Duration Settings")]
            public float spreadDuration = 0.3f;
            public float appendInterval = -0.15f;
            public float gatherDuration = 0.4f;

            [Header("Movement Settings")]
            public Vector2 spreadOffset = new Vector2(0f, 20f);
            public Ease spreadEase = Ease.Linear;
            public Ease gatherEase = Ease.OutBack;

            [Header("Fade Settings")]
            public float fadeDuration = 0.1f;
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

        private UnityAction onStartCallback;
        private UnityAction onCompleteCallback;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 재충전 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="_delay">순차적 연출을 위한 대기 시간</param>
        /// <param name="_onComplete">완료 후 호출될 콜백</param>
        public void Play(float _delay, UnityAction _onStart, UnityAction _onComplete)
        {
            if (null == targetRect)
                return;

            onStartCallback = _onStart;
            onCompleteCallback = _onComplete;

            Stop();
            CaptureInitialState();

            if (null != targetCanvasGroup)
                targetCanvasGroup.alpha = 0f;

            currentSequence = DOTween.Sequence();

            Vector2 _spreadPos = initialState.position + valueSettings.spreadOffset;
            
            // 1. 퍼지기 (Spread) - OutSine으로 부드럽게 정점 도달
            currentSequence.Append(targetRect.DOAnchorPos(_spreadPos, valueSettings.spreadDuration)
                .SetEase(Ease.OutSine));

            if (null != targetCanvasGroup)
                currentSequence.Join(targetCanvasGroup.DOFade(1f, valueSettings.fadeDuration));

            // 2. 제자리로 모이기 (Gather)
            // 미세한 음수 인터벌을 주어 정점에서 멈추는 느낌 없이 바로 탄력 있게 복귀 (Overlap)
            currentSequence.AppendInterval(valueSettings.appendInterval); 
            currentSequence.Append(targetRect.DOAnchorPos(initialState.position, valueSettings.gatherDuration)
                .SetEase(valueSettings.gatherEase));

            currentSequence.SetDelay(_delay);
            currentSequence.OnStart(HandleStart);
            currentSequence.OnComplete(HandleComplete);
        }

        /// <summary>
        /// 재생 중인 애니메이션을 정지합니다.
        /// </summary>
        public void Stop()
        {
            if (null != currentSequence && true == currentSequence.IsActive())
                currentSequence.Kill();

            if (null != targetRect)
                targetRect.DOKill();

            if (null != targetCanvasGroup)
                targetCanvasGroup.DOKill();
        }

        /// <summary>
        /// 초기 상태로 즉시 복구합니다.
        /// </summary>
        public void ResetToInitialState()
        {
            if (null == targetRect)
                return;

            targetRect.anchoredPosition = initialState.position;
            targetRect.localEulerAngles = initialState.rotation;
            targetRect.localScale = initialState.scale;

            if (null != targetCanvasGroup)
                targetCanvasGroup.alpha = 1f;
        }

        // //유니티 이벤트 함수

        public void Initialize()
        {
            CaptureInitialState();
        }

        private void OnDestroy()
        {
            Stop();
        }

        // //내부 로직 메서드

        private void CaptureInitialState()
        {
            if (null == targetRect)
                return;

            // 이미 데이터가 캡처되어 있다면 중복 캡처를 방지하여 위치가 밀리는 현상 차단
            if (0.001f < initialState.scale.sqrMagnitude)
                return;

            initialState.position = targetRect.anchoredPosition;
            initialState.rotation = targetRect.localEulerAngles;
            initialState.scale = targetRect.localScale;

            if (null != targetCanvasGroup)
                initialState.alpha = targetCanvasGroup.alpha;
        }

        private void HandleComplete()
        {
            ResetToInitialState();

            if (null != onCompleteCallback)
                onCompleteCallback.Invoke();
        }

        private void HandleStart()
        {
            if (null != onStartCallback)
                onStartCallback.Invoke();
        }
    }
}
