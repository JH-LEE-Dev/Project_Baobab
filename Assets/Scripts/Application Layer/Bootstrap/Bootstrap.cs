using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootStrap : MonoBehaviour, IBootStrapProvider
{
    [SerializeField] bool bTempScene = false;

    private static BootStrap Instance;

    private SceneManager sceneManager;
    private AudioManager audioManager;
    private InputManager inputManager;

    [Header("MainMenu Level Object")]

    [Header("Gameplay Level Object")]
    [SerializeField] GameInstaller gameInstaller_Prefab;
    [SerializeField] MainMenuInstaller mainMenuInstaller_Prefab;

    private GameInstaller gameInstaller;
    private MainMenuInstaller mainMenuInstaller;
    //private LocalizationManager localizationManager;

    //private const string koreanLocalizationFileName = "CardDescription_Korean";

    private void BootTempScene()
    {
        //SetupScene();
        //StartScene();
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        audioManager = GetComponent<AudioManager>();
        sceneManager = GetComponent<SceneManager>();
        inputManager = GetComponent<InputManager>();
        //localizationManager = new LocalizationManager();

        inputManager.Initialize();

        BindEvent();

        InitializeDoTweenPool();
    }

    private void InitializeDoTweenPool()
    {
        DOTween.Init();
        DOTween.SetTweensCapacity(1250, 312);
    }

    public void Start()
    {
        if (bTempScene)
            BootTempScene();

        //Sound.PlayBGM("BGM_MainMenu");
        //localizationManager.LoadLanguage(koreanLocalizationFileName);
    }

    public void OnDestroy()
    {
        ReleaseEvent();
    }

    private void BindEvent()
    {
        inputManager.inputReader.ESCButtonPressedEvent -= GoToMainMenuScene;
        inputManager.inputReader.ESCButtonPressedEvent += GoToMainMenuScene;
    }

    private void ReleaseEvent()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (inputManager != null)
            inputManager.inputReader.ESCButtonPressedEvent -= GoToMainMenuScene;
    }

    public void SetupScene(string _sceneName)
    {
        if (_sceneName == "TownScene")
        {
            gameInstaller = Instantiate(gameInstaller_Prefab);
            gameInstaller.Initialize(this, inputManager);
        }

        if (_sceneName == "ForestScene")
        {
            if (gameInstaller == null)
                return;

            gameInstaller.SetupScene(SceneType.Forest);
        }
    }

    private void StartMainMenuScene()
    {
        mainMenuInstaller.StartMainMenuScene();
    }

    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (sceneName != "MainMenuScene")
        {
            SetupScene(sceneName);
        }
        else
        {
            SetupMainMenuScene();
            StartMainMenuScene();
        }
    }

    public void SetupMainMenuScene()
    {
        mainMenuInstaller = Instantiate(mainMenuInstaller_Prefab);
        mainMenuInstaller.Initialize(this, inputManager);
    }

    public void GoToMainMenuScene()
    {
        if (bTempScene)
            return;

        gameInstaller.Release();
        sceneManager.ChangeScene(SceneType.MainMenu);
    }

    public void GoToTownScene()
    {
        mainMenuInstaller.Release();
        sceneManager.ChangeScene(SceneType.Town);
    }

    public void GoToOtherScene(string _sceneName)
    {
        if (_sceneName == "TownScene")
        {
            sceneManager.ChangeScene(SceneType.Town);
        }

        if (_sceneName == "ForestScene")
        { 
            sceneManager.ChangeScene(SceneType.Forest);
        }
    }
}
