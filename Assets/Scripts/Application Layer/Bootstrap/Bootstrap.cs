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

    private bool bNewGame = false;
    private MapType currentMapType = MapType.Town;
    private ForestType currentForestType = ForestType.InTown;

    // 게임 플레이 도중 ESC 메뉴를 통해 메인 메뉴로 돌아온 경우 true (앱을 처음 켜서 메인 메뉴로 진입한 경우는 false)
    public bool CameFromEscMenu { get; private set; } = false;

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

        localizationManager = GetComponentInChildren<LocalizationManager>();

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
        // 앱을 처음 켰을 때는 씬 전환(TransitionToScene) 없이 바로 로드되므로 prevSceneType이 None으로 유지됨
        CameFromEscMenu = prevSceneType != SceneType.None;

        currentSceneType = SceneType.MainMenu;

        if (mainMenuInstaller == null)
        {
            mainMenuInstaller = Instantiate(mainMenuInstallerPrefab);
            mainMenuInstaller.Initialize(this, inputManager, localizationManager, saveManager);
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

    private System.Collections.IEnumerator TransitionToScene(SceneType _sceneType)
    {
        // 1. 전환 로직 시작
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

        // 2. 비동기 씬 로드
        AsyncOperation asyncLoad = sceneManager.ChangeSceneAsync(_sceneType);
        if (asyncLoad != null)
        {
            while (!asyncLoad.isDone) yield return null;
        }

        // 3. 시스템 초기화 대기 (OnSceneLoaded 실행을 위해 1프레임 + 여유 시간)
        yield return null;
        yield return new WaitForSeconds(0.2f);
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
