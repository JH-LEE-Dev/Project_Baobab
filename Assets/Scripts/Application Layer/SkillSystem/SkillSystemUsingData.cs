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
    Rest,
    InventoryExpansion1,
    Max
}
