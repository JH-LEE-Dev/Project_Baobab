using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;

namespace PresentationLayer.DOTweenAnimationSystem
{
    public class UIMotion_AbsoluteMove : MonoBehaviour
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Relative Offset Settings")]
            public Vector2 startOffset;
            public Vector2 endOffset;

            [Header("Animation Settings")]
            public float duration = 1.0f;
            public float startDelay = 0.0f;
            public Ease moveEase = Ease.OutQuad;

            [Header("Fade Settings")]
            public bool useFade = true;
            public float startAlpha = 0.0f;
            public float endAlpha = 1.0f;
            public Ease fadeEase = Ease.Linear;
        }

        private struct InitialState
        {
            public Vector2 position;
            public float alpha;
        }

        // //외부 의존성
        [Header("Target Settings")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings;

        // //내부 의존성
        private InitialState initialState;
        private Sequence currentSequence;
        private bool isCaptured = false;
        
        private UnityAction onStartCallback;
        private UnityAction onCompleteCallback;

        // //퍼블릭 초기화 및 제어 메서드
        public void Initialize()
        {
            CaptureInitialState();
        }

        /// <summary>
        /// Start지점에서 End지점으로 재생합니다. (Fade: startAlpha -> endAlpha)
        /// </summary>
        public void Play(UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == targetRect)
                targetRect = GetComponent<RectTransform>();

            if (null == targetRect)
                return;

            onStartCallback = _onStart;
            onCompleteCallback = _onComplete;

            Stop();

            if (false == isCaptured)
                CaptureInitialState();

            // 위치 계산
            Vector2 _startPos = initialState.position + valueSettings.startOffset;
            Vector2 _endPos = initialState.position + valueSettings.endOffset;

            targetRect.anchoredPosition = _startPos;
            
            currentSequence = DOTween.Sequence();

            // 위치 이동 트윈
            currentSequence.Append(targetRect.DOAnchorPos(_endPos, valueSettings.duration)
                .SetEase(valueSettings.moveEase));

            // 페이드 트윈 처리 강화
            if (true == valueSettings.useFade)
            {
                // Play 시점에 CanvasGroup이 없다면 다시 찾기 시도
                if (null == canvasGroup)
                    canvasGroup = targetRect.GetComponent<CanvasGroup>();
                
                if (null == canvasGroup)
                    canvasGroup = GetComponent<CanvasGroup>();

                if (null != canvasGroup)
                {
                    SetAlpha(valueSettings.startAlpha);
                    currentSequence.Join(canvasGroup.DOFade(valueSettings.endAlpha, valueSettings.duration)
                        .SetEase(valueSettings.fadeEase));
                }
            }

            currentSequence.SetDelay(valueSettings.startDelay);
            currentSequence.OnStart(HandleStart);
            currentSequence.OnComplete(HandleComplete);
        }

        /// <summary>
        /// End지점에서 Start지점으로 되돌아가며 재생합니다. (Fade: endAlpha -> startAlpha)
        /// </summary>
        public void PlayBackwards(UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == targetRect)
                targetRect = GetComponent<RectTransform>();

            if (null == targetRect)
                return;

            onStartCallback = _onStart;
            onCompleteCallback = _onComplete;

            Stop();

            if (false == isCaptured)
                CaptureInitialState();

            Vector2 _startPos = initialState.position + valueSettings.endOffset;
            Vector2 _endPos = initialState.position + valueSettings.startOffset;

            targetRect.anchoredPosition = _startPos;
            
            currentSequence = DOTween.Sequence();

            currentSequence.Append(targetRect.DOAnchorPos(_endPos, valueSettings.duration)
                .SetEase(valueSettings.moveEase));

            if (true == valueSettings.useFade)
            {
                if (null == canvasGroup)
                    canvasGroup = targetRect.GetComponent<CanvasGroup>();

                if (null == canvasGroup)
                    canvasGroup = GetComponent<CanvasGroup>();

                if (null != canvasGroup)
                {
                    SetAlpha(valueSettings.endAlpha);
                    currentSequence.Join(canvasGroup.DOFade(valueSettings.startAlpha, valueSettings.duration)
                        .SetEase(valueSettings.fadeEase));
                }
            }

            currentSequence.SetDelay(valueSettings.startDelay);
            currentSequence.OnStart(HandleStart);
            currentSequence.OnComplete(HandleComplete);
        }

        public void Stop()
        {
            if (null != currentSequence && true == currentSequence.IsActive())
                currentSequence.Kill();

            if (null != targetRect)
                targetRect.DOKill();
            
            if (null != canvasGroup)
                canvasGroup.DOKill();
        }

        public void ResetToInitialState()
        {
            if (null == targetRect)
                return;

            if (false == isCaptured)
                return;

            Stop();
            
            targetRect.anchoredPosition = initialState.position;
            
            if (null != canvasGroup)
                canvasGroup.alpha = initialState.alpha;
        }

        // //내부 로직 메서드
        private void CaptureInitialState()
        {
            if (null == targetRect)
                targetRect = GetComponent<RectTransform>();

            if (null == targetRect)
                return;

            if (true == isCaptured)
                return;

            initialState.position = targetRect.anchoredPosition;

            // CanvasGroup 찾기 시도 순서: 1.할당됨 -> 2.타겟 오브젝트 -> 3.현재 오브젝트
            if (null == canvasGroup)
                canvasGroup = targetRect.GetComponent<CanvasGroup>();
            
            if (null == canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();

            if (null != canvasGroup)
                initialState.alpha = canvasGroup.alpha;

            isCaptured = true;
        }

        private void SetAlpha(float _alpha)
        {
            if (null != canvasGroup)
                canvasGroup.alpha = _alpha;
        }

        private void HandleStart()
        {
            if (null != onStartCallback)
                onStartCallback.Invoke();
        }

        private void HandleComplete()
        {
            if (null != onCompleteCallback)
                onCompleteCallback.Invoke();
        }

        // //유니티 이벤트 함수
        private void OnDestroy()
        {
            Stop();
        }
    }
}
