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

        [Header("Settings - Vehicle Offscreen Bounding Margins (1080p 기준)")]
        [SerializeField] private float vehicleWidthMargin = 50.0f;    // 차량 좌우 반폭 마진 (완전 이탈 판정용, 픽셀)
        [SerializeField] private float vehicleTopMargin = 80.0f;      // 차량 상단 지붕 마진 (하단 이탈 시 지붕까지 완전 이탈 판정용, 픽셀)
        [SerializeField] private float vehicleBottomMargin = 20.0f;   // 차량 하단 바닥 마진 (상단 이탈 시 바퀴까지 완전 이탈 판정용, 픽셀)

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
            float _scaledWidthMargin = vehicleWidthMargin * _resScale;
            float _scaledTopMargin = vehicleTopMargin * _resScale;
            float _scaledBottomMargin = vehicleBottomMargin * _resScale;
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

            // 3. 차량이 화면 시야에서 '완전히 100% 벗어났는지(Offscreen)' 판정:
            // - 왼쪽으로 나감: 차량의 우측 끝(피벗 + WidthMargin)이 화면 왼쪽(0)보다 작을 때
            // - 오른쪽으로 나감: 차량의 좌측 끝(피벗 - WidthMargin)이 화면 오른쪽(ScreenWidth)보다 클 때
            // - 위쪽으로 나감: 차량의 바닥 끝(피벗 - BottomMargin)이 화면 위쪽(ScreenHeight)보다 클 때
            // - 아래쪽으로 나감: 차량의 지붕 끝(피벗 + TopMargin)이 화면 아래쪽(0)보다 작을 때
            bool _isCompletelyOffscreen = (0f >= _screenPos.z)
                || (_screenPos.x + _scaledWidthMargin < 0f)
                || (_screenPos.x - _scaledWidthMargin > _screenWidth)
                || (_screenPos.y - _scaledBottomMargin > _screenHeight)
                || (_screenPos.y + _scaledTopMargin < 0f);

            float _finalX = 0f;
            float _finalY = 0f;

            if (true == _isCompletelyOffscreen)
            {
                // --- 상태 1: 화면 밖 (Offscreen - 차가 시야에서 100% 안 보일 때) ---
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
                // 4. 픽셀 격자 스냅 및 캔버스 렌더 모드 대응 좌표 대입
                ApplyPosition(_finalX, _finalY);
            }
            else
            {
                // --- 상태 2: 화면 안 (차량이 시야에 들어옴) -> 테두리에서 자연스럽게 퇴장 ---
                if (EIndicatorDisplayState.Offscreen == displayState)
                {
                    PlayDisappearMotion();
                }
            }
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
