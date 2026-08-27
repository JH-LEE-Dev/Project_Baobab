using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

public class UISelectionCursor : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform rootRectTransform;
    [SerializeField] private Image cursorImage;

    [Header("Size Settings")]
    [SerializeField] private Vector2 cursorSize = new Vector2(40f, 40f);
    [SerializeField] private Vector2 anchoredOffset = Vector2.zero;

    [Header("Built-in Motion Settings")]
    [SerializeField] private CursorMotionSettings motionSettings = new CursorMotionSettings();

    private CanvasGroup canvasGroup;

    private Sequence showSequence;
    private Sequence idleSequence;
    private Sequence hideSequence;

    private Vector2 currentAnchoredPosition;
    private int motionVersion = 0;
    private int showMotionVersion = 0;
    private int hideMotionVersion = 0;

    private TweenCallback onShowCompleteAction;
    private TweenCallback onHideCompleteAction;
    private TweenCallback setExpandedSizeAction;
    private TweenCallback setBaseSizeAction;
    private TweenCallback setContractedSizeAction;

    private Vector2 currentBaseSize;
    private Vector2 currentExpandedSize;
    private Vector2 currentContractedSize;
    private CursorMotionSettings activeMotionSettings;

    public Vector2 CursorSize => cursorSize;
    public CursorMotionSettings MotionSettings
    {
        get => motionSettings;
        set => motionSettings = value;
    }

    private void Awake()
    {
        CacheReferences();
        InitCallbacks();
    }

    private void OnDestroy()
    {
        KillAllSequences();
    }

    private void InitCallbacks()
    {
        if (null == onShowCompleteAction)
            onShowCompleteAction = OnShowComplete;

        if (null == onHideCompleteAction)
            onHideCompleteAction = OnHideComplete;

        if (null == setExpandedSizeAction)
            setExpandedSizeAction = ApplyExpandedSize;

        if (null == setBaseSizeAction)
            setBaseSizeAction = ApplyBaseSize;

        if (null == setContractedSizeAction)
            setContractedSizeAction = ApplyContractedSize;
    }

    #region Public APIs

    public void Initialize(Vector2 _cursorSize)
    {
        cursorSize = new Vector2(Mathf.Round(_cursorSize.x), Mathf.Round(_cursorSize.y));
        activeMotionSettings = motionSettings;
        CacheReferences();
        ApplySize();
        HideImmediately();
    }

    public void Show(RectTransform _target)
    {
        Show(_target, cursorSize);
    }

    public void Show(RectTransform _target, Vector2 _size)
    {
        if (null == _target)
            return;

        cursorSize = new Vector2(Mathf.Round(_size.x), Mathf.Round(_size.y));
        CacheReferences();
        if (null == rootRectTransform)
            return;

        RectTransform parentRectTransform = rootRectTransform.parent as RectTransform;
        if (null == parentRectTransform)
            return;

        Vector3 targetWorldCenter = _target.TransformPoint(_target.rect.center);
        Vector2 localCenter = parentRectTransform.InverseTransformPoint(targetWorldCenter);

        ShowAtAnchoredPosition(localCenter + anchoredOffset, _size, null);
    }

    public void ShowAtAnchoredPosition(Vector2 _anchoredPosition, Vector2 _size, CursorMotionSettings _customMotion = null)
    {
        cursorSize = new Vector2(Mathf.Round(_size.x), Mathf.Round(_size.y));
        CacheReferences();
        if (null == rootRectTransform)
            return;

        StopAndResetAllMotions();

        currentAnchoredPosition = GetPixelPerfectPosition(_anchoredPosition, cursorSize);
        rootRectTransform.anchoredPosition = currentAnchoredPosition;
        rootRectTransform.SetAsLastSibling();
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(true);

        int currentVersion = ++motionVersion;
        activeMotionSettings = null != _customMotion ? _customMotion : motionSettings;

        PlayBuiltInShowMotion(currentVersion, activeMotionSettings);
    }

    // Show/Hide 연출 없이 즉시 표시하고 Idle 모션만 유지한다.
    // 패드 가상 커서처럼 항상 노출되는 선택 커서에서 사용한다.
    public void ActivateIdleAtAnchoredPosition(Vector2 _anchoredPosition, Vector2 _size, CursorMotionSettings _customMotion = null)
    {
        cursorSize = new Vector2(Mathf.Round(_size.x), Mathf.Round(_size.y));
        CacheReferences();
        if (null == rootRectTransform)
            return;

        StopAndResetAllMotions();

        currentAnchoredPosition = GetPixelPerfectPosition(_anchoredPosition, cursorSize);
        rootRectTransform.anchoredPosition = currentAnchoredPosition;
        rootRectTransform.SetAsLastSibling();
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(true);

        int currentVersion = ++motionVersion;
        activeMotionSettings = null != _customMotion ? _customMotion : motionSettings;
        PlayBuiltInIdleMotion(currentVersion, activeMotionSettings);
    }

    public void SetAnchoredPosition(Vector2 _anchoredPosition)
    {
        currentAnchoredPosition = GetPixelPerfectPosition(_anchoredPosition, cursorSize);
        if (null != rootRectTransform)
        {
            rootRectTransform.anchoredPosition = currentAnchoredPosition;
        }
    }

    private Vector2 GetPixelPerfectPosition(Vector2 _pos, Vector2 _size)
    {
        int sizeX = Mathf.RoundToInt(_size.x);
        int sizeY = Mathf.RoundToInt(_size.y);

        float snapX = (sizeX % 2 == 0) ? Mathf.Round(_pos.x) : Mathf.Floor(_pos.x) + 0.5f;
        float snapY = (sizeY % 2 == 0) ? Mathf.Round(_pos.y) : Mathf.Floor(_pos.y) + 0.5f;

        return new Vector2(snapX, snapY);
    }

    public void Hide()
    {
        if (false == gameObject.activeSelf)
            return;

        int currentVersion = ++motionVersion;
        StopAndResetAllMotions();

        if (null != rootRectTransform)
        {
            rootRectTransform.anchoredPosition = currentAnchoredPosition;
        }
        ApplySize();

        PlayBuiltInHideMotion(currentVersion, activeMotionSettings);
    }

    public void HideImmediately()
    {
        ++motionVersion;
        StopAndResetAllMotions();
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(false);
    }

    #endregion

    #region Built-in DOTween Motions

    private void PlayBuiltInShowMotion(int _version, CursorMotionSettings _settings)
    {
        if (null == _settings || false == _settings.enableShowMotion)
        {
            PlayBuiltInIdleMotion(_version, _settings);
            return;
        }

        showMotionVersion = _version;
        KillSequence(ref showSequence);

        showSequence = DOTween.Sequence();
        showSequence.SetUpdate(true);

        // 1. Size Tween (Shrink -> Restore)
        Vector3 shrinkScale = Vector3.one * _settings.shrinkSizeScale;
        float shrinkDuration = _settings.showDuration * Mathf.Clamp01(_settings.shrinkTimeRatio);
        float restoreDuration = _settings.showDuration * Mathf.Clamp01(_settings.restoreTimeRatio);

        Sequence sizeSeq = DOTween.Sequence();
        Vector2 shrinkSize = cursorSize * _settings.shrinkSizeScale;
        sizeSeq.Append(rootRectTransform.DOSizeDelta(shrinkSize, shrinkDuration).SetEase(Ease.OutQuad));
        sizeSeq.Append(rootRectTransform.DOSizeDelta(cursorSize, restoreDuration).SetEase(_settings.sizeRestoreEase));
        showSequence.Join(sizeSeq);

        // 2. Rotation Tween (Damped Alternating Swing)
        Sequence rotSeq = DOTween.Sequence();
        float angle = Mathf.Abs(_settings.startAngle);
        int swingCount = Mathf.Max(_settings.swingCount, 1);
        float rotationDuration = _settings.showDuration * Mathf.Clamp01(_settings.rotationTimeRatio);
        float swingDuration = rotationDuration / (swingCount + 1);

        for (int i = 0; i < swingCount; i++)
        {
            float direction = (0 == i % 2) ? -1f : 1f;
            Vector3 targetRot = Vector3.forward * (angle * direction);
            rotSeq.Append(rootRectTransform.DOLocalRotate(targetRot, swingDuration, RotateMode.Fast).SetEase(_settings.rotationEase));
            angle *= Mathf.Clamp01(_settings.angleDamping);
        }
        rotSeq.Append(rootRectTransform.DOLocalRotate(Vector3.zero, swingDuration, RotateMode.Fast).SetEase(_settings.rotationEase));
        showSequence.Join(rotSeq);

        showSequence.OnComplete(onShowCompleteAction);
    }

    private void OnShowComplete()
    {
        PlayBuiltInIdleMotion(showMotionVersion, activeMotionSettings);
    }

    private void PlayBuiltInIdleMotion(int _version, CursorMotionSettings _settings)
    {
        if (_version != motionVersion)
            return;

        if (false == gameObject.activeSelf || null == _settings || false == _settings.enableIdleMotion)
            return;

        KillSequence(ref showSequence);
        KillSequence(ref idleSequence);

        if (null != rootRectTransform)
        {
            rootRectTransform.localEulerAngles = Vector3.zero;
            rootRectTransform.anchoredPosition = currentAnchoredPosition;
            rootRectTransform.localScale = Vector3.one;
            rootRectTransform.sizeDelta = cursorSize;
        }

        float stepDuration = Mathf.Max(_settings.idleCycleDuration / 4f, 0.0001f);
        idleSequence = DOTween.Sequence();
        idleSequence.SetUpdate(true);

        currentBaseSize = cursorSize;
        float sizeDeltaOffset = Mathf.Abs(_settings.idleSizeOffset) * 2f;
        currentExpandedSize = currentBaseSize + Vector2.one * sizeDeltaOffset;
        currentContractedSize = currentBaseSize - Vector2.one * sizeDeltaOffset;

        idleSequence.AppendCallback(setExpandedSizeAction);
        idleSequence.AppendInterval(stepDuration);
        idleSequence.AppendCallback(setBaseSizeAction);
        idleSequence.AppendInterval(stepDuration);
        idleSequence.AppendCallback(setContractedSizeAction);
        idleSequence.AppendInterval(stepDuration);
        idleSequence.AppendCallback(setBaseSizeAction);
        idleSequence.AppendInterval(stepDuration);
        idleSequence.SetLoops(-1, LoopType.Restart);
    }

    private void ApplyExpandedSize()
    {
        if (null != rootRectTransform)
            rootRectTransform.sizeDelta = currentExpandedSize;
    }

    private void ApplyBaseSize()
    {
        if (null != rootRectTransform)
            rootRectTransform.sizeDelta = currentBaseSize;
    }

    private void ApplyContractedSize()
    {
        if (null != rootRectTransform)
            rootRectTransform.sizeDelta = currentContractedSize;
    }

    private void PlayBuiltInHideMotion(int _version, CursorMotionSettings _settings)
    {
        if (null == _settings || false == _settings.enableHideMotion)
        {
            HideImmediately();
            return;
        }

        hideMotionVersion = _version;
        KillSequence(ref hideSequence);

        hideSequence = DOTween.Sequence();
        hideSequence.SetUpdate(true);

        Vector2 expandedSize = cursorSize + Vector2.one * Mathf.Abs(_settings.hideExpandOffset);
        hideSequence.Join(rootRectTransform.DOSizeDelta(expandedSize, _settings.hideDuration).SetEase(_settings.hideEase));

        if (null != canvasGroup)
        {
            hideSequence.Join(canvasGroup.DOFade(0f, _settings.hideDuration).SetEase(_settings.hideEase));
        }

        hideSequence.OnComplete(onHideCompleteAction);
    }

    private void OnHideComplete()
    {
        if (hideMotionVersion != motionVersion)
            return;

        StopAndResetAllMotions();
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(false);
    }

    #endregion

    #region Internal Helpers

    private void CacheReferences()
    {
        if (null == rootRectTransform)
            rootRectTransform = transform as RectTransform;

        if (null == cursorImage)
            cursorImage = GetComponent<Image>();

        if (null == canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();

        InitCallbacks();
    }

    private void ApplySize()
    {
        if (null != rootRectTransform)
        {
            rootRectTransform.sizeDelta = cursorSize;
            rootRectTransform.localScale = Vector3.one;
        }

        if (null != cursorImage)
        {
            cursorImage.raycastTarget = false;
            cursorImage.type = Image.Type.Sliced;
        }
    }

    private void StopAndResetAllMotions()
    {
        KillAllSequences();

        if (null != rootRectTransform)
        {
            rootRectTransform.localEulerAngles = Vector3.zero;
        }
    }

    private void KillAllSequences()
    {
        KillSequence(ref showSequence);
        KillSequence(ref idleSequence);
        KillSequence(ref hideSequence);
    }

    private void KillSequence(ref Sequence _seq)
    {
        if (null != _seq && true == _seq.IsActive())
        {
            _seq.Kill(false);
        }
        _seq = null;
    }

    private void SetAlpha(float _alpha)
    {
        if (null != canvasGroup)
            canvasGroup.alpha = _alpha;
    }

    #endregion
}
