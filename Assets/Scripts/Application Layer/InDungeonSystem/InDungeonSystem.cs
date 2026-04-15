using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    private SignalHub signalHub;
    public InDungeonObjectManager inDungeonObjectManager { get; private set; }
    public InDungeonUnitSpawner inDungeonUnitSpawner { get; private set; }
    private IEnvironmentProvider environmentProvider;


    [Header("Dungeon Data")]
    [SerializeField] private DungeonData dungeonData;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider, dungeonData);

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
        signalHub.Publish(new DungeonReadySignal(dungeonData));
        inDungeonObjectManager.SetDungeonData(dungeonData);
        inDungeonUnitSpawner.SpawnAnimals();
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
    }

    private void ReleaseEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonUnitSpawner.AnimalIsDeadEvent -= inDungeonObjectManager.SpawnCarrots;
        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
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
        inDungeonObjectManager.ReadyPortalAndCharacter();

        signalHub.Publish(new DungeonStartSignal(inDungeonObjectManager.GetPlayerStartPos()));
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
        inDungeonUnitSpawner.ReleaseAllAnimals();
        inDungeonObjectManager.ClearObjManager();
        signalHub.Publish(new GoToHomeSignal());
    }

    private void FirstTimeEarnMoney(FirstTimeEarnMoneySignal firstTimeEarnMoneySignal)
    {
        inDungeonObjectManager.CreateWelcomeNoobLoot();
    }

    private void CarrotItemAcquired()
    {
        signalHub.Publish(new CarrotItemAcquiredSignal());
    }
}
