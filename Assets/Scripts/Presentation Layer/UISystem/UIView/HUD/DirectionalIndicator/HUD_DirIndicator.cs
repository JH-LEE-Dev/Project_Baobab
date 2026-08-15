using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PresentationLayer.UISystem.UIView.HUD.DirectionalIndicator
{
    /// <summary>
    /// 화면 밖에 있는 특정 3D 타겟의 방향을 추적하여 UI 경계선에 정밀하게 표시해주고,
    /// 화면 진입 시 타겟 머리 위에 안착 후 일정 시간 뒤 부드럽게 사라지는 인디케이터 클래스입니다.
    /// 16:9 및 16:10 등 다중 화면 비율과 해상도(360p, 1080p, 4K 등)를 완벽하게 지원합니다.
    /// </summary>
    public class HUD_DirIndicator : MonoBehaviour
    {
        private enum EIndicatorDisplayState
        {
            Hidden,
            Offscreen,
            OnscreenHolding,
            Disappearing
        }

        // //외부 의존성
        [Header("UI Components")]
        [SerializeField] private Image indicatorImage;         // 화살표 이미지를 렌더링할 Image 컴포넌트
        [SerializeField] private RectTransform rectTransform;  // 인디케이터 자신의 RectTransform
        [SerializeField] private CanvasGroup canvasGroup;      // 알파 페이드 제어용 CanvasGroup

        [Header("Directional Sprites")]
        [SerializeField] private Sprite upArrowSprite;
        [SerializeField] private Sprite downArrowSprite;
        [SerializeField] private Sprite leftArrowSprite;
        [SerializeField] private Sprite rightArrowSprite;

        [Header("Settings - Resolution & Scale")]
        [SerializeField] private float referenceScreenHeight = 1080f; // 기준 해상도 높이 (1080p 기준 동적 스케일링)

        [Header("Settings - Boundary & Idle (1080p 기준)")]
        [SerializeField] private float padding = 50f;                 // 화면 테두리 밀착 여백 (패딩)
        [SerializeField] private float idleSpeed = 5.0f;              // Idle 왕복 운동 주기 속도
        [SerializeField] private float idleAmplitude = 4.0f;          // Idle 왕복 운동 진폭 (픽셀)

        [Header("Settings - OnScreen Target Tracking (1080p 기준)")]
        [SerializeField] private float onScreenHoldDuration = 2.0f;   // 화면 진입 시 머무는 시간 (초)
        [SerializeField] private float onScreenYOffset = 70.0f;       // 화면 내 차량 머리 위 Y 오프셋 (픽셀)
        [SerializeField] private float onScreenSafeMargin = 50.0f;    // 좌/우/하단 안전 진입 마진 (픽셀)
        [SerializeField] private float onScreenTopMargin = 90.0f;     // 상단 안전 진입 마진 (차량 높이 + 화살표 여백, 픽셀)
        [SerializeField] private float exitBuffer = 20.0f;            // 경계선 떨림 방지용 히스테리시스 버퍼 (픽셀)

        [Header("Settings - Animation")]
        [SerializeField] private float appearDuration = 0.35f;        // 등장 연출 시간
        [SerializeField] private float disappearDuration = 0.25f;     // 퇴장 연출 시간
        [SerializeField] private Ease appearScaleEase = Ease.OutBack;
        [SerializeField] private Ease appearFadeEase = Ease.OutQuad;
        [SerializeField] private Ease disappearScaleEase = Ease.InBack;
        [SerializeField] private Ease disappearFadeEase = Ease.InQuad;

        // //내부 의존성 및 상태
        private Transform targetTransform;
        private Camera mainCamera;
        private RectTransform parentRect;
        private Canvas parentCanvas;

        private bool isInitialized = false;
        private bool isActivated = false;

        private EIndicatorDisplayState displayState = EIndicatorDisplayState.Hidden;
        private float currentOnScreenHoldTimer = 0f;

        private TweenCallback showCallback;
        private TweenCallback onDisappearCompleteCallback;
        private Tween delayShowTween;
        private Sequence activeMotionSeq;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 추적할 3D 타겟 Transform을 바인딩하고 시스템을 초기화합니다.
        /// </summary>
        public void Initialize(Transform _target = null)
        {
            targetTransform = _target;
            mainCamera = CameraFinder.Instance.PPMainCamera;

            if (null == rectTransform)
                rectTransform = GetComponent<RectTransform>();

            if (null == canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();
            if (null == canvasGroup)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            parentRect = transform.parent as RectTransform;
            parentCanvas = GetComponentInParent<Canvas>();

            showCallback = OnShow;
            onDisappearCompleteCallback = OnDisappearComplete;

            isInitialized = true;
            isActivated = false;
            displayState = EIndicatorDisplayState.Hidden;

            gameObject.SetActive(false);

            if (null != indicatorImage)
                indicatorImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// N초 후에 비활성화를 풀고 활성화합니다.
        /// </summary>
        public void ShowAfterDelay(float _delay)
        {
            if (null != delayShowTween && true == delayShowTween.IsActive())
                delayShowTween.Kill();

            if (0f < _delay)
                delayShowTween = DOVirtual.DelayedCall(_delay, showCallback).SetEase(Ease.Linear);
            else
                OnShow();
        }

        /// <summary>
        /// 동적으로 추적 대상을 교체하거나 해제하고 싶을 때 사용합니다.
        /// </summary>
        public void SetTarget(Transform _newTarget)
        {
            targetTransform = _newTarget;

            if (null == _newTarget)
            {
                displayState = EIndicatorDisplayState.Hidden;
                KillMotionTweens();

                if (null != indicatorImage)
                    indicatorImage.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (false == isActivated || false == isInitialized || null == targetTransform)
            {
                if (EIndicatorDisplayState.Hidden != displayState)
                {
                    displayState = EIndicatorDisplayState.Hidden;
                    KillMotionTweens();
                    if (null != indicatorImage)
                        indicatorImage.gameObject.SetActive(false);
                }
                return;
            }

            if (null == mainCamera)
                mainCamera = CameraFinder.Instance.PPMainCamera;

            if (null == mainCamera)
                return;

            float _screenWidth = Screen.width;
            float _screenHeight = Screen.height;

            // 1. 현재 화면 해상도 비율(1080p 기준 스케일) 산출 (16:9, 16:10 등 모든 해상도 완벽 대응)
            float _resScale = (0f < referenceScreenHeight) ? (_screenHeight / referenceScreenHeight) : 1f;
            float _scaledPadding = padding * _resScale;
            float _scaledSafeMargin = onScreenSafeMargin * _resScale;
            float _scaledTopMargin = onScreenTopMargin * _resScale;
            float _scaledExitBuffer = exitBuffer * _resScale;
            float _scaledYOffset = onScreenYOffset * _resScale;
            float _scaledIdleAmplitude = idleAmplitude * _resScale;

            // 2. 차량의 실제 3D 월드 좌표를 스크린 공간 좌표로 투영 (순수 Transform 위치 1:1 조준)
            Vector3 _targetWorldPos = targetTransform.position;
            Vector3 _screenPos = mainCamera.WorldToScreenPoint(_targetWorldPos);

            // 카메라 등 뒤에 있는 경우 방향 반전 보정
            if (0f >= _screenPos.z)
            {
                Vector3 _diff = _targetWorldPos - mainCamera.transform.position;
                Vector3 _projected = mainCamera.transform.position - _diff.normalized * 10f;
                _screenPos = mainCamera.WorldToScreenPoint(_projected);
            }

            // 3. 화면 내 진입 판정 (히스테리시스 버퍼 적용으로 경계선 깜빡임 방지)
            bool _isInside = false;
            if (EIndicatorDisplayState.OnscreenHolding == displayState || EIndicatorDisplayState.Disappearing == displayState)
            {
                // 이미 화면 안에 있거나 퇴장 중일 때는 이탈 버퍼를 적용해 더 바깥쪽으로 나가야 Offscreen 판정
                float _exitMarginSide = Mathf.Max(0f, _scaledSafeMargin - _scaledExitBuffer);
                float _exitMarginTop = Mathf.Max(0f, _scaledTopMargin - _scaledExitBuffer);

                _isInside = (0f < _screenPos.z)
                    && (_exitMarginSide <= _screenPos.x) && (_screenPos.x <= (_screenWidth - _exitMarginSide))
                    && (_exitMarginSide <= _screenPos.y) && (_screenPos.y <= (_screenHeight - _exitMarginTop));
            }
            else
            {
                // 화면 밖에서 안으로 들어올 때의 진입 마진 판정
                _isInside = (0f < _screenPos.z)
                    && (_scaledSafeMargin <= _screenPos.x) && (_screenPos.x <= (_screenWidth - _scaledSafeMargin))
                    && (_scaledSafeMargin <= _screenPos.y) && (_screenPos.y <= (_screenHeight - _scaledTopMargin));
            }

            float _finalX = 0f;
            float _finalY = 0f;

            if (false == _isInside)
            {
                // --- 상태 1: 화면 밖 (Offscreen) ---
                if (EIndicatorDisplayState.Offscreen != displayState)
                {
                    displayState = EIndicatorDisplayState.Offscreen;
                    PlayAppearMotion();
                }

                // 화면 중심에서 차량 Transform을 향하는 방향 벡터
                Vector2 _screenCenter = new Vector2(_screenWidth * 0.5f, _screenHeight * 0.5f);
                Vector2 _dir = (Vector2)_screenPos - _screenCenter;

                if (Mathf.Approximately(_dir.x, 0f) && Mathf.Approximately(_dir.y, 0f))
                {
                    _dir = Vector2.up;
                }

                float _halfW = Mathf.Max(5f, _screenWidth * 0.5f - _scaledPadding);
                float _halfH = Mathf.Max(5f, _screenHeight * 0.5f - _scaledPadding);

                float _scaleX = (0.0001f < Mathf.Abs(_dir.x)) ? (_halfW / Mathf.Abs(_dir.x)) : float.MaxValue;
                float _scaleY = (0.0001f < Mathf.Abs(_dir.y)) ? (_halfH / Mathf.Abs(_dir.y)) : float.MaxValue;
                float _scale = Mathf.Min(_scaleX, _scaleY);

                float _snapIdleOffset = Mathf.Sin(Time.time * idleSpeed) * _scaledIdleAmplitude;

                if (_scaleY == _scale)
                {
                    // 상/하단 경계: 차량의 실제 X좌표와 1:1 직교 정렬
                    _finalX = Mathf.Clamp(_screenPos.x, _scaledPadding, _screenWidth - _scaledPadding);

                    if (0f < _dir.y)
                    {
                        // 화면 상단에서 위를 가리킴
                        _finalY = _screenHeight - _scaledPadding - _snapIdleOffset;
                        if (null != indicatorImage && upArrowSprite != indicatorImage.sprite)
                            indicatorImage.sprite = upArrowSprite;
                    }
                    else
                    {
                        // 화면 하단에서 아래를 가리킴
                        _finalY = _scaledPadding + _snapIdleOffset;
                        if (null != indicatorImage && downArrowSprite != indicatorImage.sprite)
                            indicatorImage.sprite = downArrowSprite;
                    }
                }
                else
                {
                    // 좌/우측 경계: 차량의 실제 Y좌표와 1:1 직교 정렬
                    _finalY = Mathf.Clamp(_screenPos.y, _scaledPadding, _screenHeight - _scaledPadding);

                    if (0f < _dir.x)
                    {
                        // 화면 우측에서 오른쪽을 가리킴
                        _finalX = _screenWidth - _scaledPadding - _snapIdleOffset;
                        if (null != indicatorImage && rightArrowSprite != indicatorImage.sprite)
                            indicatorImage.sprite = rightArrowSprite;
                    }
                    else
                    {
                        // 화면 좌측에서 왼쪽을 가리킴
                        _finalX = _scaledPadding + _snapIdleOffset;
                        if (null != indicatorImage && leftArrowSprite != indicatorImage.sprite)
                            indicatorImage.sprite = leftArrowSprite;
                    }
                }
            }
            else
            {
                // --- 상태 2: 화면 안 (OnscreenHolding) ---
                if (EIndicatorDisplayState.Offscreen == displayState)
                {
                    displayState = EIndicatorDisplayState.OnscreenHolding;
                    currentOnScreenHoldTimer = onScreenHoldDuration;
                }

                if (EIndicatorDisplayState.OnscreenHolding == displayState)
                {
                    currentOnScreenHoldTimer -= Time.deltaTime;
                    if (0f >= currentOnScreenHoldTimer)
                    {
                        PlayDisappearMotion();
                    }
                }

                if (EIndicatorDisplayState.Hidden == displayState)
                {
                    return;
                }

                // 화면 내 차량 머리 위(ScreenPos.y + YOffset)에 화살표 안착
                float _snapIdleOffset = Mathf.Sin(Time.time * idleSpeed) * _scaledIdleAmplitude;
                _finalX = Mathf.Clamp(_screenPos.x, _scaledPadding, _screenWidth - _scaledPadding);
                _finalY = Mathf.Clamp(_screenPos.y + _scaledYOffset + _snapIdleOffset, _scaledPadding, _screenHeight - _scaledPadding);

                if (null != indicatorImage && downArrowSprite != indicatorImage.sprite)
                {
                    indicatorImage.sprite = downArrowSprite;
                }
            }

            // 4. 픽셀 격자 스냅 및 캔버스 렌더 모드 대응 좌표 대입
            ApplyPosition(_finalX, _finalY);
        }

        private void ApplyPosition(float _screenX, float _screenY)
        {
            if (null == rectTransform)
                return;

            if (null == parentRect)
                parentRect = transform.parent as RectTransform;

            if (null != parentRect)
            {
                if (null == parentCanvas)
                    parentCanvas = GetComponentInParent<Canvas>();

                Camera _uiCamera = (null != parentCanvas) ? parentCanvas.worldCamera : null;
                Vector2 _screenPoint = new Vector2(Mathf.Round(_screenX), Mathf.Round(_screenY));

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, _screenPoint, _uiCamera, out Vector2 _localPoint))
                {
                    rectTransform.anchoredPosition = _localPoint;
                }
            }
            else
            {
                rectTransform.position = new Vector3(Mathf.Round(_screenX), Mathf.Round(_screenY), 0f);
            }
        }

        private void PlayAppearMotion()
        {
            KillMotionTweens();

            if (null != indicatorImage)
                indicatorImage.gameObject.SetActive(true);

            if (null != canvasGroup)
                canvasGroup.alpha = 0f;

            transform.localScale = Vector3.zero;

            activeMotionSeq = DOTween.Sequence();
            if (null != canvasGroup)
                activeMotionSeq.Join(canvasGroup.DOFade(1f, appearDuration).SetEase(appearFadeEase));
            activeMotionSeq.Join(transform.DOScale(Vector3.one, appearDuration).SetEase(appearScaleEase));
        }

        private void PlayDisappearMotion()
        {
            KillMotionTweens();
            displayState = EIndicatorDisplayState.Disappearing;

            activeMotionSeq = DOTween.Sequence();
            if (null != canvasGroup)
                activeMotionSeq.Join(canvasGroup.DOFade(0f, disappearDuration).SetEase(disappearFadeEase));
            activeMotionSeq.Join(transform.DOScale(Vector3.zero, disappearDuration).SetEase(disappearScaleEase));
            activeMotionSeq.OnComplete(onDisappearCompleteCallback);
        }

        private void OnDisappearComplete()
        {
            displayState = EIndicatorDisplayState.Hidden;

            if (null != indicatorImage)
                indicatorImage.gameObject.SetActive(false);
        }

        private void KillMotionTweens()
        {
            if (null != activeMotionSeq && true == activeMotionSeq.IsActive())
            {
                activeMotionSeq.Kill();
                activeMotionSeq = null;
            }
        }

        public void OnHide()
        {
            isActivated = false;
            displayState = EIndicatorDisplayState.Hidden;
            KillMotionTweens();

            if (null != indicatorImage)
                indicatorImage.gameObject.SetActive(false);

            gameObject.SetActive(false);
        }

        public void OnShow()
        {
            isActivated = true;
            displayState = EIndicatorDisplayState.Hidden;
            gameObject.SetActive(true);
        }

        // //유니티 이벤트 함수

        private void OnDisable()
        {
            if (null != delayShowTween && true == delayShowTween.IsActive())
                delayShowTween.Kill();

            KillMotionTweens();
        }

        private void OnDestroy()
        {
            if (null != delayShowTween && true == delayShowTween.IsActive())
                delayShowTween.Kill();

            KillMotionTweens();
        }
    }
}
