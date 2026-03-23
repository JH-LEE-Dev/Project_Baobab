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

    //시스템 객체들
    private UnitSystem unitSystem;
    private TownSystem townSystem;
    private InDungeonSystem inDungeonSystem;
    private EnvironmentSystem environmentSystem;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager)
    {
        DontDestroyOnLoad(gameObject);

        unitSystem = new UnitSystem();
        signalHub = new SignalHub();


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



        cameraManager.Initialize(signalHub,inputManager);
        unitSpawner.Initialize(inputManager, environmentSystem);
        teleportManager.Initialize(signalHub, bootStrapProvider);
        townSystem.Initialize(signalHub, environmentSystem);
        inDungeonSystem.Initialize(signalHub, environmentSystem);
        environmentSystem.Initialize(signalHub,unitLogicManager);
        inventoryManager.Initialize();
        gameplayUIInstaller.Initialize(bootStrapProvider, signalHub, inputManager,inventoryManager,inDungeonSystem.inDungeonObjectManager);


        unitSystem.Initialize(signalHub, unitSpawner, unitLogicManager,inventoryManager);

        unitSystem.CreateCharacter();
    }

    public void SetupGameInstaller(SceneChangeData _sceneChangeData)
    {
        cameraManager.ResetCamera();

        if(_sceneChangeData.currentScene == SceneType.Dungeon)
            inDungeonSystem.StartDungeonSystem(_sceneChangeData);
        else
            townSystem.StartTownSystem(_sceneChangeData);
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

        Destroy(gameObject);
    }

    private void Awake()
    {

    }

    private void OnDestroy()
    {

    }
}
