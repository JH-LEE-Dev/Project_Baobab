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

    // 이 라인이 컨테이너에서 마지막으로 원목을 꺼내온 뒤 경과한 시간(라인별 독립 출고 타이머).
    public float lastOutputTimeElapsed;

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
    public bool isUnlocked;
    public bool isNew;
    public bool hasPlayedUnlock;
}

[Serializable]
public struct MapLevelAccessSaveData
{
    public MapType mapType;
    public bool bCanAccess;
    public bool isUnlocked;
    public bool isNew;
    public bool hasPlayedUnlock;
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
    public List<MapLevelAccessSaveData> mapLevelAccessDatas;

    public void Initialize()
    {
        if (hiddenGaugeDatas == null) hiddenGaugeDatas = new List<MapHiddenGaugeSaveData>(8);
        else hiddenGaugeDatas.Clear();

        if (mapAccessDatas == null) mapAccessDatas = new List<MapAccessSaveData>(8);
        else mapAccessDatas.Clear();

        if (mapLevelAccessDatas == null) mapLevelAccessDatas = new List<MapLevelAccessSaveData>(8);
        else mapLevelAccessDatas.Clear();
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
    public bool bHasAcquiredLostAndFoundBox;
    public bool bHasAcquiredSporePotion;
    public float sporePotionCharge;
    public bool bHasAcquiredStarCompass;
    public bool bHasAcquiredObsidianCharm;
    public List<LootType> currentOwnedLoots;

    public void Clear()
    {
        skillTreeSaveData.Initialize();
        inventorySaveData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        offroadContainerSaveData.Initialize(SYSTEM_VAR.MAX_INVENTORY_CNT);
        logProcessingSaveData.Initialize();
        environmentSaveData.Initialize();

        if (currentOwnedLoots == null) currentOwnedLoots = new List<LootType>(10);
        else currentOwnedLoots.Clear();
    }
}

/// <summary>
/// 저장 시점에 "운반 중(포터 인벤토리/컨테이너 사이를 날아가는 중)"이라 어느 컨테이너 슬롯에도
/// 아직 커밋되지 않은 로그를, 라이브 게임 상태는 전혀 건드리지 않고 직렬화될 세이브 구조체에만
/// 가상으로 합산해주는 헬퍼. (저장은 게임을 종료하지 않고 계속 진행하는 경로라, 라이브 상태를
/// 실제로 이동시키면 재개 시 눈에 보이는 교란이 생기므로 세이브 데이터에만 반영한다.)
/// </summary>
public static class SaveDataMerge
{
    private static readonly int treeTypeArrayLen = Enum.GetValues(typeof(TreeType)).Length;

    /// <summary>
    /// 로그 한 개를 이미 채워진 슬롯 저장 데이터(_data.slots)에 병합한다. 같은 (나무종류/등급)
    /// 슬롯에 자리가 있으면 거기에 합치고, 없으면 빈 슬롯에 새로 넣는다. 넣을 자리가 전혀 없으면
    /// false를 반환한다(마을 저장 조건에선 발생하지 않아야 함 - 호출부에서 경고 로그 처리).
    /// </summary>
    public static bool AddLog(ref InventorySaveData _data, TreeType _treeType, LogState _logState, Color _color, int _maxItemsPerSlot)
    {
        if (_data.slots == null) return false;

        // 1. 같은 조합의 기존 슬롯에 자리가 있으면 거기에 합친다.
        for (int i = 0; i < _data.slots.Count; i++)
        {
            InventorySlotSaveData slot = _data.slots[i];
            if (slot.itemSaveData.itemType == ItemType.Log &&
                slot.itemSaveData.treeType == _treeType &&
                slot.itemSaveData.logState == _logState &&
                slot.totalCount < _maxItemsPerSlot)
            {
                slot.totalCount++;
                if (slot.treeTypeCounts == null || slot.treeTypeCounts.Length != treeTypeArrayLen)
                    slot.treeTypeCounts = new int[treeTypeArrayLen];
                slot.treeTypeCounts[(int)_treeType]++;
                _data.slots[i] = slot;
                return true;
            }
        }

        // 2. 빈 슬롯에 새로 넣는다.
        for (int i = 0; i < _data.slots.Count; i++)
        {
            InventorySlotSaveData slot = _data.slots[i];
            if (slot.itemSaveData.itemType == ItemType.None && slot.totalCount == 0)
            {
                slot.itemSaveData = new ItemSaveData
                {
                    itemType = ItemType.Log,
                    treeType = _treeType,
                    logState = _logState,
                    color = _color
                };
                slot.totalCount = 1;
                slot.treeTypeCounts = new int[treeTypeArrayLen];
                slot.treeTypeCounts[(int)_treeType] = 1;
                _data.slots[i] = slot;
                return true;
            }
        }

        return false;
    }
}
