using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    private SignalHub signalHub;
    public InDungeonObjectManager inDungeonObjectManager { get; private set; }
    public InDungeonUnitSpawner inDungeonUnitSpawner { get; private set; }
    private IEnvironmentProvider environmentProvider;
    private HiddenmapManager hiddenmapManager;
    private InputManager inputManager;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;

    private Character character;


    [Header("Dungeon Data Base")]
    [SerializeField] private DungeonValueDataBase dungeonDataBase;

    private MapType currentMapType;
    private ForestType currentForestType;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, IInventoryChecker _inventoryChecker,
    InputManager _inputManager, IInventory _characterInventory, OffroadContainer _offroadContainer)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;
        characterInventory = _characterInventory;
        offroadContainer = _offroadContainer;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider, _inventoryChecker, inputManager, characterInventory, offroadContainer);

        inDungeonUnitSpawner = GetComponentInChildren<InDungeonUnitSpawner>();
        inDungeonUnitSpawner.Initialize(environmentProvider);

        hiddenmapManager = GetComponentInChildren<HiddenmapManager>();
        hiddenmapManager.Initialize();

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

        signalHub.Publish(new DungeonReadySignal(dungeonDataBase.GetDungeonData(currentMapType), currentForestType));
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

        inDungeonObjectManager.GoToTownEvent -= GoToTown;
        inDungeonObjectManager.GoToTownEvent += GoToTown;

        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadSpawnedEvent += OffroadSpawned;
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
        inDungeonObjectManager.GoToTownEvent -= GoToTown;
        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.Subscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.UnSubscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
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

        character.gameObject.SetActive(true);

        CameraMoveController.Instance.SetupCamera();
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
    }

    private void GoToTown()
    {
        signalHub.Publish(new GoToHomeSignal());
    }

    private void OffroadSpawned(OffroadVehicleObj _offroadVehicleObj)
    {
        signalHub.Publish(new OffroadSpawnedSignal(_offroadVehicleObj));
    }
}
