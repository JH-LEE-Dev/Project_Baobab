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
    private readonly struct LogVariantKey : IEquatable<LogVariantKey>
    {
        public readonly TreeType treeType;
        public readonly LogState logState;

        public LogVariantKey(TreeType treeType, LogState logState)
        {
            this.treeType = treeType;
            this.logState = logState;
        }

        public bool Equals(LogVariantKey other)
        {
            return treeType == other.treeType && logState == other.logState;
        }

        public override bool Equals(object obj)
        {
            return obj is LogVariantKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)treeType * 397) ^ (int)logState;
        }
    }

    private readonly struct ResultLogCount
    {
        public readonly LogVariantKey key;
        public readonly int count;

        public ResultLogCount(LogVariantKey key, int count)
        {
            this.key = key;
            this.count = count;
        }
    }

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

            logStateCounts[0].state = sourceItemData is LogItemData logItemData
                ? logItemData.logState
                : LogState.Normal;
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
    [SerializeField] private Button tutorialGoHomeButton;
    [SerializeField] private RectTransform goHomeButtonTouchArea;
    [SerializeField] private RectTransform retryButtonTouchArea;
    [SerializeField] private RectTransform tutorialGoHomeButtonTouchArea;
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

    private Dictionary<LogVariantKey, int> startOffroadLogCounts;
    private DisplayInventorySlot[] startOffroadSlots;
    private DisplayInventorySlot[] currentOffroadSlots;
    private DisplayInventorySlot[] displayOffroadSlots;
    private readonly Dictionary<LogVariantKey, float> logDisplayProgress = new Dictionary<LogVariantKey, float>();
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
    private UIHoverSelectionTarget tutorialGoHomeHoverTarget;
    private Button goHomeTouchAreaButton;
    private Button retryTouchAreaButton;
    private Button tutorialGoHomeTouchAreaButton;
    private RectTransform goHomeButtonVisual;
    private RectTransform retryButtonVisual;
    private RectTransform tutorialGoHomeButtonVisual;
    private float lastFontPopSoundTime = float.NegativeInfinity;
    private float lastTreeKillCountSoundTime = float.NegativeInfinity;
    private float lastInventoryLogSoundTime = float.NegativeInfinity;
    private float treeKillCountSoundPitch = 1f;
    // ResultUI는 UIView.Show()/Hide()를 거치지 않고 OpenResultUI()/DungeonStarted()로 직접 열리고
    // 닫히므로, UIView.OnShow()/OnHide()의 bVisible 가드를 대신할 자체 가드가 필요하다.
    private bool bDuckRegistered;

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
        if (!bDuckRegistered)
        {
            bDuckRegistered = true;
            Sound.RequestAudioDuck();
        }

        RefreshResult();
        ApplyResultButtonVisibility();
        SetResultContentsActive(true);
        PlayResultOpenProduction();
    }

    public void DungeonStarted()
    {
        // 닫기 연출을 거치지 않고 결과창이 사라지는 경로(연출 중단 등)를 위한 안전장치.
        ReleaseAudioDuckIfRegistered();

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

        if (tutorialGoHomeTouchAreaButton != null)
            tutorialGoHomeTouchAreaButton.interactable = enabled;

        if (goHomeButton != null)
            goHomeButton.interactable = enabled;

        if (retryButton != null)
            retryButton.interactable = enabled;

        if (tutorialGoHomeButton != null)
            tutorialGoHomeButton.interactable = enabled;
    }

    private void CacheButtonTouchAreas()
    {
        goHomeButtonVisual = GetButtonVisual(goHomeButton);
        retryButtonVisual = GetButtonVisual(retryButton);
        tutorialGoHomeButtonVisual = GetButtonVisual(tutorialGoHomeButton);
        goHomeTouchAreaButton = EnsureTouchAreaButton(goHomeButtonTouchArea);
        retryTouchAreaButton = EnsureTouchAreaButton(retryButtonTouchArea);
        tutorialGoHomeTouchAreaButton = EnsureTouchAreaButton(tutorialGoHomeButtonTouchArea);

        SetButtonVisualRaycastTarget(goHomeButtonVisual, false);
        SetButtonVisualRaycastTarget(retryButtonVisual, false);
        SetButtonVisualRaycastTarget(tutorialGoHomeButtonVisual, false);
    }

    private void ApplyResultButtonVisibility()
    {
        bool showNormalButtons = false == bIsTutorial;

        SetButtonActive(goHomeButtonVisual, showNormalButtons);
        SetButtonActive(goHomeButtonTouchArea, showNormalButtons);
        SetButtonActive(retryButtonVisual, showNormalButtons);
        SetButtonActive(retryButtonTouchArea, showNormalButtons);

        SetButtonActive(tutorialGoHomeButtonVisual, bIsTutorial);
        SetButtonActive(tutorialGoHomeButtonTouchArea, bIsTutorial);
    }

    private void SetButtonActive(RectTransform target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
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
        tutorialGoHomeHoverTarget = InitializeHoverTarget(tutorialGoHomeButtonTouchArea, tutorialGoHomeButtonVisual);
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

        if (tutorialGoHomeHoverTarget != null)
            tutorialGoHomeHoverTarget.HideCursorImmediately();
    }

    private void BindButtonEvents()
    {
        if (goHomeTouchAreaButton != null)
            goHomeTouchAreaButton.onClick.AddListener(OnGoHomeButtonClicked);

        if (retryTouchAreaButton != null)
            retryTouchAreaButton.onClick.AddListener(OnRetryButtonClicked);

        if (tutorialGoHomeTouchAreaButton != null)
            tutorialGoHomeTouchAreaButton.onClick.AddListener(OnGoHomeButtonClicked);
    }

    private void UnbindButtonEvents()
    {
        if (goHomeTouchAreaButton != null)
            goHomeTouchAreaButton.onClick.RemoveListener(OnGoHomeButtonClicked);

        if (retryTouchAreaButton != null)
            retryTouchAreaButton.onClick.RemoveListener(OnRetryButtonClicked);

        if (tutorialGoHomeTouchAreaButton != null)
            tutorialGoHomeTouchAreaButton.onClick.RemoveListener(OnGoHomeButtonClicked);

        if (goHomeHoverTarget != null)
            goHomeHoverTarget.PointerEnteredEvent -= OnResultButtonHovered;

        if (retryHoverTarget != null)
            retryHoverTarget.PointerEnteredEvent -= OnResultButtonHovered;

        if (tutorialGoHomeHoverTarget != null)
            tutorialGoHomeHoverTarget.PointerEnteredEvent -= OnResultButtonHovered;
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
        ReleaseAudioDuckIfRegistered();
        completedEvent?.Invoke();
    }

    // 결과창이 실제로 닫히는 지점. DungeonStarted()는 "다음 던전이 시작될 때"만 호출되므로,
    // 여기서 풀어주지 않으면 결과창을 닫고 마을에 있는 내내 사운드가 먹먹한 채로 남는다.
    private void ReleaseAudioDuckIfRegistered()
    {
        if (false == bDuckRegistered)
            return;

        bDuckRegistered = false;
        Sound.ReleaseAudioDuck();
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
        logDisplayProgress.Clear();

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
        }

        List<ResultLogCount> acquiredLogs = GetAcquiredLogCounts();
        for (int logIndex = 0; logIndex < acquiredLogs.Count; logIndex++)
        {
            ResultLogCount acquiredLog = acquiredLogs[logIndex];
            LogVariantKey key = acquiredLog.key;
            if (acquiredLog.count <= 0)
                continue;

            float progress = logDisplayProgress.TryGetValue(key, out float value) ? value : 0f;
            int remainingAddCount = Mathf.FloorToInt((acquiredLog.count * Mathf.Clamp01(progress)) + 0.0001f);

            for (int slotIndex = 0; slotIndex < slotCount && remainingAddCount > 0; slotIndex++)
            {
                int startCount = GetSlotLogVariantCount(startOffroadSlots, slotIndex, key);
                int currentCount = GetSlotLogVariantCount(currentOffroadSlots, slotIndex, key);
                int slotDeltaCount = Mathf.Max(0, currentCount - startCount);
                int addCount = Mathf.Min(slotDeltaCount, remainingAddCount);

                displayCounts[slotIndex, (int)key.treeType] += addCount;
                remainingAddCount -= addCount;
            }
        }

        return displayCounts;
    }

    private void SetLogDisplayProgress(LogVariantKey key, float progress)
    {
        logDisplayProgress[key] = Mathf.Clamp01(progress);
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
        List<ResultLogCount> acquiredLogs = GetAcquiredLogCounts();

        EnsureResultLogRowCount(acquiredLogs.Count);

        if (acquiredLogs.Count <= 0)
            return sequence;

        sequence.AppendCallback(() => SetCanvasGroupVisible(resultLogContentCanvasGroup, true));

        for (int i = 0; i < acquiredLogs.Count && i < resultLogRows.Count; i++)
        {
            UI_ResultLogRow row = resultLogRows[i];
            ResultLogCount logCount = acquiredLogs[i];

            if (row == null || logCount.count <= 0)
                continue;

            Vector2 targetPosition = GetResultLogRowTargetPosition(row, i, acquiredLogs.Count);
            float startTime = i * resultLogRowInterval;
            sequence.Insert(startTime, CreateResultLogRowProductionSequence(row, logCount, targetPosition));
        }

        return sequence;
    }

    private Sequence CreateResultLogRowProductionSequence(UI_ResultLogRow row, ResultLogCount logCount, Vector2 targetPosition)
    {
        LogVariantKey key = logCount.key;
        int targetCount = logCount.count;
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

            row.SetDataVisible(key.treeType, key.logState, 1);
            SetLogDisplayProgress(key, targetCount <= 0 ? 1f : 1f / targetCount);
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
                    row.SetDataVisible(key.treeType, key.logState, currentCount);
                    SetLogDisplayProgress(key, targetCount <= 0 ? 1f : (float)currentCount / targetCount);
                },
                targetCount,
                resultLogRowCountUpDuration)
            .SetEase(Ease.OutQuad));

        sequence.OnComplete(() =>
        {
            row.SetDataVisible(key.treeType, key.logState, targetCount);
            SetLogDisplayProgress(key, 1f);
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
        List<ResultLogCount> acquiredLogs = GetAcquiredLogCounts();
        bool hasAcquiredLogs = 0 < acquiredLogs.Count;

        EnsureResultLogRowCount(acquiredLogs.Count);

        if (emptyLogText != null)
            emptyLogText.gameObject.SetActive(!hasAcquiredLogs);

        for (int i = 0; i < resultLogRows.Count; i++)
        {
            UI_ResultLogRow row = resultLogRows[i];
            if (row == null)
                continue;

            if (i < acquiredLogs.Count)
            {
                ResultLogCount logCount = acquiredLogs[i];
                row.SetData(logCount.key.treeType, logCount.key.logState, logCount.count);
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

    private void EnsureResultLogRowCount(int count)
    {
        if (count <= resultLogRows.Count)
            return;

        UI_ResultLogRow template = null;
        for (int i = 0; i < resultLogRows.Count; i++)
        {
            if (resultLogRows[i] != null)
            {
                template = resultLogRows[i];
                break;
            }
        }

        if (template == null)
            return;

        Transform parent = resultLogRowPivot != null ? resultLogRowPivot : template.transform.parent;
        float baseY = template.transform is RectTransform templateRect
            ? templateRect.anchoredPosition.y
            : 0f;

        while (resultLogRows.Count < count)
        {
            UI_ResultLogRow row = Instantiate(template, parent);
            row.name = $"{template.name}_{resultLogRows.Count}";
            row.Initialize();
            resultLogRows.Add(row);
            resultLogRowBasePositions.Add(new Vector2(0f, baseY));
        }
    }

    private List<ResultLogCount> GetAcquiredLogCounts()
    {
        Dictionary<LogVariantKey, int> currentCounts = GetOffroadLogCounts();
        List<ResultLogCount> acquiredLogs = new List<ResultLogCount>();
        Array logStates = Enum.GetValues(typeof(LogState));

        for (int treeIndex = (int)TreeType.None + 1; treeIndex < (int)TreeType.Max; treeIndex++)
        {
            for (int stateIndex = 0; stateIndex < logStates.Length; stateIndex++)
            {
                LogVariantKey key = new LogVariantKey((TreeType)treeIndex, (LogState)logStates.GetValue(stateIndex));
                int acquiredCount = GetLogVariantCount(currentCounts, key) - GetLogVariantCount(startOffroadLogCounts, key);

                if (acquiredCount <= 0)
                    continue;

                acquiredLogs.Add(new ResultLogCount(key, acquiredCount));
            }
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

    private int GetSlotLogVariantCount(DisplayInventorySlot[] slots, int slotIndex, LogVariantKey key)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return 0;

        if (!(slots[slotIndex].itemData is LogItemData logItemData))
            return 0;

        if (logItemData.treeType != key.treeType || logItemData.logState != key.logState)
            return 0;

        return GetSlotTreeCount(slots, slotIndex, (int)key.treeType);
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

    private Dictionary<LogVariantKey, int> GetOffroadLogCounts()
    {
        Dictionary<LogVariantKey, int> counts = new Dictionary<LogVariantKey, int>();

        if (offroadContainer == null || offroadContainer.inventorySlots == null)
            return counts;

        IReadOnlyList<IInventorySlot> slots = offroadContainer.inventorySlots;
        int slotCount = Mathf.Min(offroadContainer.currentSlotCnt, slots.Count);

        for (int i = 0; i < slotCount; i++)
        {
            IInventorySlot slot = slots[i];
            if (slot == null || !(slot.itemData is LogItemData logItemData))
                continue;

            TreeType treeType = logItemData.treeType;
            if (treeType <= TreeType.None || treeType >= TreeType.Max)
                continue;

            LogVariantKey key = new LogVariantKey(treeType, logItemData.logState);
            counts[key] = GetLogVariantCount(counts, key) + Mathf.Max(0, slot.count);
        }

        return counts;
    }

    private static int GetLogVariantCount(Dictionary<LogVariantKey, int> counts, LogVariantKey key)
    {
        return counts != null && counts.TryGetValue(key, out int count) ? count : 0;
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

        // GetItem은 인벤토리 등에서도 쓰는 게임플레이 효과음이라 SFX 그룹에 있지만, 여기서는
        // 결과창 자신의 카운트업 연출음이다. 결과창은 스스로 덕킹을 걸고 있으므로 그대로 두면
        // 자기 연출음이 자기가 건 로우패스에 먹먹해진다. 이 재생만 UI 그룹으로 우회시킨다.
        Sound.PlayUI(SoundID.GetItem, 1f, treeKillCountSoundPitch, bypassDucking: true);
        lastTreeKillCountSoundTime = currentTime;
        treeKillCountSoundPitch = Mathf.Min(treeKillCountSoundPitch + TreeKillCountPitchStep, TreeKillCountMaxPitch);
    }

    private void TryPlayInventoryLogSound()
    {
        float currentTime = Time.unscaledTime;
        if (currentTime - lastInventoryLogSoundTime < InventoryLogSoundInterval)
            return;

        // TryPlayTreeKillCountSound와 같은 이유로 UI 그룹으로 우회시킨다(결과창 자신의 연출음).
        Sound.PlayUI(SoundID.OutItem, bypassDucking: true);
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
