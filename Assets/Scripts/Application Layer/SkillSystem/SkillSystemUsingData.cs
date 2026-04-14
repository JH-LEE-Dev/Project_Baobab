using System;
using System.Collections.Generic;

[Serializable]
public struct Skill
{
    public SkillType skillType;
    public bool bApplied;
    public List<SkillCommand<ICommandHandler>> skillTypes;
    public List<SkillType> prerequisiteSkills;
}

public enum SkillType
{
    None,
    Rest,//휴식
    InventoryExpansion1,//인벤토리확장I
    AxeDamage1,//도끼데미지
    hunting,//수렵
    Max
}
