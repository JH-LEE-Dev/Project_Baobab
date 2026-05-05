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

    [Header("Motion Settings")]
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private string showMotionTag = "CursorShow";
    [SerializeField] private string idleMotionTag = "CursorIdle";
    [SerializeField] private string hideMotionTag = "CursorHide";

    private CanvasGroup canvasGroup;
    private MotionEntry showMotionEntry;
    private MotionEntry idleMotionEntry;
    private MotionEntry hideMotionEntry;
    private Vector2 currentAnchoredPosition;
    private int motionVersion;
    private int showMotionVersion;
    private int hideMotionVersion;

    public Vector2 CursorSize => cursorSize;

#region 이 세개만 쓰면 됨

    public void Initialize(Vector2 _cursorSize)
    {
        cursorSize = _cursorSize;
        CacheReferences();
        ApplySize();
        HideImmediately();
    }


    // 이곳에 보여주렴.
    public void Show(RectTransform _target)
    {
        if (_target == null)
            return;

        CacheReferences();
        if (rootRectTransform == null)
            return;

        RectTransform parentRectTransform = rootRectTransform.parent as RectTransform;
        if (parentRectTransform == null)
            return;

        StopAndResetAllMotions();

        Vector3 targetWorldCenter = _target.TransformPoint(_target.rect.center);
        Vector2 localCenter = parentRectTransform.InverseTransformPoint(targetWorldCenter);

        currentAnchoredPosition = localCenter + anchoredOffset;
        rootRectTransform.anchoredPosition = currentAnchoredPosition;
        rootRectTransform.SetAsLastSibling();
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(true);

        int currentVersion = ++motionVersion;
        PlayShowMotion(currentVersion);
    }

    public void Hide()
    {
        if (gameObject.activeSelf == false)
            return;

        int currentVersion = ++motionVersion;
        StopAndResetMotion(showMotionEntry);
        StopAndResetMotion(idleMotionEntry);
        StopAndResetMotion(hideMotionEntry);

        rootRectTransform.anchoredPosition = currentAnchoredPosition;
        ApplySize();

        if (motionPlayer == null || string.IsNullOrEmpty(hideMotionTag))
        {
            HideImmediately();
            return;
        }

        hideMotionVersion = currentVersion;
        hideMotionEntry = motionPlayer.Play(hideMotionTag, _onComplete: CompleteHide, bReset: false);
    }


#endregion

    public void HideImmediately()
    {
        ++motionVersion;
        StopAndResetAllMotions();
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(false);
    }



    private void CacheReferences()
    {
        if (rootRectTransform == null)
            rootRectTransform = transform as RectTransform;

        if (cursorImage == null)
            cursorImage = GetComponent<Image>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (motionPlayer == null)
            motionPlayer = GetComponentInChildren<ObjectMotionPlayer>(true);
    }

    private void ApplySize()
    {
        if (rootRectTransform != null)
            rootRectTransform.sizeDelta = cursorSize;

        if (cursorImage != null)
        {
            cursorImage.raycastTarget = false;
            cursorImage.type = Image.Type.Sliced;
        }
    }

    private void PlayShowMotion(int _version)
    {
        if (motionPlayer == null || string.IsNullOrEmpty(showMotionTag))
        {
            PlayIdleMotion(_version);
            return;
        }

        gameObject.SetActive(true);
        showMotionVersion = _version;
        showMotionEntry = motionPlayer.Play(showMotionTag, _onComplete: PlayIdleMotion, bReset: false);
    }

    private void PlayIdleMotion()
    {
        PlayIdleMotion(showMotionVersion);
    }

    private void PlayIdleMotion(int _version)
    {
        if (_version != motionVersion)
            return;

        if (motionPlayer == null || string.IsNullOrEmpty(idleMotionTag) || gameObject.activeSelf == false)
            return;

        StopAndResetMotion(showMotionEntry);
        rootRectTransform.anchoredPosition = currentAnchoredPosition;
        ApplySize();
        idleMotionEntry = motionPlayer.Play(idleMotionTag, bReset: false);
    }

    private void CompleteHide()
    {
        CompleteHide(hideMotionVersion);
    }

    private void CompleteHide(int _version)
    {
        if (_version != motionVersion)
            return;

        hideMotionEntry = null;
        rootRectTransform.anchoredPosition = currentAnchoredPosition;
        ApplySize();
        SetAlpha(1f);
        gameObject.SetActive(false);
    }

    private void StopAndResetAllMotions()
    {
        StopAndResetMotion(showMotionEntry);
        StopAndResetMotion(idleMotionEntry);
        StopAndResetMotion(hideMotionEntry);
    }

    private void StopAndResetMotion(MotionEntry _entry)
    {
        if (motionPlayer == null || _entry == null)
            return;

        motionPlayer.SettingEntryMotion(_entry, true, true);
    }

    private void SetAlpha(float _alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = _alpha;
    }
}
