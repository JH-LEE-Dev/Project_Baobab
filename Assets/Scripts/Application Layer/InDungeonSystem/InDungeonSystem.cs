using System.Collections;
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
    private MapType selectedMapType;
    private ForestType selectedForestType;
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
        inDungeonUnitSpawner.Initialize(environmentProvider);

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

        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadSpawnedEvent += OffroadSpawned;

        inDungeonObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        inDungeonObjectManager.OffroadInteractStateChangedEvent += OffroadInteractStateChanged;

        inDungeonObjectManager.RideOffroadEvent -= RideOffroad;
        inDungeonObjectManager.RideOffroadEvent += RideOffroad;

        inDungeonObjectManager.DropAllItemEvent -= DropAllItem;
        inDungeonObjectManager.DropAllItemEvent += DropAllItem;

        inDungeonProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        inDungeonProductionManager.CharacterRideEndEvent += CharacterRideEnd;

        inDungeonProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        inDungeonProductionManager.CameraUpIsEndEvent += CameraUpIsEnd;

        inDungeonProductionManager.CameraDownEndEvent -= CameraDownIsEnd;
        inDungeonProductionManager.CameraDownEndEvent += CameraDownIsEnd;

        inDungeonProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        inDungeonProductionManager.RollbackSkyProductionEvent += RollbackSkyProduction;

        inDungeonObjectManager.ActivateWarningUIEvent -= ActivateWarningUI;
        inDungeonObjectManager.ActivateWarningUIEvent += ActivateWarningUI;
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
        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        inDungeonObjectManager.RideOffroadEvent -= RideOffroad;
        inDungeonObjectManager.DropAllItemEvent -= DropAllItem;
        inDungeonProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        inDungeonProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        inDungeonProductionManager.CameraDownEndEvent -= CameraDownIsEnd;
        inDungeonProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        inDungeonObjectManager.ActivateWarningUIEvent -= ActivateWarningUI;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.Subscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<WarningUIClosedSignal>(WarningUIClosed);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.UnSubscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<WarningUIClosedSignal>(WarningUIClosed);
    }

    private void PortalActivated()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void MapGenerated(MapGeneratedSignal mapGeneratedSignal)
    {
        inDungeonObjectManager.ReadyTrees(mapGeneratedSignal.grassTilePositions);
        inDungeonObjectManager.ReadyPortal();
        inDungeonProductionManager.Offroad_DI(inDungeonObjectManager.portal);

        signalHub.Publish(new DungeonStartSignal(inDungeonObjectManager.GetPlayerStartPos()));
        inDungeonUnitSpawner.SpawnAnimals();

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

    private void GoHome(GoHomeButtonClickedSignal goHomeButtonClickedSignal)
    {
        inDungeonProductionManager.StartSkyProduction();
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
        inDungeonUnitSpawner.ReleaseAllAnimals();

        if ((prevbCurrentlyDungeonScene != bCurrentlyDungeonScene) && bRetryGame == false)
        {
            inDungeonProductionManager.RollbackCameraMove();
        }
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
        inDungeonProductionManager.Character_DI(character);
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

    private void RideOffroad()
    {
        inDungeonProductionManager.StartCharacterRide();
    }

    private void DropAllItem()
    {
        signalHub.Publish(new DropAllItemSignal());
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
            inDungeonUnitSpawner.ReleaseAllAnimals();

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
            character.col.enabled = true;
            inputManager.PauseInteractKey(false);
        }
    }

    private IEnumerator StaminaDecreaseCoroutine()
    {
        yield return new WaitForSeconds(0.7f);
        signalHub.Publish(new StartDecreaseStaminaSignal());
        
        character.col.enabled = true;
        inputManager.PauseInteractKey(false);
    }

    private void RollbackSkyProduction()
    {
        signalHub.Publish(new RollbackSkyProductionSignal());
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
            signalHub.Publish(new PopupUIDownSignal());
            inDungeonObjectManager.HandleGameEnd();
        }
        else
        {
            inDungeonObjectManager.AbortGameEnd(true);
        }
    }
}
