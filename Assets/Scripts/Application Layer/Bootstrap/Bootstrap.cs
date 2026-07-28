using DG.Tweening;
using GameAnalyticsSDK;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootStrap : MonoBehaviour, IBootStrapProvider
{
    // 필드 선언 (내부 의존성)
    [SerializeField] private bool isTempScene = false;

    [Header("SDK Toggles (개발 중에는 꺼두는 걸 권장)")]
    [SerializeField] private bool enableSentry = true;
    [SerializeField] private bool enableGameAnalytics = true;

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

        // Sentry는 SentryOptions.asset 기반으로 이 Awake보다도 먼저 자동 초기화되므로,
        // 여기서 막을 수 있는 건 "시작 자체"가 아니라 "초기화 직후 바로 종료"뿐이다.
        // 아직 실제 게임플레이가 시작되기 전이라 의미 있는 데이터가 새어나가지는 않는다.
        if (!enableSentry)
        {
            SentrySdk.Close();
        }

        if (enableGameAnalytics)
        {
            GameAnalytics.Initialize();
        }

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

                // MainMenu → Town 최초 진입 시에만 발생 (Town↔Dungeon 왕복 시엔 gameInstaller가 재사용되어 여기로 오지 않음)
                gameInstaller.TownIntroCurtainRollbackEvent -= OnTownIntroCurtainRollback;
                gameInstaller.TownIntroCurtainRollbackEvent += OnTownIntroCurtainRollback;

                gameInstaller.GoToMainMenuCurtainRevealEvent -= OnGoToMainMenuCurtainReveal;
                gameInstaller.GoToMainMenuCurtainRevealEvent += OnGoToMainMenuCurtainReveal;

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
            // 최초 1회(앱 부팅)만 생성한다. 이후로는 절대 파괴하지 않고 같은 인스턴스를 계속 재사용한다.
            mainMenuInstaller = Instantiate(mainMenuInstallerPrefab);
            mainMenuInstaller.Initialize(this, inputManager, localizationManager, saveManager);
            mainMenuInstaller.StartMainMenuScene();
        }
        else
        {
            // Town/Dungeon에서 돌아온 경우: 씬 전환이 실제로 완료된 이 시점에야 딤머/로고/버튼을 다시 보여준다.
            // StartGoToMainMenu()에서 걸어둔 PauseMove(true)/PauseESCKey(true)를 여기서 풀어준다(캐릭터가 없는 씬이라 위험은 없지만 위생 차원).
            inputManager.PauseMove(false);
            inputManager.PauseESCKey(false);
            mainMenuInstaller.PlayButtonsRevealAnimation();
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

        // 기존 인스톨러 해제 (mainMenuInstaller는 절대 파괴하지 않으므로 여기서 다루지 않는다)
        if (_sceneType == SceneType.MainMenu)
        {
            if (gameInstaller != null)
            {
                gameInstaller.TownIntroCurtainRollbackEvent -= OnTownIntroCurtainRollback;
                gameInstaller.GoToMainMenuCurtainRevealEvent -= OnGoToMainMenuCurtainReveal;
                gameInstaller.Release();
                gameInstaller = null;
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
        if (enableSentry)
        {
            SentryUserContextTagger.TagCurrentUser();
        }

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

    private void OnTownIntroCurtainRollback()
    {
        if (mainMenuInstaller != null)
        {
            // 파괴하지 않는다 - 화면 밖(위)에 그대로 둔 채 다음 ESC 복귀 때 같은 인스턴스를 재사용한다.
            mainMenuInstaller.PlayExitAnimation(null);
        }
    }

    private void OnGoToMainMenuCurtainReveal()
    {
        if (mainMenuInstaller != null)
        {
            mainMenuInstaller.PlayEnterAnimation(null);
        }
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
            // 이어하기 버튼 상태(HasSaveData)를 UI가 확인하기 전에 클라우드 세이브를 로컬로 먼저 반영한다.
            saveManager?.SyncCloudSaveIfNewer();
            SetupMainMenuScene();
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
