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

/// <summary>
/// 제재소 "특등품 평가"(가격 2배)를 커터에서 미리 굴려 둔 결과. 원목이 평가기에 들어갈 때 이 값을 그대로 쓴다.
/// 판정을 앞당긴 이유는 커터에서 튀어나오는 쌍둥이 원목 연출이 결과를 미리 알아야 해서다. 확률·배율은 그대로다.
/// None은 미리 굴리지 않은 원목(세이브에서 복원된 벨트 원목 등)이고, 그때는 예전처럼 평가 순간에 굴린다.
/// </summary>
public enum ETopgradePreRoll
{
    None,
    Hit,
    Miss,
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
    GemOre,
    Max,
}

/// <summary>
/// 보석 나무가 최종적으로 쓰러질 때 떨어지는 원석의 종류.
/// TreeObj의 보석 단계(gemStage 1/2/3)와 1:1로 대응한다.
/// </summary>
public enum GemOreType
{
    None,
    Gold,    // 황금 (gemStage 1)
    Diamond, // 다이아 (gemStage 2)
    Prism,   // 프리즘 (gemStage 3)
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


