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
    private LocalizationManager localizationManager;

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

    private bool bFadeComplete = false;

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

        localizationManager = new LocalizationManager();

        if (localizationManager != null)
            localizationManager.Initialize();

        if (inputManager != null)
        {
            inputManager.Initialize();
        }

        BindEvent();
        InitializeDoTweenPool();
    }


    // 퍼블릭 초기화 및 제어 메서드
    public void SetupScene(string _sceneName)
    {
        if (_sceneName == townSceneName)
        {
            currentSceneType = SceneType.Town;

            if (gameInstaller == null)
            {
                gameInstaller = Instantiate(gameInstallerPrefab);
                gameInstaller.Initialize(this, inputManager, localizationManager);
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
            mainMenuInstaller.Initialize(this, inputManager, localizationManager);
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
        StartCoroutine(TransitionToScene(SceneType.Town));
    }

    private void OnFadeComplete()
    {
        bFadeComplete = true;
    }

    private System.Collections.IEnumerator TransitionToScene(SceneType _sceneType)
    {
        // 1. 로딩창 나타나기 시작 (화면 가리기)
        bFadeComplete = false;
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.FadeOut(OnFadeComplete);
            // 로딩창이 완전히 가려질 때까지 대기
            while (!bFadeComplete) yield return null;
        }

        // 2. 이제 화면이 완전히 가려졌으므로 전환 로직 시작
        prevSceneType = currentSceneType;

        if (_sceneType == SceneType.Town && mainMenuInstaller != null)
        {
            mainMenuInstaller.Release();
            mainMenuInstaller = null;
        }

        // 3. 비동기 씬 로드
        AsyncOperation asyncLoad = sceneManager.ChangeSceneAsync(_sceneType);
        if (asyncLoad != null)
        {
            while (!asyncLoad.isDone) yield return null;
        }

        // 4. 시스템 초기화 대기 (OnSceneLoaded 실행을 위해 1프레임 + 여유 시간)
        yield return null; 
        yield return new WaitForSeconds(0.2f); 

        // 5. 모든 준비가 되면 로딩창 걷어내기 (화면 밝게)
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.FadeIn();
        }
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
