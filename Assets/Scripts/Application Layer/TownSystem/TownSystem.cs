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
    private TentManager tentManager;
    private Character character;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;
    private TownProductionManager townProductionManager;
    private MapType selectedMapType;
    private ForestType selectedForestType;
    private SkyCameraProductionManager skyCameraProductionManager;
    private TownTileManager townTileManager;
    public TownUnitSpawner townUnitSpawner { get; private set; }

    /// <summary>
    /// 게임이 처음 시작됐을 때(던전에서 돌아온 것이 아닐 때) 캐릭터가 생성되는 위치.
    /// TownUnitSpawner가 운반 NPC를 집 주변에 배치할 때 보조 기준점으로 사용한다.
    /// </summary>
    public Transform TownStartPoint => townStartPoint;

    private bool bCurrentlyTownScene = true;
    private bool bRetryGame = false;
    private bool bGoingToMainMenu = false;

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
        skyCameraProductionManager = _skyCameraProductionManager;

        townProductionManager.Initialize(inputManager, _skyCameraProductionManager);
        townObjectManager.Initialize(environmentProvider, inputManager, characterInventory, offroadContainer);
        logProcessingManager.Initialize(inputManager);
        tentManager.Initialize(inputManager);
        townTileManager.Initialize();
        townUnitSpawner?.Initialize(environmentProvider);

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
        townTileManager.CreateGrid();

        // Grid는 매번 새로 생성되므로, 제재소 증설 단계(가공 라인 수)를 여기서 다시 반영해준다.
        // 세이브 로드나 던전 안에서의 증설이 이벤트보다 먼저 끝나 있어도 이 동기화로 항상 맞춰진다.
        ApplyProcessLineCountToGrid(logProcessingManager.ActiveLineCount);

        CollisionSystem.Instance?.ClearAll();
        townObjectManager.ReadyObj();
        logProcessingManager.EnableShopObj();
        tentManager.EnableTent();

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
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<TeleportUIClosedSignal>(TeleportUIClosed);
        signalHub.Subscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.Subscribe<TeleportUIClosedWhileTeleportSignal>(TeleportUIClosedWhileTeleport);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.Subscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<TeleportUIClosedSignal>(TeleportUIClosed);
        signalHub.UnSubscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.UnSubscribe<TeleportUIClosedWhileTeleportSignal>(TeleportUIClosedWhileTeleport);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.UnSubscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
    }

    private void PortalActivated()
    {
        townProductionManager.StartCharacterRide();

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

    private void DungeonSelected(DungeonSelectedSignal dungeonSelectedSignal)
    {
        if (bRetryGame == true)
            return;

        // 던전이 실제로 선택되어 확정되는 시점 - 여기서부터는 취소 불가능한 씬 전환 연출이므로 ESC 재입력을 막는다.
        // (차량 탑승~던전 선택 팝업 단계는 ESC로 팝업을 취소할 수 있어야 하므로 여기서 막으면 안 된다)
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

    private void LogItemProcessorActiveState(bool _boolean)
    {
        signalHub.Publish(new LogItemProcessorActiveStateSignal(_boolean));
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
        // Town 전용 타일맵 오버라이드를 풀고 던전의 TileMapGenerator로 되돌린다.
        character?.SetTilemapDataProvider(environmentProvider.tilemapDataProvider);

        logProcessingManager.DisableShopObj();
        tentManager.DisableTent();
        townProductionManager.SetCharacterTransform();

        if (bRetryGame == false)
            townProductionManager.RollbackCameraMove();

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

        inputManager.PauseMove(false);
        inputManager.PauseESCKey(false); // 타운→던전 진입 연출 종료 (DungeonSelected()에서 걸어둔 PauseESCKey(true) 해제)
        signalHub.Publish(new ActivateCharacterSignal());

        StartCoroutine(PopupUIGoUPCoroutine());
    }

    private IEnumerator PopupUIGoUPCoroutine()
    {
        yield return new WaitForSeconds(0.7f);

        signalHub.Publish(new PopupUIUpSignal());

        if (bCurrentlyTownScene == false)
        {
            StartCoroutine(StaminaDecreaseCoroutine());
        }
        else
        {
            offroadContainer.col.enabled = true;

            if (townObjectManager.offroadVehicle != null)
                townObjectManager.offroadVehicle.col.enabled = true;

            inputManager.PauseInteractKey(false);
        }
    }

    private IEnumerator StaminaDecreaseCoroutine()
    {
        yield return new WaitForSeconds(0.7f);
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

        signalHub.Publish(new PopupUIUpSignal());
    }

    private void GoToMainMenuRequested(GoToMainMenuRequestedSignal _signal)
    {
        // 던전에 있을 때는 InDungeonSystem이 처리하고, 여기선 Town이 실제로 활성화된 상태일 때만 처리한다.
        if (bCurrentlyTownScene == false || bGoingToMainMenu == true)
            return;

        bGoingToMainMenu = true;

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
}
