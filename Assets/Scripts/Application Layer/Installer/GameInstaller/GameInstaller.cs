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

        _saveManager.Initialize(signalHub, skillSystem, inventoryManager, townSystem.logProcessingManager,
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
        cameraManager.ResetCamera();

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
            // Town 초기화(그리드 생성, NPC 스폰)가 끝난 뒤에만 구름이 걷히도록 여기서 명시적으로 트리거한다.
            inDungeonSystem.NotifyTownSystemReady();
            unitSystem.SetWhereIsCharacter(false);
        }
    }

    public void Release()
    {
        unitSystem.Release();
        cameraManager.Release();
        teleportManager.Release();
        townSystem.Release();
        inDungeonSystem.Release();
        environmentSystem.Release();
        gameplayUIInstaller.Release();
        skillSystem.Release();
        skillDispatcher.Release();
        saveManager.Release();
        gameSystem.Release();
        tutorialSystem.Release();
        tutorialQuestIndicatorManager?.Release();

        ReleaseEvents();
        Destroy(gameObject);
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
