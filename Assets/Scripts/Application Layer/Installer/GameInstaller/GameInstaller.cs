using UnityEngine;

public class GameInstaller : MonoBehaviour
{
    //외부 의존성
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;

    //내부 의존성
    private EnvironmentManager environmentManager;
    private UnitSpawner unitSpawner;
    private CameraManager cameraManager;
    private SignalHub signalHub;
    private TeleportManager teleportManager;
    private UnitLogicManager unitLogicManager;

    //시스템 객체들
    private UnitSystem unitSystem;
    private TownSystem townSystem;
    private InDungeonSystem inDungeonSystem;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager)
    {
        DontDestroyOnLoad(gameObject);

        unitSystem = new UnitSystem();
        signalHub = new SignalHub();


        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

        environmentManager = GetComponentInChildren<EnvironmentManager>();
        unitSpawner = GetComponent<UnitSpawner>();
        cameraManager = GetComponent<CameraManager>();
        teleportManager = GetComponent<TeleportManager>();
        unitLogicManager = GetComponent<UnitLogicManager>();
        townSystem = GetComponentInChildren<TownSystem>();
        inDungeonSystem = GetComponentInChildren<InDungeonSystem>();


        cameraManager.Initialize(signalHub);
        environmentManager.Initialize();
        unitSpawner.Initialize(inputManager, environmentManager);
        teleportManager.Initialize(signalHub, bootStrapProvider);
        townSystem.Initialize(signalHub, environmentManager);
        inDungeonSystem.Initialize(signalHub, environmentManager);


        unitSystem.Initialize(signalHub, unitSpawner, unitLogicManager);

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


        Destroy(gameObject);
    }

    private void Awake()
    {

    }

    private void OnDestroy()
    {

    }
}
