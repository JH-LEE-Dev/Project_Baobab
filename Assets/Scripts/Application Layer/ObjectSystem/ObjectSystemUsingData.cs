
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

public struct TreeInitData
{
    public TreeType type;
    public TreeGrade grade;

    public TreeInitData(TreeType _type, TreeGrade _grade)
    {
        type = _type;
        grade = _grade;
    }
}