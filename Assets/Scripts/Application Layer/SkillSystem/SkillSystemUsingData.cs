using System;
using System.Collections.Generic;

[Serializable]
public struct SkillCost
{
    public float alpha;
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
public struct Skill
{
    public SkillType skillType;
    public int maxLevel;
    public SkillCost cost;
    public List<SkillCommandInfo> skillTypes;
    public List<SkillType> prerequisiteSkills;
}

public enum SkillType
{
    None,
    Rest,//휴식


    InventoryExpansion1,//인벤토리확장I
    LogCapacityIncrease1,//원목 수납력 증가I
    SawmillLogStorageExpansion1,//제재소 원목 보관함 확장
    LogProcessingSpeed1,//원목가공속도1


    LogValue1, //원목 가치1
    VerdantPlainsOvergrowth, //초목 평원림 과성장


    AxeDamage1,//도끼데미지I

    EquipmentSwitchSpeed,//장비 스위칭 속도1

    hunting,//수렵
    GunDamage1,//총기데미지1



    CarrotBundle, // 당근 다발
    RabbitBoom, // 토끼 대 번성


    Stamina1, // 지구력1
    FatigueRecoveryBoost1, //피로도 회복 강화1
    FatigueMaxIncrease1, // 피로도 최대치 증가1
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
