using System;

public struct LogStateCount
{
    public LogState state;
    public int count;
}

public struct TreeTypeCount
{
    public TreeType treeType;
    public int count;
}

[Serializable]
public class InventorySlot : IInventorySlot
{
    public ItemData itemData;
    public int totalCount;

    // 내부 데이터 저장용 (인덱스 = TreeType enum 값)
    private int[] treeTypeCounts = new int[Enum.GetValues(typeof(TreeType)).Length];

    // 인터페이스 노출용 (정렬된 캐시)
    private LogStateCount[] logStateCountsCache = new LogStateCount[1];
    private TreeTypeCount[] sortedTreeTypeCounts;
    private bool isTreeDirty = true;

    public event Action SlotUpdatedEvent;

    public int count => totalCount;

    IItemData IInventorySlot.itemData => itemData;

    LogStateCount[] IInventorySlot.logStateCounts
    {
        get
        {
            if (itemData is LogItemData logData)
            {
                logStateCountsCache[0].state = logData.logState;
                logStateCountsCache[0].count = totalCount;
            }
            else
            {
                logStateCountsCache[0].state = LogState.Normal;
                logStateCountsCache[0].count = 0;
            }
            return logStateCountsCache;
        }
    }

    TreeTypeCount[] IInventorySlot.treeTypeCounts
    {
        get
        {
            if (isTreeDirty)
            {
                UpdateSortedTreeTypeCounts();
                isTreeDirty = false;
            }
            return sortedTreeTypeCounts;
        }
    }

    public InventorySlot()
    {
        itemData = null;
        totalCount = 0;

        // 캐시 배열 초기화 (TreeType)
        var treeTypes = (TreeType[])Enum.GetValues(typeof(TreeType));
        sortedTreeTypeCounts = new TreeTypeCount[treeTypes.Length];
        for (int i = 0; i < treeTypes.Length; i++)
        {
            sortedTreeTypeCounts[i].treeType = treeTypes[i];
            sortedTreeTypeCounts[i].count = 0;
        }

        for (int i = 0; i < treeTypeCounts.Length; i++)
        {
            treeTypeCounts[i] = 0;
        }
    }

    public void Setup(ItemData _data, int _count)
    {
        itemData = _data;
        totalCount = _count;

        for (int i = 0; i < treeTypeCounts.Length; i++)
        {
            treeTypeCounts[i] = 0;
        }

        if (_data is LogItemData logData)
        {
            treeTypeCounts[(int)logData.treeType] = _count;
        }

        isTreeDirty = true;
    }

    public void AddCount(Item _item)
    {
        if (_item is LogItem logItem)
        {
            treeTypeCounts[(int)logItem.treeType]++;
            isTreeDirty = true;
        }
        totalCount++;

        SlotUpdatedEvent?.Invoke();
    }

    public void AddCountByState(LogState _state, TreeType _treeType = TreeType.None)
    {
        // _treeType이 명시되지 않은 경우 현재 itemData의 treeType 사용
        TreeType targetTreeType = _treeType;
        if (targetTreeType == TreeType.None && itemData is LogItemData logData)
        {
            targetTreeType = logData.treeType;
        }
        
        if (targetTreeType != TreeType.None)
        {
            treeTypeCounts[(int)targetTreeType]++;
            isTreeDirty = true;
        }

        totalCount++;

        SlotUpdatedEvent?.Invoke();
    }

    public LogState TakeOneItem()
    {
        if (totalCount <= 0) return LogState.Normal;

        LogState takenState = LogState.Normal;
        if (itemData is LogItemData logData)
        {
            takenState = logData.logState;

            // 수량이 있는 나무 종류 중 가장 높은 등급부터 하나 차감
            for (int i = treeTypeCounts.Length - 1; i >= 0; i--)
            {
                if (treeTypeCounts[i] > 0)
                {
                    treeTypeCounts[i]--;
                    break;
                }
            }
        }

        totalCount--;
        isTreeDirty = true;

        SlotUpdatedEvent?.Invoke();

        return takenState;
    }

    public int GetCountByTreeType(TreeType _treeType)
    {
        return treeTypeCounts[(int)_treeType];
    }

    public int[] GetTreeTypeCounts()
    {
        int[] copy = new int[treeTypeCounts.Length];
        Array.Copy(treeTypeCounts, copy, treeTypeCounts.Length);
        return copy;
    }

    public void LoadTreeTypeCounts(int[] _counts)
    {
        if (_counts == null || _counts.Length != treeTypeCounts.Length) return;
        Array.Copy(_counts, treeTypeCounts, treeTypeCounts.Length);
        isTreeDirty = true;
    }

    private void UpdateSortedTreeTypeCounts()
    {
        // 1. 현재 데이터 동기화
        for (int i = 0; i < sortedTreeTypeCounts.Length; i++)
        {
            sortedTreeTypeCounts[i].count = treeTypeCounts[(int)sortedTreeTypeCounts[i].treeType];
        }

        // 2. 버블 정렬
        // 정렬 기준: 1. 수량 내림차순, 2. 등급(TreeType) 내림차순
        int n = sortedTreeTypeCounts.Length;
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < n - i - 1; j++)
            {
                bool swap = false;
                if (sortedTreeTypeCounts[j].count < sortedTreeTypeCounts[j + 1].count)
                {
                    swap = true;
                }
                else if (sortedTreeTypeCounts[j].count == sortedTreeTypeCounts[j + 1].count)
                {
                    if (sortedTreeTypeCounts[j].treeType < sortedTreeTypeCounts[j + 1].treeType)
                    {
                        swap = true;
                    }
                }

                if (swap)
                {
                    TreeTypeCount temp = sortedTreeTypeCounts[j];
                    sortedTreeTypeCounts[j] = sortedTreeTypeCounts[j + 1];
                    sortedTreeTypeCounts[j + 1] = temp;
                }
            }
        }
    }
}

public enum WeaponMode
{
    None,
    Axe,
    Rifle,
}

public enum MoneyType
{
    None,
    Coin,
    Carrot,
    SunEssence,
    MoonEssence,
    LightningEssnece,
    Max
}

public enum AnimalType
{
    None,
    Rabbit,
    MRabbit,
    HRabbit,
}

[Serializable]
public struct StaminaAmountData
{
    public ForestType forestType;
    public float decAmount;
}
