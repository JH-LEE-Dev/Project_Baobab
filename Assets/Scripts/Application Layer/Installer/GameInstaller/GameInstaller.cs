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

    //시스템 객체들
    private UnitSystem unitSystem;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager)
    {
        unitSystem = new UnitSystem();
        signalHub = new SignalHub();

        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

        environmentManager = GetComponentInChildren<EnvironmentManager>();
        unitSpawner = GetComponent<UnitSpawner>();
        cameraManager = GetComponent<CameraManager>();

        cameraManager.Initialize(signalHub);
        environmentManager.Initialize();
        unitSpawner.Initialize(inputManager, environmentManager);

        unitSystem.Initialize(signalHub, unitSpawner);

        SetupGamePlayScene();
    }

    public void SetupGamePlayScene()
    {
        if (unitSpawner != null)
        {
            unitSpawner.SpawnCharacter();
        }
    }

    public void StartGameplayScene()
    {
    }

    public void Release()
    {
        unitSystem.Release();
        cameraManager.Release();
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
