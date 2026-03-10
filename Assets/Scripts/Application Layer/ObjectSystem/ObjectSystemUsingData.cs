
public enum PortalType
{
    None,
    ToDungeonPortal,
    ToTownPortal,
}

public enum TreeType
{
    None,
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