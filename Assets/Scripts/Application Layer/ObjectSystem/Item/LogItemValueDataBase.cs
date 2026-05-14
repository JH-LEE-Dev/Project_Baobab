using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LogItemValueDataBase", menuName = "Game/Log Item Value Database")]
public class LogItemValueDataBase : ScriptableObject
{
    public List<LogItemValueData> datas;

    public LogItemValueData Get(TreeType _type)
    {
        return datas.Find(x => x.treeType == _type);
    }
}

[Serializable]
public struct LogItemStateValueData
{
    public LogState logState;
    public float valueMultiplier;
}

[Serializable]
public struct LogItemDurabilityData
{
    public LogState logState;
    public float durabilityMultiplier;
}
