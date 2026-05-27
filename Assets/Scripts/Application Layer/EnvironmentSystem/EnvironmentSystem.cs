using System.Collections.Generic;
using UnityEngine;

public class EnvironmentSystem : MonoBehaviour, IEnvironmentProvider
{
    //제공 인터페이스
    public IShadowDataProvider shadowDataProvider => lightingController;
    public IGroundDataProvider groundDataProvider => groundDataManager;

    public ITilemapDataProvider tilemapDataProvider => tileMapGenerator;

    public IPathfindGridProvider pathfindGridProvider => pathfindGridManager;

    public IDensityProvider densityProvider => densityManager;

    //외부 의존성
    private SignalHub signalHub;

    //내부 의존성
    private TileMapGenerator tileMapGenerator;
    private LightingController lightingController;
    public TimeController timeController { get; private set; }
    private GroundDataManager groundDataManager;
    public WeatherManager weatherManager { get; private set; }
    private PathfindGridManager pathfindGridManager;
    public DensityManager densityManager { get; private set; }
    public EnvironmentInteractionManager environmentInteractionManager { get; private set; }
    public EnvironmentObjManager environmentObjManager { get; private set; }

    //퍼블릭 초기화 및 제어 메서드

    public void Initialize(SignalHub _signalHub, IUnitLogicProvider _unitLogicProvider)
    {
        signalHub = _signalHub;

        tileMapGenerator = GetComponentInChildren<TileMapGenerator>();
        environmentInteractionManager = GetComponentInChildren<EnvironmentInteractionManager>();
        lightingController = GetComponentInChildren<LightingController>();
        timeController = GetComponentInChildren<TimeController>();
        groundDataManager = GetComponentInChildren<GroundDataManager>();
        weatherManager = GetComponentInChildren<WeatherManager>();
        pathfindGridManager = GetComponentInChildren<PathfindGridManager>();
        densityManager = GetComponentInChildren<DensityManager>();
        environmentObjManager = GetComponentInChildren<EnvironmentObjManager>();

        if (timeController != null)
            timeController.Initialize();

        if (lightingController != null)
            lightingController.Initialize(timeController);

        if (groundDataManager != null)
            groundDataManager.Initialize();

        if (weatherManager != null)
            weatherManager.Initialize(_unitLogicProvider);

        if (densityManager != null)
            densityManager.Initialize();

        if (environmentInteractionManager != null)
            environmentInteractionManager.Initialize();

        if (environmentObjManager != null)
            environmentObjManager.Initialize(tileMapGenerator);

        BindEvents();
        SubscribeSignals();
    }

    public void DI(IEnvironmentProvider _environmentProvider,
        TownObjectManager _townObjectManager,
        InDungeonObjectManager _inDungeonObjectManager,
        InDungeonUnitSpawner _inDungeonUnitSpawner)
    {
        environmentInteractionManager.DI(_environmentProvider, _townObjectManager, _inDungeonObjectManager, _inDungeonUnitSpawner);
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();

        if (environmentObjManager != null)
            environmentObjManager.ReleaseAllObjs();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<DungeonReadySignal>(DungeonStarted);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<AnimalIsDeadSignal>(AnimalIsDead);
        signalHub.Subscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.Subscribe<PrestigeLevelIncreasedSignal>(PrestigeLevelIncreased);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<DungeonReadySignal>(DungeonStarted);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<AnimalIsDeadSignal>(AnimalIsDead);
        signalHub.UnSubscribe<TreeIsDeadSignal>(TreeIsDead);
        signalHub.UnSubscribe<PrestigeLevelIncreasedSignal>(PrestigeLevelIncreased);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
    }

    private void BindEvents()
    {
        tileMapGenerator.TilemapGeneratedEvent -= TilemapGenerated;
        tileMapGenerator.TilemapGeneratedEvent += TilemapGenerated;

        tileMapGenerator.DeclareActiveTilesCntEvent -= DeclareActiveTileCnt;
        tileMapGenerator.DeclareActiveTilesCntEvent += DeclareActiveTileCnt;

        weatherManager.WeatherChagnedEvent -= WeatherChanged;
        weatherManager.WeatherChagnedEvent += WeatherChanged;
    }

    private void ReleaseEvents()
    {
        tileMapGenerator.TilemapGeneratedEvent -= TilemapGenerated;

        tileMapGenerator.DeclareActiveTilesCntEvent -= DeclareActiveTileCnt;

        weatherManager.WeatherChagnedEvent -= WeatherChanged;
    }

    private void DungeonStarted(DungeonReadySignal dungeonStartSignal)
    {
        lightingController.EnablePointLights();
        tileMapGenerator.InitializeMapData();
        pathfindGridManager.Initialize(tileMapGenerator.GridWidth, tileMapGenerator.GridHeight);
        tileMapGenerator.GenerateMap();
    }

    private void TilemapGenerated(List<Vector3> tilePositions)
    {
        signalHub.Publish(new MapGeneratedSignal(tilePositions));

        if (environmentObjManager != null)
            environmentObjManager.SpawnInDungeonEnvironmentObjs();
    }

    private void DeclareActiveTileCnt(int _grassTileCnt, int _walkableTileCnt)
    {
        densityManager.SetActiveTilesCnt(_grassTileCnt, _walkableTileCnt);
    }

    private void CharacterSpawned(CharacterSpawnedSignal characterSpawendSignal)
    {
        lightingController.DI(characterSpawendSignal.character);
        environmentInteractionManager.DI_Character(characterSpawendSignal.character);
    }

    private void WeatherChanged(WeatherType _weatherType)
    {
        lightingController.WeatherChanged(_weatherType);
    }

    public void SetupForMapType(ForestType _forestType, MapType _mapType)
    {
        densityManager.SetDensityData(_forestType, _mapType);
    }

    private void AnimalIsDead(AnimalIsDeadSignal animalIsDeadSignal)
    {
        //densityManager.AddHiddenGauge(animalIsDeadSignal.type);
    }

    private void TreeIsDead(TreeIsDeadSignal treeIsDeadSignal)
    {
        densityManager.AddHiddenGauge(treeIsDeadSignal.type);
    }

    public bool IsCurrentlyHiddenMap(MapType _mapType, ForestType _forestType)
    {
        return densityManager.IsCurrentlyHiddenMap(_mapType, _forestType);
    }

    private void PrestigeLevelIncreased(PrestigeLevelIncreasedSignal _prestigeLevelIncreasedSignal)
    {
        densityManager.PrestigeLevelIncreased(_prestigeLevelIncreasedSignal.level);
    }

    private void TownStarted(TownStartedSignal _townStartedSignal)
    {
        environmentObjManager.ReleaseAllObjs();
        environmentObjManager.SpawnTownEnvironmentObjs();
    }
}
