using System;
using System.Collections.Generic;

[Serializable]
public struct SkillLevelCost
{
    public int level;
    public int moneyCost;
    public int carrotCost;
}


[Serializable]
public struct SkillCommandInfo
{
    public SkillCommandType skillCommandType;
    public float amount;
}

[Serializable]
public struct SkillCommandInfoPerLevel
{
    public int level;
    public SkillCommandInfo info;
}

[Serializable]
public struct Skill
{
    public SkillType skillType;
    public int maxLevel;
    public List<SkillLevelCost> cost;
    public List<SkillCommandInfoPerLevel> skillTypes;
    public List<SkillType> prerequisiteSkills;
}

public enum SkillType
{
    None,
    Rest,//휴식
    InventoryExpansion1,//인벤토리확장I
    AxeDamage1,//도끼데미지I
    hunting,//수렵
    Max
}

public enum SkillCommandType
{
    None,
    InventoryExpansion,
}

public enum AbilityLevelUpRejectReason
{
    None,
    Pass,
    NotEnoughMoney,
    NotEnoughCarrot,
    MaxLevel,
}
