using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;

/// <summary>
/// 메인 메뉴가 세이브 파일 상태를 확인하는 과정의 진행 상태입니다. UI는 이 값만 보고 그리면 됩니다.
/// </summary>
public enum ESaveCheckState
{
    /// <summary>아직 확인을 시작하지 않았습니다.</summary>
    NotStarted,

    /// <summary>확인 중입니다. 이 동안 메인 메뉴 상호작용을 막아야 합니다.</summary>
    Checking,

    /// <summary>결론이 났습니다. 세이브가 있든 없든 메인 메뉴를 평소대로 진행하면 됩니다.</summary>
    Ready,

    /// <summary>
    /// 제한 시간 안에 결론을 내지 못했습니다. 파일은 있는데 계속 읽지 못하는 상태입니다.
    /// "다시 시도 / 기존 세이브 포기하고 새로 시작 / 종료"만 제시해야 합니다.
    /// 평소의 새로하기 버튼을 열어두면 유저가 멀쩡한 세이브를 스스로 지웁니다.
    /// </summary>
    Failed
}

public class SaveManager : MonoBehaviour, IMainMenuSaveSystem, ISaveCheckSystem
{
    private BootStrap bootstrap;
    private SignalHub signalHub;
    private TutorialSystem tutorialSystem;
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

    // 이어하기 가능 여부(HasSaveData) 판정 캐시. 판정에 복호화+파싱이 필요해졌고 UI가 버튼 상태를
    // 갱신할 때마다 여러 번 물어보므로, 세이브 파일이 그대로면 직전 결과를 재사용한다.
    private string cachedSaveStateKey;
    private bool bCachedHasUsableSave;

    // 다른 변형(정식) 세이브 보존을 세션당 한 번만 시도하기 위한 플래그. (아래 PreserveForeignSaveOnce 참고)
    private bool bForeignSavePreserveChecked;

    // 세이브 파일이 디스크에 있는데 읽지 못해 로드를 포기했을 때 켜진다. 이 세션 동안 저장을 막는다.
    // (LoadGameData의 설명 참고. "새로하기"로 세이브를 지우면 지킬 것이 없어지므로 함께 풀린다)
    private bool bSaveBlockedByUnreadableSave;

    // 저장이 막힌 사실을 자동저장마다 반복해서 찍지 않기 위한 플래그.
    private bool bReportedSaveBlock;

    // // 메인 메뉴 세이브 확인
    // 파일이 잠겨 있어도 대개 몇 초 안에 풀린다(백신 스캔, 런처의 클라우드 복원 직후 등).
    // SafeFileIO의 동기 재시도는 0.5초 만에 포기하는데, 그건 파일이 나빠서가 아니라 그 이상 멈추면
    // 유저에게 프리즈로 보이기 때문이다. "확인 중" 화면을 띄울 수 있으면 훨씬 오래 기다려도 되고,
    // 그만큼 실패 자체가 줄어든다. 그러려면 시도 사이에 프레임을 넘겨야 하므로 코루틴으로 돈다.
    private const float SAVE_CHECK_TIMEOUT_SECONDS = 8f;
    private const float SAVE_CHECK_INTERVAL_SECONDS = 0.25f;

    private ESaveCheckState saveCheckState = ESaveCheckState.NotStarted;
    private float saveCheckElapsedSeconds;

    /// <summary>메인 메뉴 세이브 확인의 진행 상태입니다.</summary>
    public ESaveCheckState SaveCheckState => saveCheckState;

    /// <summary>
    /// 확인을 시작한 뒤 흐른 시간입니다.
    /// "0.3초를 넘길 때만 확인 중 화면을 띄운다" 같은 판단에 씁니다. 대부분은 첫 시도에 끝나므로
    /// 그대로 띄우면 화면이 한 프레임 깜빡였다 사라집니다.
    /// </summary>
    public float SaveCheckElapsedSeconds => saveCheckElapsedSeconds;

    private void Awake()
    {
        bootstrap = GetComponent<BootStrap>();
    }

    public void Initialize(SignalHub _signalHub, TutorialSystem _tutorialSystem, SkillSystem _skillSystem, InventoryManager _inventoryManager, LogProcessingManager _logProcessingManager,
    DensityManager _densityManager, InDungeonObjectManager _inDungeonObjectManager,TownObjectManager _townObjectManager, OffroadContainer _offroadContainer,
    TownUnitSpawner _townUnitSpawner)
    {
        signalHub = _signalHub;
        tutorialSystem = _tutorialSystem;
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
            bootstrap = GetComponent<BootStrap>();
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

        if (!SteamCloudSaveService.TryGetCloudTimestampUtc(out DateTime cloudTimeUtc))
        {
            // 클라우드에 파일이 없다. 지우려던 대상이 이미 사라졌으므로 표식도 정리한다.
            ClearCloudTombstone();
            return;
        }

        if (TryResolveCloudTombstone(cloudTimeUtc)) return;

        string path = GetSaveFilePath();
        bool localExists = File.Exists(path);
        bool cloudIsNewer = !localExists || cloudTimeUtc > File.GetLastWriteTimeUtc(path);

        if (!cloudIsNewer) return;

        if (!SteamCloudSaveService.TryDownload(out byte[] cloudData)) return;

        // 손상된 클라우드 데이터가 멀쩡한 로컬 파일/백업을 덮어쓰지 않도록 반영 전 검증한다.
        if (!TryParseSaveBytes(cloudData, out GameSaveData cloudSaveData))
        {
            Debug.LogError("[SaveManager] Cloud save data is corrupted; keeping local copy.");
            return;
        }

        // 데모/정식은 스팀 App ID가 달라 클라우드 저장소도 분리되지만, 개발용 App ID를 공유하거나
        // 데모→정식 전환기에 잔여 데이터가 넘어오면 로컬 진행도를 덮어쓸 수 있으므로 여기서도 막는다.
        if (!BuildInfo.IsSaveVariantCompatible(cloudSaveData.buildVariant))
        {
            Debug.LogWarning($"[SaveManager] Cloud save was created by another build variant ({cloudSaveData.buildVariant}); current build is {BuildInfo.Variant}. Keeping local copy.");
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

        // 읽지 못한 세이브가 디스크에 남아있다. 지금 쓰면 그 파일을 덮어써버린다. (LoadGameData 참고)
        if (bSaveBlockedByUnreadableSave)
        {
            if (false == bReportedSaveBlock)
            {
                bReportedSaveBlock = true;
                Debug.LogError("[SaveManager] Saving is disabled for this session because an existing save file could not be read. Progress in this session will not be kept.");
            }

            return;
        }

        if (null == bootstrap)
        {
            bootstrap = GetComponent<BootStrap>();
        }

        if (null != bootstrap && SceneType.DungeonScene == bootstrap.CurrentSceneType)
        {
            Debug.LogWarning("[SaveManager] SaveGameData skipped because current scene is DungeonScene.");
            return;
        }

        // 튜토리얼 진행 중(마지막 스텝 완료 전)에는 자동/종료 저장을 하지 않는다.
        // 마지막 스텝이 완료되어 다시 던전으로 향하는 순간(DepartToForest 자동저장)부터 정상 저장된다.
        if (null != tutorialSystem && tutorialSystem.IsTutorialInProgress)
        {
            Debug.LogWarning("[SaveManager] SaveGameData skipped because tutorial is in progress.");
            return;
        }

        // 기존 데이터 클리어 (리스트 등 재사용)
        cachedSaveData.Clear();

        // 0. 이 세이브를 만든 빌드를 각인한다. 정식 빌드는 데모 세이브를 이어받지 않고 그대로 덮어쓴다.
        cachedSaveData.buildVariant = BuildInfo.Variant;

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
            if (SteamCloudSaveService.Upload(encryptedData))
            {
                // 클라우드가 새 진행도로 교체됐으니, 삭제 대상이던 예전 세이브는 더 이상 존재하지 않는다.
                ClearCloudTombstone();
            }

            Debug.Log(GamePaths.Redact($"[SaveManager] Game Data Encrypted & Saved to: {GetSaveFilePath()} (variant={cachedSaveData.buildVariant}, Alloc-minimized)"));
        }
        else
        {
            Debug.LogError("[SaveManager] Local save failed; skipping cloud upload to avoid diverging from local state.");
        }
    }

    // 파일이 있어도 (1) 복호화/파싱이 안 되거나 (2) 다른 빌드 변형(데모↔정식)의 세이브면 이어하기가
    // 불가능하므로 "세이브 없음"으로 취급한다. 메인이 못 쓰는 상태여도 백업이 살아있으면 이어하기가 가능하다.
    // 정식 빌드에서 데모 세이브를 만나면 여기서 false가 되어 이어하기 버튼이 사라지고, 새 게임 저장이
    // 그 파일을 그대로 덮어쓴다.
    public bool HasSaveData()
    {
        string stateKey = BuildSaveStateKey();
        if (null != cachedSaveStateKey && stateKey == cachedSaveStateKey)
        {
            return bCachedHasUsableSave;
        }

        cachedSaveStateKey = stateKey;
        bCachedHasUsableSave = IsContinuable(ReadSaveSlot(GetSaveFilePath(), out _, out _))
                            || IsContinuable(ReadSaveSlot(GamePaths.GameSaveBackupFile, out _, out _));

        return bCachedHasUsableSave;
    }

    // 세이브 파일들의 상태를 갱신 시각/크기로 요약한다. 저장/클라우드 반영/외부 삭제가 일어나면 값이 달라져
    // HasSaveData 캐시가 자연히 무효화된다.
    private string BuildSaveStateKey()
    {
        return $"{DescribeFileState(GetSaveFilePath())}|{DescribeFileState(GamePaths.GameSaveBackupFile)}";
    }

    private static string DescribeFileState(string _path)
    {
        FileInfo info = new FileInfo(_path);
        if (false == info.Exists) return "-";

        return $"{info.LastWriteTimeUtc.Ticks}:{info.Length}";
    }

    /// <summary>
    /// 세이브 슬롯(메인/백업) 하나를 읽어본 결과입니다.
    ///
    /// Unreadable과 Corrupted를 반드시 구분해야 합니다. 전자는 "지금 못 읽었다"일 뿐 파일 내용은
    /// 멀쩡할 수 있습니다. 이것을 손상으로 취급해 백업으로 치유하면, 백신이 잠깐 파일을 잡고 있었다는
    /// 이유만으로 멀쩡한 최신 진행도가 한 판 전으로 영구히 되돌아갑니다.
    /// </summary>
    private enum ESaveSlotState
    {
        /// <summary>읽고, 복호화/파싱까지 되고, 현재 빌드에서 이어서 플레이해도 되는 변형입니다.</summary>
        Usable,

        /// <summary>파일이 없습니다.</summary>
        Missing,

        /// <summary>파일은 있으나 읽지 못했습니다. (잠금/권한/장치 문제) 내용은 멀쩡할 수 있습니다.</summary>
        Unreadable,

        /// <summary>읽었지만 복호화/파싱에 실패했습니다. 내용 자체가 깨졌습니다.</summary>
        Corrupted,

        /// <summary>멀쩡하지만 다른 빌드 변형(데모↔정식)의 세이브라 이어서 플레이할 수 없습니다.</summary>
        ForeignVariant
    }

    // 슬롯 하나를 읽어 상태를 판정한다.
    // 복구 시 원본 바이트를 그대로 재사용할 수 있도록 읽어들인 바이트도 함께 돌려준다(파일을 두 번 읽지 않기 위함).
    // _data와 _rawBytes는 파싱에 성공했으면 채워진다(ForeignVariant 포함). 반드시 상태를 먼저 확인하고 쓸 것.
    //
    // _maxAttempts에 SafeFileIO.SINGLE_ATTEMPT를 주면 이 안에서 기다리지 않는다. 대기를 코루틴이
    // 쥐는 메인 메뉴 확인 경로에서 쓴다. 그 경로는 같은 실패를 초당 여러 번 만나므로 _bLogFailures를
    // 꺼서 Sentry가 같은 사건을 수십 번 받지 않게 한다. (최종 판정만 호출부가 한 번 찍는다)
    private ESaveSlotState ReadSaveSlot(string _path, out GameSaveData _data, out byte[] _rawBytes,
                                        int _maxAttempts = SafeFileIO.MAX_ATTEMPTS_DEFAULT, bool _bLogFailures = true)
    {
        _data = null;
        _rawBytes = null;

        if (false == File.Exists(_path)) return ESaveSlotState.Missing;

        if (!SafeFileIO.TryReadAllBytes(_path, out byte[] encryptedData, out Exception readError, _maxAttempts))
        {
            // File.Exists 직후에 지워졌다면 없는 것과 같다. (외부 삭제, 클라우드 정리 등)
            if (readError is FileNotFoundException || readError is DirectoryNotFoundException)
            {
                return ESaveSlotState.Missing;
            }

            if (_bLogFailures)
            {
                Debug.LogError(GamePaths.Redact($"[SaveManager] Failed to read save file '{_path}': {readError.Message}"));
            }

            return ESaveSlotState.Unreadable;
        }

        if (!TryParseSaveBytes(encryptedData, out GameSaveData data))
        {
            return ESaveSlotState.Corrupted;
        }

        _data = data;
        _rawBytes = encryptedData;

        if (!BuildInfo.IsSaveVariantCompatible(data.buildVariant))
        {
            Debug.LogWarning(GamePaths.Redact($"[SaveManager] '{_path}' was created by another build variant ({data.buildVariant}); current build is {BuildInfo.Variant}. Treating it as no save data (it will be overwritten by the next save)."));
            return ESaveSlotState.ForeignVariant;
        }

        return ESaveSlotState.Usable;
    }

    // 이어하기 버튼을 살려둘 상태인지 판정한다.
    // Unreadable을 "세이브 없음"으로 취급하면 버튼이 사라지고, 유저가 새 게임을 골라 멀쩡히 살아있는
    // 진행도를 스스로 지우게 된다. 실제로 못 읽으면 LoadGameData가 저장을 막아 파일을 지켜낸다.
    private static bool IsContinuable(ESaveSlotState _state)
    {
        return ESaveSlotState.Usable == _state || ESaveSlotState.Unreadable == _state;
    }

    // // 메인 메뉴 세이브 확인
    // BootStrap이 메인 메뉴를 세우기 전에 여기를 통과시킨다. 파일을 읽을 수 있는지 결론이 날 때까지
    // 천천히 재시도하고, 그동안 UI는 SaveCheckState를 보고 "확인 중" 화면으로 상호작용을 막는다.

    /// <summary>
    /// 확인이 시작됐음을 먼저 알립니다.
    /// 확인 코루틴 앞에 스팀 클라우드 동기화처럼 프레임을 안 넘기는 동기 작업이 있을 때,
    /// 그 구간까지 "확인 중" 화면으로 덮기 위해 미리 세워둡니다.
    /// </summary>
    public void BeginSaveCheck()
    {
        saveCheckState = ESaveCheckState.Checking;
        saveCheckElapsedSeconds = 0f;
    }

    /// <summary>
    /// 세이브 파일을 읽을 수 있는지 결론이 날 때까지 재시도합니다.
    /// 정상적인 경우 첫 시도에 끝나 프레임을 한 번도 넘기지 않습니다.
    /// </summary>
    public IEnumerator CheckSaveAvailabilityRoutine()
    {
        BeginSaveCheck();

        while (true)
        {
            if (TryResolveSaveAvailabilityOnce(out bool bHasUsableSave))
            {
                CacheSaveAvailability(bHasUsableSave);
                saveCheckState = ESaveCheckState.Ready;
                yield break;
            }

            if (saveCheckElapsedSeconds >= SAVE_CHECK_TIMEOUT_SECONDS)
            {
                // 파일은 있는데 끝내 못 읽었다. 이어하기는 열어둔다. 내용이 멀쩡할 수 있고,
                // 실제 로드가 또 실패하면 LoadGameData가 저장을 막아 파일을 지켜낸다.
                CacheSaveAvailability(true);
                saveCheckState = ESaveCheckState.Failed;

                Debug.LogError($"[SaveManager] Save files could not be read within {SAVE_CHECK_TIMEOUT_SECONDS} seconds. They are left untouched; asking the player what to do.");
                yield break;
            }

            // 메뉴에서 Time.timeScale이 0일 수 있으므로 Realtime으로 기다린다.
            yield return new WaitForSecondsRealtime(SAVE_CHECK_INTERVAL_SECONDS);
            saveCheckElapsedSeconds += SAVE_CHECK_INTERVAL_SECONDS;
        }
    }

    /// <summary>실패 화면의 "다시 시도"가 부릅니다. 잠금이 그새 풀렸으면 그대로 복구됩니다.</summary>
    public void RetrySaveAvailabilityCheck()
    {
        if (ESaveCheckState.Checking == saveCheckState) return;

        StartCoroutine(CheckSaveAvailabilityRoutine());
    }

    /// <summary>
    /// 실패 화면의 "기존 세이브를 포기하고 새로 시작"이 부릅니다.
    ///
    /// 파일을 여기서 지우지는 않습니다. 지금 못 읽는 파일은 대개 지우지도 못하고, 지운다 해도
    /// 이 시점에 그럴 이유가 없습니다. 대신 "세이브 없음"으로 결론을 확정하고 저장 차단을 풀어
    /// 유저가 평소의 새로하기 경로를 그대로 타게 합니다. 그 경로의 DeleteSaveData가 삭제를
    /// 시도하고, 실패하더라도 첫 저장이 File.Replace로 덮어씁니다.
    ///
    /// 되돌릴 수 없는 선택입니다. 부르기 전에 "기존 진행도를 덮어씁니다"를 확인 팝업으로 반드시
    /// 알려야 하며, 스팀 클라우드 사본도 새로하기 경로에서 함께 정리된다는 점까지 포함해야 합니다.
    /// </summary>
    public void AbandonUnreadableSaveAndStartFresh()
    {
        Debug.LogError("[SaveManager] The player chose to abandon the unreadable save and start fresh. The existing file will be overwritten by the first save.");

        bSaveBlockedByUnreadableSave = false;
        bReportedSaveBlock = false;

        CacheSaveAvailability(false);
        saveCheckState = ESaveCheckState.Ready;
    }

    // 확인 결과를 HasSaveData 캐시에 심어, 메인 메뉴 UI가 같은 파일을 곧바로 다시 읽지 않게 한다.
    private void CacheSaveAvailability(bool _bHasUsableSave)
    {
        cachedSaveStateKey = BuildSaveStateKey();
        bCachedHasUsableSave = _bHasUsableSave;
    }

    // 한 번만 시도해 결론이 나는지 본다. Unreadable이 하나라도 있으면 아직 결론이 아니다.
    // 초당 여러 번 도는 경로라 실패 로그는 끄고, 최종 판정만 호출부가 한 번 남긴다.
    private bool TryResolveSaveAvailabilityOnce(out bool _bHasUsableSave)
    {
        _bHasUsableSave = false;

        ESaveSlotState mainState = ReadSaveSlot(GetSaveFilePath(), out _, out _, SafeFileIO.SINGLE_ATTEMPT, false);

        if (ESaveSlotState.Unreadable == mainState) return false;

        if (ESaveSlotState.Usable == mainState)
        {
            _bHasUsableSave = true;
            return true;
        }

        ESaveSlotState backupState = ReadSaveSlot(GamePaths.GameSaveBackupFile, out _, out _, SafeFileIO.SINGLE_ATTEMPT, false);

        if (ESaveSlotState.Unreadable == backupState) return false;

        _bHasUsableSave = (ESaveSlotState.Usable == backupState);
        return true;
    }

    // "새로하기" 진입 시 기존 진행도를 즉시 제거한다. 이 호출이 없어도 SetupScene()의 bNewGame 가드가
    // 로드는 막아주지만, 파일 자체는 다음 자동저장 전까지 디스크에 남아있어 그 사이 강제 종료되면
    // 이전 진행도가 그대로 살아남는다. 메인/백업 파일을 모두 지워 확실히 새 시작을 보장한다.
    public void DeleteSaveData()
    {
        // 데모 빌드에서 남아있는 정식 빌드 세이브를 삭제하기 전에 먼저 보존한다.
        // (WriteSaveFileWithBackup 경로와 동일한 보호: PreserveForeignSaveOnce 참고)
        PreserveForeignSaveOnce();

        TryDeleteFile(GetSaveFilePath());
        TryDeleteFile(GamePaths.GameSaveBackupFile);

        // 로컬만 지우면 다음 실행 시 SyncCloudSaveIfNewer()가 "로컬 없음 = 클라우드가 최신"으로 오판해
        // 방금 지운 세이브를 클라우드에서 그대로 복원해버린다. 클라우드도 함께 지워야 삭제가 유지된다.
        // 오프라인 등으로 지금 못 지웠다면 표식을 남겨 다음 실행에서 정리한다.
        if (SteamCloudSaveService.Delete())
        {
            ClearCloudTombstone();
        }
        else
        {
            WriteCloudTombstone();
        }

        // 캐시를 무효화해 다음 HasSaveData() 호출이 삭제된 상태를 바로 반영하도록 한다.
        cachedSaveStateKey = null;
        bCachedHasUsableSave = false;

        // 읽지 못한 세이브 때문에 저장을 막아둔 상태였더라도, 유저가 직접 새로하기를 골랐으면
        // 더 지킬 진행도가 없다. 막아두면 새 게임이 통째로 저장되지 않으므로 여기서 푼다.
        bSaveBlockedByUnreadableSave = false;
        bReportedSaveBlock = false;

        // 새 게임 진입 시 내비게이션 팝업의 런타임 정적 세션 기록도 완전 초기화
        HUD_PopupNav_Main.ResetRuntimeSessionState();

        Debug.Log("[SaveManager] Existing save data deleted for New Game.");
    }

    public void LoadGameData()
    {
        string path = GetSaveFilePath();
        string backupPath = GamePaths.GameSaveBackupFile;

        ESaveSlotState mainState = ReadSaveSlot(path, out GameSaveData saveData, out _);

        if (ESaveSlotState.Usable == mainState)
        {
            ApplyLoadedData(saveData);
            Debug.Log(GamePaths.Redact($"[SaveManager] Game Data Decrypted & Loaded from: {path}"));
            return;
        }

        // 메인이 "지금 못 읽혔을 뿐"이면 백업으로 내려가지 않는다.
        //
        // 백업은 정의상 한 판 전 상태다. 여기서 백업을 로드해버리면 유저는 되돌아간 진행도로 계속
        // 플레이하고, 다음 자동저장이 그 상태를 메인 자리에 써버린다. 즉 "잠깐 못 읽었다"가 "진행도가
        // 되돌아갔다"로 확정된다. 백업이 살아있어도 마찬가지이므로 백업 상태를 보기 전에 멈춘다.
        if (ESaveSlotState.Unreadable == mainState)
        {
            BlockSavingToProtectUnreadableSave("main save file");
            return;
        }

        if (ESaveSlotState.Missing != mainState)
        {
            Debug.LogWarning($"[SaveManager] Main save file is unusable ({mainState}). Trying backup...");
        }

        ESaveSlotState backupState = ReadSaveSlot(backupPath, out GameSaveData backupData, out byte[] backupBytes);

        if (ESaveSlotState.Usable != backupState)
        {
            // 메인은 못 쓰는 게 확실한데 백업은 읽지도 못한 상태다. 백업 안에 멀쩡한 진행도가
            // 들어있을 수 있으므로 위와 같은 이유로 저장을 막는다.
            if (ESaveSlotState.Unreadable == backupState)
            {
                BlockSavingToProtectUnreadableSave("backup save file");
                return;
            }

            if (ESaveSlotState.Missing != mainState || ESaveSlotState.Missing != backupState)
            {
                Debug.LogError($"[SaveManager] No usable save file (main={mainState}, backup={backupState}). Load aborted.");
            }
            else
            {
                Debug.LogWarning("[SaveManager] Save file not found.");
            }

            return;
        }

        ApplyLoadedData(backupData);
        Debug.LogWarning("[SaveManager] Recovered game data from backup save file.");

        // 메인 파일을 백업 내용으로 치유해서 다음 실행부터 같은 손상 파일을 다시 만나지 않게 한다.
        // 여기까지 왔다면 메인은 Missing/Corrupted/ForeignVariant 중 하나로, 못 쓰는 것이 확실하다.
        // (Unreadable은 위에서 이미 걸러졌다)
        //
        // 단, 손상된 메인이 남아 있는 채로 WriteSaveFileWithBackup을 부르면 File.Replace가
        // 그 손상본을 백업 자리로 밀어넣어 방금 복구에 성공한 백업을 덮어써버린다.
        // 먼저 지워서 File.Move 경로(백업 미변경)를 타게 한다.
        // 이 경로에서는 파일이 지워진 뒤 WriteSaveFileWithBackup이 호출되므로, 그 안의 보존 로직이
        // 원본을 못 보게 된다. 지우기 전에 먼저 보존을 시도한다.
        PreserveForeignSaveOnce();

        if (!File.Exists(path) || TryDeleteFile(path))
        {
            WriteSaveFileWithBackup(backupBytes, "RecoverFromBackup");
        }
    }

    // 디스크에 세이브가 있는데 읽지 못한 상태다. 그대로 게임을 진행시키면 다음 자동저장이 그 파일을
    // 덮어써, 복구 가능했을 진행도가 영영 사라진다. 이 세션 동안 저장을 막아 파일을 손대지 않고 남긴다.
    // 유저가 "새로하기"를 고르면 지킬 것이 없어지므로 DeleteSaveData에서 다시 풀린다.
    private void BlockSavingToProtectUnreadableSave(string _which)
    {
        bSaveBlockedByUnreadableSave = true;

        Debug.LogError($"[SaveManager] The {_which} exists but could not be read (locked, or inaccessible). Load aborted and saving is disabled for this session so the file is left intact for recovery.");
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

    // 대상이 애초에 없으면 File.Delete는 예외 없이 통과하므로 true가 된다.
    private bool TryDeleteFile(string _path)
    {
        if (SafeFileIO.TryDelete(_path, out Exception _error)) return true;

        Debug.LogError(GamePaths.Redact($"[SaveManager] Failed to delete file '{_path}': {_error.Message}"));
        return false;
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
        PreserveForeignSaveOnce();

        string path = GetSaveFilePath();
        string tempPath = GamePaths.GameSaveTempFile;
        string backupPath = GamePaths.GameSaveBackupFile;

        if (!SafeFileIO.TryWriteAllBytes(tempPath, _data, out Exception writeError))
        {
            Debug.LogError(GamePaths.Redact($"[SaveManager] ({_context}) Failed to write temp save file: {writeError.Message}"));

            // 반쯤 쓰인 임시 파일을 남겨두지 않는다. 다음 저장이 덮어쓰긴 하지만, 그때까지
            // 동기화 폴더에 찌꺼기가 올라가고 유저 눈에도 보인다.
            SafeFileIO.CleanUpTempFile(tempPath);
            return false;
        }

        // 새 내용 반영 + 기존 파일을 backupPath로 이동이 한 번에 원자적으로 처리된다.
        // (대상이 없으면 백업할 것도 없으므로 단순 이동이 된다. SafeFileIO 참고)
        if (SafeFileIO.TryReplaceOrMove(tempPath, path, backupPath, out Exception replaceError))
        {
            return true;
        }

        Debug.LogError(GamePaths.Redact($"[SaveManager] ({_context}) Failed to replace save file: {replaceError.Message}"));
        SafeFileIO.CleanUpTempFile(tempPath);

        return false;
    }

    // // 클라우드 삭제 표식(tombstone)
    // "새로하기"로 로컬 세이브를 지웠는데 그 시점에 클라우드까지 지우지 못하면(오프라인/스팀 미실행),
    // 다음 실행에서 SyncCloudSaveIfNewer가 "로컬 없음 = 클라우드가 최신"으로 판단해 방금 지운 세이브를
    // 그대로 복원해버린다. 삭제 시각을 남겨두고, 다음 실행에서 그보다 오래된 클라우드 세이브는
    // 복원 대신 삭제하도록 한다.

    /// <summary>
    /// 삭제 표식이 남아있는지 확인하고, 남아있다면 클라우드 세이브를 지금 정리한다.
    /// 이번 동기화를 중단해야 하면(= 복원하면 안 되면) true를 반환한다.
    /// </summary>
    private bool TryResolveCloudTombstone(DateTime _cloudTimeUtc)
    {
        if (!TryReadCloudTombstoneUtc(out DateTime deletedAtUtc)) return false;

        // 삭제 이후에 기록된 클라우드 세이브라면 다른 기기에서 이어서 플레이한 정상 진행도다.
        // 이 경우엔 표식을 버리고 평소 동기화 규칙을 그대로 따른다.
        if (_cloudTimeUtc > deletedAtUtc)
        {
            Debug.Log("[SaveManager] Cloud save is newer than the recorded deletion; treating it as valid progress from another device.");
            ClearCloudTombstone();
            return false;
        }

        // 우리가 지우려다 실패했던 바로 그 세이브다. 복원하지 않고 이제야 지운다.
        if (SteamCloudSaveService.Delete())
        {
            ClearCloudTombstone();
            Debug.Log("[SaveManager] Deleted the leftover cloud save from an earlier offline New Game.");
        }
        else
        {
            // 아직도 못 지웠다면 표식을 유지해 다음 실행에서 다시 시도한다.
            Debug.LogWarning("[SaveManager] Could not delete the leftover cloud save; keeping the tombstone for a later attempt.");
        }

        return true;
    }

    private void WriteCloudTombstone()
    {
        if (SafeFileIO.TryWriteAllText(GamePaths.GameSaveCloudTombstoneFile, DateTime.UtcNow.Ticks.ToString(), out Exception _error))
        {
            Debug.Log("[SaveManager] Cloud delete pending; wrote tombstone to clean it up on a later launch.");
            return;
        }

        Debug.LogError(GamePaths.Redact($"[SaveManager] Failed to write cloud tombstone: {_error.Message}"));
    }

    private bool TryReadCloudTombstoneUtc(out DateTime _deletedAtUtc)
    {
        _deletedAtUtc = default;

        string path = GamePaths.GameSaveCloudTombstoneFile;
        if (false == File.Exists(path)) return false;

        if (!SafeFileIO.TryReadAllText(path, out string raw, out Exception _error))
        {
            // 읽지 못했을 뿐이므로 표식은 지우지 않는다. 다음 실행에서 다시 판단하게 둔다.
            Debug.LogError(GamePaths.Redact($"[SaveManager] Failed to read cloud tombstone: {_error.Message}"));
            return false;
        }

        if (!long.TryParse(raw.Trim(), out long ticks) || ticks < 0 || ticks > DateTime.MaxValue.Ticks)
        {
            // 내용이 깨졌으면 판단 기준이 없다. 표식을 지우고 기존 동기화 로직에 맡긴다.
            Debug.LogWarning("[SaveManager] Cloud tombstone is corrupted; removing it.");
            ClearCloudTombstone();
            return false;
        }

        _deletedAtUtc = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    private void ClearCloudTombstone()
    {
        if (false == File.Exists(GamePaths.GameSaveCloudTombstoneFile)) return;

        TryDeleteFile(GamePaths.GameSaveCloudTombstoneFile);
    }

    // 데모 빌드는 정식 빌드가 남긴 세이브를 덮어쓰기 직전에 한 번만 따로 복사해둔다.
    // 데모와 정식은 같은 폴더의 같은 파일(SaveData.dat)을 쓰므로, 정식을 플레이하던 유저가
    // PC에 남아있는 데모를 잠깐 켜기만 해도 진행도가 사라질 수 있기 때문이다.
    // (반대 방향 - 정식이 데모 세이브를 덮어쓰는 것 - 은 의도된 동작이라 보존하지 않는다.
    //  덮어쓰기 직전 상태는 File.Replace가 SaveData.dat.bak에 남긴다.)
    // 첫 저장 이후에는 메인 파일이 우리 것이 되므로 세션당 한 번만 확인하면 충분하다.
    private void PreserveForeignSaveOnce()
    {
        if (bForeignSavePreserveChecked) return;

        if (BuildInfo.IsFullRelease)
        {
            bForeignSavePreserveChecked = true;
            return;
        }

        string path = GetSaveFilePath();
        ESaveSlotState state = ReadSaveSlot(path, out GameSaveData existingData, out _);

        // 읽지 못했다면 누구의 세이브인지 판단할 수 없다. 여기서 확인 완료로 표시해버리면 이번 세션에는
        // 다시 보지 않게 되므로, 플래그를 그대로 두어 다음 호출에서 한 번 더 시도하게 한다.
        if (ESaveSlotState.Unreadable == state) return;

        bForeignSavePreserveChecked = true;

        if (ESaveSlotState.ForeignVariant != state) return;

        try
        {
            File.Copy(path, GamePaths.GameSaveForeignBackupFile, overwrite: true);
            Debug.LogWarning(GamePaths.Redact($"[SaveManager] Preserved a {existingData.buildVariant} save to '{GamePaths.GameSaveForeignBackupFile}' before this demo build overwrites it."));
        }
        catch (Exception _e)
        {
            Debug.LogError($"[SaveManager] Failed to preserve the save from another build variant: {_e.Message}");
        }
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
