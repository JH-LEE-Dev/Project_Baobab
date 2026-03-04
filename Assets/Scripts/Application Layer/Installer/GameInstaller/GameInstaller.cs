using UnityEngine;

public class GameInstaller : MonoBehaviour
{
    //외부 의존성
    private InputManager inputManager;
    private IBootStrapProvider bootStrapProvider;
    private EnvironmentManager environmentManager;


    //내부 의존성
   
    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager)
    {
        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

        environmentManager = GetComponentInChildren<EnvironmentManager>();
        environmentManager.Initialize();

        SetupGamePlayScene();
    }

    public void SetupGamePlayScene()
    {
    }

    public void StartGameplayScene()
    {
    }

    public void Release()
    {
      
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
