using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverSelectionTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Motion Settings")]
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private string hoverMotionTag = "UIHover";
    [SerializeField] private string unHoverMotionTag = "UIUnHover";
    [SerializeField] private bool resetCurrentMotionBeforePlay = false;

    private UISelectionCursor selectionCursor;
    private RectTransform targetRectTransform;
    private RectTransform cursorTargetRectTransform;
    private MotionEntry hoverMotionEntry;
    private MotionEntry unHoverMotionEntry;
    private bool isPointerHovering;

    public void Initialize(UISelectionCursor _selectionCursor)
    {
        Initialize(_selectionCursor, null, null);
    }

    public void Initialize(UISelectionCursor _selectionCursor, RectTransform _cursorTargetRectTransform, ObjectMotionPlayer _motionPlayer)
    {
        selectionCursor = _selectionCursor;
        cursorTargetRectTransform = _cursorTargetRectTransform;
        motionPlayer = _motionPlayer;
        CacheReferences();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        BeginHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EndHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HideCursorImmediately();
    }

    public void HideCursorImmediately()
    {
        isPointerHovering = false;

        if (selectionCursor != null)
            selectionCursor.HideImmediately();
    }

    public void RefreshHover(Vector2 screenPosition, Camera eventCamera = null)
    {
        CacheReferences();

        if (targetRectTransform == null)
            return;

        if (RectTransformUtility.RectangleContainsScreenPoint(targetRectTransform, screenPosition, eventCamera))
            BeginHover();
        else
            EndHover();
    }

    private void OnDisable()
    {
        HideCursorImmediately();
    }

    private void CacheReferences()
    {
        if (targetRectTransform == null)
            targetRectTransform = transform as RectTransform;

        if (motionPlayer == null)
            motionPlayer = GetComponentInChildren<ObjectMotionPlayer>(true);

        if (motionPlayer != null)
            motionPlayer.Initialize();
    }

    private void PlayHoverMotion()
    {
        if (motionPlayer == null || string.IsNullOrEmpty(hoverMotionTag))
            return;

        if (motionPlayer.IsPlaying(hoverMotionTag))
            return;

        ResetEntryMotion(unHoverMotionEntry);
        hoverMotionEntry = motionPlayer.Play(hoverMotionTag, bReset: resetCurrentMotionBeforePlay);
    }

    private void PlayUnHoverMotion()
    {
        if (motionPlayer == null || string.IsNullOrEmpty(unHoverMotionTag))
            return;

        ResetEntryMotion(hoverMotionEntry);
        unHoverMotionEntry = motionPlayer.Play(unHoverMotionTag, bReset: resetCurrentMotionBeforePlay);
    }

    private void EndHover()
    {
        if (isPointerHovering == false)
            return;

        isPointerHovering = false;

        if (selectionCursor != null)
            selectionCursor.Hide();

        PlayUnHoverMotion();
    }

    private void ResetEntryMotion(MotionEntry entry)
    {
        if (motionPlayer == null || entry == null)
            return;

        motionPlayer.SettingEntryMotion(entry, true, true);
    }

    private void BeginHover()
    {
        if (isPointerHovering)
            return;

        isPointerHovering = true;
        selectionCursor?.Show(cursorTargetRectTransform != null ? cursorTargetRectTransform : targetRectTransform);
        PlayHoverMotion();
    }
}
