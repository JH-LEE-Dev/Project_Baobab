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
    public string pictureKey;
    public int gridX;
    public int gridY;
    public string[] parentSkillTypes;
}

[Serializable]
public class AbilityLevelCostJson
{
    public int level;
    public string resourceType;
    public int amount;
}

[Serializable]
public class AbilityPictureBinding
{
    public string pictureKey;
    public UnityEngine.Sprite sprite;
}
