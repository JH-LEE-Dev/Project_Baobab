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
    private SaveManager saveManager;

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
    private bool bNewGame = false;
    private MapType currentMapType = MapType.Town;
    private ForestType currentForestType = ForestType.None;

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
        saveManager = GetComponent<SaveManager>();

        localizationManager = new LocalizationManager();

        if (localizationManager != null)
        {
            localizationManager.Initialize();
            LoadLocalizationData();
        }

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
                gameInstaller.Initialize(this, inputManager, localizationManager, saveManager);

                if (bNewGame == false)
                    gameInstaller.LoadGame();
            }
        }
        else if (_sceneName == dungeonSceneName)
        {
            currentSceneType = SceneType.DungeonScene;
        }

        if (gameInstaller != null)
        {
            gameInstaller.SetupGameInstaller(new SceneChangeData(currentSceneType, prevSceneType, currentForestType, currentMapType));
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
        if (isTempScene)
        {
            return;
        }

        StartCoroutine(TransitionToScene(SceneType.MainMenu));
    }

    public void GoToTownScene(bool _bNewGame)
    {
        if (_bNewGame == false && saveManager != null && saveManager.HasSaveData() == false)
        {
            Debug.LogError("[BootStrap] No Save Data found! Cannot load game.");
            // TODO: UI 시스템을 통해 사용자에게 에러 팝업을 보여주는 로직을 여기에 추가할 수 있습니다.
            return;
        }

        bNewGame = _bNewGame;
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

        // 기존 인스톨러 해제
        if (_sceneType == SceneType.MainMenu)
        {
            if (gameInstaller != null)
            {
                gameInstaller.Release();
                gameInstaller = null;
            }
        }
        else // Town, DungeonScene 등 게임플레이 관련 씬으로 이동할 때
        {
            if (mainMenuInstaller != null)
            {
                mainMenuInstaller.Release();
                mainMenuInstaller = null;
            }
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

    public void GoToOtherScene(MapType _mapType, ForestType _forestType)
    {
        currentMapType = _mapType;
        currentForestType = _forestType;

        if (MapType.Town == _mapType)
        {
            StartCoroutine(TransitionToScene(SceneType.Town));
        }
        else
        {
            StartCoroutine(TransitionToScene(SceneType.DungeonScene));
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

    }

    private void ReleaseEvent()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
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

    private void LoadLocalizationData()
    {
        TextAsset[] localizationAssets = Resources.LoadAll<TextAsset>("Localization");

        if (localizationAssets == null || localizationAssets.Length == 0)
        {
            Debug.LogWarning("[BootStrap] No localization files found in Resources/Localization");
            return;
        }

        for (int i = 0; i < localizationAssets.Length; i++)
        {
            if (localizationAssets[i] != null)
            {
                localizationManager.LoadLocalizationJson(localizationAssets[i].text);
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
