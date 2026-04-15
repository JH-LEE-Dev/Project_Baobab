using System;

[Serializable]
public class AbilityNodeDatabaseJson
{
    public AbilityNodeDefinitionJson[] nodes;
}

[Serializable]
public class AbilityNodeDefinitionJson
{
    public string skillType;
    public string displayName;
    public string description;
    public int maxLevel;
    public AbilityLevelCostJson[] levelCosts;
    public int gridX;
    public int gridY;
    public string[] parentSkillTypes;
    public AbilityParentLineRouteJson[] parentLineRoutes;
}

[Serializable]
public class AbilityParentLineRouteJson
{
    public string parentSkillType;
    public bool usePivot;
    public int pivotX;
    public int pivotY;
}

[Serializable]
public class AbilityLevelCostJson
{
    public int level;
    public string moneyType;
    public int amount;
}

[Serializable]
public class AbilityPictureBinding
{
    public SkillType skillType;
    public UnityEngine.Sprite sprite;
}

public enum AbilityLineSegmentSpriteType
{
    Row4,
    Col4,
    DiagSENW4,
    DiagSWNE4,
    Row8,
    Col8,
    DiagSENW8,
    DiagSWNE8,
}

[Serializable]
public class AbilityLineSegmentSpriteBinding
{
    public AbilityLineSegmentSpriteType lineType;
    public UnityEngine.Sprite sprite;
}
