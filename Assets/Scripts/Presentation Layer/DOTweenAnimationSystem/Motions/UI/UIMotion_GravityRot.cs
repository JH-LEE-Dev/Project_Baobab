using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Events;

namespace PresentationLayer.DOTweenAnimationSystem
{
    /// <summary>
    /// UI 요소에 중력 효과와 회전 연출을 적용하는 독립 컴포넌트입니다.
    /// </summary>
    public class UIMotion_GravityRot : MonoBehaviour
    {
        [System.Serializable]
        public class ValueSettings
        {
            public float duration = 0.5f;
            public float jumpPower = 30f;
            public float jumpRangeX = 30f;
            public float jumpRangeY = 30f;
            public float rotationAngle = 512f;
            public Ease jumpEase = Ease.Linear;
            public Ease rotationEase = Ease.OutQuad;
            public Ease fadeEase = Ease.InQuint;
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
        [SerializeField] private Image targetImage;

        [Header("Value Settings")]
        [SerializeField] private ValueSettings valueSettings;

        // //내부 의존성
        private InitialState initialState;
        private Sequence currentSequence;
        private UnityAction onCompleteCallback;

        public void Play(UnityAction _onComplete)
        {
            if (null == targetRect)
                return;

            onCompleteCallback = _onComplete;

            Stop();
            CaptureInitialState();

            currentSequence = DOTween.Sequence();

            Vector2 _endPos = new Vector2(
                initialState.position.x + Random.Range(-valueSettings.jumpRangeX, valueSettings.jumpRangeX),
                initialState.position.y + valueSettings.jumpRangeY
            );

            currentSequence.Join(targetRect.DOJumpAnchorPos(_endPos, valueSettings.jumpPower, 1, valueSettings.duration)
                .SetEase(valueSettings.jumpEase));

            currentSequence.Join(targetRect.DORotate(new Vector3(0f, 0f, valueSettings.rotationAngle), valueSettings.duration, RotateMode.FastBeyond360)
                .SetEase(valueSettings.rotationEase));

            //currentSequence.Join(targetRect.DOScale(Vector3.zero, valueSettings.duration)
            //    .SetEase(valueSettings.fadeEase));

            if (null != targetImage)
                currentSequence.Join(targetImage.DOFade(0f, valueSettings.duration)
                    .SetEase(valueSettings.fadeEase));

            currentSequence.OnComplete(HandleComplete);
        }

        public void Stop()
        {
            if (null != currentSequence && true == currentSequence.IsActive())
                currentSequence.Kill();

            if (null != targetRect)
                targetRect.DOKill();

            if (null != targetImage)
                targetImage.DOKill();
        }

        public void ResetToInitialState()
        {
            if (null == targetRect)
                return;

            targetRect.anchoredPosition = initialState.position;
            targetRect.localEulerAngles = initialState.rotation;
            targetRect.localScale = initialState.scale;

            if (null == targetImage)
                return;

            Color _color = targetImage.color;
            _color.a = initialState.alpha;
            targetImage.color = _color;
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

            initialState.position = targetRect.anchoredPosition;
            initialState.rotation = targetRect.localEulerAngles;
            initialState.scale = targetRect.localScale;

            if (null != targetImage)
                initialState.alpha = targetImage.color.a;
        }

        private void HandleComplete()
        {
            ResetToInitialState();

            if (null != onCompleteCallback)
                onCompleteCallback.Invoke();
        }
    }
}
