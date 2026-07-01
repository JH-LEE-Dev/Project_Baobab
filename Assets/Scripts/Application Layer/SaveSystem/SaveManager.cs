using UnityEngine;
using System.Security.Cryptography;
using System.Text;
using System.IO;

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
    private OffroadContainer offroadContainer;

    // // 내부 의존성 및 설정
    // 암호화 키 (보안을 위해 실제 서비스 시에는 더 안전한 방식으로 관리 권장)
    private readonly byte[] encryptionKey = Encoding.UTF8.GetBytes("BaobabProjectKey2026!@#$01234567"); // 32바이트 (AES-256)
    private readonly byte[] encryptionIV = Encoding.UTF8.GetBytes("BaobabIV_2026!@#"); // 16바이트

    // GC Alloc 최적화를 위한 캐싱된 세이브 데이터 객체
    private GameSaveData cachedSaveData = new GameSaveData();

    public void Initialize(SignalHub _signalHub, SkillSystem _skillSystem, InventoryManager _inventoryManager, LogProcessingManager _logProcessingManager,
    DensityManager _densityManager, InDungeonObjectManager _inDungeonObjectManager,TownObjectManager _townObjectManager, OffroadContainer _offroadContainer)
    {
        signalHub = _signalHub;
        inDungeonObjectManager = _inDungeonObjectManager;
        densityManager = _densityManager;
        skillSystem = _skillSystem;
        inventoryManager = _inventoryManager;
        logProcessingManager = _logProcessingManager;
        townObjectManager = _townObjectManager;
        offroadContainer = _offroadContainer;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    public void CharacterSpawned(CharacterSpawnedSignal _signal)
    {
        character = _signal.character;
    }

    public void SaveGameData()
    {
        if (character == null || character.statComponent == null) return;

        // 기존 데이터 클리어 (리스트 등 재사용)
        cachedSaveData.Clear();
        
        // 1. 스킬 데이터 추출 (리스트 재사용)
        if (skillSystem != null && skillSystem.skillManager != null)
        {
            skillSystem.skillManager.PopulateSkillSaveData(ref cachedSaveData.skillTreeSaveData);
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
            densityManager.PopulateSaveData(ref cachedSaveData.environmentSaveData);
        }

        // 8. 오프로드 컨테이너 데이터 추출
        if (offroadContainer != null)
        {
            offroadContainer.PopulateSaveData(ref cachedSaveData.offroadContainerSaveData);
        }

        // 9. JSON 직렬화 및 바이너리 암호화 저장
        string json = JsonUtility.ToJson(cachedSaveData);
        byte[] encryptedData = Encrypt(json);

        string path = Path.Combine(Application.persistentDataPath, "SaveData.dat");
        File.WriteAllBytes(path, encryptedData);

        Debug.Log($"[SaveManager] Game Data Encrypted & Saved to: {path} (Alloc-minimized)");
    }

    public bool HasSaveData()
    {
        string path = Path.Combine(Application.persistentDataPath, "SaveData.dat");
        return File.Exists(path);
    }

    public void LoadGameData()
    {
        string path = Path.Combine(Application.persistentDataPath, "SaveData.dat");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SaveManager] Save file not found.");
            return;
        }

        byte[] encryptedData = File.ReadAllBytes(path);
        string json = Decrypt(encryptedData);

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[SaveManager] Failed to decrypt save data.");
            return;
        }

        GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

        if (saveData == null) return;

        // 1. 스킬 데이터 복구
        if (skillSystem != null && skillSystem.skillManager != null)
        {
            skillSystem.skillManager.LoadSaveData(saveData.skillTreeSaveData);
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

        // 8. 오프로드 컨테이너 데이터 복구
        if (offroadContainer != null)
        {
            offroadContainer.LoadSaveData(saveData.offroadContainerSaveData);
        }

        Debug.Log($"[SaveManager] Game Data Decrypted & Loaded from: {path}");
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
