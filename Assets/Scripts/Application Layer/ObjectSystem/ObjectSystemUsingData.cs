using System;
using System.Collections.Generic;

public enum PortalType
{
    None,
    ToDungeonPortal,
    ToTownPortal,
}

public enum TreeType
{
    None,
    OakTree,
    PineTree,
    BirchTree,
    Max,
}

public enum TreeGrade
{
    None,
    Normal,
    Fascinating,
    Max,
}

public enum LogState
{
    Destoyed,
    Damaged,
    Normal,
    Fascinating,
    Advanced,
    Perfect,
}

public struct TreeData
{
    public TreeType type;
    public TreeGrade grade;
    public TreeVisualData treeVisualData;

    public TreeData(TreeType _type, TreeGrade _grade, TreeVisualData _treeVisualData)
    {
        type = _type;
        grade = _grade;
        treeVisualData = _treeVisualData;
    
    }
}

public enum ItemType
{
    None,
    Log,
    Loot,
    Carrot,
    Max,
}


[Serializable]
public struct LogDropData
{
    public TreeGrade treeGrade;
    public List<LogProbData> probDatas;
}

[Serializable]
public struct LogProbData
{
    public LogState type;
    public float probability;
}

public enum LootType
{
    None,
    WelcomeNoob,
    Max,
}

[Serializable]
public struct LootDropData
{
    public LootType lootType;
    public float probability;
}