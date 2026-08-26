using System;
using UnityEngine;

public class UIView_CursorBox : UIView, ICursorBoxUI
{
    [Header("Cursor Reference")]
    [SerializeField] private UISelectionCursor selectionCursor;

    [Header("Default Settings")]
    [SerializeField] private Vector2 defaultCursorSize = new Vector2(40f, 40f);
    [SerializeField] private Vector2 defaultPadding = Vector2.zero;

    private RectTransform myRectTransform;
    private Canvas myCanvas;
    private Camera myCanvasCamera;

    private RectTransform currentTarget;
    private Vector2 currentSize;
    private Vector2 currentOffset;
    private CursorMotionSettings currentCustomMotion;
    private bool isTrackingTarget = false;
    private Vector2 lastCalculatedLocalPos;

    public bool IsShowing => null != selectionCursor && true == selectionCursor.gameObject.activeSelf;
    public RectTransform CurrentTarget => currentTarget;
    public CursorMotionSettings MotionSettings
    {
        get => null != selectionCursor ? selectionCursor.MotionSettings : null;
        set
        {
            if (null != selectionCursor)
                selectionCursor.MotionSettings = value;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        // Overlay 캔버스의 자식이 되도록 설정
        bOverlay = true;
        CacheReferences();
    }

    public override void Initialize(UIViewContext ctx)
    {
        base.Initialize(ctx);
        CacheReferences();
    }

    public override void SetupUI()
    {
        base.SetupUI();
        // SetupUI()는 씬 전환(SceneChanged → Open<T> → Initialize)마다 호출된다.
        // myCanvas/myCanvasCamera는 null 가드로 캐싱되므로, 씬 재진입 시 구버전
        // Canvas 참조를 그대로 사용하게 된다. 여기서 명시적으로 초기화해 항상
        // 최신 부모 Canvas를 가리키도록 강제한다.
        myCanvas = null;
        myCanvasCamera = null;
        CacheReferences();
        if (null != selectionCursor)
        {
            selectionCursor.HideImmediately();
        }
    }

    protected override void OnHide()
    {
        HideImmediately();
        base.OnHide();
    }

    private void LateUpdate()
    {
        if (false == isTrackingTarget)
            return;

        if (null == currentTarget || false == currentTarget.gameObject.activeInHierarchy)
        {
            HideImmediately();
            return;
        }

        if (true == TryCalculateLocalPosition(currentTarget, out Vector2 localPoint))
        {
            Vector2 finalPos = localPoint + currentOffset;
            if (finalPos != lastCalculatedLocalPos)
            {
                lastCalculatedLocalPos = finalPos;
                if (null != selectionCursor)
                {
                    selectionCursor.SetAnchoredPosition(finalPos);
                }
            }
        }
    }

    #region ICursorBoxUI Implementation

    public void Show(RectTransform _target)
    {
        Vector2 size = defaultCursorSize;
        if (null != _target)
        {
            size = _target.rect.size + defaultPadding;
        }
        Show(_target, size, Vector2.zero, null);
    }

    public void Show(RectTransform _target, Vector2 _size)
    {
        Show(_target, _size, Vector2.zero, null);
    }

    public void Show(RectTransform _target, Vector2 _size, Vector2 _offset)
    {
        Show(_target, _size, _offset, null);
    }

    public void Show(RectTransform _target, Vector2 _size, Vector2 _offset, CursorMotionSettings _customMotion)
    {
        if (null == _target)
            return;

        CacheReferences();

        // Overlay Canvas 내에 다른 UI가 CursorBox보다 나중에 추가되면 형제 순서상 CursorBox를 덮어버릴 수 있으므로,
        // 표시될 때마다 항상 최상단(마지막 형제)으로 끌어올린다.
        transform.SetAsLastSibling();

        currentTarget = _target;
        currentSize = _size;
        currentOffset = _offset;
        currentCustomMotion = _customMotion;
        isTrackingTarget = true;

        if (true == TryCalculateLocalPosition(_target, out Vector2 localPoint))
        {
            lastCalculatedLocalPos = localPoint + currentOffset;
            if (null != selectionCursor)
            {
                selectionCursor.ShowAtAnchoredPosition(lastCalculatedLocalPos, currentSize, currentCustomMotion);
            }
        }
    }

    public void ShowScreenPosition(Vector2 _screenPosition, Vector2 _size)
    {
        CacheReferences();

        transform.SetAsLastSibling();

        isTrackingTarget = false;
        currentTarget = null;
        currentSize = _size;
        currentOffset = Vector2.zero;
        currentCustomMotion = null;

        Camera myCam = (null != myCanvas && RenderMode.ScreenSpaceOverlay != myCanvas.renderMode) ? myCanvasCamera : null;
        if (true == RectTransformUtility.ScreenPointToLocalPointInRectangle(myRectTransform, _screenPosition, myCam, out Vector2 localPoint))
        {
            lastCalculatedLocalPos = localPoint;
            if (null != selectionCursor)
            {
                selectionCursor.ShowAtAnchoredPosition(localPoint, _size, null);
            }
        }
    }

    public new void Hide()
    {
        isTrackingTarget = false;
        currentTarget = null;

        if (null != selectionCursor)
        {
            selectionCursor.Hide();
        }
    }

    public void Hide(RectTransform _target)
    {
        if (null != _target && _target == currentTarget)
        {
            Hide();
        }
    }

    public void HideImmediately()
    {
        isTrackingTarget = false;
        currentTarget = null;

        if (null != selectionCursor)
        {
            selectionCursor.HideImmediately();
        }
    }

    public bool IsTarget(RectTransform _target)
    {
        return null != _target && _target == currentTarget && true == IsShowing;
    }

    #endregion

    #region Coordinate Calculation

    private bool TryCalculateLocalPosition(RectTransform _target, out Vector2 _localPoint)
    {
        _localPoint = Vector2.zero;

        if (null == _target || null == myRectTransform)
            return false;

        // 1. 대상의 월드 중심점 획득
        Vector3 worldCenter = _target.TransformPoint(_target.rect.center);

        // 2. 대상 캔버스의 이벤트 카메라 획득 (Overlay 캔버스인 경우 null)
        Canvas targetCanvas = _target.GetComponentInParent<Canvas>();
        Camera targetCamera = null;
        if (null != targetCanvas && RenderMode.ScreenSpaceOverlay != targetCanvas.renderMode)
        {
            targetCamera = null != targetCanvas.worldCamera ? targetCanvas.worldCamera : Camera.main;
        }

        // 3. 월드 중심점을 화면 픽셀 좌표(Screen Point)로 변환
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, worldCenter);

        // 4. 화면 픽셀 좌표를 UIView_CursorBox의 로컬 좌표로 변환
        Camera myCam = (null != myCanvas && RenderMode.ScreenSpaceOverlay != myCanvas.renderMode) ? myCanvasCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(myRectTransform, screenPoint, myCam, out _localPoint);
    }

    private void CacheReferences()
    {
        if (null == myRectTransform)
            myRectTransform = transform as RectTransform;

        if (null == myCanvas)
            myCanvas = GetComponentInParent<Canvas>();

        if (null != myCanvas && null == myCanvasCamera)
            myCanvasCamera = myCanvas.worldCamera;

        if (null == selectionCursor)
            selectionCursor = GetComponentInChildren<UISelectionCursor>(true);
    }

    #endregion

    public override void OnDestroy()
    {
        HideImmediately();
        base.OnDestroy();
    }
}
