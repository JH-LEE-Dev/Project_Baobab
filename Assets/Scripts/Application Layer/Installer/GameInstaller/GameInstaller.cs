using UnityEngine;

public class GameInstaller : MonoBehaviour
{
    //외부 의존성
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;

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

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager)
    {
        DontDestroyOnLoad(gameObject);

        unitSystem = new UnitSystem();
        signalHub = new SignalHub();
        skillSystem = new SkillSystem();

        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

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

        cameraManager.Initialize(signalHub, inputManager);
        environmentSystem.Initialize(signalHub, unitLogicManager);
        unitSpawner.Initialize(inputManager, environmentSystem);
        teleportManager.Initialize(signalHub, bootStrapProvider);
        townSystem.Initialize(signalHub, environmentSystem, inputManager);
        inventoryManager.Initialize();
        inDungeonSystem.Initialize(signalHub, environmentSystem, inventoryManager);
        skillManager.Initialize(inventoryManager);
        gameplayUIInstaller.Initialize(bootStrapProvider, signalHub, inputManager, inventoryManager, inDungeonSystem.inDungeonObjectManager,
        townSystem.logProcessingManager.logContainer, townSystem.logProcessingManager.logCutter, skillManager);
        skillDispatcher.Initialize(signalHub,inventoryManager, townSystem.logProcessingManager.logContainer,townSystem.logProcessingManager.logCutter);

        unitSystem.Initialize(signalHub, unitSpawner, unitLogicManager, inventoryManager);
        skillSystem.Initialize(skillManager, skillDispatcher);

        unitSystem.CreateCharacter();
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

        Destroy(gameObject);
    }

    private void Awake()
    {

    }

    private void OnDestroy()
    {

    }
}
