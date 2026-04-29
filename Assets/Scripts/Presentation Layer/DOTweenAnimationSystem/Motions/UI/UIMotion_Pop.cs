using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// UI 요소가 '뽕' 하고 나타나는 찰진 느낌의 연출을 담당하는 독립 컴포넌트입니다.
    /// </summary>
    public class UIMotion_Pop : MonoBehaviour
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Duration Settings")]
            public float duration = 0.3f;

            [Header("Scale Settings")]
            public float startScale = 0.5f;
            public Ease scaleEase = Ease.OutBack;

            [Header("Rotation Settings")]
            public Vector3 punchRotation = new Vector3(0f, 0f, 15f);
            public int punchVibrato = 10;
            public float punchElasticity = 1f;
        }

        private struct InitialState
        {
            public Vector2 position;
            public Vector3 rotation;
            public Vector3 scale;
        }

        // //외부 의존성
        [Header("Target Settings")]
        [SerializeField] private RectTransform targetRect;

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
        /// Pop 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="_onComplete">완료 후 호출될 콜백</param>
        public void Play(UnityAction _onComplete = null)
        {
            if (null == targetRect)
                return;

            onCompleteCallback = _onComplete;

            Stop();

            // 캡처된 스케일이 0일 경우 재캡처를 시도하고, 실패 시 Vector3.one으로 강제 보호
            if (0.001f > initialState.scale.sqrMagnitude)
            {
                CaptureInitialState();
                if (0.001f > initialState.scale.sqrMagnitude)
                    initialState.scale = Vector3.one;
            }

            // 초기화: 원래 스케일의 startScale 비율에서 시작
            targetRect.localScale = initialState.scale * valueSettings.startScale;
            targetRect.localEulerAngles = initialState.rotation;

            currentSequence = DOTween.Sequence();

            // 1. 크기 연출: 원래 캡처된 스케일로 복귀
            currentSequence.Append(targetRect.DOScale(initialState.scale, valueSettings.duration)
                .SetEase(valueSettings.scaleEase));
            // 2. 회전 연출 (Punch)
            currentSequence.Join(targetRect.DOPunchRotation(valueSettings.punchRotation, valueSettings.duration, valueSettings.punchVibrato, valueSettings.punchElasticity));

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

            // 스케일이 0일 때 캡처되면 애니메이션 종료 후 사라지므로 안전하게 Vector3.one 처리
            if (0.001f > targetRect.localScale.sqrMagnitude)
                initialState.scale = Vector3.one;
            else
                initialState.scale = targetRect.localScale;
        }

        private void HandleComplete()
        {
            ResetToInitialState();

            if (null != onCompleteCallback)
                onCompleteCallback.Invoke();
        }
    }
}
