using UnityEngine;

public class SaveManager : MonoBehaviour
{
    private SignalHub signalHub;
    private SkillSystem skillSystem;
    private Character character;
    private InventoryManager inventoryManager;
    private LogProcessingManager logProcessingManager;
    private DensityManager densityManager;
    private InDungeonObjectManager inDungeonObjectManager;
    private TownObjectManager townObjectManager;

    // GC Alloc 최적화를 위한 캐싱된 세이브 데이터 객체
    private GameSaveData cachedSaveData = new GameSaveData();

    public void Initialize(SignalHub _signalHub, SkillSystem _skillSystem, InventoryManager _inventoryManager, LogProcessingManager _logProcessingManager,
    DensityManager _densityManager, InDungeonObjectManager _inDungeonObjectManager,TownObjectManager _townObjectManager)
    {
        signalHub = _signalHub;
        inDungeonObjectManager = _inDungeonObjectManager;
        densityManager = _densityManager;
        skillSystem = _skillSystem;
        inventoryManager = _inventoryManager;
        logProcessingManager = _logProcessingManager;
        townObjectManager = _townObjectManager;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    public void CharacterSpawned(CharacterSpawendSignal _signal)
    {
        character = _signal.character;
    }

    public void SaveGameData()
    {
        if (character == null || character.statComponent == null) return;

        // 기존 데이터 클리어 (리스트 등 재사용)
        cachedSaveData.Clear();
        
        // 1. 캐릭터 스탯 데이터 추출
        var stats = character.statComponent;
        cachedSaveData.characterStatData = new CharacterStatSaveData
        {
            speed = stats.speed,
            maxStamina = stats.maxStamina,
            maxStaminaBonus = stats.maxStaminaBonus,
            staminaIncreaseAlpha = stats.staminaIncreaseAlpha,
            staminaDecreaseAlpha = stats.staminaDecreaseAlpha,
            
            axeDamage = stats.axeDamage,
            axeDamageMultiplier = stats.axeDamageMultiplier,
            axeDurability = stats.axeDurability,
            
            rifleDamage = stats.rifleDamage,
            rifleDamageMultiplier = stats.rifleDamageMultiplier,
            
            weaponChangeCoolTime = stats.weaponChangeCoolTime,
            switchSpeedMultiplier = stats.switchSpeedMultiplier,
            
            bCanHunting = stats.bCanHunting
        };

        // 2. 스킬 데이터 추출 (리스트 재사용)
        if (skillSystem != null && skillSystem.skillManager != null)
        {
            skillSystem.skillManager.PopulateSkillSaveData(cachedSaveData.skillSaveDataList);
        }

        // 3. 인벤토리 데이터 추출 (리스트 재사용)
        if (inventoryManager != null)
        {
            inventoryManager.PopulateInventorySaveData(ref cachedSaveData.inventorySaveData);
        }

        // 4. 로그 가공 시스템 데이터 추출 (리스트 재사용)
        if (logProcessingManager != null)
        {
            logProcessingManager.PopulateSaveData(ref cachedSaveData.logProcessingSaveData);
        }

        // 5. 환경 밀도 데이터 추출
        if (densityManager != null)
        {
            cachedSaveData.environmentSaveData = densityManager.GetSaveData();
        }

        // 6. 당근 드랍 데이터 추출
        if (inDungeonObjectManager != null && inDungeonObjectManager.itemManager != null && inDungeonObjectManager.itemManager.carrrotItemController != null)
        {
            cachedSaveData.carrotSaveData = inDungeonObjectManager.itemManager.carrrotItemController.GetSaveData();
        }

        // 7. 마을 오브젝트 데이터 추출 (bCanTravel 등)
        if (townObjectManager != null)
        {
            cachedSaveData.townSaveData = townObjectManager.GetSaveData();
        }

        // 8. JSON 저장
        string json = JsonUtility.ToJson(cachedSaveData, true);
        string path = System.IO.Path.Combine(Application.persistentDataPath, "SaveData.json");
        System.IO.File.WriteAllText(path, json);

        Debug.Log($"[SaveManager] Game Data Saved to: {path} (Alloc-minimized)");
    }

    public bool HasSaveData()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "SaveData.json");
        return System.IO.File.Exists(path);
    }

    public void LoadGameData()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "SaveData.json");
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("[SaveManager] Save file not found.");
            return;
        }

        string json = System.IO.File.ReadAllText(path);
        GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

        if (saveData == null) return;

        // 1. 캐릭터 스탯 복구
        if (character != null && character.statComponent != null)
        {
            character.statComponent.LoadSaveData(saveData.characterStatData);
        }

        // 2. 스킬 데이터 복구
        if (skillSystem != null && skillSystem.skillManager != null)
        {
            skillSystem.skillManager.LoadSaveData(saveData.skillSaveDataList);
        }

        // 3. 인벤토리 데이터 복구
        if (inventoryManager != null)
        {
            inventoryManager.LoadSaveData(saveData.inventorySaveData);
        }

        // 4. 로그 가공 시스템 데이터 복구
        if (logProcessingManager != null)
        {
            logProcessingManager.LoadSaveData(saveData.logProcessingSaveData);
        }

        // 5. 환경 밀도 데이터 복구
        if (densityManager != null)
        {
            densityManager.LoadSaveData(saveData.environmentSaveData);
        }

        // 6. 당근 드랍 데이터 복구
        if (inDungeonObjectManager != null && inDungeonObjectManager.itemManager != null && inDungeonObjectManager.itemManager.carrrotItemController != null)
        {
            inDungeonObjectManager.itemManager.carrrotItemController.LoadSaveData(saveData.carrotSaveData);
        }

        // 7. 마을 오브젝트 데이터 복구
        if (townObjectManager != null)
        {
            townObjectManager.LoadSaveData(saveData.townSaveData);
        }

        Debug.Log($"[SaveManager] Game Data Loaded from: {path}");
    }
}
