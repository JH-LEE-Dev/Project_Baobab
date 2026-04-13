
using System.Collections.Generic;

public struct Skill
{
    public List<SkillCommand<ICommandHandler>> skillTypes;
}

public enum SkillType
{
    None,
    Max
}