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

public struct SkillInfo
{
    public SkillType skillType;
    public int currentLevel;
    public int maxLevel;
    public MoneyType moneyType;
    public int nextCost;
    public List<SkillType> prerequisiteSkills;
}

public enum SkillType
{
    None,
    OffRoadVehicle,//오프로드차량 


    InventoryExpansion1,//인벤토리확장I
    InventoryExpansion2,//인벤토리확장II


    LogCapacityIncrease1,//원목 수납력 증가I
    LogCapacityIncrease2,//원목 수납력 증가II


    SawmillLogStorageExpansion1,//제재소 원목 보관함 확장 I
    SawmillLogStorageExpansion2,//제재소 원목 보관함 확장 II



    ConveyorSpeed1, //컨베이어 속도 I
    PowerSupply, // 전력공급
    LogProcessingSpeed1,//원목가공속도I
    LogProcessingSpeed2, //원목가공속도II





    LogValue1, //원목 가치1
    VerdantPlainsOvergrowth, //초목 평원림 과성장

    BountifulTree1, // 아낌없이주는나무1
    FascinatingLogChance1, // 희귀원목확률I





    


    AxeDamage1,//도끼데미지I
    AxeDamage2,//도끼데미지II

    AxeDurability1, //도끼 내구도I
    SteelAxe1, // 강철도끼 I
    AxeRange1, // 도끼 범위
    Shockwave1, //충격파I
    ShockwaveDamage1, //충격파데미지I
    ShockwaveRange1, // 충격파 범위 I
    AxeAttackSpeed1, // 도끼 공격 속도 I



    EquipmentSwitchSpeed1,//장비 스위칭 속도1
    EfficientMovement1, // 효율적인 이동I
    MovementSpeed, //이동속도I




    hunting,//수렵
    GunDamage1,//총기데미지1
    GunDamage2,//총기데미지2
    ReserveAmmoIncrease1, // 예비탄창증가1
    GunMagazineCapacity1, //총기탄창증가1
    GunPenetration1, //총기관통1
    Ricochet1, //확산탄1
    RicochetDamage1, //확산탄 데미지1
    RicochetRange1, //확산탄 범위
    GunAttackSpeed1, //총기공격속도1
    GunReloadSpeed1, //총기 재장전 속도1



    CarrotBundle, // 당근 다발
    RabbitBoom, // 토끼 대 번성
    CarrotFarm, // 당근 농장
    PickupRange1, //획득범위


    Stamina1, // 지구력1
    Stamina2, // 지구력2

    FatigueMaxIncrease1, // 피로도 최대치 증가1
    FatigueMaxIncrease2, // 피로도 최대치 증가 II
    FatigueMaxIncrease3, // 피로도 최대치 증가3

    HiddenMap, // 히든맵
    HiddenMapGaugeGain1, // 히든맵 게이지 획득량I
    HiddenMapGaugeGain2, // 히든맵 게이지 획득량2
    HiddenMapFatigueRecovery, // 히든맵 피로도 회복 I

    MAX
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
    OffRoadVehicle,
    ConveyorSpeed,
    ReserveAmmoIncrease,
    GunMagazineCapacity,
    Ricochet,
    GunPenetration,
    EfficientMovement,
    Shockwave,
    AxeRange,
    AxeDurability,
    SteelAxe,
    PowerSupply,
    PickupRange,
    FascinatingLogChance,
    RicochetRange,
    RicochetDamage,
    GunReloadSpeed,
    GunAttackSpeed,
    MovementSpeed,
    ShockwaveDamage,
    ShockwaveRange,
    AxeAttackSpeed,
}

public enum AbilityLevelUpRejectReason
{
    None,
    Pass,
    NotEnoughMoney,
    NotEnoughCarrot,
    MaxLevel,
}
