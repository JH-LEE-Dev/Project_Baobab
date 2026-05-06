using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    private SignalHub signalHub;
    public InDungeonObjectManager inDungeonObjectManager { get; private set; }
    public InDungeonUnitSpawner inDungeonUnitSpawner { get; private set; }
    private IEnvironmentProvider environmentProvider;


    [Header("Dungeon Data Base")]
    [SerializeField] private DungeonValueDataBase dungeonDataBase;

    private MapType currentMapType;
    private ForestType currentForestType;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, IInventoryChecker _inventoryChecker)
    {
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider, _inventoryChecker);

        inDungeonUnitSpawner = GetComponentInChildren<InDungeonUnitSpawner>();
        inDungeonUnitSpawner.Initialize(environmentProvider);

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();
    }

    public void StartDungeonSystem(SceneChangeData _sceneChangeData)
    {
        currentMapType = _sceneChangeData.mapType;
        currentForestType = _sceneChangeData.forestType;

        signalHub.Publish(new DungeonReadySignal(dungeonDataBase.GetDungeonData(currentMapType)));
        inDungeonObjectManager.SetDungeonData(dungeonDataBase.GetDungeonData(currentMapType));
        inDungeonObjectManager.SetupItemManagerCulling();
    }

    private void BindEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.PortalActivatedEvent += PortalActivated;

        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.ItemAcquiredEvent += ItemAcquired;

        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonObjectManager.TreeGetHitEvent += TreeGetHit;

        inDungeonUnitSpawner.AnimalIsDeadEvent -= inDungeonObjectManager.SpawnCarrots;
        inDungeonUnitSpawner.AnimalIsDeadEvent += inDungeonObjectManager.SpawnCarrots;

        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
        inDungeonObjectManager.CarrotItemAcquiredEvent += CarrotItemAcquired;

        inDungeonUnitSpawner.AnimalHitEvent -= AnimalHit;
        inDungeonUnitSpawner.AnimalHitEvent += AnimalHit;

        inDungeonObjectManager.TreeDeadEvent -= TreeIsDead;
        inDungeonObjectManager.TreeDeadEvent += TreeIsDead;

        inDungeonUnitSpawner.AnimalIsDeadEvent -= AnimalIsDead;
        inDungeonUnitSpawner.AnimalIsDeadEvent += AnimalIsDead;
    }

    private void ReleaseEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonUnitSpawner.AnimalIsDeadEvent -= inDungeonObjectManager.SpawnCarrots;
        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
        inDungeonUnitSpawner.AnimalHitEvent -= AnimalHit;
        inDungeonObjectManager.TreeDeadEvent -= TreeIsDead;
        inDungeonUnitSpawner.AnimalIsDeadEvent -= AnimalIsDead;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.Subscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.Subscribe<FirstTimeEarnMoneySignal>(FirstTimeEarnMoney);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.UnSubscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.UnSubscribe<FirstTimeEarnMoneySignal>(FirstTimeEarnMoney);
    }

    private void PortalActivated()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void MapGenerated(MapGeneratedSignal mapGeneratedSignal)
    {
        inDungeonObjectManager.ReadyTrees(mapGeneratedSignal.grassTilePositions);
        inDungeonObjectManager.ReadyPortal();

        signalHub.Publish(new DungeonStartSignal(inDungeonObjectManager.GetPlayerStartPos()));
        inDungeonUnitSpawner.SpawnAnimals();

        signalHub.Publish(new DecalreDungeonTypeSignal(currentMapType, currentForestType));
    }

    private void ItemAcquired(Item _item)
    {
        signalHub.Publish(new ItemAcquiredSignal(_item));
    }

    private void TreeGetHit(TreeObj _treeObj)
    {
        signalHub.Publish(new TreeGetHitSignal(_treeObj));
    }

    private void GoHome(GoHomeButtonClickedSignal goHomeButtonClickedSignal)
    {
        signalHub.Publish(new GoToHomeSignal());
    }

    private void FirstTimeEarnMoney(FirstTimeEarnMoneySignal firstTimeEarnMoneySignal)
    {
        inDungeonObjectManager.CreateWelcomeNoobLoot();
    }

    private void CarrotItemAcquired(CarrotItem _carrotItem)
    {
        signalHub.Publish(new CarrotItemAcquiredSignal(_carrotItem.amount));
    }

    public void ClearInDungeonSystem()
    {
        inDungeonObjectManager.ClearObjManager();
        inDungeonUnitSpawner.ReleaseAllAnimals();
    }

    private void AnimalHit(Animal _animal)
    {
        signalHub.Publish(new AnimalHitSignal(_animal));
    }

    private void TreeIsDead(TreeType _type)
    {
        signalHub.Publish(new TreeIsDeadSignal(_type));
    }

    private void AnimalIsDead(Animal _animal)
    {
        signalHub.Publish(new AnimalIsDeadSignal(_animal.animalType));
    }
}
