using System;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverSelectionTarget : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler,
    ISelectHandler,
    IDeselectHandler
{
    public event Action PointerEnteredEvent;

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
    private bool isSelected;
    private bool isHoverActive;
    private Func<bool> isSelectionMode;

    public void Initialize(UISelectionCursor _selectionCursor)
    {
        Initialize(_selectionCursor, null, null);
    }

    public void Initialize(UISelectionCursor _selectionCursor, RectTransform _cursorTargetRectTransform, ObjectMotionPlayer _motionPlayer)
    {
        Initialize(_selectionCursor, _cursorTargetRectTransform, _motionPlayer, null);
    }

    public void Initialize(
        UISelectionCursor _selectionCursor,
        RectTransform _cursorTargetRectTransform,
        ObjectMotionPlayer _motionPlayer,
        Func<bool> _isSelectionMode)
    {
        selectionCursor = _selectionCursor;
        cursorTargetRectTransform = _cursorTargetRectTransform;
        motionPlayer = _motionPlayer;
        isSelectionMode = _isSelectionMode;
        CacheReferences();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerHovering = true;
        RefreshHoverState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerHovering = false;
        RefreshHoverState();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HideCursorImmediately();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (false == IsSelectionModeActive())
            return;

        isSelected = true;
        RefreshHoverState(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshHoverState();
    }

    public void ForceSelect()
    {
        if (false == IsSelectionModeActive())
            return;

        isSelected = true;
        RefreshHoverState(true);
    }

    public void HideCursorImmediately()
    {
        isPointerHovering = false;
        isSelected = false;
        isHoverActive = false;

        if (selectionCursor != null)
            selectionCursor.HideImmediately();
    }

    public void RefreshHover(Vector2 screenPosition, Camera eventCamera = null)
    {
        CacheReferences();

        if (targetRectTransform == null)
            return;

        isPointerHovering = RectTransformUtility.RectangleContainsScreenPoint(targetRectTransform, screenPosition, eventCamera);
        RefreshHoverState();
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

    private bool IsSelectionModeActive()
    {
        return null != isSelectionMode && true == isSelectionMode.Invoke();
    }

    private void RefreshHoverState(bool forceEnter = false)
    {
        bool shouldHover = isSelected || (isPointerHovering && false == IsSelectionModeActive());
        if (shouldHover == isHoverActive && false == (forceEnter && shouldHover))
            return;

        isHoverActive = shouldHover;

        if (shouldHover)
        {
            PointerEnteredEvent?.Invoke();
            selectionCursor?.Show(cursorTargetRectTransform != null ? cursorTargetRectTransform : targetRectTransform);
            PlayHoverMotion();
            return;
        }

        selectionCursor?.Hide();
        PlayUnHoverMotion();
    }

    private void ResetEntryMotion(MotionEntry entry)
    {
        if (motionPlayer == null || entry == null)
            return;

        motionPlayer.SettingEntryMotion(entry, true, true);
    }
}
