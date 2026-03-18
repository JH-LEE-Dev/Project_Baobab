using UnityEngine;

public class LogItemData : ItemData, ILogItemData
{
    public LogState logState;
    public TreeType treeType;

    LogState ILogItemData.logState => logState;

    TreeType ILogItemData.treeType => treeType;

    public override bool IsSameType(ItemData _other)
    {
        if (base.IsSameType(_other) && _other is LogItemData otherLog)
        {
            return logState == otherLog.logState && treeType == otherLog.treeType;
        }
        return false;
    }

    public override void CopyFrom(Item _item)
    {
        base.CopyFrom(_item);
        if (_item is LogItem logItem)
        {
            logState = logItem.logState;
            treeType = logItem.treeType;
        }
    }

    public override void Reset()
    {
        base.Reset();
        logState = LogState.Destoyed;
        treeType = TreeType.None;
    }
}
