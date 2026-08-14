using UnityEngine;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Threading;

public class SaveManager : MonoBehaviour, IMainMenuSaveSystem
{
    private Bootstrap bootstrap;
    private SignalHub signalHub;
    private SkillSystem skillSystem;
    private Character character;
    private InventoryManager inventoryManager;
    private LogProcessingManager logProcessingManager;
    private DensityManager densityManager;
    private InDungeonObjectManager inDungeonObjectManager;
    private TownObjectManager townObjectManager;
    private OffroadContainer offroadContainer;
    private TownUnitSpawner townUnitSpawner;

    // // 내부 의존성 및 설정
    // 암호화 키 (보안을 위해 실제 서비스 시에는 더 안전한 방식으로 관리 권장)
    private readonly byte[] encryptionKey = Encoding.UTF8.GetBytes("BaobabProjectKey2026!@#$01234567"); // 32바이트 (AES-256)
    private readonly byte[] encryptionIV = Encoding.UTF8.GetBytes("BaobabIV_2026!@#"); // 16바이트

    // GC Alloc 최적화를 위한 캐싱된 세이브 데이터 객체
    private GameSaveData cachedSaveData = new GameSaveData();

    private void Awake()
    {
        bootstrap = GetComponent<Bootstrap>();
    }

    public void Initialize(SignalHub _signalHub, SkillSystem _skillSystem, InventoryManager _inventoryManager, LogProcessingManager _logProcessingManager,
    DensityManager _densityManager, InDungeonObjectManager _inDungeonObjectManager,TownObjectManager _townObjectManager, OffroadContainer _offroadContainer,
    TownUnitSpawner _townUnitSpawner)
    {
        signalHub = _signalHub;
        inDungeonObjectManager = _inDungeonObjectManager;
        densityManager = _densityManager;
        skillSystem = _skillSystem;
        inventoryManager = _inventoryManager;
        logProcessingManager = _logProcessingManager;
        townObjectManager = _townObjectManager;
        offroadContainer = _offroadContainer;
        townUnitSpawner = _townUnitSpawner;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<AutoSaveRequestedSignal>(AutoSaveRequested);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<AutoSaveRequestedSignal>(AutoSaveRequested);
    }

    public void CharacterSpawned(CharacterSpawnedSignal _signal)
    {
        character = _signal.character;
    }

    // 마을 도착 / 숲 출발 시점에 발행되는 자동저장 요청. (게임 종료 시 자동저장은 OnApplicationQuit에서 별도 처리)
    private void AutoSaveRequested(AutoSaveRequestedSignal _signal)
    {
        Debug.Log($"[SaveManager] Auto save triggered ({_signal.reason})");
        SaveGameData();
    }

    // 런타임 도중 게임이 종료되면(Alt+F4, 인게임 종료 등 OnApplicationQuit이 호출되는 정상 종료 경로) 마지막 상태를 보존한다.
    // 작업 관리자 강제 종료/크래시/정전처럼 OnApplicationQuit 자체가 호출되지 않는 경우는 이 훅으로 막을 수 없다.
    // saveManager는 BootStrap(DontDestroyOnLoad)에 붙어있는 영구 싱글톤이라 MainMenu로 돌아가도 파괴되지 않으며,
    // 세션 없이(메인메뉴에서) 종료해도 이 메서드는 매번 호출된다. 이때 SaveGameData()가 조용히 아무 일도
    // 하지 않는 건, GameInstaller가 파괴되며 character(MonoBehaviour)가 유니티의 파괴된-오브젝트 null 비교
    // 규칙에 의해 "character == null"로 평가되어 위쪽 가드에서 걸러지기 때문이다(세션 밖이라 호출 자체가
    // 안 되는 게 아님). character를 유니티 오브젝트가 아닌 형태로 바꾸거나 Release()에서 필드를 명시적으로
    // 비우게 되면 이 안전장치가 사라지므로 주의.
    private void OnApplicationQuit()
    {
        if (null == bootstrap)
        {
            bootstrap = GetComponent<Bootstrap>();
        }

        if (null != bootstrap && SceneType.DungeonScene == bootstrap.CurrentSceneType)
        {
            Debug.Log("[SaveManager] Currently in DungeonScene; skipping auto-save on ApplicationQuit.");
            return;
        }

        Debug.Log("[SaveManager] Auto save triggered (ApplicationQuit)");
        SaveGameData();
    }

    private string GetSaveFilePath()
    {
        // 경로 규칙은 GamePaths에서 단일 관리한다. (환경설정 파일과 같은 폴더, 다른 파일)
        return GamePaths.GameSaveFile;
    }

    // BootStrap이 메인 메뉴 UI(이어하기 버튼 등)를 만들기 직전에 명시적으로 호출한다.
    // Unity의 Start()에 맡기면 sceneLoaded 이벤트(메인 메뉴 UI 생성 시점)보다 늦게 실행되어
    // 클라우드 복원 전에 버튼 상태가 이미 결정돼버리는 경합이 생긴다.
    // 이후 HasSaveData()/LoadGameData()는 그대로 로컬 파일만 보면 되므로 별도 수정이 필요 없다.
    public void SyncCloudSaveIfNewer()
    {
        if (!SteamCloudSaveService.IsAvailable) return;
        if (!SteamCloudSaveService.TryGetCloudTimestampUtc(out DateTime cloudTimeUtc)) return;

        string path = GetSaveFilePath();
        bool localExists = File.Exists(path);
        bool cloudIsNewer = !localExists || cloudTimeUtc > File.GetLastWriteTimeUtc(path);

        if (!cloudIsNewer) return;

        if (!SteamCloudSaveService.TryDownload(out byte[] cloudData)) return;

        // 손상된 클라우드 데이터가 멀쩡한 로컬 파일/백업을 덮어쓰지 않도록 반영 전 검증한다.
        if (!TryParseSaveBytes(cloudData, out _))
        {
            Debug.LogError("[SaveManager] Cloud save data is corrupted; keeping local copy.");
            return;
        }

        if (WriteSaveFileWithBackup(cloudData, "CloudSync"))
        {
            Debug.Log("[SaveManager] Cloud save applied to local (newer than local copy).");
        }
    }

    public void SaveGameData()
    {
        if (null == character || null == character.statComponent) return;

        if (null == bootstrap)
        {
            bootstrap = GetComponent<Bootstrap>();
        }

        if (null != bootstrap && SceneType.DungeonScene == bootstrap.CurrentSceneType)
        {
            Debug.LogWarning("[SaveManager] SaveGameData skipped because current scene is DungeonScene.");
            return;
        }

        // 기존 데이터 클리어 (리스트 등 재사용)
        cachedSaveData.Clear();
        
        // 1. 스킬 데이터 추출 (리스트 재사용)
        if (null != skillSystem && null != skillSystem.skillManager)
        {
            skillSystem.skillManager.PopulateSkillSaveData(ref cachedSaveData.skillTreeSaveData);
        }

        // 3. 인벤토리 데이터 추출 (리스트 재사용)
        if (null != inventoryManager)
        {
            inventoryManager.PopulateInventorySaveData(ref cachedSaveData.inventorySaveData);
        }

        // 4. 로그 가공 시스템 데이터 추출 (리스트 재사용)
        if (null != logProcessingManager)
        {
            logProcessingManager.PopulateSaveData(ref cachedSaveData.logProcessingSaveData);
        }

        // 5. 환경 밀도 데이터 추출
        if (null != densityManager)
        {
            densityManager.PopulateSaveData(ref cachedSaveData.environmentSaveData);
        }

        // 8. 오프로드 컨테이너 데이터 추출
        if (null != offroadContainer)
        {
            offroadContainer.PopulateSaveData(ref cachedSaveData.offroadContainerSaveData);
        }

        // 8-2. "분실물 보관함" 영구 획득 플래그, "포자 포션" 획득 여부/충전량
        if (null != inDungeonObjectManager)
        {
            cachedSaveData.bHasAcquiredLostAndFoundBox = inDungeonObjectManager.bHasAcquiredLostAndFoundBox;
            cachedSaveData.bHasAcquiredSporePotion = inDungeonObjectManager.bHasAcquiredSporePotion;
            cachedSaveData.sporePotionCharge = inDungeonObjectManager.sporePotionCharge;
            cachedSaveData.bHasAcquiredStarCompass = inDungeonObjectManager.bHasAcquiredStarCompass;
            cachedSaveData.bHasAcquiredObsidianCharm = inDungeonObjectManager.bHasAcquiredObsidianCharm;
            
            cachedSaveData.currentOwnedLoots.Clear();
            if (null != inDungeonObjectManager.CurrentOwnedLoots)
            {
                cachedSaveData.currentOwnedLoots.AddRange(inDungeonObjectManager.CurrentOwnedLoots);
            }
        }

        // 8-1. 운반 중(포터 인벤토리/컨테이너 사이 비행) 로그 정산.
        //      라이브 상태는 건드리지 않고, 위에서 채운 세이브 데이터에만 가상으로 합산한다.
        //      - LogContainer로 납품되던 비행분 -> LogContainer 세이브로 착지
        //      - OffroadContainer<->캐릭터/포터 비행분, 포터가 들고 있던 분 -> 각 규칙대로 정산
        //      (반드시 모든 Populate 이후에 호출: 슬롯 리스트가 구성된 뒤여야 병합 가능)
        if (null != logProcessingManager)
        {
            logProcessingManager.AppendTransitToSaveData(ref cachedSaveData.logProcessingSaveData);
        }

        if (null != offroadContainer)
        {
            int characterMaxPerSlot = null != inventoryManager ? inventoryManager.GetMaxItemsPerSlot() : 0;
            int logContainerMaxPerSlot = null != logProcessingManager ? logProcessingManager.GetContainerMaxItemsPerSlot() : 0;
            IReadOnlyList<OffroadPorterNPC> porters = null != townUnitSpawner ? townUnitSpawner.NPCs : null;
            // OffroadContainer가 가득이면 포터 로그를 LogContainer 세이브로 전진 납품(fallback)하므로
            // 그 LogContainer 세이브 데이터도 ref로 넘긴다. (LogContainer 자체 운반분은 위에서 이미 정산됨)
            offroadContainer.AppendTransitToSaveData(ref cachedSaveData.offroadContainerSaveData,
                ref cachedSaveData.inventorySaveData, ref cachedSaveData.logProcessingSaveData.containerInventoryData,
                characterMaxPerSlot, logContainerMaxPerSlot, porters);
        }

        // 9. JSON 직렬화 및 바이너리 암호화 저장
        string json = JsonUtility.ToJson(cachedSaveData);
        byte[] encryptedData = Encrypt(json);

        if (WriteSaveFileWithBackup(encryptedData, "SaveGameData"))
        {
            // 로컬 쓰기가 실패했는데 클라우드에 업로드하면, 클라우드의 "최신" 스냅샷과 로컬 파일이 어긋난다.
            SteamCloudSaveService.Upload(encryptedData);
            Debug.Log($"[SaveManager] Game Data Encrypted & Saved to: {GetSaveFilePath()} (Alloc-minimized)");
        }
        else
        {
            Debug.LogError("[SaveManager] Local save failed; skipping cloud upload to avoid diverging from local state.");
        }
    }

    // 메인 파일이 손상됐어도 백업이 살아있으면 이어하기가 가능하므로 둘 중 하나라도 있으면 true.
    public bool HasSaveData()
    {
        return File.Exists(GetSaveFilePath()) || File.Exists(GamePaths.GameSaveBackupFile);
    }

    public void LoadGameData()
    {
        string path = GetSaveFilePath();

        if (File.Exists(path) && TryReadAndParse(path, out GameSaveData saveData, out _))
        {
            ApplyLoadedData(saveData);
            Debug.Log($"[SaveManager] Game Data Decrypted & Loaded from: {path}");
            return;
        }

        string backupPath = GamePaths.GameSaveBackupFile;
        if (!File.Exists(backupPath))
        {
            if (File.Exists(path))
            {
                Debug.LogError("[SaveManager] Save file is corrupted and no backup exists. Load aborted.");
            }
            else
            {
                Debug.LogWarning("[SaveManager] Save file not found.");
            }
            return;
        }

        Debug.LogWarning("[SaveManager] Main save file is missing or corrupted. Trying backup...");

        if (!TryReadAndParse(backupPath, out GameSaveData backupData, out byte[] backupBytes))
        {
            Debug.LogError("[SaveManager] Both main and backup save files are corrupted or unreadable. Load aborted.");
            return;
        }

        ApplyLoadedData(backupData);
        Debug.LogWarning("[SaveManager] Recovered game data from backup save file.");

        // 메인 파일을 백업 내용으로 치유해서 다음 실행부터 같은 손상 파일을 다시 만나지 않게 한다.
        // 단, 손상된 메인이 남아 있는 채로 WriteSaveFileWithBackup을 부르면 File.Replace가
        // 그 손상본을 백업 자리로 밀어넣어 방금 복구에 성공한 백업을 덮어써버린다.
        // 먼저 지워서 File.Move 경로(백업 미변경)를 타게 한다.
        if (!File.Exists(path) || TryDeleteFile(path))
        {
            WriteSaveFileWithBackup(backupBytes, "RecoverFromBackup");
        }
    }

    private void ApplyLoadedData(GameSaveData _data)
    {
        // 1. 스킬 데이터 복구
        if (skillSystem != null && skillSystem.skillManager != null)
        {
            skillSystem.skillManager.LoadSaveData(_data.skillTreeSaveData);
        }

        // 3. 인벤토리 데이터 복구
        if (inventoryManager != null)
        {
            inventoryManager.LoadSaveData(_data.inventorySaveData);
        }

        // 4. 로그 가공 시스템 데이터 복구
        if (logProcessingManager != null)
        {
            logProcessingManager.LoadSaveData(_data.logProcessingSaveData);
        }

        // 5. 환경 밀도 데이터 복구
        if (densityManager != null)
        {
            densityManager.LoadSaveData(_data.environmentSaveData);
        }

        // 8. 오프로드 컨테이너 데이터 복구
        if (offroadContainer != null)
        {
            offroadContainer.LoadSaveData(_data.offroadContainerSaveData);
        }

        // 8-2. "분실물 보관함" 영구 획득 플래그, "포자 포션" 획득 여부/충전량 복구
        if (inDungeonObjectManager != null)
        {
            inDungeonObjectManager.bHasAcquiredLostAndFoundBox = _data.bHasAcquiredLostAndFoundBox;
            inDungeonObjectManager.bHasAcquiredSporePotion = _data.bHasAcquiredSporePotion;
            inDungeonObjectManager.sporePotionCharge = _data.sporePotionCharge;
            inDungeonObjectManager.bHasAcquiredStarCompass = _data.bHasAcquiredStarCompass;
            inDungeonObjectManager.bHasAcquiredObsidianCharm = _data.bHasAcquiredObsidianCharm;

            inDungeonObjectManager.RestoreOwnedLoots(_data.currentOwnedLoots);
        }
    }

    // 파일을 읽고 복호화+파싱까지 시도한다. 파일 자체가 잠겨있거나 손상된 경우 모두 false를 반환한다.
    // 복구 시 원본 바이트를 그대로 재사용할 수 있도록 읽어들인 바이트도 함께 돌려준다(파일을 두 번 읽지 않기 위함).
    private bool TryReadAndParse(string _path, out GameSaveData _data, out byte[] _rawBytes)
    {
        _data = null;
        _rawBytes = null;

        byte[] encryptedData;
        try
        {
            encryptedData = File.ReadAllBytes(_path);
        }
        catch (Exception _e)
        {
            Debug.LogError($"[SaveManager] Failed to read save file '{_path}': {_e.Message}");
            return false;
        }

        if (!TryParseSaveBytes(encryptedData, out _data))
        {
            return false;
        }

        _rawBytes = encryptedData;
        return true;
    }

    private bool TryDeleteFile(string _path)
    {
        try
        {
            File.Delete(_path);
            return true;
        }
        catch (Exception _e)
        {
            Debug.LogError($"[SaveManager] Failed to delete file '{_path}': {_e.Message}");
            return false;
        }
    }

    // 복호화 + JSON 파싱만 담당한다(서브시스템 반영은 ApplyLoadedData에서). 클라우드 데이터 사전 검증에도 재사용된다.
    private bool TryParseSaveBytes(byte[] _encryptedData, out GameSaveData _data)
    {
        _data = null;

        string json = Decrypt(_encryptedData);
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
            if (saveData == null)
            {
                return false;
            }

            _data = saveData;
            return true;
        }
        catch (Exception _e)
        {
            Debug.LogError($"[SaveManager] Failed to parse save JSON: {_e.Message}");
            return false;
        }
    }

    // 임시 파일에 먼저 쓰고 File.Replace(원자적 교체 + 기존 파일을 백업으로 자동 이동)로 반영한다.
    // 실패 시 기존 main/backup 파일은 전혀 건드려지지 않은 상태로 남는다.
    private bool WriteSaveFileWithBackup(byte[] _data, string _context)
    {
        string path = GetSaveFilePath();
        string tempPath = GamePaths.GameSaveTempFile;
        string backupPath = GamePaths.GameSaveBackupFile;

        try
        {
            File.WriteAllBytes(tempPath, _data);
        }
        catch (Exception _e)
        {
            Debug.LogError($"[SaveManager] ({_context}) Failed to write temp save file: {_e.Message}");
            return false;
        }

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    // 새 내용 반영 + 기존 파일을 backupPath로 이동을 한 번에 원자적으로 처리한다.
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (IOException _e)
            {
                // OneDrive 동기화/백신 스캔 등으로 대상 파일이 잠깐 잠겨있을 수 있어 짧게 재시도한다.
                if (attempt >= maxRetries)
                {
                    Debug.LogError($"[SaveManager] ({_context}) Failed to replace save file after {maxRetries} attempts: {_e.Message}");
                    return false;
                }

                Thread.Sleep(75);
            }
            catch (Exception _e)
            {
                Debug.LogError($"[SaveManager] ({_context}) Unexpected error replacing save file: {_e.Message}");
                return false;
            }
        }

        return false;
    }

    // // 프라이빗 암호화 로직

    private byte[] Encrypt(string _plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = encryptionKey;
            aes.IV = encryptionIV;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(_plainText);
                    }
                    return ms.ToArray();
                }
            }
        }
    }

    private string Decrypt(byte[] _cipherText)
    {
        try
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = encryptionIV;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream(_cipherText))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
        catch (System.Exception _e)
        {
            Debug.LogError($"[SaveManager] Decryption Error: {_e.Message}");
            return null;
        }
    }
}
