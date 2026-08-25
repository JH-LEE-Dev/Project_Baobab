using System;
using UnityEngine;

public class GameInstaller : MonoBehaviour
{
    // BootStrap이 SignalHub에 접근할 수 없으므로, MainMenu 커튼 롤백 시점을 plain event로 한 번 더 감싸 노출한다.
    public event Action TownIntroCurtainRollbackEvent;
    // Town/Dungeon → MainMenu 카메라 상승 시작 시점. Town/Dungeon 둘 중 활성화된 쪽만 실제로 발행한다.
    public event Action GoToMainMenuCurtainRevealEvent;

    //외부 의존성
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;
    private LocalizationManager localizationManager;
    private SaveManager saveManager;

    //내부 의존성
    private UnitSpawner unitSpawner;
    private CameraManager cameraManager;
    private SignalHub signalHub;
    private TeleportManager teleportManager;
    private UnitLogicManager unitLogicManager;
    private GameplayUIInstaller gameplayUIInstaller;
    private InventoryManager inventoryManager;
    private SkillDispatcher skillDispatcher;
    private SkillManager skillManager;
    private OffroadContainer offroadContainer;
    private SkyCameraProductionManager skyCameraProductionManager;
    private InDungeonResultManager inDungeonResultManager;
    private TutorialQuestIndicatorManager tutorialQuestIndicatorManager;

    //시스템 객체들
    private UnitSystem unitSystem;
    private TownSystem townSystem;
    private InDungeonSystem inDungeonSystem;
    private EnvironmentSystem environmentSystem;

    private SkillSystem skillSystem;
    private GameSystem gameSystem;
    private TutorialSystem tutorialSystem;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager, SaveManager _saveManager)
    {
        DontDestroyOnLoad(gameObject);

        unitSystem = new UnitSystem();
        signalHub = new SignalHub();
        skillSystem = new SkillSystem();
        gameSystem = new GameSystem();
        tutorialSystem = new TutorialSystem();

        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;
        localizationManager = _localizeManager;
        saveManager = _saveManager;

        unitSpawner = GetComponentInChildren<UnitSpawner>();
        cameraManager = GetComponent<CameraManager>();
        teleportManager = GetComponent<TeleportManager>();
        unitLogicManager = GetComponentInChildren<UnitLogicManager>();
        townSystem = GetComponentInChildren<TownSystem>();
        inDungeonSystem = GetComponentInChildren<InDungeonSystem>();
        environmentSystem = GetComponentInChildren<EnvironmentSystem>();
        gameplayUIInstaller = GetComponentInChildren<GameplayUIInstaller>();
        inventoryManager = GetComponentInChildren<InventoryManager>();
        skillManager = GetComponentInChildren<SkillManager>();
        skillDispatcher = GetComponentInChildren<SkillDispatcher>();
        offroadContainer = GetComponentInChildren<OffroadContainer>();
        skyCameraProductionManager = GetComponentInChildren<SkyCameraProductionManager>();
        inDungeonResultManager = GetComponentInChildren<InDungeonResultManager>();
        tutorialQuestIndicatorManager = GetComponentInChildren<TutorialQuestIndicatorManager>(true);

        inDungeonResultManager.Initialize();
        skyCameraProductionManager.Initialize();
        unitLogicManager.Initialize(inputManager);
        environmentSystem.Initialize(signalHub, unitLogicManager);
        cameraManager.Initialize(signalHub, inputManager);
        unitSpawner.Initialize(inputManager, environmentSystem);
        teleportManager.Initialize(signalHub, bootStrapProvider, inputManager);
        inventoryManager.Initialize();
        offroadContainer.Initialize(inventoryManager, inputManager);
        townSystem.Initialize(signalHub, environmentSystem, inputManager, inventoryManager, offroadContainer, skyCameraProductionManager);
        inDungeonSystem.Initialize(signalHub, environmentSystem, inventoryManager, inputManager, inventoryManager, offroadContainer
        , skyCameraProductionManager, inDungeonResultManager);
        skillManager.Initialize(inventoryManager);
        gameplayUIInstaller.Initialize(bootStrapProvider, signalHub, inputManager, inventoryManager, inDungeonSystem.inDungeonObjectManager,
        townSystem.logProcessingManager.logContainer, townSystem.logProcessingManager.logCutter, skillManager, townSystem.logProcessingManager.shopNPC,
        inventoryManager, localizationManager, environmentSystem.densityManager, environmentSystem.weatherManager, environmentSystem.timeController,
        offroadContainer, inDungeonSystem.inDungeonResultManager);

        skillDispatcher.Initialize(signalHub,
         inventoryManager,
          townSystem.logProcessingManager.logContainer,
           townSystem.logProcessingManager,
        townSystem.logProcessingManager,
         environmentSystem.densityManager,
          inDungeonSystem.inDungeonObjectManager.itemManager.carrrotItemController,
           townSystem.townObjectManager,
            townSystem.logProcessingManager,
             inDungeonSystem.inDungeonObjectManager.itemManager.logItemController,
             offroadContainer,
             inDungeonSystem.inDungeonObjectManager,
             inDungeonSystem.inDungeonUnitSpawner,
             townSystem.townUnitSpawner);

        unitSystem.Initialize(signalHub, unitSpawner, unitLogicManager, inventoryManager, offroadContainer, inDungeonSystem.inDungeonResultManager,
        environmentSystem);
        skillSystem.Initialize(signalHub, skillManager, skillDispatcher);
        tutorialSystem.Initialize(signalHub, inventoryManager);

        // 튜토리얼 퀘스트 목표 오브젝트 위에 뜨는 화살표 인디케이터. 대상(차량/보관함 등)은 런타임에
        // 생성되므로 참조가 아니라 매니저를 넘겨 퀘스트가 시작될 때마다 현재 오브젝트를 찾아가게 한다.
        tutorialQuestIndicatorManager?.Initialize(signalHub, offroadContainer,
            inDungeonSystem.inDungeonObjectManager, townSystem.townObjectManager,
            townSystem.logProcessingManager, townSystem.tentManager);

        _saveManager.Initialize(signalHub, tutorialSystem, skillSystem, inventoryManager, townSystem.logProcessingManager,
        environmentSystem.densityManager, inDungeonSystem.inDungeonObjectManager, townSystem.townObjectManager, offroadContainer,
        townSystem.townUnitSpawner);

        unitSystem.CreateCharacter();
        environmentSystem.DI(environmentSystem, townSystem.townObjectManager, inDungeonSystem.inDungeonObjectManager,
        inDungeonSystem.inDungeonUnitSpawner, townSystem.townUnitSpawner);

        gameSystem.Initialize(inDungeonSystem, townSystem);

        BindEvents();
    }

    public void LoadGame()
    {
        saveManager.LoadGameData();
        gameplayUIInstaller.Refresh();
    }

    public void SetupGameInstaller(SceneChangeData _sceneChangeData)
    {
        // 씬 전환마다(던전 재도전 포함) 충돌 풀 사용량을 남긴다. 타운을 거치지 않는 재도전 경로에서는
        // CollisionSystem.ClearAll()이 호출되지 않으므로, 슬롯이 새고 있다면 이 값이 계속 커진다.
        CollisionSystem.Instance?.LogPoolUsage($"SetupGameInstaller({_sceneChangeData.currentScene})");

        cameraManager.ResetCamera();

        // 가상 커서 요청을 씬 전환마다 초기화한다. 커서를 띄운 창이 정상적으로 닫히면 스스로
        // 요청을 거두지만, 씬 전환처럼 OnHide를 거치지 않고 사라지는 경로가 있어서 여기서 한 번 막는다.
        inputManager.SetVirtualCursorRequested(false);

        if (_sceneChangeData.currentScene == SceneType.DungeonScene)
        {
            environmentSystem.SetupForMapType(_sceneChangeData.forestType, _sceneChangeData.mapType);
            inDungeonSystem.StartDungeonSystem(_sceneChangeData);
            gameplayUIInstaller.SetupCanvas();
            unitSystem.SetWhereIsCharacter(true);
        }
        else
        {
            environmentSystem.SetupForTownMap();
            inDungeonSystem.ClearInDungeonSystem();
            townSystem.StartTownSystem(_sceneChangeData);
            townSystem.lootPillarManager?.SpawnAcquiredPillars(inDungeonSystem.inDungeonObjectManager);
            // Town 초기화(그리드 생성, NPC 스폰)가 끝난 뒤에만 구름이 걷히도록 여기서 명시적으로 트리거한다.
            inDungeonSystem.NotifyTownSystemReady();
            unitSystem.SetWhereIsCharacter(false);

            // 마을 도착(최초 진입/이어하기 포함) 시점 자동저장.
            signalHub.Publish(new AutoSaveRequestedSignal(AutoSaveReason.ArriveTown));
        }
    }

    // 해제 순서는 기존과 동일하다. 달라진 건 "한 단계가 예외로 실패해도 나머지 단계와 Destroy가
    // 반드시 실행된다"는 점뿐이다. 예전엔 앞 단계에서 예외가 나면 뒤쪽 시스템이 시그널 구독을 남긴 채
    // 좀비로 남고, 호출자(BootStrap.TransitionToScene)의 씬 로드까지 도달하지 못했다.
    public void Release()
    {
        SafeRelease("unitSystem", () => unitSystem.Release());
        SafeRelease("cameraManager", () => cameraManager.Release());
        SafeRelease("teleportManager", () => teleportManager.Release());
        SafeRelease("townSystem", () => townSystem.Release());
        SafeRelease("inDungeonSystem", () => inDungeonSystem.Release());
        SafeRelease("environmentSystem", () => environmentSystem.Release());
        SafeRelease("gameplayUIInstaller", () => gameplayUIInstaller.Release());
        SafeRelease("skillSystem", () => skillSystem.Release());
        SafeRelease("skillDispatcher", () => skillDispatcher.Release());
        SafeRelease("saveManager", () => saveManager.Release());
        SafeRelease("gameSystem", () => gameSystem.Release());
        SafeRelease("tutorialSystem", () => tutorialSystem.Release());
        SafeRelease("tutorialQuestIndicatorManager", () => tutorialQuestIndicatorManager?.Release());

        SafeRelease("ReleaseEvents", ReleaseEvents);
        Destroy(gameObject);
    }

    /// <summary>
    /// 해제 단계 하나를 예외로부터 격리한다. 실패하면 어느 단계였는지 에러 로그로 남기고 다음 단계로 넘어간다.
    /// (Debug.LogError는 Sentry가 스택과 함께 자동 수집하므로 별도 전송 코드가 필요 없다)
    /// </summary>
    private void SafeRelease(string _stepName, Action _step)
    {
        // 멈춤(무한 루프/블로킹) 지점을 특정하기 위한 진입 흔적.
        // 에디터가 먹통이 됐을 때 Editor.log에 마지막으로 남은 이 로그가 곧 범인 단계다.
        // 씬 전환당 한 번씩만 찍히므로 로그 부담은 없다.
        Debug.Log($"[GameInstaller] Release 단계 진입: {_stepName}");
        System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _step();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameInstaller] Release 단계 '{_stepName}'에서 예외가 발생했습니다. 나머지 해제는 계속 진행합니다.");
            Debug.LogException(e);
        }

        // 정상 종료라도 오래 걸린 단계는 남긴다. 멈춘 게 아니라 느린 경우를 잡기 위해서다.
        if (_sw.ElapsedMilliseconds >= 50)
        {
            Debug.LogWarning($"[GameInstaller] Release 단계 '{_stepName}'가 {_sw.ElapsedMilliseconds}ms 걸렸습니다.");
        }
    }

    private void Awake()
    {

    }

    private void OnDestroy()
    {

    }

    private void SaveGame()
    {
        saveManager.SaveGameData();
    }

    private void BindEvents()
    {
        gameplayUIInstaller.SaveGameEvent -= SaveGame;
        gameplayUIInstaller.SaveGameEvent += SaveGame;

        townSystem.MainMenuCurtainRollbackEvent -= TownIntroCurtainRollback;
        townSystem.MainMenuCurtainRollbackEvent += TownIntroCurtainRollback;

        townSystem.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        townSystem.GoToMainMenuCurtainRevealEvent += GoToMainMenuCurtainReveal;

        inDungeonSystem.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        inDungeonSystem.GoToMainMenuCurtainRevealEvent += GoToMainMenuCurtainReveal;
    }

    private void ReleaseEvents()
    {
        gameplayUIInstaller.SaveGameEvent -= SaveGame;
        townSystem.MainMenuCurtainRollbackEvent -= TownIntroCurtainRollback;
        townSystem.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        inDungeonSystem.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
    }

    private void TownIntroCurtainRollback()
    {
        TownIntroCurtainRollbackEvent?.Invoke();
    }

    private void GoToMainMenuCurtainReveal()
    {
        GoToMainMenuCurtainRevealEvent?.Invoke();
    }
}
