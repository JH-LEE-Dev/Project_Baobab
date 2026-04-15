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
public struct Skill
{
    public SkillType skillType;
    public int maxLevel;
    public List<SkillLevelCost> cost;
    public List<SkillCommand<ICommandHandler>> skillTypes;
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

public enum AbilityLevelUpRejectReason
{
    None,
    Pass,
    NotEnoughMoney,
    NotEnoughCarrot,
    MaxLevel,
}
