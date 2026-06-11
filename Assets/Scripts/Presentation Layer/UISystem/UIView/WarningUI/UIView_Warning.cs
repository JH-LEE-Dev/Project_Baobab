using System;
using System.Collections.Generic;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIView_Warning : UIView
{
    private const int WarningLocalizationJsonId = 4;
    private const int MainTextEntryId = 1;
    private const int SubTextEntryId = 2;
    private const float HiddenBGWidth = 0f;
    private const float WarningBGTargetAlpha = 0.95f;

    private IInventory characterInventory;

    public event Action DeActivateWarningUIEvent;

    [Header("UI References")]
    [SerializeField] private RectTransform warningBG;
    [SerializeField] private RectTransform mainText;
    [SerializeField] private RectTransform subText;
    [SerializeField] private RectTransform buttonRoot;
    [SerializeField] private Button okButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private RectTransform okButtonTouchArea;
    [SerializeField] private RectTransform cancelButtonTouchArea;
    [SerializeField] private UISelectionCursor selectionCursorPrefab;
    [SerializeField] private RectTransform selectionCursorParent;
    [SerializeField] private Vector2 selectionCursorSize = new Vector2(40f, 40f);

    [Header("Position Pivots")]
    [SerializeField] private RectTransform mainTextPivot;
    [SerializeField] private RectTransform subTextPivot;
    [SerializeField] private RectTransform buttonPivot;
    [SerializeField] private RectTransform soloTextPivot;

    [Header("Production Settings")]
    [SerializeField] private float bgOpenDuration = 0.25f;
    [SerializeField] private float bgTargetWidth = 700f;
    [SerializeField] private float bgPieceOpenDelay = 0.04f;
    [SerializeField] private float contentOpenDuration = 0.25f;
    [SerializeField] private float contentOpenInterval = 0.15f;
    [SerializeField] private float contentOpenYOffset = -20f;
    [SerializeField] private float closeDuration = 0.2f;
    [SerializeField] private Ease productionEase = Ease.OutCubic;

    public bool bApproved = false;

    private Sequence openSequence;
    private Sequence closeSequence;
    private CanvasGroup warningBGCanvasGroup;
    private CanvasGroup mainTextCanvasGroup;
    private CanvasGroup subTextCanvasGroup;
    private CanvasGroup buttonRootCanvasGroup;
    private CanvasGroup[] closeFadeCanvasGroups = Array.Empty<CanvasGroup>();
    private TMP_Text mainTMPText;
    private TMP_Text subTMPText;
    private WarningBGPiece[] warningBGPieces = Array.Empty<WarningBGPiece>();
    private UISelectionCursor selectionCursorInstance;
    private UIHoverSelectionTarget okHoverTarget;
    private UIHoverSelectionTarget cancelHoverTarget;
    private Button okTouchAreaButton;
    private Button cancelTouchAreaButton;
    private RectTransform okButtonVisual;
    private RectTransform cancelButtonVisual;
    private LocalizationManager localizationManager;
    private bool isClosing;

    private sealed class WarningBGPiece
    {
        public RectTransform rectTransform;
        public Graphic graphic;
        public float targetWidth;
        public float targetAlpha;
        public float delay;
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        localizationManager = _ctx?.localizationManager;
        if (localizationManager != null)
            localizationManager.OnLanguageChanged += RefreshLocalizedTexts;

        CacheCanvasGroups();
        CacheTextReferences();
        CacheButtonTouchAreas();
        RefreshLocalizedTexts();
        InitializeButtonHoverTargets();
        BindButtonEvents();
    }

    public void DependencyInjection(IInventory _characterInventory)
    {
        characterInventory = _characterInventory;
    }

    public override void Hide()
    {
        if (IsVisible == false || isClosing)
            return;

        PlayCloseProduction();
    }

    protected override void OnShow()
    {
        base.OnShow();
        bApproved = false;
        gameObject.SetActive(true);
        PlayOpenProduction();
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
        DeActivateWarningUI();
        bApproved = false;
    }

    public override void OnDestroy()
    {
        KillProductionSequences();
        UnbindButtonEvents();

        if (localizationManager != null)
            localizationManager.OnLanguageChanged -= RefreshLocalizedTexts;

        DeActivateWarningUIEvent = null;
        base.OnDestroy();
    }

    public void OnOKButtonClicked()
    {
        HideSelectionCursorImmediately();
        bApproved = true;
        Hide();
    }

    public void OnCancelButtonClicked()
    {
        HideSelectionCursorImmediately();
        bApproved = false;
        Hide();
    }

    private void CacheCanvasGroups()
    {
        if (warningBG == null)
            warningBG = FindChildRecursive(transform, "BGRoot") as RectTransform;

        warningBGCanvasGroup = GetOrAddCanvasGroup(warningBG);
        mainTextCanvasGroup = GetOrAddCanvasGroup(mainText);
        subTextCanvasGroup = GetOrAddCanvasGroup(subText);
        buttonRootCanvasGroup = GetOrAddCanvasGroup(buttonRoot);
        closeFadeCanvasGroups = new[] { warningBGCanvasGroup, mainTextCanvasGroup, subTextCanvasGroup, buttonRootCanvasGroup };
        CacheWarningBGPieces();
    }

    private void CacheTextReferences()
    {
        if (mainTMPText == null && mainText != null)
            mainTMPText = mainText.GetComponent<TMP_Text>();

        if (subTMPText == null && subText != null)
            subTMPText = subText.GetComponent<TMP_Text>();
    }

    private void RefreshLocalizedTexts()
    {
        SetLocalizedText(mainTMPText, MainTextEntryId);
        SetLocalizedText(subTMPText, SubTextEntryId);
    }

    private void SetLocalizedText(TMP_Text text, int entryId)
    {
        if (text == null || localizationManager == null)
            return;

        string localizedText = localizationManager.GetText(WarningLocalizationJsonId, entryId);
        if (string.IsNullOrEmpty(localizedText))
            return;

        text.text = localizedText;
    }

    private void PlayOpenProduction()
    {
        KillProductionSequences();
        CacheTargetSize();
        bool hasLogInInventory = HasLogInCharacterInventory();
        RectTransform mainTargetPivot = hasLogInInventory || soloTextPivot == null ? mainTextPivot : soloTextPivot;
        AssignRandomWarningBGDelays();
        float bgProductionDuration = GetWarningBGProductionDuration();
        float buttonStartTime = hasLogInInventory
            ? bgProductionDuration + (contentOpenInterval * 2f)
            : bgProductionDuration + contentOpenInterval;

        PrepareOpenState(hasLogInInventory, mainTargetPivot);
        EnableButtons();

        openSequence = DOTween.Sequence().SetUpdate(true);

        if (warningBGPieces.Length > 0)
        {
            SetCanvasGroupAlpha(warningBGCanvasGroup, 1f);
            InsertWarningBGOpenTweens();
        }
        else if (warningBG != null)
        {
            openSequence.Join(DOTween.To(GetWarningBGWidth, SetWarningBGWidth, bgTargetWidth, bgOpenDuration).SetEase(Ease.OutCubic));

            if (warningBGCanvasGroup != null)
                openSequence.Join(warningBGCanvasGroup.DOFade(1f, bgOpenDuration).SetEase(productionEase));
        }

        InsertContentOpenTween(mainText, mainTextCanvasGroup, mainTargetPivot, bgProductionDuration);

        if (hasLogInInventory)
            InsertContentOpenTween(subText, subTextCanvasGroup, subTextPivot, bgProductionDuration + contentOpenInterval);

        InsertContentOpenTween(buttonRoot, buttonRootCanvasGroup, buttonPivot, buttonStartTime);

    }

    private void CacheTargetSize()
    {
        if (warningBGPieces.Length > 0 && bgTargetWidth <= 0f)
            bgTargetWidth = warningBGPieces[0].targetWidth;

        if (warningBG != null && bgTargetWidth <= 0f)
            bgTargetWidth = warningBG.rect.width;
    }

    private void PrepareOpenState(bool showSubText, RectTransform mainTargetPivot)
    {
        if (warningBGPieces.Length > 0)
            SetWarningBGPiecesHidden();
        else if (warningBG != null)
            SetWarningBGWidth(HiddenBGWidth);

        SetSubTextActive(showSubText);
        SetCanvasGroupAlpha(warningBGCanvasGroup, 0f);
        SetCanvasGroupAlpha(mainTextCanvasGroup, 0f);
        SetCanvasGroupAlpha(subTextCanvasGroup, 0f);
        SetCanvasGroupAlpha(buttonRootCanvasGroup, 0f);

        SetContentToHiddenPosition(mainText, mainTargetPivot);

        if (showSubText)
            SetContentToHiddenPosition(subText, subTextPivot);

        SetContentToHiddenPosition(buttonRoot, buttonPivot);

        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(mainTextCanvasGroup, false);
        SetCanvasGroupRaycast(subTextCanvasGroup, false);
        SetCanvasGroupRaycast(buttonRootCanvasGroup, true);
    }

    private void SetContentToHiddenPosition(RectTransform target, RectTransform pivot)
    {
        if (target == null || pivot == null)
            return;

        target.localPosition = GetHiddenPosition(pivot);
    }

    private void SetSubTextActive(bool active)
    {
        if (subText != null && subText.gameObject.activeSelf != active)
            subText.gameObject.SetActive(active);
    }

    private void InsertContentOpenTween(RectTransform target, CanvasGroup canvasGroup, RectTransform pivot, float startTime)
    {
        if (target != null && pivot != null)
        {
            target.localPosition = GetHiddenPosition(pivot);
            openSequence.Insert(startTime, target.DOLocalMove(pivot.localPosition, contentOpenDuration)
                .SetEase(productionEase));
        }

        if (canvasGroup != null)
        {
            openSequence.Insert(startTime, canvasGroup.DOFade(1f, contentOpenDuration)
                .SetEase(productionEase));
        }
    }

    private Vector3 GetHiddenPosition(RectTransform pivot)
    {
        return pivot.localPosition + new Vector3(0f, contentOpenYOffset, 0f);
    }

    private bool HasLogInCharacterInventory()
    {
        if (characterInventory == null || characterInventory.inventorySlots == null)
            return false;

        IReadOnlyList<IInventorySlot> slots = characterInventory.inventorySlots;
        int slotCount = Mathf.Min(characterInventory.currentSlotCnt, slots.Count);

        for (int i = 0; i < slotCount; i++)
        {
            IInventorySlot slot = slots[i];
            if (slot == null || slot.count <= 0)
                continue;

            if (slot.itemData is ILogItemData)
                return true;

            if (HasTreeTypeCount(slot.treeTypeCounts))
                return true;
        }

        return false;
    }

    private bool HasTreeTypeCount(TreeTypeCount[] treeTypeCounts)
    {
        if (treeTypeCounts == null)
            return false;

        for (int i = 0; i < treeTypeCounts.Length; i++)
        {
            if (treeTypeCounts[i].treeType != TreeType.None && treeTypeCounts[i].count > 0)
                return true;
        }

        return false;
    }

    private void CacheWarningBGPieces()
    {
        warningBGPieces = Array.Empty<WarningBGPiece>();

        if (warningBG == null)
            return;

        RectTransform[] rectTransforms = warningBG.GetComponentsInChildren<RectTransform>(true);
        List<WarningBGPiece> pieces = new List<WarningBGPiece>();

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform == warningBG || rectTransform.name.StartsWith("BG_", StringComparison.Ordinal) == false)
                continue;

            Graphic graphic = rectTransform.GetComponent<Graphic>();
            if (graphic == null)
                continue;

            pieces.Add(new WarningBGPiece
            {
                rectTransform = rectTransform,
                graphic = graphic,
                targetWidth = rectTransform.rect.width,
                targetAlpha = WarningBGTargetAlpha
            });
        }

        pieces.Sort((left, right) => string.CompareOrdinal(left.rectTransform.name, right.rectTransform.name));

        warningBGPieces = pieces.ToArray();
    }

    private float GetWarningBGProductionDuration()
    {
        if (warningBGPieces.Length <= 0)
            return bgOpenDuration;

        float lastDelay = 0f;
        for (int i = 0; i < warningBGPieces.Length; i++)
            lastDelay = Mathf.Max(lastDelay, warningBGPieces[i].delay);

        return bgOpenDuration + lastDelay;
    }

    private void InsertWarningBGOpenTweens()
    {
        if (warningBGPieces.Length > 0)
        {
            openSequence.Insert(0f, DOTween.To(
                () => GetWarningBGPiecesAlpha(),
                SetWarningBGPiecesAlpha,
                1f,
                bgOpenDuration).SetEase(Ease.Linear));
        }

        for (int i = 0; i < warningBGPieces.Length; i++)
        {
            WarningBGPiece piece = warningBGPieces[i];
            float targetWidth = bgTargetWidth > 0f ? bgTargetWidth : piece.targetWidth;

            openSequence.Insert(piece.delay, DOTween.To(
                () => GetWarningBGPieceWidth(piece),
                width => SetWarningBGPieceWidth(piece, width),
                targetWidth,
                bgOpenDuration).SetEase(Ease.OutCubic));
        }
    }

    private void AssignRandomWarningBGDelays()
    {
        if (warningBGPieces.Length <= 0)
            return;

        float[] delays = new float[warningBGPieces.Length];
        for (int i = 0; i < delays.Length; i++)
            delays[i] = i * bgPieceOpenDelay;

        for (int i = delays.Length - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            float temp = delays[i];
            delays[i] = delays[randomIndex];
            delays[randomIndex] = temp;
        }

        for (int i = 0; i < warningBGPieces.Length; i++)
            warningBGPieces[i].delay = delays[i];
    }

    private void SetWarningBGPiecesHidden()
    {
        for (int i = 0; i < warningBGPieces.Length; i++)
        {
            SetWarningBGPieceWidth(warningBGPieces[i], HiddenBGWidth);
            SetGraphicAlpha(warningBGPieces[i].graphic, 0f);
        }
    }

    private float GetWarningBGPieceWidth(WarningBGPiece piece)
    {
        return piece?.rectTransform != null ? piece.rectTransform.rect.width : 0f;
    }

    private void SetWarningBGPieceWidth(WarningBGPiece piece, float width)
    {
        if (piece?.rectTransform == null)
            return;

        piece.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private float GetWarningBGPiecesAlpha()
    {
        if (warningBGPieces.Length <= 0 || warningBGPieces[0].graphic == null)
            return 0f;

        float targetAlpha = Mathf.Max(warningBGPieces[0].targetAlpha, 0.0001f);
        return Mathf.Clamp01(warningBGPieces[0].graphic.color.a / targetAlpha);
    }

    private void SetWarningBGPiecesAlpha(float ratio)
    {
        for (int i = 0; i < warningBGPieces.Length; i++)
            SetGraphicAlpha(warningBGPieces[i].graphic, warningBGPieces[i].targetAlpha * ratio);
    }

    private void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private float GetWarningBGWidth()
    {
        return warningBG != null ? warningBG.rect.width : 0f;
    }

    private void SetWarningBGWidth(float width)
    {
        if (warningBG == null)
            return;

        warningBG.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void PlayCloseProduction()
    {
        KillProductionSequences();
        isClosing = true;
        HideSelectionCursorImmediately();
        SetButtonsInteractable(false);
        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(buttonRootCanvasGroup, false);

        closeSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < closeFadeCanvasGroups.Length; i++)
        {
            CanvasGroup canvasGroup = closeFadeCanvasGroups[i];
            if (canvasGroup != null)
                closeSequence.Join(canvasGroup.DOFade(0f, closeDuration).SetEase(productionEase));
        }

        closeSequence.OnComplete(OnCloseProductionComplete);
    }

    private void OnCloseProductionComplete()
    {
        closeSequence = null;
        isClosing = false;
        base.Hide();
    }

    private void KillProductionSequences()
    {
        if (openSequence != null)
        {
            openSequence.Kill();
            openSequence = null;
        }

        if (closeSequence != null)
        {
            closeSequence.Kill();
            closeSequence = null;
        }

        isClosing = false;
    }

    private CanvasGroup GetOrAddCanvasGroup(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return null;

        CanvasGroup canvasGroup = rectTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
    }

    private void SetCanvasGroupRaycast(CanvasGroup canvasGroup, bool enabled)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void EnableButtons()
    {
        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(mainTextCanvasGroup, false);
        SetCanvasGroupRaycast(subTextCanvasGroup, false);
        SetCanvasGroupRaycast(buttonRootCanvasGroup, true);
        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(bool enabled)
    {
        if (okTouchAreaButton != null)
            okTouchAreaButton.interactable = enabled;

        if (cancelTouchAreaButton != null)
            cancelTouchAreaButton.interactable = enabled;

        if (okButton != null)
            okButton.interactable = enabled;

        if (cancelButton != null)
            cancelButton.interactable = enabled;
    }

    private void CacheButtonTouchAreas()
    {
        if (okButtonTouchArea == null)
            okButtonTouchArea = FindChildRecursive(transform, "Button_OK_TouchArea") as RectTransform;

        if (cancelButtonTouchArea == null)
            cancelButtonTouchArea = FindChildRecursive(transform, "Button_Cancel_TouchArea") as RectTransform;

        okButtonVisual = GetButtonVisual(okButton, "Button_OK");
        cancelButtonVisual = GetButtonVisual(cancelButton, "Button_Cancel");
        okTouchAreaButton = EnsureTouchAreaButton(okButtonTouchArea);
        cancelTouchAreaButton = EnsureTouchAreaButton(cancelButtonTouchArea);

        SetButtonVisualRaycastTarget(okButtonVisual, false);
        SetButtonVisualRaycastTarget(cancelButtonVisual, false);
    }

    private RectTransform GetButtonVisual(Button button, string visualName)
    {
        if (button != null)
            return button.transform as RectTransform;

        RectTransform visual = FindChildRecursive(transform, visualName) as RectTransform;
        Button visualButton = visual != null ? visual.GetComponent<Button>() : null;

        if (visualName == "Button_OK")
            okButton = visualButton;
        else if (visualName == "Button_Cancel")
            cancelButton = visualButton;

        return visual;
    }

    private Button EnsureTouchAreaButton(RectTransform touchArea)
    {
        if (touchArea == null)
            return null;

        Image touchImage = touchArea.GetComponent<Image>();
        if (touchImage != null)
            touchImage.raycastTarget = true;

        Button button = touchArea.GetComponent<Button>();
        if (button == null)
            button = touchArea.gameObject.AddComponent<Button>();

        button.targetGraphic = touchImage;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private void SetButtonVisualRaycastTarget(RectTransform visual, bool enabled)
    {
        if (visual == null)
            return;

        Graphic[] graphics = visual.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = enabled;
    }

    private void InitializeButtonHoverTargets()
    {
        EnsureSelectionCursorInstance();

        okHoverTarget = InitializeHoverTarget(okButtonTouchArea, okButtonVisual);
        cancelHoverTarget = InitializeHoverTarget(cancelButtonTouchArea, cancelButtonVisual);
    }

    private UIHoverSelectionTarget InitializeHoverTarget(RectTransform touchArea, RectTransform visual)
    {
        if (touchArea == null)
            return null;

        UIHoverSelectionTarget hoverTarget = touchArea.GetComponent<UIHoverSelectionTarget>();
        if (hoverTarget == null)
            hoverTarget = touchArea.gameObject.AddComponent<UIHoverSelectionTarget>();

        RectTransform visualRectTransform = visual != null ? visual : touchArea;
        ObjectMotionPlayer motionPlayer = visual != null ? visual.GetComponentInChildren<ObjectMotionPlayer>(true) : null;
        hoverTarget.Initialize(selectionCursorInstance, visualRectTransform, motionPlayer);
        return hoverTarget;
    }

    private void EnsureSelectionCursorInstance()
    {
        if (selectionCursorInstance != null || selectionCursorPrefab == null)
            return;

        RectTransform parent = selectionCursorParent != null ? selectionCursorParent : transform as RectTransform;
        if (parent == null)
            return;

        selectionCursorInstance = Instantiate(selectionCursorPrefab, parent);
        selectionCursorInstance.Initialize(selectionCursorSize);
    }

    private void HideSelectionCursorImmediately()
    {
        if (selectionCursorInstance != null)
            selectionCursorInstance.HideImmediately();

        if (okHoverTarget != null)
            okHoverTarget.HideCursorImmediately();

        if (cancelHoverTarget != null)
            cancelHoverTarget.HideCursorImmediately();
    }

    private void BindButtonEvents()
    {
        if (okTouchAreaButton != null)
            okTouchAreaButton.onClick.AddListener(OnOKButtonClicked);

        if (cancelTouchAreaButton != null)
            cancelTouchAreaButton.onClick.AddListener(OnCancelButtonClicked);
    }

    private void UnbindButtonEvents()
    {
        if (okTouchAreaButton != null)
            okTouchAreaButton.onClick.RemoveListener(OnOKButtonClicked);

        if (cancelTouchAreaButton != null)
            cancelTouchAreaButton.onClick.RemoveListener(OnCancelButtonClicked);
    }

    private void DeActivateWarningUI()
    {
        DeActivateWarningUIEvent?.Invoke();
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }
}
