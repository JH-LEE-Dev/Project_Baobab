using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 스마트 스왑 대상 슬롯임을 안내하는 인디케이터 연출 전담 컴포넌트입니다.
/// CanvasGroup 알파 페이드와 RectTransform 스케일 뽀잉 등장 연출,
/// 대기 상태의 픽셀 단위 위아래 왕복 바운스 루프, 빠른 알파 퇴장 연출을 수행합니다.
/// </summary>
public class UI_SwapIndicator : MonoBehaviour
{
    private enum IndicatorState
    {
        Hidden,
        Appearing,
        Looping,
        Disappearing
    }

    // //외부 의존성
    [Header("Component References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform motionTarget;
    [SerializeField] private UI_KeyboardImage keyboardImage;
    [SerializeField] private Canvas indicatorCanvas;

    [Header("Sorting Settings")]
    [Tooltip("스왑 인디케이터가 슬롯 숫자 폰트(Order 10)보다 상위에 렌더링되도록 보장하는 소팅 레이어")]
    [SerializeField] private string sortingLayerName = "HUD";
    [Tooltip("스왑 인디케이터 소팅 오더 (슬롯 숫자 폰트 Order 10보다 상위)")]
    [SerializeField] private int sortingOrder = 20;

    [Header("Appear Motion Settings")]
    [Tooltip("등장 연출 지속 시간 (초)")]
    [SerializeField] private float appearDuration = 0.16f;
    [Tooltip("뽀잉 연출 시 도달할 최대 스케일 오버슈트 값")]
    [SerializeField] private float overshootScale = 1.35f;

    public enum BobbingStepMode
    {
        PingPong, // 0 -> -2 -> -4 -> -6 -> -4 -> -2 -> 0 (위아래 계단식 왕복)
        Jab       // 0 -> -2 -> -4 -> -6 -> 0 (아래로 콕콕콕 찌른 뒤 원위치 복귀)
    }

    [Header("Bobbing Motion Settings")]
    [Tooltip("화살표가 위아래로 가리키듯 뚝뚝뚝 왕복하는 픽셀 거리")]
    [SerializeField] private float bobDistance = 6.0f;
    [Tooltip("왕복 바운스 1회 주기 시간 (초) - 스무스 모드용")]
    [SerializeField] private float bobDuration = 0.35f;
    [Tooltip("연속 곡선(그래프) 대신 픽셀 단위로 딱딱 끊어지는 스텝 모션을 사용할지 여부")]
    [SerializeField] private bool useStepMotion = true;
    [Tooltip("하강 단계 수 (예: 3이면 3단계로 뚝뚝뚝 이동)")]
    [Range(1, 6)]
    [SerializeField] private int stepCount = 3;
    [Tooltip("스텝 간 정지 대기 시간 (초)")]
    [SerializeField] private float stepHoldDuration = 0.12f;
    [Tooltip("상단 기준 위치에서의 정지 대기 시간 (초)")]
    [SerializeField] private float topHoldDuration = 0.20f;
    [Tooltip("최하단 위치에서의 정지 대기 시간 (초)")]
    [SerializeField] private float bottomHoldDuration = 0.15f;
    [Tooltip("스텝 모드: PingPong(계단식 양방향 왕복), Jab(아래로 콕콕콕 후 원위치 리셋)")]
    [SerializeField] private BobbingStepMode stepMode = BobbingStepMode.PingPong;

    [Header("Disappear Motion Settings")]
    [Tooltip("퇴장 시 알파가 사라지는 지속 시간 (초)")]
    [SerializeField] private float disappearDuration = 0.12f;

    // //내부 의존성
    private const float STEP_SNAP_DURATION = 0.001f;

    // 기준 위치가 "실제로" 움직였다고 볼 최소 이동량(제곱 거리). 같은 자리 재갱신과 구분하기 위한 값이다.
    private const float BASE_MOVE_SQR_EPSILON = 0.0001f;

    private IndicatorState currentState = IndicatorState.Hidden;
    private Sequence currentSequence = null;
    private Vector2 baseAnchoredPosition = Vector2.zero;
    private bool isInitialized = false;

    // 무할당(Zero GC)을 위한 OnComplete 대리자 캐싱
    private TweenCallback cachedOnAppearComplete;
    private TweenCallback cachedOnDisappearComplete;

    private Coroutine sortingCoroutine = null;

    // //퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// 대상 게임오브젝트와 모든 자식 계층의 Layer를 지정된 레이어로 재귀 동기화합니다.
    /// </summary>
    private static void SetLayerRecursively(GameObject _obj, int _layer)
    {
        if (null == _obj)
            return;

        _obj.layer = _layer;
        Transform _t = _obj.transform;
        int _childCount = _t.childCount;
        for (int _i = 0; _childCount > _i; ++_i)
        {
            SetLayerRecursively(_t.GetChild(_i).gameObject, _layer);
        }
    }

    /// <summary>
    /// 인디케이터 및 키 바인딩 이미지를 초기화합니다.
    /// </summary>
    public void Initialize(InputManager _inputManager)
    {
        InitializeIfNeeded();

        if (null != transform.parent)
        {
            SetLayerRecursively(gameObject, transform.parent.gameObject.layer);
        }

        if (null != keyboardImage && null != _inputManager)
        {
            keyboardImage.Initialize(_inputManager);
        }
    }

    private void InitializeIfNeeded()
    {
        if (true == isInitialized)
            return;

        if (null == canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();

        if (null == motionTarget)
            motionTarget = GetComponent<RectTransform>();

        if (null == indicatorCanvas)
            indicatorCanvas = GetComponent<Canvas>();

        if (null != transform.parent)
        {
            SetLayerRecursively(gameObject, transform.parent.gameObject.layer);
        }

        BindCameraFinderEvent();
        ApplySorting();

        if (null != motionTarget)
            baseAnchoredPosition = motionTarget.anchoredPosition;

        isInitialized = true;
    }

    private void BindCameraFinderEvent()
    {
        if (null != CameraFinder.Instance)
        {
            CameraFinder.Instance.HandleCameraFindingEvent -= ApplySorting;
            CameraFinder.Instance.HandleCameraFindingEvent += ApplySorting;

            if (null != CameraFinder.Instance.PPMainCamera || null != CameraFinder.Instance.PPUiCamera)
            {
                ApplySorting();
            }
        }
    }

    private void UnbindCameraFinderEvent()
    {
        if (null != CameraFinder.Instance)
        {
            CameraFinder.Instance.HandleCameraFindingEvent -= ApplySorting;
        }
    }

    private void ApplySorting()
    {
        if (null == indicatorCanvas)
            return;

        if (true == gameObject.activeInHierarchy)
        {
            if (null != sortingCoroutine)
            {
                StopCoroutine(sortingCoroutine);
            }
            sortingCoroutine = StartCoroutine(ApplySortingRoutine());
        }
        else
        {
            ExecuteSortingDirect();
        }
    }

    private IEnumerator ApplySortingRoutine()
    {
        int _retryCount = 0;

        while (null != indicatorCanvas && 10 > _retryCount)
        {
            ApplyCanvasSortingInternal(indicatorCanvas, sortingLayerName, sortingOrder);

            if (true == indicatorCanvas.overrideSorting)
            {
                sortingCoroutine = null;
                yield break;
            }

            _retryCount++;
            yield return null;
        }

        sortingCoroutine = null;
    }

    private void ExecuteSortingDirect()
    {
        ApplyCanvasSortingInternal(indicatorCanvas, sortingLayerName, sortingOrder);
    }

    private static void ApplyCanvasSortingInternal(Canvas _canvas, string _layerName, int _order)
    {
        if (null == _canvas)
            return;

        Canvas _rootCanvas = _canvas.rootCanvas;
        if (null != _rootCanvas && (null != _rootCanvas.worldCamera || RenderMode.ScreenSpaceOverlay == _rootCanvas.renderMode))
        {
            _canvas.overrideSorting = true;
            _canvas.sortingLayerName = _layerName;
            _canvas.sortingOrder = _order;
        }
    }

    public bool IsActiveAndShowing => true == gameObject.activeInHierarchy && (IndicatorState.Appearing == currentState || IndicatorState.Looping == currentState);

    /// <summary>
    /// 인디케이터가 이미 활성화된 상태에서 부모 팝업의 오픈 연출 완료 등으로 슬롯의 최종 월드 좌표가 안착되었을 때,
    /// 애니메이션을 중단하지 않고 기준 위치(baseAnchoredPosition)만 정확하게 재동기화합니다.
    /// </summary>
    /// <param name="_worldPosition">대상 슬롯의 갱신된 월드 좌표</param>
    public void UpdateTargetPosition(Vector3 _worldPosition)
    {
        if (null == motionTarget)
            return;

        Vector2 _previousBase = baseAnchoredPosition;
        Vector2 _currentOffset = motionTarget.anchoredPosition - _previousBase;

        transform.position = _worldPosition;
        baseAnchoredPosition = motionTarget.anchoredPosition;

        if (IndicatorState.Looping != currentState)
        {
            motionTarget.anchoredPosition = baseAnchoredPosition;
            return;
        }

        // 기준 위치가 실제로 움직였다면 루프를 다시 만든다. 스텝 트윈은 시퀀스를 만들던 시점의
        // "절대" 앵커 좌표로 구워져 있어서, 기준만 바꾸고 두면 다음 스텝에서 옛 좌표로 되돌아가
        // 그 자리에 눌러앉는다(예: 상자 슬롯이 늘어 그리드가 2행이 되며 같은 칸의 Y가 바뀔 때).
        if (BASE_MOVE_SQR_EPSILON < (baseAnchoredPosition - _previousBase).sqrMagnitude)
        {
            StartBobbingLoop();
            return;
        }

        motionTarget.anchoredPosition = baseAnchoredPosition + _currentOffset;
    }

    /// <summary>
    /// 지정된 월드 좌표로 위치를 이동한 뒤 등장 연출을 시작합니다.
    /// </summary>
    /// <param name="_worldPosition">대상 슬롯 위치</param>
    public void Show(Vector3 _worldPosition)
    {
        InitializeIfNeeded();

        KillCurrentAnimation();
        currentState = IndicatorState.Hidden;

        // 1. 이전 잔여 프레임의 위치/스케일 깜빡임을 원천 차단하기 위해 알파와 스케일을 먼저 0으로 초기화
        if (null != canvasGroup)
        {
            canvasGroup.alpha = 0.0f;
        }

        if (null != motionTarget)
        {
            motionTarget.localScale = Vector3.zero;
        }

        // 2. 대상 월드 좌표 적용 및 기준 앵커 좌표 동기화
        transform.position = _worldPosition;

        if (null != motionTarget)
        {
            baseAnchoredPosition = motionTarget.anchoredPosition;
            motionTarget.anchoredPosition = baseAnchoredPosition;
        }

        // 3. 좌표와 초기 스케일/알파가 완전히 설정된 상태에서 활성화
        if (false == gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }

        Show();
    }

    /// <summary>
    /// 스왑 인디케이터를 쫀득하고 역동적인 뽀잉 연출과 함께 등장시키고, 완료 후 대기 루프를 시작합니다.
    /// </summary>
    public void Show()
    {
        InitializeIfNeeded();

        if (null != transform.parent)
        {
            SetLayerRecursively(gameObject, transform.parent.gameObject.layer);
        }

        ApplySorting();

        // 이미 등장 중이거나 루프 중인 경우 중복 실행 방지
        if (IndicatorState.Appearing == currentState || IndicatorState.Looping == currentState)
            return;

        KillCurrentAnimation();

        if (null == motionTarget || null == canvasGroup)
            return;

        if (false == gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }

        baseAnchoredPosition = motionTarget.anchoredPosition;

        currentState = IndicatorState.Appearing;

        // 초기 연출 상태 설정
        canvasGroup.alpha = 0.0f;
        motionTarget.localScale = Vector3.zero;
        motionTarget.anchoredPosition = baseAnchoredPosition;

        currentSequence = DOTween.Sequence().SetLink(gameObject);

        // 1. 알파 페이드인 (등장 전반부에 신속하게 1.0 도달)
        currentSequence.Join(canvasGroup.DOFade(1.0f, appearDuration * 0.6f).SetEase(Ease.OutQuad));

        // 2. 스케일 뽀잉 (0 -> 오버슈트 -> 수축 -> 1.0 완착)
        float _firstStage = appearDuration * 0.65f;
        float _secondStage = appearDuration * 0.2f;
        float _thirdStage = appearDuration * 0.15f;

        currentSequence.Join(motionTarget.DOScale(overshootScale, _firstStage).SetEase(Ease.OutQuad));
        currentSequence.Append(motionTarget.DOScale(0.9f, _secondStage).SetEase(Ease.InOutQuad));
        currentSequence.Append(motionTarget.DOScale(1.0f, _thirdStage).SetEase(Ease.OutBack));

        // 등장 완료 시 대기 루프로 전이
        currentSequence.OnComplete(cachedOnAppearComplete);
    }

    private void HandleAppearComplete()
    {
        StartBobbingLoop();
    }

    /// <summary>
    /// 픽셀 단위로 짧게 위아래로 뚝뚝뚝 왕복하며 대상을 가리키는 무한 루프 모션을 재생합니다.
    /// </summary>
    private void StartBobbingLoop()
    {
        if (null == motionTarget)
            return;

        currentState = IndicatorState.Looping;

        KillCurrentAnimation();

        motionTarget.localScale = Vector3.one;
        motionTarget.anchoredPosition = baseAnchoredPosition;

        currentSequence = DOTween.Sequence().SetLink(gameObject);

        if (true == useStepMotion)
        {
            BuildStepMotionSequence();
        }
        else
        {
            BuildSmoothMotionSequence();
        }

        currentSequence.SetLoops(-1);
    }

    private void BuildStepMotionSequence()
    {
        int _validSteps = Mathf.Max(1, stepCount);
        float _stepDistance = bobDistance / _validSteps;

        // 1. 상단 기본 위치 대기
        if (0.0f < topHoldDuration)
        {
            currentSequence.AppendInterval(topHoldDuration);
        }

        // 2. 하강 스텝 (1단계 ~ 마지막 직전 단계)
        for (int _i = 1; _validSteps > _i; ++_i)
        {
            float _y = baseAnchoredPosition.y - (_stepDistance * _i);
            currentSequence.Append(motionTarget.DOAnchorPosY(_y, STEP_SNAP_DURATION).SetEase(Ease.Linear));
            currentSequence.AppendInterval(stepHoldDuration);
        }

        // 3. 최하단 스텝 (콕 찌르기)
        float _bottomY = baseAnchoredPosition.y - bobDistance;
        currentSequence.Append(motionTarget.DOAnchorPosY(_bottomY, STEP_SNAP_DURATION).SetEase(Ease.Linear));
        currentSequence.AppendInterval(bottomHoldDuration);

        // 4. 복귀 스텝
        if (BobbingStepMode.PingPong == stepMode)
        {
            // 상향 스텝 (최하단 바로 위 ~ 1단계)
            for (int _i = _validSteps - 1; 1 <= _i; --_i)
            {
                float _y = baseAnchoredPosition.y - (_stepDistance * _i);
                currentSequence.Append(motionTarget.DOAnchorPosY(_y, STEP_SNAP_DURATION).SetEase(Ease.Linear));
                currentSequence.AppendInterval(stepHoldDuration);
            }

            // 원위치 복귀
            currentSequence.Append(motionTarget.DOAnchorPosY(baseAnchoredPosition.y, STEP_SNAP_DURATION).SetEase(Ease.Linear));
        }
        else
        {
            // Jab 모드: 최하단 찍고 원위치로 한 번에 복귀
            currentSequence.Append(motionTarget.DOAnchorPosY(baseAnchoredPosition.y, STEP_SNAP_DURATION).SetEase(Ease.Linear));
        }
    }

    private void BuildSmoothMotionSequence()
    {
        // 아래로 콕 찌르듯 내려갔다가(OutQuad), 빠르게 원래 위치로 복귀(InQuad)
        float _downDuration = bobDuration * 0.45f;
        float _upDuration = bobDuration * 0.55f;
        Vector2 _targetPos = new Vector2(baseAnchoredPosition.x, baseAnchoredPosition.y - bobDistance);

        currentSequence.Append(motionTarget.DOAnchorPos(_targetPos, _downDuration).SetEase(Ease.OutQuad));
        currentSequence.Append(motionTarget.DOAnchorPos(baseAnchoredPosition, _upDuration).SetEase(Ease.InQuad));
    }

    /// <summary>
    /// 알파가 찍 사라지듯 빠르게 페이드아웃되며 퇴장 연출을 수행합니다.
    /// </summary>
    public void Hide()
    {
        if (IndicatorState.Disappearing == currentState)
            return;

        if (IndicatorState.Hidden == currentState)
        {
            // 부모가 꺼지면서 OnDisable이 상태만 Hidden으로 되돌린 경우, 오브젝트는 아직 켜진 채로
            // 남아 있다. 그대로 두면 부모가 다시 켜질 때 옛 슬롯 위에 인디케이터가 그대로 떠오르고
            // 이후 Hide()로는 영영 지워지지 않으므로, 여기서 확실히 정리한다.
            if (true == gameObject.activeSelf)
                HideImmediate();

            return;
        }

        KillCurrentAnimation();

        if (null == canvasGroup || null == motionTarget)
        {
            HandleDisappearComplete();
            return;
        }

        currentState = IndicatorState.Disappearing;

        currentSequence = DOTween.Sequence().SetLink(gameObject);

        // 순수 알파 신속 페이드아웃 (스케일 축소 없이 원형 유지)
        currentSequence.Join(canvasGroup.DOFade(0.0f, disappearDuration).SetEase(Ease.InQuad));

        currentSequence.OnComplete(cachedOnDisappearComplete);
    }

    private void HandleDisappearComplete()
    {
        currentState = IndicatorState.Hidden;
        KillCurrentAnimation();

        if (null != motionTarget)
        {
            motionTarget.localScale = Vector3.one;
            motionTarget.anchoredPosition = baseAnchoredPosition;
        }

        if (null != canvasGroup)
        {
            canvasGroup.alpha = 0.0f;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 애니메이션 없이 즉시 인디케이터를 완전히 숨기고 초기 상태로 리셋합니다. (닫기 스킵용)
    /// </summary>
    public void HideImmediate()
    {
        InitializeIfNeeded();

        currentState = IndicatorState.Hidden;
        KillCurrentAnimation();

        if (null != motionTarget)
        {
            motionTarget.localScale = Vector3.one;
            motionTarget.anchoredPosition = baseAnchoredPosition;
        }

        if (null != canvasGroup)
        {
            canvasGroup.alpha = 0.0f;
        }

        gameObject.SetActive(false);
    }

    private void KillCurrentAnimation()
    {
        if (null != currentSequence && true == currentSequence.IsActive())
        {
            currentSequence.Kill(false);
        }

        currentSequence = null;
    }

    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Awake()
    {
        cachedOnAppearComplete = HandleAppearComplete;
        cachedOnDisappearComplete = HandleDisappearComplete;

        InitializeIfNeeded();
        HideImmediate();
    }

    private void OnDisable()
    {
        if (null != sortingCoroutine)
        {
            StopCoroutine(sortingCoroutine);
            sortingCoroutine = null;
        }

        KillCurrentAnimation();
        currentState = IndicatorState.Hidden;

        // 상태만 되돌리고 연출 값을 그대로 두면, 부모가 다시 켜질 때 페이드 중간 알파로 굳은
        // 인디케이터가 옛 자리에 나타난다. 숨김 상태와 화면을 항상 같이 맞춰 둔다.
        if (null != canvasGroup)
        {
            canvasGroup.alpha = 0.0f;
        }

        if (null != motionTarget)
        {
            motionTarget.localScale = Vector3.one;
            motionTarget.anchoredPosition = baseAnchoredPosition;
        }
    }

    private void OnDestroy()
    {
        UnbindCameraFinderEvent();

        if (null != sortingCoroutine)
        {
            StopCoroutine(sortingCoroutine);
            sortingCoroutine = null;
        }

        KillCurrentAnimation();
        cachedOnAppearComplete = null;
        cachedOnDisappearComplete = null;
    }
}
