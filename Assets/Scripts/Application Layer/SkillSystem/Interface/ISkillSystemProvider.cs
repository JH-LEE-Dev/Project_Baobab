using System.Collections.Generic;

public interface ISkillSystemProvider
{
    AbilityLevelUpRejectReason TryApplySkill(SkillType _type);
    AbilityLevelUpRejectReason CanApplySkill(SkillType _type);
    bool IsApplied(SkillType _type);
    List<SkillNode> GetPrerequisites(SkillType _type);
}
