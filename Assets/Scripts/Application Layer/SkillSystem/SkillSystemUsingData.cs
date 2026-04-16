using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProgressionType
{
    Manual,            // 1. 공식 없이 직접 값을 리스트로 책정
    BasePlusLevelBase, // 2. 값 + (레벨 * 값)
    BaseTimesLevel,    // 3. 값 * 레벨
    Constant           // 4. 레벨에 상관없이 고정된 값 (baseValue 사용)
}

[Serializable]
public struct ProgressionCurve
{
    public ProgressionType type;
    public float baseValue;
    public List<float> manualValues; // Manual 타입일 때 사용 (인덱스 0이 1레벨)

    public float Evaluate(int _targetLevel)
    {
        if (_targetLevel <= 0) return 0;

        switch (type)
        {
            case ProgressionType.Manual:
                if (manualValues != null && manualValues.Count >= _targetLevel)
                    return manualValues[_targetLevel - 1];
                return 0;
            case ProgressionType.BasePlusLevelBase:
                // 값 + (레벨 * 값)
                return baseValue + (_targetLevel * baseValue);
            case ProgressionType.BaseTimesLevel:
                // 값 * 레벨
                return baseValue * _targetLevel;
            case ProgressionType.Constant:
                // 레벨에 관계없이 고정 값
                return baseValue;
            default:
                return 0;
        }
    }
}

[Serializable]
public struct SkillCost
{
    public ProgressionCurve moneyCurve;
    public ProgressionCurve carrotCurve;
}


[Serializable]
public struct SkillCommandInfo
{
    public SkillCommandType skillCommandType;
    public ProgressionCurve amountCurve;
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
    logCapacityIncrease,
    SawmillLogStorageExpansion,
    LogProcessingSpeed,
    AxeDamage,
    hunting,
    EquipmentSwitchSpeed,
    GunDamage,
    LogValue,
    VerdantPlainsOvergrowth,
    CarrotBundle,
    RabbitBoom,
    Stamina,
    StaminaRecoveryBoost,
    StaminaMaxIncrease,
}

public enum AbilityLevelUpRejectReason
{
    None,
    Pass,
    NotEnoughMoney,
    NotEnoughCarrot,
    MaxLevel,
}
