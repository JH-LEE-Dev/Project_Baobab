using System.Collections;
using UnityEngine;
using System;

public class TownSystem : MonoBehaviour
{
    public event Action ActivatePortalEvent;
    public event Action MainMenuCurtainRollbackEvent;
    public event Action GoToMainMenuCurtainRevealEvent;
    //외부 의존성
    private InputManager inputManager;

    //내부 의존성
    [SerializeField] private Transform townStartPoint;
    private SignalHub signalHub;
    public TownObjectManager townObjectManager { get; private set; }
    private IEnvironmentProvider environmentProvider;
    public LogProcessingManager logProcessingManager { get; private set; }
    public TentManager tentManager { get; private set; }
    private Character character;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;
    private TownProductionManager townProductionManager;
    private MapType selectedMapType;
    private ForestType selectedForestType;
    private SkyCameraProductionManager skyCameraProductionManager;
    private TownTileManager townTileManager;
    public TownUnitSpawner townUnitSpawner { get; private set; }
    public LootPillarManager lootPillarManager { get; private set; }

    /// <summary>
    /// 게임이 처음 시작됐을 때(던전에서 돌아온 것이 아닐 때) 캐릭터가 생성되는 위치.
    /// TownUnitSpawner가 운반 NPC를 집 주변에 배치할 때 보조 기준점으로 사용한다.
    /// </summary>
    public Transform TownStartPoint => townStartPoint;

    private bool bCurrentlyTownScene = true;
    private bool bRetryGame = false;
    private bool bGoingToMainMenu = false;
    private bool bTownSystemStarted = false;

    // 메인메뉴 이탈이 요청됐는지를 "씬과 무관하게" 기록한다. bGoingToMainMenu는 마을에 있을 때만
    // 세워지는데(아래 GoToMainMenuRequested의 씬 가드), 정작 마을→던전 입장 연출 코루틴은 던전 씬에서
    // 도는 것이 TownSystem 쪽 사본이라 그 플래그로는 이탈을 감지할 수 없다. 그래서 가드 전용으로
    // 하나 더 둔다. 이탈 요청은 GameplayUICoordinator.GoToMainMenu()가 ESC 창을 즉시 닫고 곧바로
    // 발행하는 취소 불가 지점이므로, 한 번 서면 되돌릴 필요가 없다(이탈이 끝나면 GameInstaller가
    // 통째로 파괴되면서 이 인스턴스도 함께 사라진다).
    private bool bMainMenuExitRequested = false;

    // 이번 던전 하강의 입장 연출을 InDungeonSystem이 맡는지(= 재도전으로 들어가는지). 두 연출 매니저가
    // 같은 SkyProductionRollbackEndEvent를 구독하므로, 재도전에서는 양쪽이 모두 입장 연출을 돌려
    // HUD 상승과 BGM 재생이 두 번씩 일어났다. TownProductionManager.CameraDownIsEnd의 bRetryGame
    // 가드가 이걸 막으라고 있었지만, 아래 DungeonStarted가 그 플래그를 하강이 시작되기도 전에
    // 지워버려 무력했다(DungeonStartSignal 발행 → 여기서 리셋 → 같은 메서드 뒤쪽에서 하강 시작).
    // DungeonStarted에서 매번 무조건 대입하므로 값이 눌러붙지 않는다.
    private bool bRetryArrivalOwnedByDungeon = false;

    // 튜토리얼 "도끼를 강화하세요"(UpgradeAxe)가 완료되기 전까지 마을의 OffroadVehicle 상호작용을 잠가둔다.
    // 튜토리얼 첫 스텝이 시작되면 true가 되고, UpgradeAxe가 완료되면 false로 풀린다.
    private bool bTutorialAxeUpgradePending = false;

    // 튜토리얼 "도끼를 강화하세요"(UpgradeAxe) 안내가 화면에 뜨기 전까지 집(Tent)의 특성 창을 잠가둔다.
    // 차량 잠금(bTutorialAxeUpgradePending)이 UpgradeAxe "완료"에 풀리는 것과 달리 이쪽은 UpgradeAxe
    // "시작"에 풀린다 - 정산금을 받고 안내 UI가 사라지기 전 빈틈에 도끼를 미리 강화해버리면 남은 돈이
    // 재강화 비용에 못 미쳐 그 퀘스트를 완료할 방법이 사라지고, 차량도 잠긴 채라 마을에 갇히기 때문이다.
    private bool bTutorialTentLocked = false;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer, SkyCameraProductionManager _skyCameraProductionManager)
    {
        inputManager = _inputManager;
        signalHub = _signalHub;
        environmentProvider = _environmentProvider;
        characterInventory = _characterInventory;
        offroadContainer = _offroadContainer;

        townObjectManager = GetComponentInChildren<TownObjectManager>();
        logProcessingManager = GetComponentInChildren<LogProcessingManager>();
        tentManager = GetComponentInChildren<TentManager>();
        townProductionManager = GetComponentInChildren<TownProductionManager>();
        townTileManager = GetComponentInChildren<TownTileManager>();
        townUnitSpawner = GetComponentInChildren<TownUnitSpawner>();
        lootPillarManager = GetComponentInChildren<LootPillarManager>();
        skyCameraProductionManager = _skyCameraProductionManager;

        townProductionManager.Initialize(inputManager, _skyCameraProductionManager);
        townObjectManager.Initialize(environmentProvider, inputManager, characterInventory, offroadContainer);
        logProcessingManager.Initialize(inputManager);
        tentManager.Initialize(inputManager);
        townObjectManager.RegisterBuildingShadowCaster(logProcessingManager.shopNPC);
        townObjectManager.RegisterBuildingShadowCaster(tentManager.Tent);
        townTileManager.Initialize();
        townUnitSpawner?.Initialize(environmentProvider);
        lootPillarManager?.Initialize(inputManager);

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        logProcessingManager.Release();
        townObjectManager.Release();
        tentManager.Release();
        townProductionManager.Release();

        ReleaseEvents();
        UnSubscribeSignals();
    }

    public void StartTownSystem(SceneChangeData _sceneChangeData)
    {
        bTownSystemStarted = true;
        townTileManager.CreateGrid();

        // Grid는 매번 새로 생성되므로, 제재소 증설 단계(가공 라인 수)를 여기서 다시 반영해준다.
        // 세이브 로드나 던전 안에서의 증설이 이벤트보다 먼저 끝나 있어도 이 동기화로 항상 맞춰진다.
        ApplyProcessLineCountToGrid(logProcessingManager.ActiveLineCount);

        CollisionSystem.Instance?.ClearAll();
        townObjectManager.ReadyObj();

        // 튜토리얼 "도끼를 강화하세요"가 아직 안 끝났다면, 마을에 도착할 때마다(재입장 포함)
        // ReadyObj()가 기본값(true)으로 되돌려놓은 차량 상호작용을 다시 잠근다.
        if (bTutorialAxeUpgradePending)
            townObjectManager.SetCanTravel(false);
        logProcessingManager.EnableShopObj();
        tentManager.EnableTent();

        // 텐트도 차량과 같은 이유로, 마을에 다시 들어와 새로 켜질 때마다 튜토리얼 잠금을 다시 반영한다.
        tentManager.SetTutorialLock(bTutorialTentLocked);

        if (townProductionManager.offroadVehicleObj == null)
        {
            townProductionManager.Offroad_DI(townObjectManager.offroadVehicle);
        }

        townUnitSpawner?.SpawnNPCsIfNeeded(townTileManager, townObjectManager.offroadVehicle, offroadContainer,
            logProcessingManager.logContainer, tentManager.TentSpawnPoint, townStartPoint);

        // 발소리 등 타일 판정이 던전 전용 TileMapGenerator가 아닌 Town의 실제 타일맵을 보도록 갈아끼운다.
        character?.SetTilemapDataProvider(townUnitSpawner?.TilemapDataProvider);

        if (_sceneChangeData.prevScene == SceneType.DungeonScene)
        {
            signalHub.Publish(new TownStartedSignal(townObjectManager.GetTownReturnPoint()));
        }
        else // MainMenu에서 온 New Game / Load Game
        {
            signalHub.Publish(new TownStartedSignal(townStartPoint));
            signalHub.Publish(new PopupUIDownSignal());

            townProductionManager.SetCharacterTransform();
            townProductionManager.StartMainMenuIntro();
        }

        logProcessingManager.SetMapType(MapType.Town);

        bCurrentlyTownScene = true;
        townProductionManager.bCurrentlyTownScene = true;

        townUnitSpawner?.ResetAllNPCsToSpawn();
    }

    private void BindEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
        townObjectManager.PortalActivatedEvent += PortalActivated;

        logProcessingManager.ContainerUpdatedEvent -= ContainerUpdated;
        logProcessingManager.ContainerUpdatedEvent += ContainerUpdated;

        logProcessingManager.InteractStateChangedEvent -= LogContainerInteractStateChanged;
        logProcessingManager.InteractStateChangedEvent += LogContainerInteractStateChanged;

        logProcessingManager.EarnMoneyEvent -= EarnMoney;
        logProcessingManager.EarnMoneyEvent += EarnMoney;

        tentManager.TentInteractEvent -= TentInteract;
        tentManager.TentInteractEvent += TentInteract;

        logProcessingManager.LogContainerSpecChangedEvent -= logContainerSpecChanged;
        logProcessingManager.LogContainerSpecChangedEvent += logContainerSpecChanged;

        townObjectManager.PortalDeActivatedEvent -= PortalDeActivated;
        townObjectManager.PortalDeActivatedEvent += PortalDeActivated;

        townProductionManager.OffroadDriveEndEvent -= OffroadDriveEnd;
        townProductionManager.OffroadDriveEndEvent += OffroadDriveEnd;

        tentManager.TentInteractStateChangedEvent -= TentInteractStateChanged;
        tentManager.TentInteractStateChangedEvent += TentInteractStateChanged;

        townObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        townObjectManager.OffroadInteractStateChangedEvent += OffroadInteractStateChanged;

        logProcessingManager.ShopInteracteStateChangedEvent -= ShopInteractStateChanged;
        logProcessingManager.ShopInteracteStateChangedEvent += ShopInteractStateChanged;

        logProcessingManager.LogProcessorIsActiveEvent -= LogItemProcessorActiveState;
        logProcessingManager.LogProcessorIsActiveEvent += LogItemProcessorActiveState;

        logProcessingManager.ShopMoneyChangedEvent -= ShopMoneyChanged;
        logProcessingManager.ShopMoneyChangedEvent += ShopMoneyChanged;

        logProcessingManager.ItemAddedToLogContainerEvent -= ItemAddedToLogContainer;
        logProcessingManager.ItemAddedToLogContainerEvent += ItemAddedToLogContainer;

        logProcessingManager.ActiveLineCountChangedEvent -= ApplyProcessLineCountToGrid;
        logProcessingManager.ActiveLineCountChangedEvent += ApplyProcessLineCountToGrid;

        townProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        townProductionManager.CharacterRideEndEvent += CharacterRideEnd;

        townProductionManager.StartSkyProductionEvent -= StartSkyProduction;
        townProductionManager.StartSkyProductionEvent += StartSkyProduction;

        townProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        townProductionManager.RollbackSkyProductionEvent += RollbackSkyProduction;

        townProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        townProductionManager.CameraUpIsEndEvent += CameraUpIsEnd;

        townProductionManager.CameraUpDownEndEvent -= CameraDownIsEnd;
        townProductionManager.CameraUpDownEndEvent += CameraDownIsEnd;

        townProductionManager.PopupUIDownEvent -= PopupUIDown;
        townProductionManager.PopupUIDownEvent += PopupUIDown;

        townProductionManager.MainMenuCurtainRollbackEvent -= MainMenuCurtainRollback;
        townProductionManager.MainMenuCurtainRollbackEvent += MainMenuCurtainRollback;

        townProductionManager.MainMenuIntroEndEvent -= MainMenuIntroEnd;
        townProductionManager.MainMenuIntroEndEvent += MainMenuIntroEnd;

        townProductionManager.GoToMainMenuReadyEvent -= GoToMainMenuReady;
        townProductionManager.GoToMainMenuReadyEvent += GoToMainMenuReady;

        townProductionManager.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        townProductionManager.GoToMainMenuCurtainRevealEvent += GoToMainMenuCurtainReveal;

        if (lootPillarManager != null)
        {
            lootPillarManager.LootPillarInteractStateChangedEvent -= LootPillarInteractStateChanged;
            lootPillarManager.LootPillarInteractStateChangedEvent += LootPillarInteractStateChanged;

            lootPillarManager.LootPillarInteractEvent -= LootPillarInteract;
            lootPillarManager.LootPillarInteractEvent += LootPillarInteract;
        }
    }

    private void ReleaseEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
        logProcessingManager.ContainerUpdatedEvent -= ContainerUpdated;
        logProcessingManager.InteractStateChangedEvent -= LogContainerInteractStateChanged;
        logProcessingManager.EarnMoneyEvent -= EarnMoney;
        tentManager.TentInteractEvent -= TentInteract;
        logProcessingManager.LogContainerSpecChangedEvent -= logContainerSpecChanged;
        townObjectManager.PortalDeActivatedEvent -= PortalDeActivated;
        townProductionManager.OffroadDriveEndEvent -= OffroadDriveEnd;
        tentManager.TentInteractStateChangedEvent -= TentInteractStateChanged;
        townObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        logProcessingManager.ShopInteracteStateChangedEvent -= ShopInteractStateChanged;
        logProcessingManager.LogProcessorIsActiveEvent -= LogItemProcessorActiveState;
        logProcessingManager.ShopMoneyChangedEvent -= ShopMoneyChanged;
        logProcessingManager.ItemAddedToLogContainerEvent -= ItemAddedToLogContainer;
        logProcessingManager.ActiveLineCountChangedEvent -= ApplyProcessLineCountToGrid;
        townProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        townProductionManager.StartSkyProductionEvent -= StartSkyProduction;
        townProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        townProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        townProductionManager.CameraUpDownEndEvent -= CameraDownIsEnd;
        townProductionManager.PopupUIDownEvent -= PopupUIDown;
        townProductionManager.MainMenuCurtainRollbackEvent -= MainMenuCurtainRollback;
        townProductionManager.MainMenuIntroEndEvent -= MainMenuIntroEnd;
        townProductionManager.GoToMainMenuReadyEvent -= GoToMainMenuReady;
        townProductionManager.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;

        if (lootPillarManager != null)
        {
            lootPillarManager.LootPillarInteractStateChangedEvent -= LootPillarInteractStateChanged;
            lootPillarManager.LootPillarInteractEvent -= LootPillarInteract;
        }
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<TeleportUIClosedSignal>(TeleportUIClosed);
        signalHub.Subscribe<TentUIClosedSignal>(TentUIClosed);
        signalHub.Subscribe<LootPillarUIClosedSignal>(LootPillarUIClosed);
        signalHub.Subscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.Subscribe<TeleportUIClosedWhileTeleportSignal>(TeleportUIClosedWhileTeleport);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.Subscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
        signalHub.Subscribe<CompleteDungeonEntrySignal>(CompleteDungeonEntry);
        signalHub.Subscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.Subscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<TeleportUIClosedSignal>(TeleportUIClosed);
        signalHub.UnSubscribe<TentUIClosedSignal>(TentUIClosed);
        signalHub.UnSubscribe<LootPillarUIClosedSignal>(LootPillarUIClosed);
        signalHub.UnSubscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.UnSubscribe<TeleportUIClosedWhileTeleportSignal>(TeleportUIClosedWhileTeleport);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.UnSubscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
        signalHub.UnSubscribe<CompleteDungeonEntrySignal>(CompleteDungeonEntry);
        signalHub.UnSubscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.UnSubscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
    }

    // 튜토리얼 스텝이 시작되면(던전 쪽 스텝 포함) 마을에 도착했을 때 바로 차량을 타고 나가버리는
    // 일이 없도록 잠금을 예약해둔다. 실제로 마을 차량에 반영되는 시점은 StartTownSystem().
    // 단, 마지막 스텝(StartNewLogging)은 차량 상호작용 자체가 완료 조건이므로 잠그지 않는다.
    private void TutorialStepStarted(TutorialStepStartedSignal _signal)
    {
        if (_signal.step == TutorialStep.StartNewLogging)
        {
            // 이미 UpgradeAxe 완료 시점에 풀렸지만, 순서가 어긋나더라도 확실히 열려 있도록 한 번 더 보장한다.
            UnlockTownVehicleForTutorial();
            UnlockTentForTutorial();
            return;
        }

        bTutorialAxeUpgradePending = true;

        // "도끼를 강화하세요" 안내가 화면에 뜨는 바로 그 시점에 특성 창을 열어준다. 이보다 이르면
        // (정산 퀘스트 완료 ~ 이 안내 등장 사이의 빈틈에) 도끼를 미리 강화해버릴 수 있는데, 그러면
        // 남은 돈이 재강화 비용에 못 미쳐 이 퀘스트를 완료할 방법이 사라진다.
        if (_signal.step == TutorialStep.UpgradeAxe)
        {
            UnlockTentForTutorial();
            return;
        }

        LockTentForTutorial();
    }

    // "도끼를 강화하세요"가 완료되면 그 즉시 잠금을 푼다. 안내 UI가 사라지는 연출이 끝나기를 기다렸다가
    // 풀면, 그 연출이 취소·유실될 경우 차량이 영영 잠긴 채 남아 마을에서 빠져나갈 수 없게 된다.
    // (마지막 스텝 시작 전에 미리 열려 생기는 빈틈은 TutorialSystem 쪽에서 처리한다)
    private void TutorialStepCompleted(TutorialStepCompletedSignal _signal)
    {
        if (_signal.step != TutorialStep.UpgradeAxe)
            return;

        UnlockTownVehicleForTutorial();
    }

    private void UnlockTownVehicleForTutorial()
    {
        bTutorialAxeUpgradePending = false;
        townObjectManager.SetCanTravel(true);
    }

    private void LockTentForTutorial()
    {
        bTutorialTentLocked = true;
        tentManager.SetTutorialLock(true);
    }

    private void UnlockTentForTutorial()
    {
        bTutorialTentLocked = false;
        tentManager.SetTutorialLock(false);
    }

    private void PortalActivated()
    {
        townProductionManager.StartCharacterRide();

        signalHub.Publish(new TownOffroadVehicleActivatedSignal());

        // 컨테이너/차량 콜라이더 비활성화는 DungeonSelected()에서 NPC 일시정지(PauseAllNPCs)와
        // 같은 시점에 함께 처리한다. 여기서 미리 꺼두면 "탑승~던전 선택" 대기 구간 동안 운반 NPC의
        // IsWithinInteractRadius(col.OverlapPoint 기반)가 항상 false로 판정되어, 감지 반경에서
        // 멈추지 못하고 경로 끝(컨테이너 안쪽)까지 걸어 들어가 버리는 문제가 있었다. 이 시점엔
        // 캐릭터 오브젝트가 비활성화되고 차량도 아직 물리적으로 움직이지 않으므로 미리 꺼둘 필요가 없다.
    }

    private void InventoryInitialized(InventoryInitializedSignal inventoryInitializedSignal)
    {
        logProcessingManager.DI_Inventory(inventoryInitializedSignal.inventory);
    }

    private void ContainerUpdated()
    {
        signalHub.Publish(new ContainerUpdatedSignal());
    }

    private void LogContainerInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new ContainerInteractStateChangedSignal(_boolean));
    }

    private void EarnMoney(int _money)
    {
        signalHub.Publish(new MoneyEarnedSignal(_money));
    }

    private void TentInteract(bool _bInteract)
    {
        signalHub.Publish(new TentInteractSignal(_bInteract));
    }

    // TentUI가 실제로 닫힐 때(E 토글이 아닌 ESC·패드 Cancel로 닫힌 경우 포함) 항상 발행되는 신호.
    // Tent의 내부 상호작용 토글(bInteract)이 UI가 이미 닫혔다는 사실을 놓치지 않도록 여기서 맞춰준다.
    // (E 토글 경로로 닫힌 경우는 이미 bInteract가 false라 이 호출은 아무 효과가 없다)
    private void TentUIClosed(TentUIClosedSignal _tentUIClosedSignal)
    {
        tentManager.SyncInteractStateOnExternalClose();
    }

    // LootPillar UI(UIView_ScreenModal)가 실제로 닫힐 때(상호작용 키 토글이 아닌 ESC·패드 Cancel로
    // 닫힌 경우 포함) 항상 발행되는 신호. 필러의 내부 토글(bInteracting)이 UI가 이미 닫혔다는 사실을
    // 놓치지 않도록 여기서 맞춰준다. (키 토글 경로로 닫힌 경우는 이미 false라 이 호출은 아무 효과가 없다)
    private void LootPillarUIClosed(LootPillarUIClosedSignal _lootPillarUIClosedSignal)
    {
        lootPillarManager?.SyncInteractStateOnExternalClose();
    }

    private void DungeonSelected(DungeonSelectedSignal dungeonSelectedSignal)
    {
        if (bRetryGame == true)
            return;

        // 던전이 실제로 선택되어 확정된 신호 - 이 시점부터는 취소 불가능한 씬 전환 연출이므로 ESC를 막는다.
        // (차량 탑승~던전 선택 팝업 단계는 ESC로 팝업을 취소할 수 있어야 하므로 여기서 막으면 안 된다)
        // 실제 잠금은 이보다 더 앞선, 플레이어가 내비게이션에서 던전을 클릭한 시점(GameplayUICoordinator.
        // DungeonConfirmStarted)에 이미 걸려 있다 - 이 신호는 UI가 닫히는 연출 + 확정 딜레이만큼 늦게
        // 도착하기 때문. 여기서는 안전망 차원에서 같은 상태를 한 번 더 확정해둔다.
        inputManager.PauseESCKey(true); // 종료 시점은 TownSystem.CameraDownIsEnd()

        // 상자에서 인출 중이던 NPC는 이미 날아온 것만 습득하고 그만두게 하고,
        // 상점으로 납품하러 가던 NPC는 곧바로 Idle로 되돌린 뒤, 그대로 멈춰서 다시 새 작업을
        // 찾아 나서지 않게 한다(원래 CameraUpIsEnd에서만 Pause했는데, 그 사이 텀에 Idle이 스스로
        // 새 작업을 찾아 다시 움직여버리는 문제가 있었다).
        townUnitSpawner?.CancelActiveTasksForTeleport();
        townUnitSpawner?.PauseAllNPCs();

        selectedMapType = dungeonSelectedSignal.type;
        selectedForestType = dungeonSelectedSignal.forestType;

        townProductionManager.StartDrive();

        offroadContainer.col.enabled = false;
        if (townObjectManager.offroadVehicle != null)
            townObjectManager.offroadVehicle.col.enabled = false;
    }

    private void logContainerSpecChanged()
    {
        signalHub.Publish(new LogContainerSpecChangedSignal());
    }

    private void CurrentlyInDungeon(DecalreDungeonTypeSignal decalreDungeonTypeSignal)
    {
        logProcessingManager.SetMapType(decalreDungeonTypeSignal.mapType);
    }

    private void CharacterSpawned(CharacterSpawnedSignal _signal)
    {
        character = _signal.character;
        logProcessingManager.SetCharacter(character);
        townProductionManager.Character_DI(character);
        townObjectManager.SetCharacter(character);
    }

    private void TeleportUIClosed(TeleportUIClosedSignal _teleportUIClosedSignal)
    {
        townProductionManager.GetOffFromTheVehicle();

        if (townProductionManager.bCanGetOff == true)
        {
            offroadContainer.col.enabled = true;
            if (townObjectManager.offroadVehicle != null)
                townObjectManager.offroadVehicle.col.enabled = true;
        }

        townObjectManager.TeleportUIClosed();
    }

    private void PortalDeActivated()
    {
        signalHub.Publish(new PortalDeActivatedSignal());
    }

    private void OffroadDriveEnd()
    {
        //townProductionManager.StartSkyProduction();
    }

    private void TentInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new TentInteractStateChangedSignal(_boolean));
    }

    private void OffroadInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new OffroadInteractStateChangedSignal(_boolean));
    }

    private void ShopInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new ShopInteractStateChangedSignal(_boolean));
    }

    private void LootPillarInteractStateChanged(bool _state, LootType _lootType)
    {
        signalHub.Publish(new LootPillarInteractStateChangedSignal(_state, _lootType));
    }

    private void LootPillarInteract(bool _bInteract, LootType _lootType)
    {
        signalHub.Publish(new LootPillarInteractSignal(_bInteract, _lootType));
    }

    private void LogItemProcessorActiveState(bool _boolean)
    {
        signalHub.Publish(new LogItemProcessorActiveStateSignal(_boolean));
    }

    private void ShopMoneyChanged(int _money)
    {
        signalHub.Publish(new ShopMoneyUpdatedSignal(_money));
    }

    private void ItemAddedToLogContainer()
    {
        signalHub.Publish(new ItemAddedToLogContainerSignal());
    }

    /// <summary>
    /// 제재소 가공 라인 수(1~3)를 마을 Grid의 증설분 건물 충돌 타일맵 개수로 변환해 반영한다.
    /// 1라인 = 기본 건물만(추가 0동), 2라인 = BuildingColliderTilemap_1까지, 3라인 = _2까지 활성화.
    /// 던전에 있는 동안(Grid가 파괴된 상태) 증설되어도 TownTileManager가 값을 기억해 다음 CreateGrid에서 적용한다.
    /// </summary>
    private void ApplyProcessLineCountToGrid(int _activeLineCount)
    {
        townTileManager.SetBuildingExpansionCount(_activeLineCount - 1);
    }

    private void DungeonStarted(DungeonStartSignal _dungeonStartSignal)
    {
        // 이번 하강의 입장 연출을 누가 맡는지 여기서 확정한다. 재도전이면 InDungeonSystem이 맡으므로
        // 아래에서 bRetryGame을 지운 뒤에도 CameraDownIsEnd가 그 사실을 알 수 있어야 한다.
        // (매 진입마다 무조건 대입하므로 하강이 생략되는 경로가 있어도 값이 눌러붙지 않는다)
        bRetryArrivalOwnedByDungeon = bRetryGame;

        // Town 전용 타일맵 오버라이드를 풀고 던전의 TileMapGenerator로 되돌린다.
        character?.SetTilemapDataProvider(environmentProvider.tilemapDataProvider);

        logProcessingManager.DisableShopObj();
        tentManager.DisableTent();
        townProductionManager.SetCharacterTransform();

        if (bRetryGame == false)
        {
            // MainMenu → Dungeon: PrepareForDescend가 이미 카메라를 배치했으므로 딜레이 없이 즉시 하강
            bool bNoDelay = !bTownSystemStarted;
            townProductionManager.RollbackCameraMove(bNoDelay);

            // MainMenu → Dungeon: 메인 메뉴 커튼(딤머/오버레이)을 걷어낸다
            if (!bTownSystemStarted)
            {
                MainMenuCurtainRollbackEvent?.Invoke();
            }
        }

        bCurrentlyTownScene = false;
        townProductionManager.bCurrentlyTownScene = false;

        bRetryGame = false;
        townProductionManager.bRetryGame = false;
    }

    private void CharacterRideEnd()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void TeleportUIClosedWhileTeleport(TeleportUIClosedWhileTeleportSignal _teleportUIClosedWhileTeleport)
    {
        townProductionManager.SetbCanGetOff(false);
    }

    private void StartSkyProduction(bool isMainMenu)
    {
        signalHub.Publish(new StartSkyProductionSignal(isMainMenu));
    }

    private void RollbackSkyProduction()
    {
        signalHub.Publish(new RollbackSkyProductionSignal());
    }

    private void CameraUpIsEnd()
    {
        if (bCurrentlyTownScene == false)
            return;

        // 마을 → 숲 출발 시점 자동저장. townObjectManager가 정리(Clear)되기 전, 가장 온전한 상태에서 저장한다.
        signalHub.Publish(new AutoSaveRequestedSignal(AutoSaveReason.DepartToForest));

        townUnitSpawner?.PauseAllNPCs();
        townUnitSpawner?.DeactivateAllNPCs();

        townObjectManager.ClearObjManager();
        townTileManager.DestroyGrid();
        signalHub.Publish(new GoToDungeonSignal(selectedMapType, selectedForestType));
    }

    private void CameraDownIsEnd()
    {
        if (bCurrentlyTownScene == true)
            return;

        // 재도전으로 들어가는 하강은 InDungeonSystem이 입장 연출을 맡는다(InDungeonSystem.CameraDownIsEnd가
        // 자기 bRetryGame을 보고 통과시킨다). 여기서 한 번 더 돌면 HUD 상승과 캐릭터 활성화(→ BGM 재생)가
        // 두 번씩 일어나므로, 이번 하강 몫을 소비하고 물러난다. 조작 잠금 해제는 재도전 경로에서도
        // InDungeonProductionManager.CameraDownIsEnd(PauseMove/PauseESCKey)와 InDungeonSystem.CameraDownIsEnd
        // (EnableCharacterAimSignal)가 이미 담당한다. PauseInventoryKey는 마을 차량 내비(HUD_PopupNav_Main)를
        // 거칠 때만 잠기므로 재도전 경로에는 애초에 잠금이 없다.
        if (bRetryArrivalOwnedByDungeon == true)
        {
            bRetryArrivalOwnedByDungeon = false;
            return;
        }

        // MainMenu → Dungeon 튜토리얼 최초 진입: 조작 해제/캐릭터 활성화(ActivateCharacterSignal)/HUD 복귀는
        // 별도 트리거로 원하는 시점에 실행할 예정이므로 카메라 하강 완료 시점엔 대신 스튜디오 로고 연출만 예약한다.
        // 단, BGM은 다른 경로와 동일하게 카메라 하강이 끝나는 이 시점부터 흐르게 한다.
        // (ActivateCharacterSignal이 없어 BGM 재생 지점도 같이 사라지므로 전용 신호로 분리했다)
        if (!bTownSystemStarted)
        {
            signalHub.Publish(new DungeonBGMStartSignal());
            StartCoroutine(StudioLogoRevealCoroutine());
            return;
        }

        CompleteDungeonEntry();
    }

    /// <summary>
    /// 던전 입장 연출 마무리: 조작 잠금 해제 + HUD 복귀(+ 그 시점의 캐릭터 활성화).
    /// 캐릭터 활성화(ActivateCharacterSignal, AttackIndicator 포함)는 조작 잠금 해제와 동시가 아니라
    /// HUD가 올라오는 PopupUIGoUPCoroutine 시점에 함께 발행한다.
    /// 일반 경로(Town → Dungeon)에서는 카메라 하강 완료 시점에, MainMenu → Dungeon 튜토리얼에서는
    /// 캐릭터가 차량에서 내린 1초 뒤(CompleteDungeonEntrySignal)에 호출된다.
    /// </summary>
    private void CompleteDungeonEntry()
    {
        inputManager.PauseMove(false);
        inputManager.PauseESCKey(false); // 타운→던전 진입 연출 종료 (DungeonSelected()에서 걸어둔 PauseESCKey(true) 해제)
        inputManager.PauseInventoryKey(false); // 타운→던전 진입 연출 종료 (GameplayUICoordinator.DungeonConfirmStarted()에서 걸어둔 PauseInventoryKey(true) 해제)

        // 조준은 여기서 바로 켠다. 아래 PopupUIGoUPCoroutine의 ActivateCharacterSignal은 0.7초 뒤에
        // 발행되는데, 그때까지 조준이 잠겨 있으면 이미 움직일 수 있는 캐릭터가 마우스를 움직여도
        // 이전 방향을 그대로 바라보는 구간이 생긴다(AttackIndicator 노출만 기존대로 HUD와 함께 유지).
        signalHub.Publish(new EnableCharacterAimSignal());

        StartCoroutine(PopupUIGoUPCoroutine());
    }

    private void CompleteDungeonEntry(CompleteDungeonEntrySignal _signal)
    {
        CompleteDungeonEntry();
    }

    private IEnumerator StudioLogoRevealCoroutine()
    {
        yield return new WaitForSeconds(2f);
        signalHub.Publish(new StudioLogoRevealSignal());
    }

    private IEnumerator PopupUIGoUPCoroutine()
    {
        yield return new WaitForSeconds(0.7f);

        // 대기하는 0.7초 사이에 ESC로 메인메뉴 이탈이 요청됐다면, 이미 내려가고 있는 HUD를
        // 다시 올리면 안 되므로 여기서 멈춘다(HUDDown 직후 HUDUp이 뒤따라오던 레이스 컨디션 방지).
        // bGoingToMainMenu가 아니라 bMainMenuExitRequested를 보는 이유는 그 필드 주석 참조 -
        // 이 코루틴은 던전 씬에서도 도는데, 그때 bGoingToMainMenu는 씬 가드에 막혀 서지 않는다.
        if (bMainMenuExitRequested == true)
        {
            // 여기서 빠지면 아래 StaminaDecreaseCoroutine이 시작조차 못 하므로, 거기서 하던
            // 상호작용 키 잠금 해제를 대신 해준다. 이유는 그쪽 가드 주석 참조.
            inputManager.PauseInteractKey(false);
            yield break;
        }

        signalHub.Publish(new PopupUIUpSignal());

        // 이 코루틴은 CompleteDungeonEntry()에서만 시작되고 그 시점 bCurrentlyTownScene은 반드시
        // false이므로, 여기 도달했다는 것은 곧 "던전 입장"이라는 뜻이다. 반대편(던전 → 마을 귀환)은
        // InDungeonSystem.PopupUIGoUPCoroutine이 담당한다 - 예전엔 이 아래에 그 경로용 else 분기가
        // 대칭으로 있었지만 도달할 수 없었고, 심지어 살아 있는 원본과 내용까지 갈라져 있어 지웠다.
        // 조건문 자체는 남겨둔다: ActivateCharacterSignal은 던전 입장 전용(attackComponent 활성화 포함)이라,
        // 혹시 마을에서 이 코루틴이 돌게 되는 경로가 생기더라도 그때 발행되면 안 된다.
        if (bCurrentlyTownScene == false)
        {
            // AttackIndicator(공격 사거리 인디케이터)는 캐릭터가 움직일 수 있게 되는 시점이 아니라
            // HUD가 올라오는 이 시점에 함께 나타나야 자연스러우므로, 캐릭터 활성화를 여기로 옮겼다.
            signalHub.Publish(new ActivateCharacterSignal());

            StartCoroutine(StaminaDecreaseCoroutine());
        }
    }

    private IEnumerator StaminaDecreaseCoroutine()
    {
        yield return new WaitForSeconds(0.7f);

        // 이 코루틴이 시작되는 시점(PopupUIGoUPCoroutine)엔 이미 ESC가 풀려 있으므로, 대기하는
        // 0.7초 사이에 ESC → 메인메뉴 이탈이 요청될 수 있다. 게다가 ESC 메뉴는 Time.timeScale = 0이라
        // 이 대기가 멈춘 채로 있다가 이탈이 확정되는 순간(GoToMainMenu가 timeScale을 되돌린다)
        // 되살아난다. 그대로 두면 카메라 상승 연출 도중에 피로도 감소가 시작되고 컨테이너/차량
        // 콜라이더까지 되살아나 상호작용 팝업이 뜬다. InDungeonSystem 쪽 같은 이름의 코루틴에는
        // 이미 들어 있던 가드인데, 마을→던전 입장이라는 가장 흔한 경로에서 실제로 도는 것은
        // 이쪽 사본이라 여기가 비어 있으면 아무 소용이 없다.
        //
        // 단, 아래 PauseInteractKey(false)만은 빠져나가면서도 반드시 해줘야 한다.
        // PauseInteractKey는 bool이 아니라 카운터라(InputReader.pauseInteractCount), 마을 출발 때
        // TownProductionManager.StartSkyProduction()이 올려둔 +1이 상쇄되지 않으면 0으로 돌아오지
        // 않는다. InputManager는 BootStrap이 들고 있어 게임 실행 내내 살아남고 SetupMainMenuScene()도
        // PauseMove/PauseESCKey만 풀어주므로, 그 불균형은 메인메뉴를 거쳐 다음 세션까지 따라가
        // 상호작용 키가 통째로 죽는다. 잠금만 풀 뿐 콜라이더는 되살리지 않으므로 이탈 연출 도중에
        // 상호작용 팝업이 뜨지도 않는다.
        if (bMainMenuExitRequested == true)
        {
            inputManager.PauseInteractKey(false);
            yield break;
        }

        signalHub.Publish(new StartDecreaseStaminaSignal());

        offroadContainer.col.enabled = true;

        if (townObjectManager.offroadVehicle != null)
            townObjectManager.offroadVehicle.col.enabled = true;

        ActivatePortalEvent?.Invoke();

        inputManager.PauseInteractKey(false);
    }

    private void PopupUIDown()
    {
        signalHub.Publish(new PopupUIDownSignal());
    }

    private void MainMenuCurtainRollback()
    {
        MainMenuCurtainRollbackEvent?.Invoke();
    }

    private void MainMenuIntroEnd()
    {
        // ActivateCharacterSignal은 던전 입장 연출 전용(attackComponent.SetEnable(true) 포함)이라 Town에서는 쓰지 않는다.
        // Town 진입 시 공격 인디케이터를 끄는 처리는 이미 GameInstaller.SetupGameInstaller() → unitSystem.SetWhereIsCharacter(false)가 담당한다.
        inputManager.PauseMove(false);
        inputManager.PauseESCKey(false); // 메인메뉴→타운 인트로 연출 종료 (TownProductionManager.StartMainMenuIntro()에서 걸어둔 PauseESCKey(true) 해제)

        // 캐릭터가 실제로 움직일 수 있게 되는 시점(메인메뉴 -> 타운)에 타운 BGM을 재생한다.
        Sound.PlayBGM(SoundID.TownBGM);

        StartCoroutine(MainMenuIntroPopupUIUpCoroutine());
    }

    private IEnumerator MainMenuIntroPopupUIUpCoroutine()
    {
        yield return new WaitForSeconds(0.7f);

        // 대기하는 0.7초 사이에 ESC로 메인메뉴 이탈이 요청됐다면, 이미 내려가고 있는 HUD를
        // 다시 올리면 안 되므로 여기서 멈춘다(HUDDown 직후 HUDUp이 뒤따라오던 레이스 컨디션 방지).
        // 이쪽은 마을 씬에서만 도는 인트로 전용이라 bGoingToMainMenu로도 충분하지만, 이탈 가드는
        // 한 가지 플래그로만 판단하도록 위 두 코루틴과 통일한다(bMainMenuExitRequested가 상위 집합이다).
        if (bMainMenuExitRequested == true)
            yield break;

        signalHub.Publish(new PopupUIUpSignal());
    }

    private void GoToMainMenuRequested(GoToMainMenuRequestedSignal _signal)
    {
        // 이탈 요청 자체는 씬과 무관하게 먼저 기록한다. 아래 씬 가드보다 반드시 앞이어야 한다 -
        // 마을→던전 입장 연출 코루틴은 던전 씬에서 도는 TownSystem 쪽 사본이라, 그 구간의 이탈은
        // 여기서 씬 가드에 걸려 되돌아간다. 그 코루틴들이 볼 수 있는 유일한 신호가 이 플래그다.
        bMainMenuExitRequested = true;

        // 던전에 있을 때는 InDungeonSystem이 처리하고, 여기선 Town이 실제로 활성화된 상태일 때만 처리한다.
        if (bCurrentlyTownScene == false || bGoingToMainMenu == true)
            return;

        bGoingToMainMenu = true;

        // 마을 → 메인메뉴 이탈 시점 자동저장. 게임 종료(OnApplicationQuit)와 같은 느낌의 타이밍으로,
        // townObjectManager가 정리되기 전, 가장 온전한 상태에서 저장한다.
        signalHub.Publish(new AutoSaveRequestedSignal(AutoSaveReason.DepartToMainMenu));

        // 메인메뉴로 나갈 때도 카메라 상승 연출 시간 안에 BGM이 반드시 꺼지도록 페이드아웃한다.
        Sound.FadeOutBGM(skyCameraProductionManager.MoveDuration);

        townProductionManager.StartGoToMainMenu();
    }

    private void GoToMainMenuReady()
    {
        signalHub.Publish(new GoToMainMenuSignal());
    }

    private void GoToMainMenuCurtainReveal()
    {
        GoToMainMenuCurtainRevealEvent?.Invoke();
    }

    private void RetryButtonClicked(RetryButtonClickedSignal _retryButtonClickedSignal)
    {
        bRetryGame = true;
        townProductionManager.bRetryGame = true;
    }

    public void ActivatePortal()
    {
        if (townObjectManager.offroadVehicle != null)
            townObjectManager.offroadVehicle.col.enabled = true;
    }

    /// <summary>
    /// 영구 획득한 전리품 종류에 맞춰 LootPhillarColliderTilemap의 타일을 켠다.
    /// LootPillarManager.SpawnAcquiredPillars()와 마찬가지로 Grid가 새로 생성될 때마다(마을 재입장 포함)
    /// 현재 영구 획득 상태를 기준으로 다시 적용해야 하므로, GameInstaller가 StartTownSystem() 직후 호출한다.
    /// </summary>
    public void ApplyLootPillarColliderState(InDungeonObjectManager _inDungeonObjectManager)
    {
        townTileManager.ApplyLootPillarColliderState(_inDungeonObjectManager);
    }
}
