using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIView_Result : UIView
{
    public event Action GoHomeButtonClickedEvent;
    public event Action RetryButtonClickedEvent;

    private IInventory offroadContainer;
    private IInventory characterInventory;
    private IDungeonResultProvider dungeonResultProvider;

    [Header("UI References")]
    [SerializeField] private Button goHomeButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private GameObject resultContentsRoot;
    [SerializeField] private TMP_Text treeKillCountText;
    [SerializeField] private Transform resultLogRowPivot;
    [SerializeField] private TMP_Text emptyLogText;
    [SerializeField] private List<UI_ResultLogRow> resultLogRows = new List<UI_ResultLogRow>(2);

    private int[] startOffroadLogCounts;

    #region Public Override Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        CacheUIReferences();
        InitializeResultLogRows();

        if (goHomeButton != null)
            goHomeButton.onClick.AddListener(OnGoHomeButtonClicked);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryButtonClicked);

        SetResultContentsActive(false);
    }

    public void DependencyInjection(IInventory _offroadContainer, IInventory _characterInventory, IDungeonResultProvider _dungeonResultProvider)
    {
        offroadContainer = _offroadContainer;
        characterInventory = _characterInventory;
        dungeonResultProvider = _dungeonResultProvider;
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Release()
    {
        base.Release();
    }

    public void OnGoHomeButtonClicked()
    {
        GoHomeButtonClickedEvent?.Invoke();
        SetResultContentsActive(false);
    }

    public void OnRetryButtonClicked()
    {
        RetryButtonClickedEvent?.Invoke();
        SetResultContentsActive(false);
    }

    public void OpenResultUI()
    {
        RefreshResult();
        SetResultContentsActive(true);
    }

    public void DungeonStarted()
    {
        SnapshotOffroadContainer();
        SetResultContentsActive(false);
    }

    #endregion

    #region Protected Override Methods

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    #endregion

    #region Unity Event Functions

    public override void OnDestroy()
    {
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
            if (treeKillCount != null)
                treeKillCountText = treeKillCount.GetComponent<TMP_Text>();
        }

        if (resultLogRowPivot == null)
            resultLogRowPivot = FindChildRecursive(transform, "UI_ResultLogRowPivot");

        if (emptyLogText == null)
        {
            Transform emptyText = FindChildRecursive(transform, "Text_Empty");
            if (emptyText != null)
                emptyLogText = emptyText.GetComponent<TMP_Text>();
        }
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
        RefreshTreeKillCount();
        RefreshAcquiredLogs();
    }

    private void RefreshTreeKillCount()
    {
        if (treeKillCountText == null)
            return;

        int treeKillCount = dungeonResultProvider != null ? dungeonResultProvider.GetTreeKillCnt() : 0;
        treeKillCountText.text = $"\uBC8C\uBAA9\uD55C \uB098\uBB34 {treeKillCount}\uAC1C";
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
