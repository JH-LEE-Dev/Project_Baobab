using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LogItemTypeDataBase", menuName = "Game/Log Item Type Database")]
public class LogItemTypeDataBase : ScriptableObject
{
    public List<LogItemTypeData> datas;

    public LogItemTypeData Get(TreeType _type)
    {
        return datas.Find(x => x.treeType == _type);
    }
}
