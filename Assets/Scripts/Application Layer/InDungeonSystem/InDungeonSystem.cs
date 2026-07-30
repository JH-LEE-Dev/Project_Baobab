using System.Collections;
using System;
using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    public event Action ActivatePortalEvent;
    public event Action GoToMainMenuCurtainRevealEvent;

    private SignalHub signalHub;
    public InDungeonObjectManager inDungeonObjectManager { get; private set; }
    public InDungeonUnitSpawner inDungeonUnitSpawner { get; private set; }
    private IEnvironmentProvider environmentProvider;
    private HiddenmapManager hiddenmapManager;
    private InputManager inputManager;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;
    private InDungeonProductionManager inDungeonProductionManager;
    private Character character;
    private SkyCameraProductionManager skyCameraProductionManager;
    public InDungeonResultManager inDungeonResultManager { get; private set; }
    private InDungeonStateManager inDungeonStateManager;

    [Header("Dungeon Data Base")]
    [SerializeField] private DungeonValueDataBase dungeonDataBase;

    private MapType currentMapType;
    private ForestType currentForestType;

    private bool bCurrentlyDungeonScene = false;
    private bool prevbCurrentlyDungeonScene = false;
    private bool bRetryGame = false;
    private bool bGoingToMainMenu = false;
    private MapType selectedMapType;
    private ForestType selectedForestType;

    // "포자 포션" - 던전 입장 후 캐릭터가 실제로 조작 가능해진 시점(ActivateCharacterSignal)부터만
    // PotionKey를 허용한다. 매 던전 진입마다(StartDungeonSystem) 리셋된다.
    private bool bCharacterActivated;
    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, IInventoryChecker _inventoryChecker,
    InputManager _inputManager, IInventory _characterInventory, OffroadContainer _offroadContainer, SkyCameraProductionManager _skyCameraProductionManager,
    InDungeonResultManager _inDungeonResultManager)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;
        characterInventory = _characterInventory;
        offroadContainer = _offroadContainer;
        skyCameraProductionManager = _skyCameraProductionManager;
        inDungeonResultManager = _inDungeonResultManager;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider, _inventoryChecker, inputManager, characterInventory, offroadContainer,
        inDungeonResultManager);

        inDungeonUnitSpawner = GetComponentInChildren<InDungeonUnitSpawner>();
        inDungeonUnitSpawner.Initialize(environmentProvider, inDungeonObjectManager, offroadContainer);

        hiddenmapManager = GetComponentInChildren<HiddenmapManager>();
        hiddenmapManager.Initialize();

        inDungeonStateManager = GetComponentInChildren<InDungeonStateManager>();

        inDungeonProductionManager = GetComponentInChildren<InDungeonProductionManager>();
        inDungeonProductionManager.Initialize(inputManager, _skyCameraProductionManager);

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();
        inDungeonProductionManager.Release();
        inDungeonObjectManager.Release();
    }

    public void StartDungeonSystem(SceneChangeData _sceneChangeData)
    {
        inDungeonResultManager.Reset();
        bCharacterActivated = false;

        currentMapType = _sceneChangeData.mapType;
        currentForestType = _sceneChangeData.forestType;

        inDungeonObjectManager.SetDungeonData(dungeonDataBase.GetDungeonData(currentMapType));
        inDungeonObjectManager.SetupForMapType(currentMapType);
        inDungeonObjectManager.SetupForForestType(currentForestType);
        inDungeonObjectManager.SetupItemManagerCulling();

        signalHub.Publish(new DungeonReadySignal(dungeonDataBase.GetDungeonData(currentMapType), currentForestType));
    }

    private void BindEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.PortalActivatedEvent += PortalActivated;

        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.ItemAcquiredEvent += ItemAcquired;

        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonObjectManager.TreeGetHitEvent += TreeGetHit;

        inDungeonObjectManager.TreeShieldRecoveringEvent -= TreeShieldRecovering;
        inDungeonObjectManager.TreeShieldRecoveringEvent += TreeShieldRecovering;

        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
        inDungeonObjectManager.CarrotItemAcquiredEvent += CarrotItemAcquired;

        inDungeonObjectManager.TreeDeadEvent -= TreeIsDead;
        inDungeonObjectManager.TreeDeadEvent += TreeIsDead;

        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadSpawnedEvent += OffroadSpawned;

        inDungeonObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        inDungeonObjectManager.OffroadInteractStateChangedEvent += OffroadInteractStateChanged;

        inDungeonObjectManager.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        inDungeonObjectManager.RepairBoxInteractStateChangedEvent += RepairBoxInteractStateChanged;

        inDungeonObjectManager.RideOffroadEvent -= RideOffroad;
        inDungeonObjectManager.RideOffroadEvent += RideOffroad;

        inDungeonObjectManager.DropAllItemEvent -= DropAllItem;
        inDungeonObjectManager.DropAllItemEvent += DropAllItem;

        inDungeonObjectManager.LostAndFoundBoxAcquiredEvent -= LostAndFoundBoxAcquired;
        inDungeonObjectManager.LostAndFoundBoxAcquiredEvent += LostAndFoundBoxAcquired;

        inputManager.inputReader.PotionKeyPressedEvent -= PotionKeyPressed;
        inputManager.inputReader.PotionKeyPressedEvent += PotionKeyPressed;

        inDungeonProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        inDungeonProductionManager.CharacterRideEndEvent += CharacterRideEnd;

        inDungeonProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        inDungeonProductionManager.CameraUpIsEndEvent += CameraUpIsEnd;

        inDungeonProductionManager.CameraDownEndEvent -= CameraDownIsEnd;
        inDungeonProductionManager.CameraDownEndEvent += CameraDownIsEnd;

        inDungeonProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        inDungeonProductionManager.RollbackSkyProductionEvent += RollbackSkyProduction;

        inDungeonProductionManager.GoToMainMenuReadyEvent -= GoToMainMenuReady;
        inDungeonProductionManager.GoToMainMenuReadyEvent += GoToMainMenuReady;

        inDungeonProductionManager.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        inDungeonProductionManager.GoToMainMenuCurtainRevealEvent += GoToMainMenuCurtainReveal;

        inDungeonObjectManager.ActivateWarningUIEvent -= ActivateWarningUI;
        inDungeonObjectManager.ActivateWarningUIEvent += ActivateWarningUI;

        inDungeonObjectManager.NPCPauseRequestedEvent -= NPCPauseRequested;
        inDungeonObjectManager.NPCPauseRequestedEvent += NPCPauseRequested;

        inDungeonObjectManager.FlyingItemPauseRequestedEvent -= FlyingItemPauseRequested;
        inDungeonObjectManager.FlyingItemPauseRequestedEvent += FlyingItemPauseRequested;

        inDungeonObjectManager.FlyingItemResumeRequestedEvent -= FlyingItemResumeRequested;
        inDungeonObjectManager.FlyingItemResumeRequestedEvent += FlyingItemResumeRequested;

        inDungeonObjectManager.FlyingItemDismissRequestedEvent -= FlyingItemDismissRequested;
        inDungeonObjectManager.FlyingItemDismissRequestedEvent += FlyingItemDismissRequested;
    }

    private void ReleaseEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonObjectManager.TreeShieldRecoveringEvent -= TreeShieldRecovering;
        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
        inDungeonObjectManager.TreeDeadEvent -= TreeIsDead;
        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        inDungeonObjectManager.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        inDungeonObjectManager.RideOffroadEvent -= RideOffroad;
        inDungeonObjectManager.DropAllItemEvent -= DropAllItem;
        inDungeonObjectManager.LostAndFoundBoxAcquiredEvent -= LostAndFoundBoxAcquired;
        inputManager.inputReader.PotionKeyPressedEvent -= PotionKeyPressed;
        inDungeonProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        inDungeonProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        inDungeonProductionManager.CameraDownEndEvent -= CameraDownIsEnd;
        inDungeonProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        inDungeonProductionManager.GoToMainMenuReadyEvent -= GoToMainMenuReady;
        inDungeonProductionManager.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        inDungeonObjectManager.ActivateWarningUIEvent -= ActivateWarningUI;
        inDungeonObjectManager.NPCPauseRequestedEvent -= NPCPauseRequested;
        inDungeonObjectManager.FlyingItemPauseRequestedEvent -= FlyingItemPauseRequested;
        inDungeonObjectManager.FlyingItemResumeRequestedEvent -= FlyingItemResumeRequested;
        inDungeonObjectManager.FlyingItemDismissRequestedEvent -= FlyingItemDismissRequested;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.Subscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<WarningUIClosedSignal>(WarningUIClosed);
        signalHub.Subscribe<ActivateCharacterSignal>(CharacterActivated);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
        signalHub.Subscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.UnSubscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<WarningUIClosedSignal>(WarningUIClosed);
        signalHub.UnSubscribe<ActivateCharacterSignal>(CharacterActivated);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
        signalHub.UnSubscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
    }

    private void PortalActivated()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void MapGenerated(MapGeneratedSignal mapGeneratedSignal)
    {
        // 나무 초기 스폰은 프레임 분산 코루틴으로 실행되어 즉시 끝나지 않을 수 있다. 구름이 걷히는
        // 트리거(DungeonStartSignal/RollbackCameraMove 포함)를 담은 나머지 로직은 반드시 스폰이
        // 실제로 끝난 뒤(OnTreesReady)에 실행되어야, 스폰 도중 구름이 걷혀버리는 일이 없다.
        inDungeonObjectManager.ReadyTrees(mapGeneratedSignal.grassTilePositions, OnTreesReady);
    }

    private void OnTreesReady()
    {
        inDungeonObjectManager.ReadyPortal();
        inDungeonProductionManager.Offroad_DI(inDungeonObjectManager.offroadVehicle);

        signalHub.Publish(new DungeonStartSignal(inDungeonObjectManager.GetPlayerStartPos()));
        inDungeonUnitSpawner.SpawnNPC();

        signalHub.Publish(new DecalreDungeonTypeSignal(currentMapType, currentForestType));

        character.gameObject.SetActive(true);

        CameraMoveController.Instance.SetupCamera();

        if (bRetryGame == false)
        {
            prevbCurrentlyDungeonScene = bCurrentlyDungeonScene;
            bCurrentlyDungeonScene = true;
            inDungeonProductionManager.bCurrentlyDungeonScene = true;
        }
        else
        {
            inDungeonProductionManager.RollbackCameraMove();
        }

        signalHub.Publish(new DeclareDungeonStateSignal(inDungeonStateManager.CalcDungeonState(selectedMapType)));
    }

    private void ItemAcquired(Item _item)
    {
        signalHub.Publish(new ItemAcquiredSignal(_item));
    }

    private void TreeGetHit(TreeObj _treeObj)
    {
        signalHub.Publish(new TreeGetHitSignal(_treeObj));
    }

    private void TreeShieldRecovering(TreeObj _treeObj)
    {
        signalHub.Publish(new TreeShieldRecoveringSignal(_treeObj));
    }

    private void GoHome(GoHomeButtonClickedSignal goHomeButtonClickedSignal)
    {
        inputManager.PauseESCKey(true); // 던전→타운(귀환) 연출 시작 - 종료 시점은 InDungeonProductionManager.CameraDownIsEnd()

        inDungeonProductionManager.StartSkyProduction();

        offroadContainer.col.enabled = false;
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = false;

        signalHub.Publish(new StartSkyProductionSignal());
    }

    private void CarrotItemAcquired(CarrotItem _carrotItem)
    {
        signalHub.Publish(new CarrotItemAcquiredSignal(_carrotItem.amount));
    }

    public void ClearInDungeonSystem()
    {
        if (bRetryGame == false)
        {
            prevbCurrentlyDungeonScene = bCurrentlyDungeonScene;
            bCurrentlyDungeonScene = false;
            inDungeonProductionManager.bCurrentlyDungeonScene = false;
        }

        inDungeonObjectManager.ClearObjManager();
        inDungeonUnitSpawner.ReleaseAllNPC();
    }

    /// <summary>
    /// Dungeon → Town 귀환 시, TownSystem.StartTownSystem()의 동기 초기화(그리드 생성, NPC 스폰)가
    /// 전부 끝난 뒤에 호출되어야 한다. 예전엔 ClearInDungeonSystem()이 StartTownSystem()보다 먼저
    /// 구름 걷힘 코루틴(0.75초 타이머)을 걸어둬서, Town 초기화가 0.75초보다 오래 걸리면 초기화 도중
    /// 구름이 걷혀버릴 수 있는 시간 경쟁 구조였다. 다른 전환 경로(Town→Dungeon, Retry 등)와 동일하게
    /// "무거운 초기화가 끝난 뒤에만 구름이 걷힌다"는 인과적 순서를 보장하기 위해 분리했다.
    /// 조건/필드는 ClearInDungeonSystem()이 이미 갱신해둔 값을 그대로 사용한다(그 사이 다른 코드가
    /// 이 필드들을 건드리지 않으므로 안전).
    /// </summary>
    public void NotifyTownSystemReady()
    {
        if ((prevbCurrentlyDungeonScene != bCurrentlyDungeonScene) && bRetryGame == false)
        {
            inDungeonProductionManager.RollbackCameraMove();
        }
    }

    private void AnimalHit(Animal _animal)
    {
        signalHub.Publish(new AnimalHitSignal(_animal));
    }

    private void TreeIsDead(TreeType _type, bool isPlayerKilled)
    {
        signalHub.Publish(new TreeIsDeadSignal(_type, isPlayerKilled));
    }

    private void AnimalIsDead(Animal _animal)
    {
        signalHub.Publish(new AnimalIsDeadSignal(_animal.animalType));
    }

    public void SetHiddenMapGrade()
    {
        inDungeonObjectManager.SetHiddenMapGrade(hiddenmapManager.CalcHiddenMapGrade());
    }

    public void ResetHiddenMapGrade()
    {
        inDungeonObjectManager.SetHiddenMapGrade(HiddenMapGrade.None);
    }

    private void CharacterSpawned(CharacterSpawnedSignal _characterSpawnedSignal)
    {
        character = _characterSpawnedSignal.character;
        inDungeonObjectManager.SetCharacter(_characterSpawnedSignal.character);
        inDungeonProductionManager.Character_DI(character);

        // 럼버잭 NPC들이 셰이크웨이브를 쓸 때 캐릭터의 StatComponent를 그대로 참조하도록 뒤늦게 주입.
        // (InDungeonUnitSpawner.Initialize() 시점엔 캐릭터가 아직 스폰되기 전이라 여기서 넘겨줘야 한다)
        inDungeonUnitSpawner.SetPlayerStatForShockWave(character.statComponent);
        // 부메랑도 동일한 이유로 캐릭터 스폰 이후 뒤늦게 주입한다.
        inDungeonUnitSpawner.SetPlayerStatForBoomerang(character.statComponent);
    }

    private void GameEnd()
    {
        signalHub.Publish(new GameEndSignal());
    }

    private void OffroadSpawned(OffroadVehicleObj _offroadVehicleObj)
    {
        signalHub.Publish(new OffroadSpawnedSignal(_offroadVehicleObj));
    }

    private void OffroadInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new OffroadInteractStateChangedSignal(_boolean));
    }

    private void RepairBoxInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new RepairBoxInteractStateChangedSignal(_boolean));
    }

    private void RideOffroad()
    {
        signalHub.Publish(new PopupUIDownSignal());
        inDungeonProductionManager.StartCharacterRide();

        offroadContainer.col.enabled = false;
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = false;
    }

    private void DropAllItem()
    {
        signalHub.Publish(new DropAllItemSignal());
    }

    private void LostAndFoundBoxAcquired()
    {
        signalHub.Publish(new LostAndFoundBoxAcquiredSignal());
    }

    // "포자 포션" - 던전에 입장해 캐릭터가 실제로 조작 가능해진 시점부터만 특수 키(PotionKey)로 마실 수 있다.
    private void PotionKeyPressed()
    {
        if (bCurrentlyDungeonScene == false || bCharacterActivated == false) return;

        inDungeonObjectManager.TryDrinkSporePotion();
    }

    private void CharacterActivated(ActivateCharacterSignal _signal)
    {
        bCharacterActivated = true;

        // 캐릭터가 실제로 움직일 수 있게 되는 시점에 스테이지별 BGM을 재생한다.
        if (selectedForestType == ForestType.WideGreenForest_1)
        {
            Sound.PlayBGM(SoundID.WideGreenForest1BGM);
        }
    }

    // "포자 포션" - 실제 Town 씬이 로드된 시점에 이번 원정에서 마시지 않았다면 충전한다.
    private void TownStarted(TownStartedSignal _signal)
    {
        inDungeonObjectManager.RefillSporePotionCharge();
    }

    private void CharacterRideEnd()
    {
        GameEnd();
    }

    private void CameraUpIsEnd()
    {
        if (bCurrentlyDungeonScene == false)
            return;

        if (bRetryGame == false)
            signalHub.Publish(new GoToHomeSignal());
        else
        {
            inDungeonObjectManager.ClearObjManager();
            inDungeonUnitSpawner.ReleaseAllNPC();

            signalHub.Publish(new GoToDungeonSignal(selectedMapType, selectedForestType));
        }
    }

    private void CameraDownIsEnd()
    {
        if (bCurrentlyDungeonScene == true && bRetryGame == false)
            return;

        if (bCurrentlyDungeonScene == true)
            signalHub.Publish(new ActivateCharacterSignal());

        StartCoroutine(PopupUIGoUPCoroutine());

        if (bRetryGame == true)
        {
            bRetryGame = false;
            inDungeonProductionManager.bRetryGame = false;
        }
    }

    private IEnumerator PopupUIGoUPCoroutine()
    {
        yield return new WaitForSeconds(0.7f);

        signalHub.Publish(new PopupUIUpSignal());

        if (bCurrentlyDungeonScene == true)
        {
            StartCoroutine(StaminaDecreaseCoroutine());
        }
        else
        {
            offroadContainer.col.enabled = true;
            if (inDungeonObjectManager.offroadVehicle != null)
                inDungeonObjectManager.offroadVehicle.col.enabled = true;

            ActivatePortalEvent?.Invoke();

            inputManager.PauseInteractKey(false);
        }
    }

    private IEnumerator StaminaDecreaseCoroutine()
    {
        yield return new WaitForSeconds(0.7f);
        signalHub.Publish(new StartDecreaseStaminaSignal());

        offroadContainer.col.enabled = true;
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = true;

        inputManager.PauseInteractKey(false);
    }

    private void RollbackSkyProduction()
    {
        signalHub.Publish(new RollbackSkyProductionSignal());
    }

    private void GoToMainMenuRequested(GoToMainMenuRequestedSignal _signal)
    {
        // Town에 있을 때는 TownSystem이 처리하고, 여기선 Dungeon이 실제로 활성화된 상태일 때만 처리한다.
        if (bCurrentlyDungeonScene == false || bGoingToMainMenu == true)
            return;

        bGoingToMainMenu = true;

        signalHub.Publish(new StartSkyProductionSignal(true));
        signalHub.Publish(new PopupUIDownSignal());

        inDungeonProductionManager.StartGoToMainMenu();
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
        inDungeonProductionManager.bRetryGame = true;

        inDungeonProductionManager.StartSkyProduction();
        signalHub.Publish(new StartSkyProductionSignal());
    }

    private void DungeonSelected(DungeonSelectedSignal _dungeonSelectedSignal)
    {
        selectedMapType = _dungeonSelectedSignal.type;
        selectedForestType = _dungeonSelectedSignal.forestType;
    }

    private void ActivateWarningUI()
    {
        signalHub.Publish(new ActivateWarningUISignal());
    }

    private void WarningUIClosed(WarningUIClosedSignal _warningUIClosedSignal)
    {
        if (bCurrentlyDungeonScene == false)
            return;

        if (_warningUIClosedSignal.bResult == true)
        {
            inDungeonObjectManager.HandleGameEnd();
        }
        else
        {
            inDungeonObjectManager.AbortGameEnd(true);
        }
    }

    public void ActivatePortal()
    {
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = true;
    }

    private void NPCPauseRequested(bool _pause)
    {
        if (_pause)
            inDungeonUnitSpawner.PauseAllNPC();
        else
            inDungeonUnitSpawner.ResumeAllNPC();
    }

    private void FlyingItemPauseRequested()
    {
        offroadContainer.PauseAllFlyingItems();
    }

    private void FlyingItemResumeRequested()
    {
        offroadContainer.ResumeAllFlyingItems();
    }

    private void FlyingItemDismissRequested()
    {
        offroadContainer.DismissAllFlyingItems();
    }
}
