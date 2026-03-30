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

public enum TreeState
{
    Idle,
    Wet,
    Max,
}

public enum LogState
{
    Destoyed,
    Damaged,
    Wet,
    Normal,
    Fascinating,
    Advanced,
    Perfect,
}

public struct TreeData
{
    public TreeType type;
    public TreeGrade grade;
    public TreeState treeState;

    public TreeData(TreeType _type, TreeGrade _grade,TreeState _treeState)
    {
        treeState = _treeState;
        type = _type;
        grade = _grade;
    }
}

public enum ItemType
{
    None,
    Log,
    Max,
}


[Serializable]
public struct LogDropData
{
    public TreeGrade treeGrade;
    public List<LogStateProbData> probDatas;
}

[Serializable]
public struct LogStateProbData
{
    public TreeState treeState;
    public List<LogProbData> probDatas;
}

[Serializable]
public struct LogProbData
{
    public LogState type;
    public float probability;
}