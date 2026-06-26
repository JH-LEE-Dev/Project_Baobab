
using System;

public enum DungeonType
{
    None,
    Forest1_1,
    Forest1_2,
    Forest1_3,
    Max,
}

[Serializable]
public struct TreeGradeProb
{
    public TreeGrade grade;
    public float probability;
    public TreeGradeProb(TreeGrade _grade, float _probability)
    {
        grade = _grade;
        probability = _probability;
    }
}

public enum HiddenMapGrade
{
    None,
    Normal,
    Fascinating,
    Advanced,
    Perfect,
    Max,
}

[Serializable]
public struct HiddenMapGradeProbData
{
    public HiddenMapGrade grade;
    public float probability;
    public HiddenMapGradeProbData(HiddenMapGrade _hiddenMapGrade, float _probability)
    {
        grade = _hiddenMapGrade;
        probability = _probability;
    }
}

public enum DungeonState
{
    None,
    Stage1_Idle0,
    Stage1_Idle1,
    Stage1_Idle2,
    Stage1_Idle3,
    Stage2_Idle0,
    Stage2_Idle1,
    Stage2_Idle2,
    Stage3_Idle0,
    Stage3_Idle1,
    Stage3_Idle2,
    Stage4_Idle0,
    Stage4_Idle1,
    Stage4_Idle2,
}