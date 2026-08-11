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

    [Header("SDK Toggles")]
    [SerializeField] private bool enableSentry = true;
    [SerializeField] private bool enableGameAnalytics = true;

    [Header("Tutorial")]
    [Tooltip("켜면 '새 게임'이 MainMenu → Dungeon 튜토리얼로 직행한다. 끄면 튜토리얼을 거치지 않고 " +
        "바로 Town으로 진입한다(튜토리얼 도입 이전의 기존 새 게임 경로).")]
    [SerializeField] private bool enableTutorial = true;

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

    // 씬 전환이 진행 중인 동안 추가 요청을 막는다. 카메라 상승 완료 이벤트(AscendOutEndEvent)는
    // TownProductionManager와 InDungeonProductionManager 양쪽에 동시에 전달되고 둘 다 씬 전환을
    // 요청할 수 있어서, 같은 프레임에 요청이 두 번 들어올 수 있다. 지금까지는 첫 요청이 동기적으로
    // gameInstaller를 해제하며 두 번째 요청의 경로를 끊어놓는 덕에 우연히 문제가 없었지만,
    // 해제가 도중에 실패하면 그 보호가 사라져 LoadSceneAsync가 두 번 걸린다.
    private bool bIsSceneTransitioning = false;

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

            // MainMenu → Dungeon 직행: gameInstaller가 아직 없으면 최초 생성
            if (gameInstaller == null)
            {
                gameInstaller = Instantiate(gameInstallerPrefab);
                gameInstaller.Initialize(this, inputManager, localizationManager, saveManager);

                gameInstaller.TownIntroCurtainRollbackEvent -= OnTownIntroCurtainRollback;
                gameInstaller.TownIntroCurtainRollbackEvent += OnTownIntroCurtainRollback;

                gameInstaller.GoToMainMenuCurtainRevealEvent -= OnGoToMainMenuCurtainReveal;
                gameInstaller.GoToMainMenuCurtainRevealEvent += OnGoToMainMenuCurtainReveal;
            }
        }

        if (gameInstaller != null)
        {
            gameInstaller.SetupGameInstaller(new SceneChangeData(currentSceneType, prevSceneType, currentForestType, currentMapType));
        }
    }

    public void SetupMainMenuScene()
    {
        // 간헐적으로 메인 메뉴가 뜨지 않는 문제를 추적하기 위한 도달 기록.
        // GoToMainMenuScene() 로그는 찍혔는데 이 로그가 없으면 씬 전환 자체가 실패한 것이고,
        // 둘 다 찍혔는데 화면이 비어 있으면 메인 메뉴 UI 쪽 문제다.
        Debug.Log($"[BootStrap] SetupMainMenuScene 진입 (timeScale={Time.timeScale}, " +
            $"installer={(mainMenuInstaller != null ? "재사용" : "신규 생성")})");

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
            // Town/Dungeon에서 돌아온 경우: UIView 계층(CursorBox 포함)을 올바른 Canvas에
            // 재배치한 뒤, 씬 전환이 실제로 완료된 이 시점에야 딤머/로고/버튼을 다시 보여준다.
            // StartGoToMainMenu()에서 걸어둔 PauseMove(true)/PauseESCKey(true)를 여기서 풀어준다(캐릭터가 없는 씬이라 위험은 없지만 위생 차원).
            inputManager.PauseMove(false);
            inputManager.PauseESCKey(false);
            mainMenuInstaller.MainMenuReturned();
            mainMenuInstaller.PlayButtonsRevealAnimation();
        }

        Sound.PlayBGM(SoundID.MainBGM);
    }

    public void GoToMainMenuScene()
    {
        if (isTempScene)
        {
            return;
        }

        Debug.Log($"[BootStrap] GoToMainMenuScene 요청 수신 - 현재 씬={currentSceneType}, 씬 전환을 시작합니다.");

        TryBeginTransition(SceneType.MainMenu);
    }

    public void GoToTownScene(bool _bNewGame)
    {
        if (_bNewGame == false && saveManager != null && saveManager.HasSaveData() == false)
        {
            Debug.LogError("[BootStrap] No Save Data found! Cannot load game.");
            // TODO: UI 시스템을 통해 사용자에게 에러 팝업을 보여주는 로직을 여기에 추가할 수 있습니다.
            return;
        }

        // 중복 요청이면 bNewGame까지 건드리지 않도록 시작 여부를 먼저 확인한다.
        if (IsTransitionBlocked(SceneType.Town)) return;

        bNewGame = _bNewGame;
        TryBeginTransition(SceneType.Town);
    }

    public void GoToDungeonFromMainMenu()
    {
        if (enableTutorial == false)
        {
            // 튜토리얼 비활성화: 던전 직행 대신 기존 새 게임 경로(MainMenu → Town)로 보낸다.
            // InDungeonSystem/TutorialSystem/UnitSystem의 튜토리얼 로직은 전부 MainMenu → Dungeon
            // 진입(bIsFromMainMenu)에서만 트리거되므로, 이 경로로는 전혀 발동하지 않는다.
            GoToTownScene(true);
            return;
        }

        if (IsTransitionBlocked(SceneType.DungeonScene)) return;

        bNewGame = true;
        currentMapType = MapType.WideGreenForest;
        currentForestType = ForestType.WideGreenForest_1;
        TryBeginTransition(SceneType.DungeonScene);
    }

    /// <summary>
    /// 이미 씬 전환이 진행 중이라 이번 요청을 무시해야 하는지 확인한다.
    /// 요청 상태(bNewGame/currentMapType 등)를 바꾸기 전에 먼저 호출해, 무시된 요청이
    /// 진행 중인 전환의 목적지를 오염시키지 않도록 한다.
    /// </summary>
    private bool IsTransitionBlocked(SceneType _sceneType)
    {
        if (bIsSceneTransitioning == false) return false;

        Debug.LogWarning($"[BootStrap] 씬 전환 요청({_sceneType})이 중복으로 들어왔습니다. " +
            $"이미 전환이 진행 중이므로 무시합니다.");
        return true;
    }

    private void TryBeginTransition(SceneType _sceneType)
    {
        if (IsTransitionBlocked(_sceneType)) return;

        bIsSceneTransitioning = true;
        StartCoroutine(TransitionToScene(_sceneType));
    }

    private System.Collections.IEnumerator TransitionToScene(SceneType _sceneType)
    {
        // 각 구간의 실제 소요 시간을 남긴다. "멈췄다"는 제보가 실제로는 "매우 느리다"인 경우를
        // 로그만으로 구분하기 위해서다(먹통 상태에서 CPU를 확인해달라고 부탁하지 않아도 된다).
        System.Diagnostics.Stopwatch _swTotal = System.Diagnostics.Stopwatch.StartNew();

        try
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

                    // 해제가 실패하더라도 아래 씬 로드는 반드시 실행되어야 한다.
                    // 이 코루틴의 첫 구간은 카메라 상승 연출의 DOTween 완료 콜백 안에서 동기 실행되는데,
                    // DOTween이 세이프 모드(useSafeMode)로 콜백 예외를 삼켜버리기 때문에, 예전엔 여기서
                    // 예외가 나면 ChangeSceneAsync에 도달하지 못한 채 원인도 남지 않고 조용히 멈췄다.
                    // (연출은 정상적으로 끝났는데 메인 메뉴가 영영 뜨지 않는 증상)
                    // 멈춤 지점 추적용 흔적. 아래 "해제 완료"가 안 찍히면 Release() 안에서 멈춘 것이다.
                    Debug.Log("[BootStrap] gameInstaller.Release() 시작");
                    System.Diagnostics.Stopwatch _swRelease = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        gameInstaller.Release();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("[BootStrap] gameInstaller.Release()에서 예외가 발생했습니다. " +
                            "메인 메뉴로는 계속 진행합니다.");
                        Debug.LogException(e);
                    }

                    Debug.Log($"[BootStrap] gameInstaller.Release() 완료 ({_swRelease.ElapsedMilliseconds}ms)");

                    gameInstaller = null;

                    // Release()의 Destroy(gameObject)는 프레임 끝에 처리된다. 그 대량 파괴와 아래
                    // ChangeSceneAsync의 비동기 씬 로드를 같은 프레임에 걸치게 하면 엔진 내부에서
                    // 락이 물려 메인 스레드가 영구 정지한다(덤프 확인: Unity Main Thread /
                    // BatchDeleteObjects / Loading.PreloadManager 세 스레드가 모두 같은 락 대기).
                    // 한 프레임 양보해 파괴를 완전히 끝낸 뒤에 씬 로드를 시작한다.
                    // Town↔Dungeon 전환은 gameInstaller를 파괴하지 않아 이 문제가 없었다.
                    yield return null;

                    Debug.Log($"[BootStrap] 지연 파괴 완료 대기 후 씬 로드 진행 ({_swTotal.ElapsedMilliseconds}ms)");
                }
            }

            // 2. 비동기 씬 로드
            Debug.Log($"[BootStrap] ChangeSceneAsync({_sceneType}) 호출 (요청 후 {_swTotal.ElapsedMilliseconds}ms)");
            AsyncOperation asyncLoad = sceneManager.ChangeSceneAsync(_sceneType);

            // 여기까지가 카메라 연출의 DOTween 콜백 안에서 동기 실행되는 구간이다.
            // 이 다음 로그가 찍혔다는 건 프레임이 정상적으로 끝났다는 뜻이고, 곧 프레임 끝에 밀려 있던
            // GameInstaller 계층의 지연 파괴(Destroy)까지 통과했다는 의미다.
            // 반대로 이 로그가 없으면 멈춤은 "프레임 종료 처리" 안에 있다.
            yield return null;
            Debug.Log($"[BootStrap] 첫 프레임 통과 - 지연 파괴 처리 완료 ({_swTotal.ElapsedMilliseconds}ms)");

            if (asyncLoad != null)
            {
                // 로드가 느리게라도 진행 중인지, 특정 지점에서 멈춰 있는지 구분하기 위해 1초마다 진행률을 남긴다.
                float _nextReportSec = 1f;
                while (!asyncLoad.isDone)
                {
                    if (_swTotal.Elapsed.TotalSeconds >= _nextReportSec)
                    {
                        Debug.Log($"[BootStrap] 씬 로드 대기 중... progress={asyncLoad.progress:F2} " +
                            $"({_swTotal.ElapsedMilliseconds}ms 경과)");
                        _nextReportSec += 1f;
                    }

                    yield return null;
                }
            }
            Debug.Log($"[BootStrap] 씬 로드 완료({_sceneType}) - 전환 시작부터 {_swTotal.ElapsedMilliseconds}ms");

            // 3. 시스템 초기화 대기 (OnSceneLoaded 실행을 위해 1프레임 + 여유 시간)
            yield return null;
            yield return new WaitForSeconds(0.2f);
        }
        finally
        {
            // 어떤 경로로 끝나든(정상 종료/코루틴 중단) 다음 전환이 막히지 않도록 반드시 해제한다.
            bIsSceneTransitioning = false;
        }
    }

    public void GoToOtherScene(MapType _mapType, ForestType _forestType)
    {
        SceneType _targetScene = (MapType.Town == _mapType) ? SceneType.Town : SceneType.DungeonScene;

        // 중복 요청이면 currentMapType/currentForestType까지 건드리지 않도록 먼저 확인한다.
        if (IsTransitionBlocked(_targetScene)) return;

        currentMapType = _mapType;
        currentForestType = _forestType;

        TryBeginTransition(_targetScene);
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
            // SyncCloudSaveIfNewer()는 Steam Remote Storage를 동기로 호출하므로, 멈춤 지점을 가리기 위해
            // 앞뒤로 흔적을 남긴다. "시작"만 찍히고 "완료"가 없으면 여기서 블로킹된 것이다.
            Debug.Log("[BootStrap] SyncCloudSaveIfNewer 시작");
            saveManager?.SyncCloudSaveIfNewer();
            Debug.Log("[BootStrap] SyncCloudSaveIfNewer 완료");

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

        // 세이프 모드는 트윈 콜백 안에서 발생한 예외를 삼키고 로그만 남긴다. 기본값(Warning)으로 두면
        // Sentry가 에러만 수집하도록 설정돼 있어(CaptureLogErrorEvents) 그 예외가 영영 보고되지 않는다.
        // 씬 전환 로직 상당 부분이 카메라 연출의 트윈 완료 콜백 안에서 동기 실행되므로, 여기서 삼켜진
        // 예외는 그대로 "연출은 끝났는데 아무 일도 일어나지 않는" 증상이 된다. 반드시 에러로 올린다.
        DOTween.safeModeLogBehaviour = DG.Tweening.Core.Enums.SafeModeLogBehaviour.Error;
    }

    private void BootTempScene()
    {
        // 임시 부팅 로직
    }
}
