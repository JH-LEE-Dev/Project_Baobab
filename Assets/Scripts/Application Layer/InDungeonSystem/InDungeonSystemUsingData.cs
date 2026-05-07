
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