using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CharacterStatSaveData
{
    public float pickupRangeMultiplier;
    public float originalSpeed;
    public float speedMultiplier;
    public float maxStamina;
    public float maxStaminaBonus;
    public float staminaIncreaseAlpha;
    public float staminaDecreaseAlpha;
    
    public float axeDamage;
    public float axeDamageMultiplier;
    public float axeAttackCoolTime;
    public float axeAttackSpeedMultiplier;
    public float axeDurability;
    public float speedDecreaseWhileAction;
    public float axeAttackRangeMultiplier;
    public float axeDurabilityDecIgnoreChance;

    public float rifleDamage;
    public float rifleDamageMultiplier;
    public float shotDelay;
    public float rifleAttackSpeedMultiplier;
    public float gunPenetrationChance;
    public float reloadDuration;
    public float reloadSpeedMultiplier;

    public int ricochetCnt;
    public float ricochetAngle;
    public float ricochetDist;
    public float ricochetDamage;
    
    public float weaponChangeCoolTime;
    public float switchSpeedMultiplier;
    
    public bool bCanHunting;

    public float shockWaveChance;
    public float shockWaveDamage;
    public float shockWaveDamageMultiplier;
    public float shockWaveSpeed;
    public float shockWaveSpeedMultiplier;
    public float shockWaveCreateDelay;
}

[Serializable]
public struct SkillSaveData
{
    public SkillType skillType;
    public int currentLevel;
}

[Serializable]
public struct ItemSaveData
{
    public ItemType itemType;
    // Log 전용
    public TreeType treeType;
    public LogState logState;
    // Loot 전용
    public LootType lootType;
    
    // 공용
    public Color color;

    // 실시간 상태 (벨트/커터 위 아이템용)
    public float durability;
}

[Serializable]
public struct BeltItemSaveData
{
    public ItemSaveData itemData;
    public Vector3 position;
    public int targetIndex;
}

[Serializable]
public struct BeltSaveData
{
    public List<BeltItemSaveData> activeItems;
    public bool isMoving;
    public float beltSpeed;

    public void Initialize()
    {
        if (activeItems == null) activeItems = new List<BeltItemSaveData>(10);
        else activeItems.Clear();
    }
}

[Serializable]
public struct CutterSaveData
{
    public bool bIsCutting;
    public ItemSaveData cuttingItemData;
    public float totalSpeedMultiplier;
    public bool bPowerSupply;
}

[Serializable]
public struct EvaluatorSaveData
{
    public float logValueMultiplier;
}

[Serializable]
public struct InventorySlotSaveData
{
    public ItemSaveData itemSaveData;
    public int totalCount;
    public int[] logStateCounts; // Log 아이템인 경우 세부 상태별 개수
}

[Serializable]
public struct InventorySaveData
{
    public int money;
    public int carrot;
    public int currentSlotCount;
    public List<InventorySlotSaveData> slots;

    public void Initialize(int _capacity)
    {
        if (slots == null) slots = new List<InventorySlotSaveData>(_capacity);
        else slots.Clear();
    }
}

[Serializable]
public struct LogProcessingSaveData
{
    public InventorySaveData containerInventoryData;
    public int maxItemsPerSlot;
    public int shopMoney;
    public bool bFirstTimeEarnMoney;
    public bool bStop;
    public float transferInterval;
    
    // 타이밍 정보
    public float lastTransferTimeElapsed;
    public float lastOutputTimeElapsed;
    public float lastInterval;

    // 벨트, 커터, 평가기 상태
    public BeltSaveData logInBeltData;
    public BeltSaveData logOutBeltData;
    public CutterSaveData cutterData;
    public EvaluatorSaveData evaluatorData;

    public void Initialize()
    {
        containerInventoryData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        logInBeltData.Initialize();
        logOutBeltData.Initialize();
    }
}

[Serializable]
public struct EnvironmentSaveData
{
    public float treeDensityMultiplier;
    public float rabbitDensityMultiplier;
}

[Serializable]
public struct CarrotSaveData
{
    public float dropMultiplier;
}

[Serializable]
public struct TownSaveData
{
    public bool bCanTravel;
}

[Serializable]
public struct LogDropProbSaveData
{
    public List<LogDropData> logProbDatas;
}

[Serializable]
public class GameSaveData
{
    public CharacterStatSaveData characterStatData;
    public List<SkillSaveData> skillSaveDataList = new List<SkillSaveData>(30);
    public InventorySaveData inventorySaveData;
    public LogProcessingSaveData logProcessingSaveData;
    public EnvironmentSaveData environmentSaveData;
    public CarrotSaveData carrotSaveData;
    public LogDropProbSaveData logDropProbSaveData;
    public TownSaveData townSaveData;

    public void Clear()
    {
        skillSaveDataList.Clear();
        inventorySaveData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        logProcessingSaveData.Initialize();
        if (logDropProbSaveData.logProbDatas != null) logDropProbSaveData.logProbDatas.Clear();
    }
}
