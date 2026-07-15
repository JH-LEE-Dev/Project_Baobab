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
    public long nextCost;
    public List<SkillType> prerequisiteSkills;
}

public enum SkillType
{
    None,
    InventoryExpansion1,//인벤토리확장I


    LogCapacityIncrease1, //원목 수납력 증가I
    LogCapacityIncrease2, //원목 수납력 증가II


    SawmillLogStorageExpansion1,//제재소 원목 보관함 확장 I
    SawmillLogStorageExpansion2,//제재소 원목 보관함 확장 II


    ConveyorSpeed1, //컨베이어 속도 I
    PowerSupply, // 전력공급
    LogProcessingSpeed1,//원목가공속도I
    LogProcessingSpeed2, //원목가공속도II





    LogValue1, //원목 가치1
    WideGreenForestOvergrowth, // 풀빛너른숲 과성장 

    BountifulTree1, // 아낌없이주는나무1
    BountifulTree2, // 아낌없이주는나무2

    FascinatingLogChance1, // 희귀원목확률I








    AxeDamage1,//도끼데미지I
    AxeDamage2,//도끼데미지II
    AxeDamage3,//도끼데미지II


    AxeDurability1, //도끼 내구도I

    SteelAxe1, // 강철도끼 I

    AxeRange1, // 도끼 범위 I
    Shockwave1, //충격파I
    ShockwaveDamage1, //충격파데미지I
    ShockwaveRange1, // 충격파 범위 I


    AxeAttackSpeed1, // 도끼 공격 속도 I

    AxeCriticalChance1, // 도끼 치명타 확률1
    AxeCriticalDamage1, // 도끼 치명타 데미지1



    EfficientMovement1, // 효율적인 이동I
    MovementSpeed, //이동속도I

    SporeShieldRegenBlock1, // 포자막 회복 억제1




    PickupRange1, //획득범위 I


    Stamina1, // 지구력1
    Stamina2, // 지구력2

    FatigueMaxIncrease1, // 피로도 최대치 증가1
    FatigueMaxIncrease2, // 피로도 최대치 증가 II
    FatigueMaxIncrease3, // 피로도 최대치 증가3

    SourceOfSpeed1, // 속도의 원천

    WoodenTransportBoxExpansion1,  // 운반상자확장1
    WoodenTransportBoxExpansion2,   //  운반상자확장2
    LogValue2, // 원목가치2

    Shockwave2, //충격파2
    ShockwaveDamage2, //충격파데미지2
    ShockwaveRange2, // 충격파 범위2

    ShockWaveEnforcement,  // 충격파 파동 강화 M
    ShockWaveCritical,  // 충격파 치명타 적용 M
    ShockwaveDamage3, // 충격파데미지3
    Shockwave3, //충격파 M
    ShockWaveMastery, // 충격파 경지
    ShockwaveRange3, // 충격파 범위3

    SteelAxe2, // 강철도끼2
    AxeDurability2, // 도끼내구도2
    MultiAttack, //도끼 다중 공격
    AxeDamage4, // 도끼 공격력 4
    AxeCriticalChance2, // 도끼 치명타 확률2
    AxeCriticalDamage2, // 도끼 치명타 데미지2


    Boomerang1, //부메랑1
    BoomerangDamage1, //부메랑데미지1
    BoomerangRange1, //부메랑 범위1

    LogValue3, // 원목가치 3
    BountifulTree3, // 아낌없나무 3
    FluffySporeForestOverGrowth, // 몽글포자과성장
    FascinatingLogChance2, // 희귀 원목 확률2
    TreeVitamin1, //나무 영양제1
    LogProcessingSpeed3, // 원목 가공 속도 3
    ProcessLineExpand1, // 제재소 증설 1
    ItemTransferSpeedUP1, // 상자 넣기 속도 1
    FatigueMaxIncrease4, //피로도 최대치 증가4
    SourceOfStamina1, // 체력의 원천

    SporeShieldRegenBlock2, // 포자막 회복 억제2
    SporeShieldCut1, // 포자 절단
    SporeShieldPenetration1, // 포자 관통력


    TestNode,  // TestNode
    MAX
}

public enum SkillCommandType
{
    None,
    InventoryExpansion,
    LogCapacityIncrease,
    SawmillLogStorageExpansion,
    LogProcessingSpeed,
    AxeDamage,
    hunting,   // 불필요
    EquipmentSwitchSpeed, // 불필요
    GunDamage, // 불필요
    LogValue,
    WideGreenForestOvergrowth, // 풀빛너른숲 과성장 
    CarrotBundle,   // 불필요
    RabbitBoom,  // 불필요
    Stamina,
    StaminaRecoveryBoost,   // 불필요
    StaminaMaxIncrease,
    OffRoadVehicle,  // 불필요
    ConveyorSpeed,
    ReserveAmmoIncrease, // 불필요
    GunMagazineCapacity, // 불필요
    Ricochet, // 불필요
    GunPenetration, // 불필요
    EfficientMovement,
    Shockwave,
    AxeRange,
    AxeDurability,
    SteelAxe,
    PowerSupply,
    PickupRange,
    FascinatingLogChance,
    RicochetRange, // 불필요
    RicochetDamage, // 불필요
    GunReloadSpeed, // 불필요
    GunAttackSpeed, // 불필요
    MovementSpeed,
    ShockwaveDamage,
    ShockwaveRange,
    AxeAttackSpeed,
    WoodenTransportBox,
    WeakPointHit,
    Hello,
    MultiAttack,
    FinalAttack,
    AttackRythm,
    WhirlWind,
    AxeCriticalChance,
    AxeCriticalDamage,
    ShockWaveCritical,
    ShockWaveEnforcement,
    ShockWaveMastery,
    IncreaseJackPotChance,
    IncreaseJackPotAmount,
    TreeVitamin,
    FluffySporeForestOverGrowth,
    StarrootForestOverGrowth,
    MagmaForestOverGrowth,
    ItemTransferSpeedUP,
    OffroadContainerRangeIncrease,
    LogProcessorSpeedUp,
    TopgradeAssessment,
    SourceOfStamina,
    SourceOfSpeed,
    StaminaRecover,
    Repair,
    IncreaseRepairEfficiency,
    LumberjackNPC,
    LumberjackNPCAttackSpeed,
    LumberjackNPCDamage,
    LumberjackNPCShockWave,
    OffroadPorterNPC,
    OffroadPorterNPCSpeed,
    OffroadPorterNPCSlotCapacity,
    OffroadPorterNPCJackpot,
    Boomerang,
    BoomerangDamage,
    BoomerangRange,
    BoomerangDistance,
    BoomerangCooldownReduction,
    BoomerangAttackSpeed,
    BoomerangCritical,
    Drone,
    DroneDamage,
    DroneAttackSpeed,
    DroneRange,
    DroneChainAttack,
    DroneChainAttackRange,
    LumberjackNPCBoomerang,
    ProcessLineExpand,
    SporeShieldRegenBlock,
    SporeShieldCut,
    SporeShieldPenetration,
    ShieldExplosionUnlock,
    ShieldExplosionDamage,
    ShieldExplosionRange,
    ShieldExplosionResearch,
    ConstellationManifestUnlock,
    StarMarkDamage,
    StarPathSpeedBoost,
    ConstellationDamage,
    ConstellationAfterimage,
    ManifestationBrand,
    StarGazeUnlock,
}

public enum AbilityLevelUpRejectReason
{
    None,
    Pass,
    NotEnoughMoney,
    NotEnoughCarrot,
    MaxLevel,
}

public struct SkillAccumulatedValueData
{
    public SkillCommandType type;
    public float amount;
}

public struct SkillAccumulatedValueChangeData
{
    public SkillCommandType type;
    public float currentValueX;
    public float addedValueY;
    public float totalValueZ;
}

