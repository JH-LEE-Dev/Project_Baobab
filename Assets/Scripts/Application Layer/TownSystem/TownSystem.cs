using UnityEngine;

public class TownSystem : MonoBehaviour
{
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

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer)
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

        townProductionManager.Initialize(inputManager);
        townObjectManager.Initialize(environmentProvider, inputManager, characterInventory, offroadContainer);
        logProcessingManager.Initialize(inputManager);
        tentManager.Initialize(inputManager);

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
        CollisionSystem.Instance?.ClearAll();
        townObjectManager.ReadyObj();
        logProcessingManager.EnableShopObj();

        if (townProductionManager.offroadVehicleObj == null)
        {
            townProductionManager.Offroad_DI(townObjectManager.portal);
        }

        if (_sceneChangeData.prevScene == SceneType.DungeonScene)
            signalHub.Publish(new TownStartedSignal(townObjectManager.GetTownReturnPoint()));
        else
            signalHub.Publish(new TownStartedSignal(townStartPoint));

        logProcessingManager.SetMapType(MapType.Town);
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
        townProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        townProductionManager.StartSkyProductionEvent -= StartSkyProduction;
        townProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        townProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        townProductionManager.CameraUpDownEndEvent -= CameraDownIsEnd;
        townProductionManager.PopupUIDownEvent -= PopupUIDown;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<TeleportUIClosedSignal>(TeleportUIClosed);
        signalHub.Subscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.Subscribe<TeleportUIClosedWhileTeleport>(TeleportUIClosedWhileTeleport);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<TeleportUIClosedSignal>(TeleportUIClosed);
        signalHub.UnSubscribe<DungeonStartSignal>(DungeonStarted);
        signalHub.UnSubscribe<TeleportUIClosedWhileTeleport>(TeleportUIClosedWhileTeleport);
    }

    private void PortalActivated()
    {
        townProductionManager.StartCharacterRide();
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
        selectedMapType = dungeonSelectedSignal.type;
        selectedForestType = dungeonSelectedSignal.forestType;

        townProductionManager.StartDrive();
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

    private void DungeonStarted(DungeonStartSignal _dungeonStartSignal)
    {
        logProcessingManager.DisableShopObj();
        townProductionManager.SetCharacterTransform();
        townProductionManager.RollbackCameraMove();
    }

    private void CharacterRideEnd()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void TeleportUIClosedWhileTeleport(TeleportUIClosedWhileTeleport _teleportUIClosedWhileTeleport)
    {
        townProductionManager.SetbCanGetOff(false);
    }

    private void StartSkyProduction()
    {
        signalHub.Publish(new StartSkyProductionSignal());
    }

    private void RollbackSkyProduction()
    {
        signalHub.Publish(new RollbackSkyProductionSignal());
    }

    private void CameraUpIsEnd()
    {
        townObjectManager.ClearObjManager();
        signalHub.Publish(new GoToDungeonSignal(selectedMapType, selectedForestType));
    }

    private void CameraDownIsEnd()
    {
        signalHub.Publish(new PopupUIUpSignal());
        signalHub.Publish(new StartDecreaseStaminaSignal());
    }

    private void PopupUIDown()
    {
        signalHub.Publish(new PopupUIDownSignal());
    }
}
