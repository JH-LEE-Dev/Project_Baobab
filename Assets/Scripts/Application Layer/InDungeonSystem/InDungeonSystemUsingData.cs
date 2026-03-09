
using System;

public enum DungeonType
{
    None,
    Forest1,
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
