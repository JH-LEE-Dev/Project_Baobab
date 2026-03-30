using System.Collections.Generic;
using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    private SignalHub signalHub;
    public InDungeonObjectManager inDungeonObjectManager {get; private set;}
    public InDungeonUnitSpawner inDungeonUnitSpawner {get; private set;}
    private IEnvironmentProvider environmentProvider;
    private GroupManager groupManager;


    [Header("Dungeon Data")]
    [SerializeField] private DungeonData dungeonData;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider,dungeonData);

        inDungeonUnitSpawner = GetComponentInChildren<InDungeonUnitSpawner>();
        inDungeonUnitSpawner.Initialize(environmentProvider);

        groupManager = GetComponentInChildren<GroupManager>();
        groupManager.Initialize(environmentProvider);

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

        inDungeonUnitSpawner.AnimalSpawnedEvent -= AnimalSpawned;
        inDungeonUnitSpawner.AnimalSpawnedEvent += AnimalSpawned;
    }

    private void ReleaseEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonUnitSpawner.AnimalSpawnedEvent -= AnimalSpawned;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
    }

    private void PortalActivated(PortalType _type)
    {
        inDungeonUnitSpawner.ReleaseAllAnimals();
        signalHub.Publish(new PortalActivatedSignal(_type));
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

    private void AnimalSpawned(IReadOnlyList<AnimalGroup> _animapGroup)
    {
        groupManager.SetAnimalGroup(_animapGroup);
    }
}
