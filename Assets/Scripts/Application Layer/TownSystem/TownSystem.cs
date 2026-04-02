using UnityEngine;

public class TownSystem : MonoBehaviour
{
    //외부 의존성
    private InputManager inputManager;

    //내부 의존성
    [SerializeField] private Transform townStartPoint;
    private SignalHub signalHub;
    private TownObjectManager townObjectManager;
    private IEnvironmentProvider environmentProvider;
    public LogProcessingManager logProcessingManager { get; private set; }


    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, InputManager _inputManager)
    {
        inputManager = _inputManager;
        signalHub = _signalHub;
        environmentProvider = _environmentProvider;

        townObjectManager = GetComponentInChildren<TownObjectManager>();
        logProcessingManager = GetComponentInChildren<LogProcessingManager>();

        townObjectManager.Initialize(environmentProvider);
        logProcessingManager.Initialize(inputManager);

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        logProcessingManager.Release();
        townObjectManager.Release();

        ReleaseEvents();
        UnSubscribeSignals();
    }

    public void StartTownSystem(SceneChangeData _sceneChangeData)
    {
        townObjectManager.ReadyObj();

        if (_sceneChangeData.prevScene == SceneType.Dungeon)
            signalHub.Publish(new TownStartedSignal(townObjectManager.GetPortalTransform()));
        else
            signalHub.Publish(new TownStartedSignal(townStartPoint));
    }

    private void BindEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
        townObjectManager.PortalActivatedEvent += PortalActivated;

        logProcessingManager.ContainerUpdatedEvent -= ContainerUpdated;
        logProcessingManager.ContainerUpdatedEvent += ContainerUpdated;
    }

    private void ReleaseEvents()
    {
        townObjectManager.PortalActivatedEvent -= PortalActivated;
        logProcessingManager.ContainerUpdatedEvent -= ContainerUpdated;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryInitializedSignal>(InventoryInitialized);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryInitializedSignal>(InventoryInitialized);
    }

    private void PortalActivated(PortalType _type)
    {
        signalHub.Publish(new PortalActivatedSignal(_type));
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
}
