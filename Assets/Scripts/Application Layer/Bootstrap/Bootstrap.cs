using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootStrap : MonoBehaviour, IBootStrapProvider
{
    // 필드 선언 (내부 의존성)
    [SerializeField] private bool isTempScene = false;

    private static BootStrap instance;
    private SceneManager sceneManager;
    private InputManager inputManager;

    [Header("Gameplay Level Object")]
    [SerializeField] private GameInstaller gameInstallerPrefab;
    [SerializeField] private MainMenuInstaller mainMenuInstallerPrefab;

    private GameInstaller gameInstaller;
    private MainMenuInstaller mainMenuInstaller;

    // 캐싱된 씬 이름 (문자열 비교 최적화 및 GC 할당 최소화)
    private static readonly string mainMenuSceneName = "MainMenuScene";
    private static readonly string townSceneName = "TownScene";
    private static readonly string dungeonSceneName = "DungeonScene";

    private SceneType currentSceneType = SceneType.None;
    private SceneType prevSceneType = SceneType.None;



    // 퍼블릭 초기화 및 제어 메서드
    public void SetupScene(string _sceneName)
    {
        if (_sceneName == townSceneName)
        {
            currentSceneType = SceneType.Town;

            if (gameInstaller == null)
            {
                gameInstaller = Instantiate(gameInstallerPrefab);
                gameInstaller.Initialize(this, inputManager);
            }
        }
        else if (_sceneName == dungeonSceneName)
        {
            currentSceneType = SceneType.DungeonScene;
        }

        if (gameInstaller != null)
        {
            gameInstaller.SetupGameInstaller(new SceneChangeData(currentSceneType, prevSceneType));
        }
    }

    public void SetupMainMenuScene()
    {
        currentSceneType = SceneType.MainMenu;

        if (mainMenuInstaller == null)
        {
            mainMenuInstaller = Instantiate(mainMenuInstallerPrefab);
            mainMenuInstaller.Initialize(this, inputManager);
        }
    }

    public void GoToMainMenuScene()
    {
        prevSceneType = currentSceneType;

        if (isTempScene)
        {
            return;
        }

        if (gameInstaller != null)
        {
            gameInstaller.Release();
            gameInstaller = null; // 참조 해제하여 GC 대상 포함
        }

        sceneManager.ChangeScene(SceneType.MainMenu);
    }

    public void GoToTownScene()
    {
        prevSceneType = currentSceneType;

        if (mainMenuInstaller != null)
        {
            mainMenuInstaller.Release();
            mainMenuInstaller = null;
        }

        sceneManager.ChangeScene(SceneType.Town);
    }

    public void GoToOtherScene(string _sceneName)
    {
        prevSceneType = currentSceneType;

        if (_sceneName == townSceneName)
        {
            sceneManager.ChangeScene(SceneType.Town);
        }
        else if (_sceneName == dungeonSceneName)
        {
            sceneManager.ChangeScene(SceneType.DungeonScene);
        }
    }

    // 유니티 이벤트 함수
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 이벤트 중복 등록 방지
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        sceneManager = GetComponent<SceneManager>();
        inputManager = GetComponent<InputManager>();

        if (inputManager != null)
        {
            inputManager.Initialize();
        }

        BindEvent();
        InitializeDoTweenPool();
    }

    private void Start()
    {
        if (isTempScene)
        {
            BootTempScene();
        }
    }

    private void OnDestroy()
    {
        ReleaseEvent();
    }

    // 내부 로직
    private void BindEvent()
    {
        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.ESCButtonPressedEvent -= GoToMainMenuScene;
            inputManager.inputReader.ESCButtonPressedEvent += GoToMainMenuScene;
        }
    }

    private void ReleaseEvent()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.ESCButtonPressedEvent -= GoToMainMenuScene;
        }
    }

    private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        // 최적화: SceneManager API 호출 대신 이벤트 인자 활용
        string loadedSceneName = _scene.name;

        if (loadedSceneName != mainMenuSceneName)
        {
            SetupScene(loadedSceneName);
        }
        else
        {
            SetupMainMenuScene();
            if (mainMenuInstaller != null)
            {
                mainMenuInstaller.StartMainMenuScene();
            }
        }
    }

    private void InitializeDoTweenPool()
    {
        DOTween.Init();
        DOTween.SetTweensCapacity(1250, 312);
    }

    private void BootTempScene()
    {
        // 임시 부팅 로직
    }
}
