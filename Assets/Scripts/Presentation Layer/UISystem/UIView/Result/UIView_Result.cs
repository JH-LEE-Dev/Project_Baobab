using System;
using System.Collections.Generic;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIView_Result : UIView
{
    private sealed class DisplayInventorySlot : IInventorySlot
    {
        public IItemData itemData { get; private set; }
        public int count { get; private set; }
        public LogStateCount[] logStateCounts { get; private set; }
        public TreeTypeCount[] treeTypeCounts { get; private set; }
        public bool HasChanged { get; private set; }

        event Action IInventorySlot.SlotUpdatedEvent
        {
            add { }
            remove { }
        }

        public DisplayInventorySlot()
        {
            logStateCounts = new LogStateCount[1];
            treeTypeCounts = CreateEmptyTreeTypeCounts();
        }

        public void SetData(IItemData sourceItemData, int[] counts)
        {
            IItemData previousItemData = itemData;
            int previousCount = count;
            bool changed = previousItemData != sourceItemData;

            int newCount = 0;

            if (counts == null)
                counts = Array.Empty<int>();

            for (int i = 0; i < treeTypeCounts.Length; i++)
            {
                TreeType treeType = treeTypeCounts[i].treeType;
                int treeTypeIndex = (int)treeType;
                int treeTypeCount = treeTypeIndex >= 0 && treeTypeIndex < counts.Length ? counts[treeTypeIndex] : 0;
                changed |= treeTypeCounts[i].count != treeTypeCount;
                treeTypeCounts[i].count = treeTypeCount;
                newCount += treeTypeCount;
            }

            itemData = sourceItemData;
            count = newCount;

            if (count <= 0)
                itemData = null;

            changed |= previousCount != count;
            changed |= previousItemData != itemData;

            logStateCounts[0].state = LogState.Normal;
            logStateCounts[0].count = count;
            HasChanged = changed;
        }

        public void CopyFrom(IInventorySlot source)
        {
            if (source == null)
            {
                SetData(null, null);
                return;
            }

            SetData(source.itemData, ExtractTreeTypeCounts(source));
        }

        private static TreeTypeCount[] CreateEmptyTreeTypeCounts()
        {
            TreeType[] treeTypes = (TreeType[])Enum.GetValues(typeof(TreeType));
            TreeTypeCount[] counts = new TreeTypeCount[treeTypes.Length];

            for (int i = 0; i < treeTypes.Length; i++)
            {
                counts[i].treeType = treeTypes[i];
                counts[i].count = 0;
            }

            return counts;
        }
    }

    private const int ResultLocalizationJsonId = 3;
    private const int ResultTitleEntryId = 1;
    private const int TreeKillCountZeroEntryId = 2;
    private const int TreeKillCountLowEntryId = 3;
    private const int TreeKillCountMiddleEntryId = 4;
    private const int TreeKillCountHighEntryId = 5;
    private const int AcquiredLogsHeaderEntryId = 6;
    private const int EmptyAcquiredLogsEntryId = 7;
    private const int ContainerStateHeaderEntryId = 8;

    private const string ResultTitleFallback = "\uC6D0\uC815 \uC644\uB8CC";
    private const string TreeKillCountZeroFallback = "<COLOR=FF833D>\uB098\uBB34</COLOR>\uB4E4\uC774 \uC624\uB298\uC740 \uBB34\uC0AC\uD588\uC5B4!";
    private const string TreeKillCountLowFallback = "{0}\uADF8\uB8E8\uC758 <COLOR=FF833D>\uB098\uBB34</COLOR>\uB97C \uBC8C\uBAA9\uD588\uC5B4";
    private const string TreeKillCountMiddleFallback = "{0}\uADF8\uB8E8\uC758 <COLOR=FF833D>\uB098\uBB34</COLOR>\uB97C \uBC8C\uBAA9\uD588\uC5B4!";
    private const string TreeKillCountHighFallback = "{0}\uADF8\uB8E8\uC758 <COLOR=FF833D>\uB098\uBB34</COLOR>\uB97C \uBC8C\uBAA9\uD588\uC5B4!!";
    private const string AcquiredLogsHeaderFallback = "\uC774\uBC88 \uC6D0\uC815\uC5D0\uC11C \uD68D\uB4DD\uD55C \uC6D0\uBAA9";
    private const string EmptyAcquiredLogsFallback = "\uC5C6\uC74C";
    private const string ContainerStateHeaderFallback = "\uC6B4\uBC18 \uC0C1\uC790 \uC0C1\uD0DC";
    private const float FontPopSoundInterval = 0.04f;
    private const float TreeKillCountSoundInterval = 0.05f;
    private const float TreeKillCountPitchStep = 0.02f;
    private const float TreeKillCountMaxPitch = 1.5f;
    private const float InventoryLogSoundInterval = 0.03f;

    public event Action GoHomeButtonClickedEvent;
    public event Action RetryButtonClickedEvent;

    private IInventory offroadContainer;
    private IDungeonResultProvider dungeonResultProvider;
    private LocalizationManager localizationManager;
    private bool bIsTutorial;

    [Header("UI References")]
    [SerializeField] private Button goHomeButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private RectTransform goHomeButtonTouchArea;
    [SerializeField] private RectTransform retryButtonTouchArea;
    [SerializeField] private GameObject resultContentsRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text treeKillCountText;
    [SerializeField] private TMP_Text acquiredLogsHeaderText;
    [SerializeField] private TMP_Text containerHeaderText;
    [SerializeField] private Transform resultLogRowPivot;
    [SerializeField] private TMP_Text emptyLogText;
    [SerializeField] private List<UI_ResultLogRow> resultLogRows = new List<UI_ResultLogRow>(2);
    [SerializeField] private GameObject inventorySlotPrefab;
    [SerializeField] private Transform containerSlotBackground;
    [SerializeField] private float containerTopY = -15f;
    [SerializeField] private UISelectionCursor selectionCursorPrefab;
    [SerializeField] private RectTransform selectionCursorParent;
    [SerializeField] private Vector2 selectionCursorSize = new Vector2(40f, 40f);

    [Header("Production References")]
    [SerializeField] private Image blackBG;
    [SerializeField] private RectTransform sectionTitle;
    [SerializeField] private RectTransform sectionKillCount;
    [SerializeField] private RectTransform sectionAcquiredLogs;
    [SerializeField] private RectTransform sectionContainer;
    [SerializeField] private RectTransform sectionButton;
    [SerializeField] private TMPInlineStyleAnimator treeKillCountTextAnimator;
    [SerializeField] private TMPInlineStyleAnimator acquiredLogsHeaderAnimator;
    [SerializeField] private TMPInlineStyleAnimator containerHeaderAnimator;

    [Header("Production Settings")]
    [SerializeField] private float resultOpenDuration = 0.3f;
    [SerializeField] private float resultOpenYOffset = -30f;
    [SerializeField] private Ease resultOpenEase = Ease.OutCubic;
    [SerializeField] private float resultHeaderOverlapDelay = 0.15f;
    [SerializeField] private float treeKillCountUpDuration = 0.8f;
    [SerializeField] private float slotBackgroundOpenDuration = 0.2f;
    [SerializeField] private float resultLogRowOpenDelay = 0.1f;
    [SerializeField] private float resultLogRowInterval = 0.1f;
    [SerializeField] private float resultLogRowCountUpDuration = 0.8f;
    [SerializeField] private float resultCloseDuration = 0.3f;
    [SerializeField] private float resultCloseYOffset = -20f;

    private int[] startOffroadLogCounts;
    private DisplayInventorySlot[] startOffroadSlots;
    private DisplayInventorySlot[] currentOffroadSlots;
    private DisplayInventorySlot[] displayOffroadSlots;
    private float[] treeDisplayProgress;
    private readonly List<UI_InventorySlot> containerSlots = new List<UI_InventorySlot>();
    private readonly List<Vector2> resultLogRowBasePositions = new List<Vector2>(2);
    private Sequence resultOpenSequence;
    private Sequence resultCloseSequence;
    private CanvasGroup sectionTitleCanvasGroup;
    private CanvasGroup sectionButtonCanvasGroup;
    private CanvasGroup sectionKillCountCanvasGroup;
    private CanvasGroup sectionAcquiredLogsCanvasGroup;
    private CanvasGroup sectionContainerCanvasGroup;
    private CanvasGroup slotBackgroundCanvasGroup;
    private CanvasGroup resultLogContentCanvasGroup;
    private Vector2 sectionTitleStartPosition;
    private Vector2 sectionButtonStartPosition;
    private Vector2 sectionKillCountStartPosition;
    private Vector2 sectionAcquiredLogsStartPosition;
    private Vector2 sectionContainerStartPosition;
    private Vector2 slotBackgroundStartPosition;
    private float blackBGTargetAlpha;
    private bool hasCachedProductionStartState;
    private bool isClosingProduction;
    private Action pendingCloseCompletedEvent;
    private UISelectionCursor selectionCursorInstance;
    private UIHoverSelectionTarget goHomeHoverTarget;
    private UIHoverSelectionTarget retryHoverTarget;
    private Button goHomeTouchAreaButton;
    private Button retryTouchAreaButton;
    private RectTransform goHomeButtonVisual;
    private RectTransform retryButtonVisual;
    private float lastFontPopSoundTime = float.NegativeInfinity;
    private float lastTreeKillCountSoundTime = float.NegativeInfinity;
    private float lastInventoryLogSoundTime = float.NegativeInfinity;
    private float treeKillCountSoundPitch = 1f;

    #region Public Override Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = _ctx?.localizationManager;
        if (localizationManager != null)
            localizationManager.OnLanguageChanged += RefreshLocalizedResultTexts;

        CacheProductionRuntimeReferences();
        CacheProductionStartState();
        InitializeResultLogRows();
        RefreshLocalizedStaticTexts();
        CacheButtonTouchAreas();
        InitializeButtonHoverTargets();
        BindButtonEvents();

        SetResultContentsActive(false);
    }

    public void DependencyInjection(IInventory _offroadContainer, IInventory _characterInventory, IDungeonResultProvider _dungeonResultProvider)
    {
        offroadContainer = _offroadContainer;
        dungeonResultProvider = _dungeonResultProvider;
    }

    /// <summary>
    /// 이번에 열릴 결과창이 튜토리얼 퀘스트 체인 도중(GoHomeBeforeExhausted 완료 ~ UpgradeAxe 완료 전)인지 알려준다.
    /// OpenResultUI()보다 먼저 호출되어야 하며, 튜토리얼 중에는 Retry를 막는 등의 판단에 쓰인다.
    /// </summary>
    public void SetTutorialState(bool _bIsTutorial)
    {
        bIsTutorial = _bIsTutorial;
    }

    public void OnGoHomeButtonClicked()
    {
        Sound.PlayUI(SoundID.MainClick);
        HideSelectionCursorImmediately();
        PlayResultCloseProduction(InvokeGoHomeButtonClickedEvent);
    }

    public void OnRetryButtonClicked()
    {
        Sound.PlayUI(SoundID.MainClick);
        HideSelectionCursorImmediately();
        PlayResultCloseProduction(InvokeRetryButtonClickedEvent);
    }

    public void OpenResultUI()
    {
        RefreshResult();
        SetResultContentsActive(true);
        PlayResultOpenProduction();
    }

    public void DungeonStarted()
    {
        SnapshotOffroadContainer();
        KillResultProductionSequences();
        SetResultContentsActive(false);
    }

    #endregion

    #region Unity Event Functions

    public override void OnDestroy()
    {
        KillResultProductionSequences();

        if (localizationManager != null)
            localizationManager.OnLanguageChanged -= RefreshLocalizedResultTexts;

        UnbindButtonEvents();
        base.OnDestroy();

        GoHomeButtonClickedEvent = null;
        RetryButtonClickedEvent = null;
    }

    #endregion

    private void SetResultContentsActive(bool active)
    {
        if (resultContentsRoot != null)
        {
            resultContentsRoot.SetActive(active);
            return;
        }

        foreach (Transform child in transform)
            child.gameObject.SetActive(active);
    }

    private void RefreshLocalizedStaticTexts()
    {
        if (titleText != null)
            titleText.text = GetLocalizedText(ResultTitleEntryId, ResultTitleFallback);

        if (acquiredLogsHeaderText != null)
            acquiredLogsHeaderText.text = GetLocalizedText(AcquiredLogsHeaderEntryId, AcquiredLogsHeaderFallback);

        if (containerHeaderText != null)
            containerHeaderText.text = GetLocalizedText(ContainerStateHeaderEntryId, ContainerStateHeaderFallback);

        if (emptyLogText != null)
            emptyLogText.text = GetLocalizedText(EmptyAcquiredLogsEntryId, EmptyAcquiredLogsFallback);
    }

    private void RefreshLocalizedResultTexts()
    {
        RefreshLocalizedStaticTexts();
        RefreshTreeKillCount();
    }

    private void CacheProductionRuntimeReferences()
    {
        sectionTitleCanvasGroup = GetOrAddCanvasGroup(sectionTitle);
        sectionButtonCanvasGroup = GetOrAddCanvasGroup(sectionButton);

        sectionKillCountCanvasGroup = GetOrAddCanvasGroup(sectionKillCount);
        sectionAcquiredLogsCanvasGroup = GetOrAddCanvasGroup(sectionAcquiredLogs);
        sectionContainerCanvasGroup = GetOrAddCanvasGroup(sectionContainer);
        slotBackgroundCanvasGroup = GetOrAddCanvasGroup(containerSlotBackground as RectTransform);
        resultLogContentCanvasGroup = GetOrAddCanvasGroup(resultLogRowPivot as RectTransform);
    }

    private void CacheProductionStartState()
    {
        if (hasCachedProductionStartState)
            return;

        if (blackBG != null)
            blackBGTargetAlpha = blackBG.color.a;

        if (sectionTitle != null)
            sectionTitleStartPosition = sectionTitle.anchoredPosition;

        if (sectionButton != null)
            sectionButtonStartPosition = sectionButton.anchoredPosition;

        if (sectionKillCount != null)
            sectionKillCountStartPosition = sectionKillCount.anchoredPosition;

        if (sectionAcquiredLogs != null)
            sectionAcquiredLogsStartPosition = sectionAcquiredLogs.anchoredPosition;

        if (sectionContainer != null)
            sectionContainerStartPosition = sectionContainer.anchoredPosition;

        if (containerSlotBackground is RectTransform slotBackgroundRect)
            slotBackgroundStartPosition = slotBackgroundRect.anchoredPosition;

        hasCachedProductionStartState = true;
    }

    private void PlayResultOpenProduction()
    {
        CacheProductionStartState();
        KillResultProductionSequences();
        isClosingProduction = false;
        SetButtonsInteractable(false);
        PrepareResultProductionHidden();
        ResetResultProductionSounds();
        Sound.PlayUI(SoundID.ResultUIOpen);

        resultOpenSequence = DOTween.Sequence();

        if (blackBG != null)
            resultOpenSequence.Join(blackBG.DOFade(blackBGTargetAlpha, resultOpenDuration));

        JoinSectionOpenTween(sectionTitle, sectionTitleCanvasGroup, sectionTitleStartPosition);
        JoinSectionOpenTween(sectionButton, sectionButtonCanvasGroup, sectionButtonStartPosition);
        PrepareContainerDisplayProduction();

        resultOpenSequence.InsertCallback(resultOpenDuration, () =>
        {
            SetCanvasGroupRaycast(sectionTitleCanvasGroup, false);
            SetCanvasGroupRaycast(sectionButtonCanvasGroup, true);
            SetButtonsInteractable(true);
        });

        resultOpenSequence.Insert(resultOpenDuration, CreateTreeKillCountProductionSequence());
        resultOpenSequence.InsertCallback(resultOpenDuration + resultHeaderOverlapDelay, PlayAcquiredLogsHeaderProduction);
        resultOpenSequence.InsertCallback(resultOpenDuration + (resultHeaderOverlapDelay * 2f), PlayContainerHeaderProduction);
        resultOpenSequence.Insert(resultOpenDuration + (resultHeaderOverlapDelay * 3f), CreateSlotBackgroundProductionSequence());
    }

    private void PrepareResultProductionHidden()
    {
        if (blackBG != null)
        {
            Color color = blackBG.color;
            color.a = 0f;
            blackBG.color = color;
        }

        SetSectionHidden(sectionTitle, sectionTitleCanvasGroup, sectionTitleStartPosition);
        SetSectionHidden(sectionButton, sectionButtonCanvasGroup, sectionButtonStartPosition);
        SetSectionInvisibleAtStart(sectionKillCount, sectionKillCountCanvasGroup, sectionKillCountStartPosition);
        SetSectionInvisibleAtStart(sectionAcquiredLogs, sectionAcquiredLogsCanvasGroup, sectionAcquiredLogsStartPosition);
        SetSectionInvisibleAtStart(sectionContainer, sectionContainerCanvasGroup, sectionContainerStartPosition);

        SetCanvasGroupVisible(resultLogContentCanvasGroup, false);
        PrepareResultLogRowsHidden();
        SetSlotBackgroundHidden();
    }

    private void PrepareResultLogRowsHidden()
    {
        for (int i = 0; i < resultLogRows.Count; i++)
        {
            UI_ResultLogRow row = resultLogRows[i];
            if (row == null)
                continue;

            CanvasGroup rowCanvasGroup = GetOrAddCanvasGroup(row.transform as RectTransform);
            if (rowCanvasGroup != null)
            {
                rowCanvasGroup.alpha = 0f;
                SetCanvasGroupRaycast(rowCanvasGroup, false);
            }
        }
    }

    private void SetSlotBackgroundHidden()
    {
        if (containerSlotBackground is RectTransform slotBackgroundRect)
            slotBackgroundRect.anchoredPosition = slotBackgroundStartPosition + new Vector2(0f, resultOpenYOffset);

        if (slotBackgroundCanvasGroup == null)
            return;

        slotBackgroundCanvasGroup.alpha = 0f;
        SetCanvasGroupRaycast(slotBackgroundCanvasGroup, false);
    }

    private void SetSectionHidden(RectTransform rectTransform, CanvasGroup canvasGroup, Vector2 startPosition)
    {
        if (rectTransform != null)
            rectTransform.anchoredPosition = startPosition + new Vector2(0f, resultOpenYOffset);

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        SetCanvasGroupRaycast(canvasGroup, false);
    }

    private void SetSectionInvisibleAtStart(RectTransform rectTransform, CanvasGroup canvasGroup, Vector2 startPosition)
    {
        if (rectTransform != null)
            rectTransform.anchoredPosition = startPosition;

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        SetCanvasGroupRaycast(canvasGroup, false);
    }

    private void JoinSectionOpenTween(RectTransform rectTransform, CanvasGroup canvasGroup, Vector2 startPosition)
    {
        if (rectTransform != null)
        {
            resultOpenSequence.Join(rectTransform.DOAnchorPos(startPosition, resultOpenDuration)
                .SetEase(resultOpenEase)
                .SetUpdate(false));
        }

        if (canvasGroup != null)
        {
            resultOpenSequence.Join(canvasGroup.DOFade(1f, resultOpenDuration)
                .SetEase(resultOpenEase)
                .SetUpdate(false));
        }
    }

    private void PlayResultCloseProduction(Action completedEvent)
    {
        if (isClosingProduction)
            return;

        CacheProductionStartState();
        KillResultProductionSequences();
        isClosingProduction = true;
        Sound.PlayUI(SoundID.ResultUIClose);
        HideSelectionCursorImmediately();
        SetButtonsInteractable(false);

        resultCloseSequence = DOTween.Sequence();

        if (blackBG != null)
        {
            resultCloseSequence.Join(blackBG.DOFade(0f, resultCloseDuration)
                .SetEase(resultOpenEase)
                .SetUpdate(false));
        }

        JoinSectionCloseTween(sectionTitle, sectionTitleCanvasGroup, sectionTitleStartPosition);
        JoinSectionCloseTween(sectionKillCount, sectionKillCountCanvasGroup, sectionKillCountStartPosition);
        JoinSectionCloseTween(sectionAcquiredLogs, sectionAcquiredLogsCanvasGroup, sectionAcquiredLogsStartPosition);
        JoinSectionCloseTween(sectionContainer, sectionContainerCanvasGroup, sectionContainerStartPosition);
        JoinSectionCloseTween(sectionButton, sectionButtonCanvasGroup, sectionButtonStartPosition);

        pendingCloseCompletedEvent = completedEvent;
        resultCloseSequence.OnComplete(OnResultCloseProductionComplete);
    }

    private void JoinSectionCloseTween(RectTransform rectTransform, CanvasGroup canvasGroup, Vector2 startPosition)
    {
        if (rectTransform != null)
        {
            Vector2 targetPosition = startPosition + new Vector2(0f, resultCloseYOffset);
            resultCloseSequence.Join(rectTransform.DOAnchorPos(targetPosition, resultCloseDuration)
                .SetEase(resultOpenEase)
                .SetUpdate(false));
        }

        if (canvasGroup != null)
        {
            resultCloseSequence.Join(canvasGroup.DOFade(0f, resultCloseDuration)
                .SetEase(resultOpenEase)
                .SetUpdate(false));
        }
    }

    private void SetButtonsInteractable(bool enabled)
    {
        if (goHomeTouchAreaButton != null)
            goHomeTouchAreaButton.interactable = enabled;

        if (retryTouchAreaButton != null)
            retryTouchAreaButton.interactable = enabled;

        if (goHomeButton != null)
            goHomeButton.interactable = enabled;

        if (retryButton != null)
            retryButton.interactable = enabled;
    }

    private void CacheButtonTouchAreas()
    {
        goHomeButtonVisual = GetButtonVisual(goHomeButton);
        retryButtonVisual = GetButtonVisual(retryButton);
        goHomeTouchAreaButton = EnsureTouchAreaButton(goHomeButtonTouchArea);
        retryTouchAreaButton = EnsureTouchAreaButton(retryButtonTouchArea);

        SetButtonVisualRaycastTarget(goHomeButtonVisual, false);
        SetButtonVisualRaycastTarget(retryButtonVisual, false);
    }

    private RectTransform GetButtonVisual(Button button)
    {
        if (button != null)
            return button.transform as RectTransform;

        return null;
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

        goHomeHoverTarget = InitializeHoverTarget(goHomeButtonTouchArea, goHomeButtonVisual);
        retryHoverTarget = InitializeHoverTarget(retryButtonTouchArea, retryButtonVisual);
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
        hoverTarget.PointerEnteredEvent -= OnResultButtonHovered;
        hoverTarget.PointerEnteredEvent += OnResultButtonHovered;
        return hoverTarget;
    }

    private void EnsureSelectionCursorInstance()
    {
        if (selectionCursorInstance != null || selectionCursorPrefab == null)
            return;

        RectTransform parent = selectionCursorParent != null ? selectionCursorParent : resultContentsRoot != null ? resultContentsRoot.transform as RectTransform : transform as RectTransform;
        if (parent == null)
            return;

        selectionCursorInstance = Instantiate(selectionCursorPrefab, parent);
        selectionCursorInstance.Initialize(selectionCursorSize);
    }

    private void HideSelectionCursorImmediately()
    {
        if (selectionCursorInstance != null)
            selectionCursorInstance.HideImmediately();

        if (goHomeHoverTarget != null)
            goHomeHoverTarget.HideCursorImmediately();

        if (retryHoverTarget != null)
            retryHoverTarget.HideCursorImmediately();
    }

    private void BindButtonEvents()
    {
        if (goHomeTouchAreaButton != null)
            goHomeTouchAreaButton.onClick.AddListener(OnGoHomeButtonClicked);

        if (retryTouchAreaButton != null)
            retryTouchAreaButton.onClick.AddListener(OnRetryButtonClicked);
    }

    private void UnbindButtonEvents()
    {
        if (goHomeTouchAreaButton != null)
            goHomeTouchAreaButton.onClick.RemoveListener(OnGoHomeButtonClicked);

        if (retryTouchAreaButton != null)
            retryTouchAreaButton.onClick.RemoveListener(OnRetryButtonClicked);

        if (goHomeHoverTarget != null)
            goHomeHoverTarget.PointerEnteredEvent -= OnResultButtonHovered;

        if (retryHoverTarget != null)
            retryHoverTarget.PointerEnteredEvent -= OnResultButtonHovered;
    }

    private void OnResultButtonHovered()
    {
        Sound.PlayUI(SoundID.ResultUIHover);
    }

    private void InvokeGoHomeButtonClickedEvent()
    {
        GoHomeButtonClickedEvent?.Invoke();
    }

    private void InvokeRetryButtonClickedEvent()
    {
        RetryButtonClickedEvent?.Invoke();
    }

    private void OnResultCloseProductionComplete()
    {
        Action completedEvent = pendingCloseCompletedEvent;
        pendingCloseCompletedEvent = null;
        resultCloseSequence = null;
        isClosingProduction = false;

        SetResultContentsActive(false);
        completedEvent?.Invoke();
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

    private void SetCanvasGroupRaycast(CanvasGroup canvasGroup, bool enabled)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        SetCanvasGroupRaycast(canvasGroup, visible);
    }

    private Sequence CreateTreeKillCountProductionSequence()
    {
        Sequence sequence = DOTween.Sequence();
        int targetTreeKillCount = dungeonResultProvider != null ? dungeonResultProvider.GetTreeKillCnt() : 0;

        sequence.AppendCallback(() =>
        {
            SetCanvasGroupVisible(sectionKillCountCanvasGroup, true);

            if (targetTreeKillCount <= 0)
            {
                RefreshTreeKillCount();
                treeKillCountTextAnimator?.PlayRevealBounce(TryPlayFontPopSound);
                return;
            }

            SetTreeKillCountText(1);
            treeKillCountTextAnimator?.PlayRevealBounce(TryPlayFontPopSound);
            TryPlayTreeKillCountSound();
        });

        if (targetTreeKillCount <= 0)
            return sequence;

        int currentTreeKillCount = 1;
        sequence.Join(DOTween.To(
                () => currentTreeKillCount,
                value =>
                {
                    if (value == currentTreeKillCount)
                        return;

                    currentTreeKillCount = value;
                    SetTreeKillCountText(currentTreeKillCount);
                    TryPlayTreeKillCountSound();
                },
                targetTreeKillCount,
                treeKillCountUpDuration)
            .SetEase(Ease.OutQuad));

        return sequence;
    }

    private void PlayAcquiredLogsHeaderProduction()
    {
        SetCanvasGroupVisible(sectionAcquiredLogsCanvasGroup, true);
        acquiredLogsHeaderAnimator?.PlayRevealBounce(TryPlayFontPopSound);
    }

    private void PlayContainerHeaderProduction()
    {
        SetCanvasGroupVisible(sectionContainerCanvasGroup, true);
        containerHeaderAnimator?.PlayRevealBounce(TryPlayFontPopSound);
    }

    private void PrepareContainerDisplayProduction()
    {
        currentOffroadSlots = CaptureOffroadSlots();

        if (startOffroadSlots == null)
            startOffroadSlots = CreateDisplaySlotArray(currentOffroadSlots.Length);

        displayOffroadSlots = CreateDisplaySlotArray(Mathf.Max(startOffroadSlots.Length, currentOffroadSlots.Length));
        treeDisplayProgress = new float[(int)TreeType.Max];

        EnsureContainerSlotCount(displayOffroadSlots.Length);
        ApplyDisplayOffroadSlotsFromProgress(false);
    }

    private void ApplyDisplayOffroadSlotsFromProgress(bool playChangedSlotInteraction)
    {
        if (displayOffroadSlots == null)
            return;

        int[,] displayCounts = BuildDisplaySlotCounts();

        for (int i = 0; i < displayOffroadSlots.Length; i++)
        {
            int[] counts = new int[(int)TreeType.Max];

            for (int treeIndex = (int)TreeType.None + 1; treeIndex < (int)TreeType.Max; treeIndex++)
                counts[treeIndex] = displayCounts[i, treeIndex];

            IItemData sourceItemData = GetDisplaySlotItemData(i, counts);
            displayOffroadSlots[i].SetData(sourceItemData, counts);
        }

        BindDisplayContainerSlots(displayOffroadSlots, playChangedSlotInteraction);
    }

    private int[,] BuildDisplaySlotCounts()
    {
        int slotCount = displayOffroadSlots != null ? displayOffroadSlots.Length : 0;
        int[,] displayCounts = new int[slotCount, (int)TreeType.Max];

        for (int treeIndex = (int)TreeType.None + 1; treeIndex < (int)TreeType.Max; treeIndex++)
        {
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                displayCounts[slotIndex, treeIndex] = GetSlotTreeCount(startOffroadSlots, slotIndex, treeIndex);

            int totalDeltaCount = GetTotalSlotTreeDeltaCount(treeIndex);
            if (totalDeltaCount <= 0)
                continue;

            float progress = treeDisplayProgress != null && treeIndex < treeDisplayProgress.Length ? treeDisplayProgress[treeIndex] : 0f;
            int remainingAddCount = Mathf.FloorToInt((totalDeltaCount * Mathf.Clamp01(progress)) + 0.0001f);

            for (int slotIndex = 0; slotIndex < slotCount && remainingAddCount > 0; slotIndex++)
            {
                int startCount = GetSlotTreeCount(startOffroadSlots, slotIndex, treeIndex);
                int currentCount = GetSlotTreeCount(currentOffroadSlots, slotIndex, treeIndex);
                int slotDeltaCount = Mathf.Max(0, currentCount - startCount);
                int addCount = Mathf.Min(slotDeltaCount, remainingAddCount);

                displayCounts[slotIndex, treeIndex] += addCount;
                remainingAddCount -= addCount;
            }
        }

        return displayCounts;
    }

    private void SetTreeDisplayProgress(TreeType treeType, float progress)
    {
        int treeIndex = (int)treeType;
        if (treeDisplayProgress == null || treeIndex < 0 || treeIndex >= treeDisplayProgress.Length)
            return;

        treeDisplayProgress[treeIndex] = Mathf.Clamp01(progress);
        ApplyDisplayOffroadSlotsFromProgress(true);
    }

    private Sequence CreateSlotBackgroundProductionSequence()
    {
        Sequence sequence = DOTween.Sequence();

        sequence.AppendCallback(() =>
        {
            ApplyDisplayOffroadSlotsFromProgress(false);

            if (HasAcquiredLogs() == false)
                SetCanvasGroupVisible(resultLogContentCanvasGroup, true);
        });

        if (containerSlotBackground is RectTransform slotBackgroundRect)
        {
            sequence.Join(slotBackgroundRect.DOAnchorPos(slotBackgroundStartPosition, slotBackgroundOpenDuration)
                .SetEase(resultOpenEase));
        }

        if (slotBackgroundCanvasGroup != null)
            sequence.Join(slotBackgroundCanvasGroup.DOFade(1f, slotBackgroundOpenDuration));

        sequence.AppendCallback(() => SetCanvasGroupRaycast(slotBackgroundCanvasGroup, false));
        sequence.AppendInterval(resultLogRowOpenDelay);
        sequence.Append(CreateResultLogRowsProductionSequence());

        return sequence;
    }

    private Sequence CreateResultLogRowsProductionSequence()
    {
        Sequence sequence = DOTween.Sequence();
        List<TreeTypeCount> acquiredLogs = GetAcquiredLogCounts();

        if (acquiredLogs.Count <= 0)
            return sequence;

        sequence.AppendCallback(() => SetCanvasGroupVisible(resultLogContentCanvasGroup, true));

        for (int i = 0; i < acquiredLogs.Count && i < resultLogRows.Count; i++)
        {
            UI_ResultLogRow row = resultLogRows[i];
            TreeTypeCount logCount = acquiredLogs[i];

            if (row == null || logCount.count <= 0)
                continue;

            Vector2 targetPosition = GetResultLogRowTargetPosition(row, i, acquiredLogs.Count);
            float startTime = i * resultLogRowInterval;
            sequence.Insert(startTime, CreateResultLogRowProductionSequence(row, logCount.treeType, logCount.count, targetPosition));
        }

        return sequence;
    }

    private Sequence CreateResultLogRowProductionSequence(UI_ResultLogRow row, TreeType treeType, int targetCount, Vector2 targetPosition)
    {
        Sequence sequence = DOTween.Sequence();
        CanvasGroup rowCanvasGroup = GetOrAddCanvasGroup(row.transform as RectTransform);
        RectTransform rowRect = row.transform as RectTransform;

        sequence.AppendCallback(() =>
        {
            if (rowRect != null)
                rowRect.anchoredPosition = targetPosition + new Vector2(0f, resultOpenYOffset);

            if (rowCanvasGroup != null)
            {
                rowCanvasGroup.alpha = 0f;
                SetCanvasGroupRaycast(rowCanvasGroup, false);
            }

            row.SetDataVisible(treeType, 1);
            SetTreeDisplayProgress(treeType, targetCount <= 0 ? 1f : 1f / targetCount);
        });

        if (rowRect != null)
        {
            sequence.Join(rowRect.DOAnchorPos(targetPosition, slotBackgroundOpenDuration)
                .SetEase(resultOpenEase));
        }

        if (rowCanvasGroup != null)
            sequence.Join(rowCanvasGroup.DOFade(1f, slotBackgroundOpenDuration));

        int currentCount = 1;
        sequence.Join(DOTween.To(
                () => currentCount,
                value =>
                {
                    if (value == currentCount)
                        return;

                    currentCount = value;
                    row.SetDataVisible(treeType, currentCount);
                    SetTreeDisplayProgress(treeType, targetCount <= 0 ? 1f : (float)currentCount / targetCount);
                },
                targetCount,
                resultLogRowCountUpDuration)
            .SetEase(Ease.OutQuad));

        sequence.OnComplete(() =>
        {
            row.SetDataVisible(treeType, targetCount);
            SetTreeDisplayProgress(treeType, 1f);
            SetCanvasGroupRaycast(rowCanvasGroup, false);
        });

        return sequence;
    }

    private void KillResultProductionSequences()
    {
        if (resultOpenSequence != null)
        {
            resultOpenSequence.Kill();
            resultOpenSequence = null;
        }

        if (resultCloseSequence != null)
        {
            resultCloseSequence.Kill();
            resultCloseSequence = null;
        }

        isClosingProduction = false;
        pendingCloseCompletedEvent = null;
        treeKillCountTextAnimator?.StopRevealBounce();
        acquiredLogsHeaderAnimator?.StopRevealBounce();
        containerHeaderAnimator?.StopRevealBounce();
    }

    private void InitializeResultLogRows()
    {
        RemoveInvalidResultLogRows();
        CacheResultLogRowBasePositions();

        for (int i = 0; i < resultLogRows.Count; i++)
        {
            if (resultLogRows[i] != null)
                resultLogRows[i].Initialize();
        }
    }

    private void CacheResultLogRowBasePositions()
    {
        resultLogRowBasePositions.Clear();

        for (int i = 0; i < resultLogRows.Count; i++)
        {
            RectTransform rectTransform = resultLogRows[i] != null ? resultLogRows[i].transform as RectTransform : null;
            resultLogRowBasePositions.Add(rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero);
        }
    }

    private void RefreshResult()
    {
        RefreshLocalizedStaticTexts();
        RefreshTreeKillCount();
        RefreshAcquiredLogs();
        RefreshContainerSlots();
    }

    private void RefreshTreeKillCount()
    {
        if (treeKillCountText == null)
            return;

        int treeKillCount = dungeonResultProvider != null ? dungeonResultProvider.GetTreeKillCnt() : 0;
        SetTreeKillCountText(treeKillCount);
    }

    private void SetTreeKillCountText(int treeKillCount)
    {
        if (treeKillCountText == null)
            return;

        int normalizedTreeKillCount = Mathf.Max(0, treeKillCount);
        int entryId = GetTreeKillCountEntryId(normalizedTreeKillCount);
        string fallbackFormat = GetTreeKillCountFallbackFormat(normalizedTreeKillCount);
        treeKillCountText.text = normalizedTreeKillCount == 0
            ? GetLocalizedText(entryId, fallbackFormat)
            : GetLocalizedFormatText(entryId, fallbackFormat, normalizedTreeKillCount);
    }

    private int GetTreeKillCountEntryId(int treeKillCount)
    {
        if (treeKillCount <= 0)
            return TreeKillCountZeroEntryId;

        if (treeKillCount <= 10)
            return TreeKillCountLowEntryId;

        if (treeKillCount < 30)
            return TreeKillCountMiddleEntryId;

        return TreeKillCountHighEntryId;
    }

    private string GetTreeKillCountFallbackFormat(int treeKillCount)
    {
        if (treeKillCount <= 0)
            return TreeKillCountZeroFallback;

        if (treeKillCount <= 10)
            return TreeKillCountLowFallback;

        if (treeKillCount < 30)
            return TreeKillCountMiddleFallback;

        return TreeKillCountHighFallback;
    }

    private string GetLocalizedText(int entryId, string fallback)
    {
        if (localizationManager == null)
            return fallback;

        string localizedText = localizationManager.GetText(ResultLocalizationJsonId, entryId);
        return string.IsNullOrEmpty(localizedText) ? fallback : localizedText;
    }

    private string GetLocalizedFormatText(int entryId, string fallbackFormat, params object[] args)
    {
        string format = GetLocalizedText(entryId, fallbackFormat);
        return string.Format(format, args);
    }

    private void RefreshAcquiredLogs()
    {
        List<TreeTypeCount> acquiredLogs = GetAcquiredLogCounts();
        bool hasAcquiredLogs = 0 < acquiredLogs.Count;

        if (emptyLogText != null)
            emptyLogText.gameObject.SetActive(!hasAcquiredLogs);

        for (int i = 0; i < resultLogRows.Count; i++)
        {
            UI_ResultLogRow row = resultLogRows[i];
            if (row == null)
                continue;

            if (i < acquiredLogs.Count)
            {
                row.SetData(acquiredLogs[i].treeType, acquiredLogs[i].count);
                SetResultLogRowPosition(row, i, acquiredLogs.Count);
            }
            else
            {
                row.gameObject.SetActive(false);
            }
        }
    }

    private void RemoveInvalidResultLogRows()
    {
        for (int i = resultLogRows.Count - 1; i >= 0; i--)
        {
            UI_ResultLogRow row = resultLogRows[i];
            if (row == null || false == row.transform.IsChildOf(transform))
                resultLogRows.RemoveAt(i);
        }
    }

    private List<TreeTypeCount> GetAcquiredLogCounts()
    {
        int[] currentCounts = GetOffroadLogCounts();
        List<TreeTypeCount> acquiredLogs = new List<TreeTypeCount>();

        for (int i = (int)TreeType.None + 1; i < (int)TreeType.Max; i++)
        {
            int startCount = startOffroadLogCounts != null && i < startOffroadLogCounts.Length ? startOffroadLogCounts[i] : 0;
            int acquiredCount = currentCounts[i] - startCount;

            if (0 >= acquiredCount)
                continue;

            acquiredLogs.Add(new TreeTypeCount
            {
                treeType = (TreeType)i,
                count = acquiredCount
            });
        }

        return acquiredLogs;
    }

    private void SnapshotOffroadContainer()
    {
        startOffroadLogCounts = GetOffroadLogCounts();
        startOffroadSlots = CaptureOffroadSlots();
    }

    private DisplayInventorySlot[] CaptureOffroadSlots()
    {
        if (offroadContainer == null || offroadContainer.inventorySlots == null)
            return Array.Empty<DisplayInventorySlot>();

        IReadOnlyList<IInventorySlot> sourceSlots = offroadContainer.inventorySlots;
        int slotCount = Mathf.Min(offroadContainer.currentSlotCnt, sourceSlots.Count);
        DisplayInventorySlot[] slots = CreateDisplaySlotArray(slotCount);

        for (int i = 0; i < slotCount; i++)
            slots[i].CopyFrom(sourceSlots[i]);

        return slots;
    }

    private DisplayInventorySlot[] CreateDisplaySlotArray(int slotCount)
    {
        DisplayInventorySlot[] slots = new DisplayInventorySlot[Mathf.Max(0, slotCount)];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = new DisplayInventorySlot();

        return slots;
    }

    private static int[] ExtractTreeTypeCounts(IInventorySlot slot)
    {
        int[] counts = new int[(int)TreeType.Max];

        if (slot == null || slot.treeTypeCounts == null)
            return counts;

        TreeTypeCount[] treeTypeCounts = slot.treeTypeCounts;
        for (int i = 0; i < treeTypeCounts.Length; i++)
        {
            TreeType treeType = treeTypeCounts[i].treeType;
            if (treeType <= TreeType.None || treeType >= TreeType.Max)
                continue;

            counts[(int)treeType] = treeTypeCounts[i].count;
        }

        return counts;
    }

    private int GetSlotTreeCount(DisplayInventorySlot[] slots, int slotIndex, int treeIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return 0;

        TreeTypeCount[] treeTypeCounts = slots[slotIndex].treeTypeCounts;
        if (treeTypeCounts == null)
            return 0;

        TreeType treeType = (TreeType)treeIndex;
        for (int i = 0; i < treeTypeCounts.Length; i++)
        {
            if (treeTypeCounts[i].treeType == treeType)
                return treeTypeCounts[i].count;
        }

        return 0;
    }

    private int GetTotalSlotTreeDeltaCount(int treeIndex)
    {
        int totalDeltaCount = 0;
        int slotCount = Mathf.Max(startOffroadSlots != null ? startOffroadSlots.Length : 0, currentOffroadSlots != null ? currentOffroadSlots.Length : 0);

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            int startCount = GetSlotTreeCount(startOffroadSlots, slotIndex, treeIndex);
            int currentCount = GetSlotTreeCount(currentOffroadSlots, slotIndex, treeIndex);
            totalDeltaCount += Mathf.Max(0, currentCount - startCount);
        }

        return totalDeltaCount;
    }

    private IItemData GetDisplaySlotItemData(int slotIndex, int[] counts)
    {
        IItemData sourceItemData = GetSlotItemDataForCounts(currentOffroadSlots, slotIndex, counts);
        if (sourceItemData != null)
            return sourceItemData;

        return GetSlotItemDataForCounts(startOffroadSlots, slotIndex, counts);
    }

    private IItemData GetSlotItemDataForCounts(DisplayInventorySlot[] slots, int slotIndex, int[] counts)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return null;

        IItemData itemData = slots[slotIndex].itemData;
        if (itemData is LogItemData logItemData)
        {
            int treeIndex = (int)logItemData.treeType;
            if (counts != null && treeIndex >= 0 && treeIndex < counts.Length && counts[treeIndex] > 0)
                return itemData;
        }

        return null;
    }

    private int[] GetOffroadLogCounts()
    {
        int[] counts = new int[(int)TreeType.Max];

        if (offroadContainer == null || offroadContainer.inventorySlots == null)
            return counts;

        IReadOnlyList<IInventorySlot> slots = offroadContainer.inventorySlots;
        int slotCount = Mathf.Min(offroadContainer.currentSlotCnt, slots.Count);

        for (int i = 0; i < slotCount; i++)
        {
            IInventorySlot slot = slots[i];
            if (slot == null || slot.treeTypeCounts == null)
                continue;

            TreeTypeCount[] treeTypeCounts = slot.treeTypeCounts;
            for (int j = 0; j < treeTypeCounts.Length; j++)
            {
                TreeType treeType = treeTypeCounts[j].treeType;
                if (treeType <= TreeType.None || treeType >= TreeType.Max)
                    continue;

                counts[(int)treeType] += treeTypeCounts[j].count;
            }
        }

        return counts;
    }

    private void SetResultLogRowPosition(UI_ResultLogRow row, int index, int count)
    {
        RectTransform rectTransform = row.transform as RectTransform;
        if (rectTransform == null)
            return;

        rectTransform.anchoredPosition = GetResultLogRowTargetPosition(row, index, count);
    }

    private Vector2 GetResultLogRowTargetPosition(UI_ResultLogRow row, int index, int count)
    {
        float x = 0f;
        if (count == 2)
            x = index == 0 ? -12f : 12f;
        else if (2 < count)
            x = (index - ((count - 1) * 0.5f)) * 24f;

        return new Vector2(x, GetResultLogRowBaseY(row, index));
    }

    private float GetResultLogRowBaseY(UI_ResultLogRow row, int index)
    {
        if (0 <= index && index < resultLogRowBasePositions.Count)
            return resultLogRowBasePositions[index].y;

        RectTransform rectTransform = row != null ? row.transform as RectTransform : null;
        return rectTransform != null ? rectTransform.anchoredPosition.y : 0f;
    }

    private void RefreshContainerSlots()
    {
        if (offroadContainer == null || offroadContainer.inventorySlots == null)
            return;

        EnsureContainerSlotCount(offroadContainer.inventorySlots.Count);

        IReadOnlyList<IInventorySlot> slots = offroadContainer.inventorySlots;
        int activeSlotCount = offroadContainer.currentSlotCnt;

        for (int i = 0; i < containerSlots.Count; i++)
        {
            UI_InventorySlot slotUI = containerSlots[i];
            if (slotUI == null)
                continue;

            bool isActive = i < activeSlotCount;
            slotUI.gameObject.SetActive(isActive);

            if (isActive && i < slots.Count)
                slotUI.UpdateBindSlotData(slots[i], offroadContainer.maxItemCntPerSlot);
        }

        ApplyContainerSlotLayout();
    }

    private void BindDisplayContainerSlots(DisplayInventorySlot[] displaySlots, bool playChangedSlotInteraction)
    {
        if (displaySlots == null)
            return;

        EnsureContainerSlotCount(displaySlots.Length);
        bool playedChangedSlotInteraction = false;

        for (int i = 0; i < containerSlots.Count; i++)
        {
            UI_InventorySlot slotUI = containerSlots[i];
            if (slotUI == null)
                continue;

            bool isActive = i < displaySlots.Length;
            slotUI.gameObject.SetActive(isActive);

            if (isActive)
            {
                bool shouldPlayChangedInteraction = playChangedSlotInteraction && displaySlots[i].HasChanged;
                slotUI.UpdateBindSlotData(displaySlots[i], offroadContainer != null ? offroadContainer.maxItemCntPerSlot : 99, shouldPlayChangedInteraction);
                playedChangedSlotInteraction |= shouldPlayChangedInteraction;
            }
        }

        if (playedChangedSlotInteraction)
            TryPlayInventoryLogSound();

        ApplyContainerSlotLayout();
    }

    private void ResetResultProductionSounds()
    {
        lastFontPopSoundTime = float.NegativeInfinity;
        lastTreeKillCountSoundTime = float.NegativeInfinity;
        lastInventoryLogSoundTime = float.NegativeInfinity;
        treeKillCountSoundPitch = 1f;
    }

    private void TryPlayFontPopSound()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - lastFontPopSoundTime < FontPopSoundInterval)
            return;

        Sound.PlayUI(SoundID.FontPop);
        lastFontPopSoundTime = currentTime;
    }

    private void TryPlayTreeKillCountSound()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - lastTreeKillCountSoundTime < TreeKillCountSoundInterval)
            return;

        Sound.PlayUI(SoundID.GetItem, 1f, treeKillCountSoundPitch);
        lastTreeKillCountSoundTime = currentTime;
        treeKillCountSoundPitch = Mathf.Min(treeKillCountSoundPitch + TreeKillCountPitchStep, TreeKillCountMaxPitch);
    }

    private void TryPlayInventoryLogSound()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - lastInventoryLogSoundTime < InventoryLogSoundInterval)
            return;

        Sound.PlayUI(SoundID.OutItem);
        lastInventoryLogSoundTime = currentTime;
    }

    private void EnsureContainerSlotCount(int count)
    {
        if (containerSlotBackground == null || inventorySlotPrefab == null)
            return;

        while (containerSlots.Count < count)
        {
            GameObject slotObject = Instantiate(inventorySlotPrefab, containerSlotBackground);
            UI_InventorySlot slot = slotObject.GetComponent<UI_InventorySlot>();

            if (slot == null)
                return;

            slot.Initialize();
            slot.DisableRayCast();
            containerSlots.Add(slot);
        }
    }

    private bool HasAcquiredLogs()
    {
        return 0 < GetAcquiredLogCounts().Count;
    }

    private void ApplyContainerSlotLayout()
    {
        if (containerSlotBackground == null)
            return;

        GridLayoutGroup gridLayout = containerSlotBackground.GetComponent<GridLayoutGroup>();
        RectTransform rootRect = containerSlotBackground.parent as RectTransform;

        if (gridLayout == null || rootRect == null)
            return;

        gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;

        rootRect.anchoredPosition = new Vector2(0f, Mathf.Round(containerTopY));
    }

}
