using System.Collections.Generic;

public interface ISkillSystemProvider
{
    AbilityLevelUpRejectReason TryApplySkill(SkillType _type);
#if UNITY_EDITOR
    // 비용 없이 찍는 테스트 기능. 에디터 전용이다.
    AbilityLevelUpRejectReason TryApplySkillWithoutCost(SkillType _type);
#endif
    AbilityLevelUpRejectReason CanApplySkill(SkillType _type);
    void RequestSkillValuePreviewData(SkillType _type);
    bool IsApplied(SkillType _type, out int _level);
    List<SkillNode> GetPrerequisites(SkillType _type);
    SkillInfo GetSkillInfo(SkillType _type);
    int GetCurrentPrestigeLevel();
    int GetCurrentPrestigeExp();
    int GetPrestigeExpLimit();
}
