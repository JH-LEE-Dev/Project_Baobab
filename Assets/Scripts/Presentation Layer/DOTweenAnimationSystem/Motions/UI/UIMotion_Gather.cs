using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;

namespace PresentationLayer.DOTweenAnimationSystem.Motions.UI
{
    /// <summary>
    /// UI 요소들이 자신의 위치에서 특정 지점(첫 번째 총알 등)으로 촤라락 모여드는 연출을 담당하는 독립 컴포넌트입니다.
    /// 월드 좌표를 사용하여 레이아웃 그룹 환경에서도 정확한 위치로 수렴합니다.
    /// </summary>
    public class UIMotion_Gather : MonoBehaviour
    {
        [System.Serializable]
        public class ValueSettings
        {
            [Header("Duration Settings")]
            public float duration = 0.25f;

            [Header("Movement Settings")]
            public Ease moveEase = Ease.InBack;
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

        private UnityAction onStartCallback;
        private UnityAction onCompleteCallback;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            CaptureInitialState();
        }

        /// <summary>
        /// 지정된 월드 목표 지점으로 모여드는 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="_delay">순차적 연출을 위한 대기 시간</param>
        /// <param name="_targetWorldPosition">모여들 월드 지점 (position)</param>
        /// <param name="_onStart">시작 시 호출될 콜백</param>
        /// <param name="_onComplete">완료 후 호출될 콜백</param>
        public void Play(float _delay, Vector3 _targetWorldPosition, UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == targetRect)
                return;

            onStartCallback = _onStart;
            onCompleteCallback = _onComplete;

            Stop();

            // 캡처 보호 로직 포함
            if (0.001f > initialState.scale.sqrMagnitude)
                CaptureInitialState();

            // 시작 상태 설정 (원래 로컬 위치 보장)
            targetRect.anchoredPosition = initialState.position;

            currentSequence = DOTween.Sequence();

            // 1. 월드 좌표를 기준으로 목표 지점으로 이동 (Move to Target Position)
            currentSequence.Append(targetRect.DOMove(_targetWorldPosition, valueSettings.duration)
                .SetEase(valueSettings.moveEase));

            currentSequence.SetDelay(_delay);
            currentSequence.OnStart(HandleStart);
            currentSequence.OnComplete(HandleComplete);
        }

        public void Stop()
        {
            if (null != currentSequence && true == currentSequence.IsActive())
                currentSequence.Kill();

            if (null != targetRect)
                targetRect.DOKill();
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

            // 이미 데이터가 캡처되어 있다면 중복 캡처 방지
            if (0.001f < initialState.scale.sqrMagnitude)
                return;

            initialState.position = targetRect.anchoredPosition;
            initialState.rotation = targetRect.localEulerAngles;
            initialState.scale = targetRect.localScale;
        }

        private void HandleComplete()
        {
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
