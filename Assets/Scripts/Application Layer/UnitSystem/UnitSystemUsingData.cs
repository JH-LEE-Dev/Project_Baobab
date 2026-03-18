using System;

public struct LogStateCount
{
    public LogState state;
    public int count;
}

[Serializable]
public class InventorySlot : IInventorySlot
{
    public ItemData itemData;
    public int totalCount;
    
    // 내부 데이터 저장용 (인덱스 = LogState enum 값)
    private int[] logStateCounts = new int[Enum.GetValues(typeof(LogState)).Length];
    
    // 인터페이스 노출용 (정렬된 캐시)
    private LogStateCount[] sortedLogStateCounts;
    private bool isDirty = true;

    public int count => totalCount;

    IItemData IInventorySlot.itemData => itemData;

    LogStateCount[] IInventorySlot.logStateCounts
    {
        get
        {
            if (isDirty)
            {
                UpdateSortedCounts();
                isDirty = false;
            }
            return sortedLogStateCounts;
        }
    }

    public InventorySlot()
    {
        itemData = null;
        totalCount = 0;

        // 캐시 배열 초기화
        var states = (LogState[])Enum.GetValues(typeof(LogState));
        sortedLogStateCounts = new LogStateCount[states.Length];
        for (int i = 0; i < states.Length; i++)
        {
            sortedLogStateCounts[i].state = states[i];
            sortedLogStateCounts[i].count = 0;
        }
        
        for (int i = 0; i < logStateCounts.Length; i++)
        {
            logStateCounts[i] = 0;
        }
    }

    public void Setup(ItemData _data, int _count)
    {
        itemData = _data;
        totalCount = _count;

        for (int i = 0; i < logStateCounts.Length; i++)
        {
            logStateCounts[i] = 0;
        }

        if (_data is LogItemData logData)
        {
            logStateCounts[(int)logData.logState] = _count;
        }
        
        isDirty = true;
    }

    public void AddCount(Item _item)
    {
        if (_item is LogItem logItem)
        {
            logStateCounts[(int)logItem.logState]++;
            isDirty = true;
        }
        totalCount++;
    }

    public int GetCountByState(LogState _state)
    {
        return logStateCounts[(int)_state];
    }

    private void UpdateSortedCounts()
    {
        // 1. 현재 데이터 동기화
        for (int i = 0; i < sortedLogStateCounts.Length; i++)
        {
            sortedLogStateCounts[i].count = logStateCounts[(int)sortedLogStateCounts[i].state];
        }

        // 2. 버블 정렬 (N=7이므로 GC Alloc 없이 가장 효율적)
        // 정렬 기준: 1. 수량 내림차순, 2. 등급(LogState) 내림차순
        int n = sortedLogStateCounts.Length;
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < n - i - 1; j++)
            {
                bool swap = false;
                if (sortedLogStateCounts[j].count < sortedLogStateCounts[j + 1].count)
                {
                    swap = true;
                }
                else if (sortedLogStateCounts[j].count == sortedLogStateCounts[j + 1].count)
                {
                    if (sortedLogStateCounts[j].state < sortedLogStateCounts[j + 1].state)
                    {
                        swap = true;
                    }
                }

                if (swap)
                {
                    LogStateCount temp = sortedLogStateCounts[j];
                    sortedLogStateCounts[j] = sortedLogStateCounts[j + 1];
                    sortedLogStateCounts[j + 1] = temp;
                }
            }
        }
    }
}
