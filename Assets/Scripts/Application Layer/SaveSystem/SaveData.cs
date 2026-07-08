using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public struct SkillSaveData
{
    public SkillType skillType;
    public int currentLevel;
}

[Serializable]
public struct SkillTreeSaveData
{
    public int prestigeLevel;
    public int skillExperience;
    public List<SkillSaveData> skillSaveDatas;

    public void Initialize()
    {
        if (skillSaveDatas == null) skillSaveDatas = new List<SkillSaveData>(30);
        else skillSaveDatas.Clear();
    }
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
public struct DeactivatingItemSaveData
{
    public ItemSaveData itemData;
    public Vector3 position;
    public float remainingTime;
}

[Serializable]
public struct BeltSaveData
{
    public List<BeltItemSaveData> activeItems;
    // 벨트 끝단 퇴출 연출 대기 중인 아이템(체크포인트 통과 직후, 다음 단계로 넘어가기 직전).
    // 저장하지 않으면 저장 순간 이 구간에 걸린 아이템이 유실된다.
    public List<DeactivatingItemSaveData> deactivatingItems;
    public bool isMoving;

    public void Initialize()
    {
        if (activeItems == null) activeItems = new List<BeltItemSaveData>(10);
        else activeItems.Clear();

        if (deactivatingItems == null) deactivatingItems = new List<DeactivatingItemSaveData>(10);
        else deactivatingItems.Clear();
    }
}

[Serializable]
public struct CutterSaveData
{
    public bool bIsCutting;
    public ItemSaveData cuttingItemData;
}

[Serializable]
public struct LogProcessLineSaveData
{
    public BeltSaveData inBeltData;
    public BeltSaveData outBeltData;
    public CutterSaveData cutterData;

    public void Initialize()
    {
        inBeltData.Initialize();
        outBeltData.Initialize();
    }
}

[Serializable]
public struct InventorySlotSaveData
{
    public ItemSaveData itemSaveData;
    public int totalCount;
    public int[] treeTypeCounts; // Log 아이템인 경우 세부 나무 종류별 개수
}

[Serializable]
public struct InventorySaveData
{
    public long money;
    public long carrot;
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
    public int shopMoney;
    public bool bFirstTimeEarnMoney;
    public bool bStop;
    public float transferInterval;
    
    // 타이밍 정보
    public float lastTransferTimeElapsed;
    public float lastOutputTimeElapsed;
    public float lastInterval;

    // 라인(벨트+커터+평가기) 상태 - 세트별로 1개씩
    public List<LogProcessLineSaveData> lineDatas;
    public int activeLineCount;

    public int logProcessingStack;

    public void Initialize()
    {
        containerInventoryData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        if (lineDatas == null) lineDatas = new List<LogProcessLineSaveData>(3);
        else lineDatas.Clear();
    }
}

[Serializable]
public struct MapHiddenGaugeSaveData
{
    public MapType mapType;
    public ForestType forestType;
    public float hiddenGauge;
}

[Serializable]
public struct MapAccessSaveData
{
    public MapType mapType;
    public ForestType forestType;
    public bool bCanAccess;
}

[Serializable]
public struct MapTreeDensitySaveData
{
    public MapType mapType;
    public float multiplier;
}

[Serializable]
public struct EnvironmentSaveData
{
    public List<MapHiddenGaugeSaveData> hiddenGaugeDatas;
    public List<MapAccessSaveData> mapAccessDatas;

    public void Initialize()
    {
        if (hiddenGaugeDatas == null) hiddenGaugeDatas = new List<MapHiddenGaugeSaveData>(8);
        else hiddenGaugeDatas.Clear();

        if (mapAccessDatas == null) mapAccessDatas = new List<MapAccessSaveData>(8);
        else mapAccessDatas.Clear();
    }
}



[Serializable]
public class GameSaveData
{
    public SkillTreeSaveData skillTreeSaveData;
    public InventorySaveData inventorySaveData;
    public LogProcessingSaveData logProcessingSaveData;
    public EnvironmentSaveData environmentSaveData;
    public InventorySaveData offroadContainerSaveData;

    public void Clear()
    {
        skillTreeSaveData.Initialize();
        inventorySaveData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        offroadContainerSaveData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        logProcessingSaveData.Initialize();
        environmentSaveData.Initialize();
    }
}
