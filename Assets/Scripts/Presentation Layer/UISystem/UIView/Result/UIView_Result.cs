using System;
using System.Collections.Generic;
using DG.Tweening;
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

        public event Action SlotUpdatedEvent;

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

    public event Action GoHomeButtonClickedEvent;
    public event Action RetryButtonClickedEvent;

    private IInventory offroadContainer;
    private IDungeonResultProvider dungeonResultProvider;
    private LocalizationManager localizationManager;

    [Header("UI References")]
    [SerializeField] private Button goHomeButton;
    [SerializeField] private Button retryButton;
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

    [Header("Production References")]
    [SerializeField] private Image blackBG;
    [SerializeField] private RectTransform sectionTitle;
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

    private int[] startOffroadLogCounts;
    private DisplayInventorySlot[] startOffroadSlots;
    private DisplayInventorySlot[] currentOffroadSlots;
    private DisplayInventorySlot[] displayOffroadSlots;
    private float[] treeDisplayProgress;
    private readonly List<UI_InventorySlot> containerSlots = new List<UI_InventorySlot>();
    private readonly List<CanvasGroup> pendingSectionGroups = new List<CanvasGroup>(3);

    private Sequence resultOpenSequence;
    private CanvasGroup sectionTitleCanvasGroup;
    private CanvasGroup sectionButtonCanvasGroup;
    private CanvasGroup sectionKillCountCanvasGroup;
    private CanvasGroup sectionAcquiredLogsCanvasGroup;
    private CanvasGroup sectionContainerCanvasGroup;
    private CanvasGroup slotBackgroundCanvasGroup;
    private CanvasGroup resultLogContentCanvasGroup;
    private Vector2 sectionTitleStartPosition;
    private Vector2 sectionButtonStartPosition;
    private Vector2 slotBackgroundStartPosition;
    private float blackBGTargetAlpha;
    private bool hasCachedProductionStartState;

    #region Public Override Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        localizationManager = _ctx?.localizationManager;
        if (localizationManager != null)
            localizationManager.OnLanguageChanged += RefreshLocalizedResultTexts;

        CacheUIReferences();
        CacheProductionReferences();
        CacheProductionStartState();
        InitializeResultLogRows();
        RefreshLocalizedStaticTexts();

        if (goHomeButton != null)
            goHomeButton.onClick.AddListener(OnGoHomeButtonClicked);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryButtonClicked);

        SetResultContentsActive(false);
    }

    public void DependencyInjection(IInventory _offroadContainer, IInventory _characterInventory, IDungeonResultProvider _dungeonResultProvider)
    {
        offroadContainer = _offroadContainer;
        dungeonResultProvider = _dungeonResultProvider;
    }

    public void OnGoHomeButtonClicked()
    {
        GoHomeButtonClickedEvent?.Invoke();
        KillResultOpenSequence();
        SetResultContentsActive(false);
    }

    public void OnRetryButtonClicked()
    {
        RetryButtonClickedEvent?.Invoke();
        KillResultOpenSequence();
        SetResultContentsActive(false);
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
        KillResultOpenSequence();
        SetResultContentsActive(false);
    }

    #endregion

    #region Unity Event Functions

    public override void OnDestroy()
    {
        KillResultOpenSequence();

        if (localizationManager != null)
            localizationManager.OnLanguageChanged -= RefreshLocalizedResultTexts;

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

    private void CacheUIReferences()
    {
        if (resultContentsRoot == null)
        {
            Transform contents = transform.Find("Panel_ResultContents");
            if (contents != null)
                resultContentsRoot = contents.gameObject;
        }

        if (treeKillCountText == null)
        {
            Transform treeKillCount = transform.Find("Panel_ResultContents/Text_TreeKillCount");
            if (treeKillCount == null)
                treeKillCount = FindChildRecursive(transform, "Text_TreeKillCount");

            if (treeKillCount != null)
                treeKillCountText = treeKillCount.GetComponent<TMP_Text>();
        }

        if (titleText == null)
        {
            Transform title = FindChildRecursive(transform, "Text_Title");
            if (title != null)
                titleText = title.GetComponent<TMP_Text>();
        }

        if (acquiredLogsHeaderText == null)
            acquiredLogsHeaderText = GetSectionHeaderText("Section_AcquiredLogs");

        if (containerHeaderText == null)
            containerHeaderText = GetSectionHeaderText("Section_Container");

        if (resultLogRowPivot == null)
            resultLogRowPivot = FindChildRecursive(transform, "UI_ResultLogRowPivot");

        if (emptyLogText == null)
        {
            Transform emptyText = FindChildRecursive(transform, "Text_Empty");
            if (emptyText != null)
                emptyLogText = emptyText.GetComponent<TMP_Text>();
        }

        if (containerSlotBackground == null)
            containerSlotBackground = FindChildRecursive(transform, "SlotBackground");
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

    private void CacheProductionReferences()
    {
        if (blackBG == null)
        {
            Transform blackBGTransform = FindChildRecursive(transform, "BlackBG");
            if (blackBGTransform != null)
                blackBG = blackBGTransform.GetComponent<Image>();
        }

        if (sectionTitle == null)
            sectionTitle = FindChildRecursive(transform, "Section_Title") as RectTransform;

        if (sectionButton == null)
        {
            sectionButton = FindChildRecursive(transform, "Section_Button") as RectTransform;

            if (sectionButton == null)
                sectionButton = FindChildRecursive(transform, "ButtonRoot") as RectTransform;
        }

        if (treeKillCountTextAnimator == null && treeKillCountText != null)
            treeKillCountTextAnimator = treeKillCountText.GetComponent<TMPInlineStyleAnimator>();

        if (acquiredLogsHeaderAnimator == null)
            acquiredLogsHeaderAnimator = GetSectionHeaderAnimator("Section_AcquiredLogs");

        if (containerHeaderAnimator == null)
            containerHeaderAnimator = GetSectionHeaderAnimator("Section_Container");

        sectionTitleCanvasGroup = GetOrAddCanvasGroup(sectionTitle);
        sectionButtonCanvasGroup = GetOrAddCanvasGroup(sectionButton);

        sectionKillCountCanvasGroup = AddPendingSectionGroup("Section_KillCount");
        sectionAcquiredLogsCanvasGroup = AddPendingSectionGroup("Section_AcquiredLogs");
        sectionContainerCanvasGroup = AddPendingSectionGroup("Section_Container");
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

        if (containerSlotBackground is RectTransform slotBackgroundRect)
            slotBackgroundStartPosition = slotBackgroundRect.anchoredPosition;

        hasCachedProductionStartState = true;
    }

    private void PlayResultOpenProduction()
    {
        CacheProductionStartState();
        KillResultOpenSequence();
        PrepareResultProductionHidden();

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

        for (int i = 0; i < pendingSectionGroups.Count; i++)
        {
            CanvasGroup canvasGroup = pendingSectionGroups[i];
            if (canvasGroup == null)
                continue;

            canvasGroup.alpha = 0f;
            SetCanvasGroupRaycast(canvasGroup, false);
        }

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

    private CanvasGroup AddPendingSectionGroup(string sectionName)
    {
        RectTransform section = FindChildRecursive(transform, sectionName) as RectTransform;
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(section);

        if (canvasGroup != null && false == pendingSectionGroups.Contains(canvasGroup))
            pendingSectionGroups.Add(canvasGroup);

        return canvasGroup;
    }

    private TMPInlineStyleAnimator GetSectionHeaderAnimator(string sectionName)
    {
        Transform section = FindChildRecursive(transform, sectionName);
        Transform header = FindChildRecursive(section, "Text_Header");

        if (header == null)
            return null;

        return header.GetComponent<TMPInlineStyleAnimator>();
    }

    private TMP_Text GetSectionHeaderText(string sectionName)
    {
        Transform section = FindChildRecursive(transform, sectionName);
        Transform header = FindChildRecursive(section, "Text_Header");

        if (header == null)
            return null;

        return header.GetComponent<TMP_Text>();
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
                treeKillCountTextAnimator?.PlayRevealBounce();
                return;
            }

            SetTreeKillCountText(1);
            treeKillCountTextAnimator?.PlayRevealBounce();
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
                },
                targetTreeKillCount,
                treeKillCountUpDuration)
            .SetEase(Ease.OutQuad));

        return sequence;
    }

    private void PlayAcquiredLogsHeaderProduction()
    {
        SetCanvasGroupVisible(sectionAcquiredLogsCanvasGroup, true);
        acquiredLogsHeaderAnimator?.PlayRevealBounce();
    }

    private void PlayContainerHeaderProduction()
    {
        SetCanvasGroupVisible(sectionContainerCanvasGroup, true);
        containerHeaderAnimator?.PlayRevealBounce();
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

            float startTime = i * resultLogRowInterval;
            sequence.Insert(startTime, CreateResultLogRowProductionSequence(row, logCount.treeType, logCount.count));
        }

        return sequence;
    }

    private Sequence CreateResultLogRowProductionSequence(UI_ResultLogRow row, TreeType treeType, int targetCount)
    {
        Sequence sequence = DOTween.Sequence();
        CanvasGroup rowCanvasGroup = GetOrAddCanvasGroup(row.transform as RectTransform);
        RectTransform rowRect = row.transform as RectTransform;
        Vector2 rowStartPosition = rowRect != null ? rowRect.anchoredPosition : Vector2.zero;

        sequence.AppendCallback(() =>
        {
            if (rowRect != null)
                rowRect.anchoredPosition = rowStartPosition + new Vector2(0f, resultOpenYOffset);

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
            sequence.Join(rowRect.DOAnchorPos(rowStartPosition, slotBackgroundOpenDuration)
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

    private void KillResultOpenSequence()
    {
        if (resultOpenSequence == null)
            return;

        resultOpenSequence.Kill();
        resultOpenSequence = null;

        treeKillCountTextAnimator?.StopRevealBounce();
        acquiredLogsHeaderAnimator?.StopRevealBounce();
        containerHeaderAnimator?.StopRevealBounce();
    }

    private void InitializeResultLogRows()
    {
        RemoveInvalidResultLogRows();
        AddPivotResultLogRows();

        for (int i = 0; i < resultLogRows.Count; i++)
        {
            if (resultLogRows[i] != null)
                resultLogRows[i].Initialize();
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

    private void AddPivotResultLogRows()
    {
        if (resultLogRowPivot == null)
            return;

        UI_ResultLogRow[] rows = resultLogRowPivot.GetComponentsInChildren<UI_ResultLogRow>(true);
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && false == resultLogRows.Contains(rows[i]))
                resultLogRows.Add(rows[i]);
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

        float x = 0f;
        if (count == 2)
            x = index == 0 ? -12f : 12f;
        else if (2 < count)
            x = (index - ((count - 1) * 0.5f)) * 24f;

        rectTransform.anchoredPosition = new Vector2(x, rectTransform.anchoredPosition.y);
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

        for (int i = 0; i < containerSlots.Count; i++)
        {
            UI_InventorySlot slotUI = containerSlots[i];
            if (slotUI == null)
                continue;

            bool isActive = i < displaySlots.Length;
            slotUI.gameObject.SetActive(isActive);

            if (isActive)
                slotUI.UpdateBindSlotData(displaySlots[i], offroadContainer != null ? offroadContainer.maxItemCntPerSlot : 99, playChangedSlotInteraction && displaySlots[i].HasChanged);
        }

        ApplyContainerSlotLayout();
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
