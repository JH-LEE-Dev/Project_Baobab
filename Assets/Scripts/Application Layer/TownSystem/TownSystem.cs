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

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, InputManager _inputManager)
    {
        inputManager = _inputManager;
        signalHub = _signalHub;
        environmentProvider = _environmentProvider;

        townObjectManager = GetComponentInChildren<TownObjectManager>();
        logProcessingManager = GetComponentInChildren<LogProcessingManager>();
        tentManager = GetComponentInChildren<TentManager>();

        townObjectManager.Initialize(environmentProvider);
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

        ReleaseEvents();
        UnSubscribeSignals();
    }

    public void StartTownSystem(SceneChangeData _sceneChangeData)
    {
        CollisionSystem.Instance?.ClearAll();
        townObjectManager.ReadyObj();

        if (_sceneChangeData.prevScene == SceneType.DungeonScene)
            signalHub.Publish(new TownStartedSignal(townObjectManager.GetPortalTransform()));
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

        logProcessingManager.FirstTimeEarnMoneyEvent -= FirstTimeEarnMoney;
        logProcessingManager.FirstTimeEarnMoneyEvent += FirstTimeEarnMoney;

        logProcessingManager.LogContainerSpecChangedEvent -= logContainerSpecChanged;
        logProcessingManager.LogContainerSpecChangedEvent += logContainerSpecChanged;
    }

    private void ReleaseEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
        logProcessingManager.ContainerUpdatedEvent -= ContainerUpdated;
        logProcessingManager.InteractStateChangedEvent -= LogContainerInteractStateChanged;
        logProcessingManager.EarnMoneyEvent -= EarnMoney;
        tentManager.TentInteractEvent -= TentInteract;
        logProcessingManager.FirstTimeEarnMoneyEvent -= FirstTimeEarnMoney;
        logProcessingManager.LogContainerSpecChangedEvent -= logContainerSpecChanged;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryInitializedSignal>(InventoryInitialized);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(CurrentlyInDungeon);
    }

    private void PortalActivated()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void InventoryInitialized(InventoryInitializedSignal inventoryInitializedSignal)
    {
        logProcessingManager.DI_Inventory(inventoryInitializedSignal.inventory);
    }

    private void ContainerUpdated()
    {
        signalHub.Publish(new InventoryUpdatedSignal());
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

    private void FirstTimeEarnMoney()
    {
        signalHub.Publish(new FirstTimeEarnMoneySignal());
    }

    private void DungeonSelected(DungeonSelectedSignal dungeonSelectedSignal)
    {
        townObjectManager.ClearObjManager();
        signalHub.Publish(new GoToDungeonSignal(dungeonSelectedSignal.type, dungeonSelectedSignal.forestType));
    }

    private void logContainerSpecChanged()
    {
        signalHub.Publish(new LogContainerSpecChangedSignal());
    }

    private void CurrentlyInDungeon(DecalreDungeonTypeSignal decalreDungeonTypeSignal)
    {
        logProcessingManager.SetMapType(decalreDungeonTypeSignal.mapType);
    }
}
