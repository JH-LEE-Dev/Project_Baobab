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
public struct LogDropCntData
{
    public TreeType treeType;
    public int minCnt;
    public int maxCnt;
}

/// <summary>
/// 보석 등급 원목이 드랍될 때 붙일 아우라 프리셋 매핑.
/// 등급별로 색·스케일·프리즘 설정이 다른 프리팹을 그대로 쓴다.
/// </summary>
[Serializable]
public struct LogStateAuraData
{
    public LogState logState;
    public ItemAuraEffectController auraPrefab;
}

public enum LootType
{
    None,
    WelcomeNoob,
    LostAndFoundBox,
    SporePotion,
    StarCompass,
    ObsidianCharm,
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


