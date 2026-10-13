using System.Collections.Generic;
using UnityEngine;
using Project_Baobab.Presentation.EnvironmentSystem;

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
    private EnvironmentParticleSystem environmentParticleSystem;



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
        environmentParticleSystem = GetComponentInChildren<EnvironmentParticleSystem>();

        if (environmentParticleSystem != null)
            environmentParticleSystem.Initialize();

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
        InDungeonUnitSpawner _inDungeonUnitSpawner,
        TownUnitSpawner _townUnitSpawner)
    {
        environmentInteractionManager.DI(_environmentProvider, _townObjectManager, _inDungeonObjectManager, _inDungeonUnitSpawner, _townUnitSpawner);
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
        tileMapGenerator.InitializeMapData();
        pathfindGridManager.Initialize(tileMapGenerator.GridWidth, tileMapGenerator.GridHeight);
        tileMapGenerator.GenerateMap();
    }

    /// <summary>[로그라이크] 타일 한 칸의 월드 크기(아이소메트릭 마름모 가로·세로).</summary>
    public Vector2 TileCellSize => tileMapGenerator.CellSize;

    // [로그라이크] 맵 생성 완료를 MapGeneratedSignal 대신 이 콜백으로 돌려받는 중인지.
    // 그 시그널을 받으면 InDungeonSystem이 던전 원정 흐름(포탈·NPC·카메라 하강)을 시작하므로 로그라이크는 피해 간다.
    private System.Action<List<Vector3>> roguelikeMapGeneratedCallback;

    /// <summary>
    /// [로그라이크 전용] 필드 맵을 만든다. 던전 진입(DungeonStarted)과 같은 순서로 생성하되, 완료 알림을
    /// 시그널이 아니라 _onGenerated 콜백으로만 보낸다. 본편 경로(DungeonReadySignal)는 바뀌지 않는다.
    /// </summary>
    public void GenerateRoguelikeField(ForestType _forestType, MapType _mapType, float _centerSafeZoneRadius,
        float _islandRadiusX, float _islandRadiusY, System.Action<List<Vector3>> _onGenerated)
    {
        SetupForMapType(_forestType, _mapType);
        tileMapGenerator.SetCenterSafeZoneRadiusOverride(_centerSafeZoneRadius);
        tileMapGenerator.SetEllipseIslandOverride(_islandRadiusX, _islandRadiusY);

        roguelikeMapGeneratedCallback = _onGenerated;
        try
        {
            tileMapGenerator.InitializeMapData();
            pathfindGridManager.Initialize(tileMapGenerator.GridWidth, tileMapGenerator.GridHeight);
            tileMapGenerator.GenerateMap();
        }
        finally
        {
            roguelikeMapGeneratedCallback = null;
        }
    }

    private void TilemapGenerated(List<Vector3> tilePositions)
    {
        if (null != roguelikeMapGeneratedCallback)
        {
            System.Action<List<Vector3>> callback = roguelikeMapGeneratedCallback;
            roguelikeMapGeneratedCallback = null;
            callback.Invoke(tilePositions);

            if (environmentObjManager != null)
                environmentObjManager.SpawnInDungeonEnvironmentObjs();
            return;
        }

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
        //lightingController.WeatherChanged(_weatherType);
    }

    public void SetupForMapType(ForestType _forestType, MapType _mapType)
    {
        densityManager.SetDensityData(_forestType, _mapType);
        tileMapGenerator.SetupForMapType(_mapType);
        environmentObjManager.SetupForMapType(_mapType);
        environmentParticleSystem.ChangeMap(_mapType);
    }

    public void SetupForTownMap()
    {
        environmentParticleSystem.ChangeMap(MapType.Town);
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
        tileMapGenerator.ReleaseAllAnimatedObj();
        environmentObjManager.ReleaseAllObjs();
        environmentObjManager.SpawnTownEnvironmentObjs();
    }
}
