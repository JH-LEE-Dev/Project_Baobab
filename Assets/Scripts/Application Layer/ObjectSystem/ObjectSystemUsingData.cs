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
    SporepuffTree,
    FluffyMyceliumTree,
    BellpineTree,
    StarrootTree,
    MoonhaloTree,
    GalaxygrainTree,
    CinderTree,
    LavasapTree,
    ObsidianTree,
    Max,
}

public enum TreeGrade
{
    None,
    Normal,
    Fascinating,
    Advanced,
    Perfect,
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
    public TreeStatData treeStatData;

    public TreeData(TreeType _type, TreeGrade _grade, TreeVisualData _treeVisualData, TreeStatData _treeStatData)
    {
        type = _type;
        grade = _grade;
        treeVisualData = _treeVisualData;
        treeStatData = _treeStatData;
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
public struct LogDropProbData
{
    public TreeGrade treeGrade;
    public List<LogProbData> probDatas;
}

[Serializable]
public struct LogDropCntData
{
    public TreeType treeType;
    public int minCnt;
    public int maxCnt;
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
    LostAndFoundBox,
    SporePotion,
    StarCompass,
    Max,
}

[Serializable]
public struct LootDropData
{
    public LootType lootType;
    public float probability;
}

[Serializable]
public struct CarrotSpawnData
{
    public AnimalType animalType;
    public int minAmountPerBundle;
    public int maxAmountPerBundle;
    public int minSpawnBundle;
    public int maxSpawnBundle;
}

[Serializable]
public struct HiddenMapTreeGradeData
{
    public TreeGrade treeGrade;
    public float probability;
}

[Serializable]
public struct HiddenMapTreeGradeProbData
{
    public HiddenMapGrade grade;
    public List<HiddenMapTreeGradeData> probability;
}


