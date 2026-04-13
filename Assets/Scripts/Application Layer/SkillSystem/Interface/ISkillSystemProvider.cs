using System.Collections.Generic;

public interface ISkillSystemProvider
{
    bool TryApplySkill(SkillType _type);
    bool CanApplySkill(SkillType _type);
    bool IsApplied(SkillType _type);
    List<SkillNode> GetPrerequisites(SkillType _type);
}
