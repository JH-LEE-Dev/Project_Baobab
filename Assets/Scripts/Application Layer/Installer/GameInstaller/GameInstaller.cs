using UnityEngine;

public class GameInstaller : MonoBehaviour
{
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

    //시스템 객체들
    private UnitSystem unitSystem;
    private TownSystem townSystem;
    private InDungeonSystem inDungeonSystem;
    private EnvironmentSystem environmentSystem;

    private SkillSystem skillSystem;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager,SaveManager _saveManager)
    {
        DontDestroyOnLoad(gameObject);

        unitSystem = new UnitSystem();
        signalHub = new SignalHub();
        skillSystem = new SkillSystem();

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

        environmentSystem.Initialize(signalHub, unitLogicManager);
        cameraManager.Initialize(signalHub, inputManager);
        unitSpawner.Initialize(inputManager, environmentSystem);
        teleportManager.Initialize(signalHub, bootStrapProvider);
        townSystem.Initialize(signalHub, environmentSystem, inputManager);
        inventoryManager.Initialize();
        inDungeonSystem.Initialize(signalHub, environmentSystem, inventoryManager);
        skillManager.Initialize(inventoryManager);
        gameplayUIInstaller.Initialize(bootStrapProvider, signalHub, inputManager, inventoryManager, inDungeonSystem.inDungeonObjectManager,
        townSystem.logProcessingManager.logContainer, townSystem.logProcessingManager.logCutter, skillManager, townSystem.logProcessingManager.shopNPC,
        inventoryManager, localizationManager);

        skillDispatcher.Initialize(signalHub,
         inventoryManager,
          townSystem.logProcessingManager.logContainer,
           townSystem.logProcessingManager.logCutter,
        townSystem.logProcessingManager.logEvaluator,
         environmentSystem.densityManager,
          inDungeonSystem.inDungeonObjectManager.itemManager.carrrotItemController,
           townSystem.townObjectManager);

        unitSystem.Initialize(signalHub, unitSpawner, unitLogicManager, inventoryManager);
        skillSystem.Initialize(skillManager, skillDispatcher);

        _saveManager.Initialize(signalHub, skillSystem, inventoryManager, townSystem.logProcessingManager,
        environmentSystem.densityManager, inDungeonSystem.inDungeonObjectManager, townSystem.townObjectManager);

        unitSystem.CreateCharacter();
        environmentSystem.DI(environmentSystem, townSystem.townObjectManager, inDungeonSystem.inDungeonObjectManager, inDungeonSystem.inDungeonUnitSpawner);
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
            inDungeonSystem.StartDungeonSystem(_sceneChangeData);
            gameplayUIInstaller.SetupCanvas();
            unitSystem.SetWhereIsCharacter(true);
        }
        else
        {
            townSystem.StartTownSystem(_sceneChangeData);
            unitSystem.SetWhereIsCharacter(false);
            inDungeonSystem.ClearInDungeonSystem();
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
    }

    private void ReleaseEvents()
    {
        gameplayUIInstaller.SaveGameEvent -= SaveGame;
    }
}
