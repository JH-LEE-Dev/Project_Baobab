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
    private ObjectManager objectManager;
    private TeleportManager teleportManager;

    //시스템 객체들
    private UnitSystem unitSystem;
    private ObjectSystem objectSystem;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager)
    {
        DontDestroyOnLoad(gameObject);

        unitSystem = new UnitSystem();
        signalHub = new SignalHub();
        objectSystem = new ObjectSystem();


        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

        environmentManager = GetComponentInChildren<EnvironmentManager>();
        unitSpawner = GetComponent<UnitSpawner>();
        cameraManager = GetComponent<CameraManager>();
        objectManager = GetComponent<ObjectManager>();
        teleportManager = GetComponent<TeleportManager>();


        cameraManager.Initialize(signalHub);
        environmentManager.Initialize();
        unitSpawner.Initialize(inputManager, environmentManager);
        objectManager.Initialize(environmentManager);
        teleportManager.Initialize(signalHub, bootStrapProvider);


        unitSystem.Initialize(signalHub, unitSpawner);
        objectSystem.Initailize(signalHub,objectManager);

        unitSystem.SetupUnits();
    }

    public void SetupScene(SceneType _type)
    {
        if (objectSystem != null)
            objectSystem.SetupObjects(_type);

        cameraManager.ResetCamera();
    }

    public void Release()
    {
        unitSystem.Release();
        cameraManager.Release();
        objectSystem.Release();
        teleportManager.Release();

        Destroy(gameObject);
    }

    private void Awake()
    {

    }

    private void Start()
    {
        //Sound.PlayBGM("BGM");    
    }

    private void OnDestroy()
    {

    }
}
