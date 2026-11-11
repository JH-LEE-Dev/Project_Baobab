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

    // 지진 바닥 물결(QuakeTileRipple). 실제 타일맵은 건드리지 않고 복제층만 움직이는 연출이다.
    [SerializeField] private QuakeTileRippleSettings quakeRippleSettings = new QuakeTileRippleSettings();
    private readonly QuakeTileRipple quakeRipple = new QuakeTileRipple();
    private readonly List<UnityEngine.Tilemaps.Tilemap> quakeFloorLayers = new List<UnityEngine.Tilemaps.Tilemap>(4);
    private readonly List<UnityEngine.Tilemaps.Tilemap> quakeWaterLayers = new List<UnityEngine.Tilemaps.Tilemap>(4);
    private readonly List<UnityEngine.Tilemaps.Tilemap> quakeShoreLayers = new List<UnityEngine.Tilemaps.Tilemap>(2);
    // 시작한 물결이 준비(복제층 옮겨 쓰기)를 마치면 카메라 흔들림·진동을 낸다.
    private bool bQuakeFeedbackPending;
    // 히트스톱·섬광·여진·충격파 왜곡(QuakeImpactFeedback). 시작한 물결의 진원·피해 범위를 기억했다가 퍼지기 시작할 때 넘긴다.
    private readonly QuakeImpactFeedback quakeImpact = new QuakeImpactFeedback();
    private Vector3 quakeCenter;
    private float quakeStrikeRadius;
    // 흙먼지·파편·잔돌 낙하(QuakeDebrisEffect). 물결이 퍼지기 시작할 때 함께 시작한다.
    private readonly QuakeDebrisEffect quakeDebris = new QuakeDebrisEffect();
    // 전조(QuakeTileRipple.IsBuildingUp) 연출을 냈는지와, 카메라 잔떨림을 다시 건 뒤 지난 시간.
    private bool bQuakeBuildUpPlayed;
    private float quakeBuildUpShakeTimer;
    // 전조 동안 카메라 잔떨림을 이 간격마다 조금씩 세게 다시 건다(ShakeCamera는 건 세기에서 잦아드는 흔들림이라, 다시 걸어 차오르게 한다).
    private const float QuakeBuildUpShakeInterval = 0.06f;
    // 던전 맵이 깔려 있는지(DungeonStarted ~ TownStarted). 마을에서는 지진 물결을 내지 않는다.
    private bool bDungeonMapActive;


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

        quakeRipple.Initialize(quakeRippleSettings);
        quakeImpact.Initialize(quakeRippleSettings, transform);
        quakeDebris.Initialize(quakeRippleSettings, transform);

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
        StopQuakeEffects();
        quakeImpact.Release();
        quakeDebris.Release();

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
        // 새 맵을 깔기 전에 이전 맵 위의 물결 복제층을 비운다.
        StopQuakeEffects();

        tileMapGenerator.InitializeMapData();
        pathfindGridManager.Initialize(tileMapGenerator.GridWidth, tileMapGenerator.GridHeight);
        tileMapGenerator.GenerateMap();
        bDungeonMapActive = true;
    }

    private void Update()
    {
        // 게임 시간으로 진행한다(일시정지 중에는 물결도 멈춘다). 끝난 뒤 복제층을 나눠 비우는 동안에도 부른다.
        if (quakeRipple.IsBusy) quakeRipple.Tick(Time.deltaTime);

        // 전조(복제층을 다 옮겨 쓴 뒤 터지기 전): 화면이 서서히 어두워지고 카메라가 잘게 떨며 진동이 차오른다.
        if (bQuakeFeedbackPending && quakeRipple.IsBuildingUp) TickQuakeBuildUp();

        // 카메라 흔들림·진동은 물결이 실제로 퍼지기 시작하는 프레임에 낸다(복제층을 다 옮겨 쓰고 전조가 끝난 뒤 - QuakeTileRipple [시작 준비]).
        if (bQuakeFeedbackPending && false == quakeRipple.IsPreparing && false == quakeRipple.IsBuildingUp)
        {
            bQuakeFeedbackPending = false;
            if (quakeRipple.IsPlaying) PlayQuakeFeedback();
            // 전조만 내고 물결이 끊겼으면(맵이 바뀌는 등) 어두워진 화면을 되돌린다.
            else if (bQuakeBuildUpPlayed) PostProcessSettingsApplier.Instance?.StopQuakeDarken();
        }

        // 히트스톱은 실제 시간, 여진·충격파·흙먼지는 게임 시간으로 진행한다.
        quakeImpact.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        quakeDebris.Tick(Time.deltaTime);
    }

    /// <summary>
    /// 지진 전조 한 프레임. 첫 프레임에 화면 어두워짐·진동을 걸고, 카메라 잔떨림은 QuakeBuildUpShakeInterval마다 조금씩 세게 다시 건다
    /// (전조 끝에 다다를수록 buildUpShakeIntensity에 가까워진다). 터지는 순간의 흔들림(PlayQuakeFeedback)이 이어받는다.
    /// </summary>
    private void TickQuakeBuildUp()
    {
        float seconds = Mathf.Max(0.01f, quakeRippleSettings.buildUpSeconds);
        if (false == bQuakeBuildUpPlayed)
        {
            bQuakeBuildUpPlayed = true;
            quakeBuildUpShakeTimer = QuakeBuildUpShakeInterval;
            PostProcessSettingsApplier.Instance?.PlayQuakeDarken(quakeRippleSettings.buildUpDarkenExposure, seconds);
            Rumble.Play(EHapticEvent.QuakeBuildUp);
        }

        if (quakeRippleSettings.buildUpShakeIntensity <= 0f) return;

        quakeBuildUpShakeTimer += Time.deltaTime;
        if (quakeBuildUpShakeTimer < QuakeBuildUpShakeInterval) return;

        quakeBuildUpShakeTimer = 0f;
        float ramp = Mathf.Clamp01(quakeRipple.BuildUpProgress);
        float intensity = quakeRippleSettings.buildUpShakeIntensity * (0.35f + 0.65f * ramp);
        // 먼저 걸린 센 흔들림(불씨를 주운 도끼질 등)은 끊지 않는다.
        CameraMoveController.Instance?.ShakeCameraIfStronger(intensity, QuakeBuildUpShakeInterval * 2f);
    }

    /// <summary>지진 연출을 모두 바로 끝낸다(물결·여진·왜곡·흙먼지, 전조 어두워짐).</summary>
    private void StopQuakeEffects()
    {
        if (bQuakeFeedbackPending && bQuakeBuildUpPlayed) PostProcessSettingsApplier.Instance?.StopQuakeDarken();
        bQuakeFeedbackPending = false;
        bQuakeBuildUpPlayed = false;
        quakeRipple.Stop();
        quakeImpact.Stop();
        quakeDebris.Stop();
    }

    /// <summary>
    /// 지진 바닥 물결을 _center(보통 캐릭터 자리)에서 시작한다. 지금 화면 둘레의 바닥 칸이 동심원 물결로 출렁이고
    /// 칸마다 윗면 테두리가 드러난다. 카메라 흔들림·진동이 함께 나간다. 실제 지형·판정은 바뀌지 않는다(QuakeTileRipple).
    /// 던전 맵이 깔려 있지 않거나 화면에 바닥이 없으면 아무것도 하지 않고 false. 이미 도는 물결은 끊고 새로 시작한다.
    /// </summary>
    public bool PlayQuakeRipple(Vector3 _center, float _strikeRadius = 0f)
    {
        if (false == bDungeonMapActive) return false;

        Camera camera = Camera.main;
        if (null == camera || false == camera.orthographic) return false;

        tileMapGenerator.GetVisualTilemapsForRun(quakeFloorLayers, quakeWaterLayers, quakeShoreLayers);
        if (quakeFloorLayers.Count == 0 || null == quakeFloorLayers[0]) return false;

        Grid grid = quakeFloorLayers[0].layoutGrid;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector3 view = camera.transform.position;
        Rect viewRect = new Rect(view.x - halfWidth, view.y - halfHeight, halfWidth * 2f, halfHeight * 2f);

        _center.z = 0f;
        // 앞의 지진 연출(전조 어두워짐·여진·왜곡·흙먼지)이 남아 있으면 끊고 새로 시작한다.
        StopQuakeEffects();
        if (false == quakeRipple.Begin(grid, quakeFloorLayers, quakeWaterLayers, quakeShoreLayers, _center, viewRect, _strikeRadius)) return false;

        quakeCenter = _center;
        quakeStrikeRadius = _strikeRadius;
        bQuakeFeedbackPending = true;
        return true;
    }

    private void PlayQuakeFeedback()
    {
        // 아래로 한 번 세게 찍고(되튀며 잦아든다) 웅웅대는 흔들림으로 이어진다. 진동(Earthquake)도 같은 모양이다.
        // 흔들림 세기가 0이면 찍기만 한다(ShakeCameraKick이 흔들림을 걸지 않아 다른 흔들림을 끊지 않는다).
        if (quakeRippleSettings.cameraShakeIntensity > 0f || quakeRippleSettings.cameraKickDistance > 0f)
        {
            // 흔들림은 앞머리가 피해 범위 끝(화면 끝)에 닿을 때까지 이어진다(cameraShakeFollowFront).
            CameraMoveController.Instance?.ShakeCameraKick(Vector3.down, quakeRippleSettings.cameraKickDistance, quakeRippleSettings.cameraKickSeconds,
                quakeRippleSettings.cameraShakeIntensity, QuakeImpactFeedback.ShakeSeconds(quakeRippleSettings, quakeStrikeRadius));
        }
        PostProcessSettingsApplier.Instance?.PlayQuakeChromaticAberrationPulse();
        Rumble.Play(EHapticEvent.Earthquake);

        // 히트스톱·섬광을 걸고(섬광이 전조 어두워짐을 덮는다), 여진·충격파 왜곡을 이어 간다. 앞머리를 따라 흙먼지가 일고 위에서 잔돌이 떨어진다.
        quakeImpact.Play(quakeCenter, quakeStrikeRadius);
        PostProcessSettingsApplier.Instance?.StopQuakeDarken();
        quakeDebris.Play(quakeRipple);

        // 터지는 순간 화면을 살짝 확대했다 되돌린다("쾅" 찍는 한 방). 카메라 흔들림 옵션 비율만큼 줄이고 0%면 하지 않는다.
        float shakeScale = null != SettingsManager.Instance ? SettingsManager.Instance.Current.cameraShake / SettingsData.SLIDER_MAX : 1f;
        float zoom = 1f + (quakeRippleSettings.cameraZoomPunch - 1f) * shakeScale;
        if (zoom > 1.0001f)
        {
            CameraMoveController.Instance?.ZoomCamera(zoom, quakeRippleSettings.cameraZoomInSeconds, quakeRippleSettings.cameraZoomHoldSeconds, quakeRippleSettings.cameraZoomOutSeconds);
        }
    }

    public bool IsQuakeRipplePlaying => quakeRipple.IsPlaying;

    public bool IsQuakeRipplePreparing => quakeRipple.IsPreparing || quakeRipple.IsBuildingUp;

    public float QuakeRippleFrontSpeed => Mathf.Max(0.1f, quakeRippleSettings.frontSpeed);

    public void NotifyQuakeStruck(Transform _target) => quakeRipple.MarkStruck(_target);

    private void OnDisable()
    {
        // 지진 도중 꺼지면(씬 정리·에디터 플레이 종료) 물결을 끝낸다. 꺼진 동안 Tick이 돌지 않아 물체 출렁임(그리기 콜백)만 남고,
        // 나무 셰이더 전역값이 남으면 다음 씬·편집 화면의 나무가 계속 떤다. 물결이 없던 때도 전역값은 끈다.
        StopQuakeEffects();
        QuakeTileRipple.ClearTreeShake();
    }

    /// <summary>[로그라이크] 타일 한 칸의 월드 크기(아이소메트릭 마름모 가로·세로).</summary>
    public Vector2 TileCellSize => tileMapGenerator.CellSize;

    /// <summary>[로그라이크] 그리드 전체 칸 범위.</summary>
    public BoundsInt TileGridBoundsForRun => tileMapGenerator.GridBoundsForRun;

    /// <summary>[로그라이크] 칸 중심의 월드 위치.</summary>
    public Vector3 GetTileCellCenterWorldForRun(Vector3Int _cell) => tileMapGenerator.GetCellCenterWorldForRun(_cell);

    /// <summary>[로그라이크] 지금 맵 종류의 물 장식 프리팹(TileMapGenerator.WaterDecoPrefabsForRun).</summary>
    public List<DecoSpritePatternAnimator> WaterDecoPrefabsForRun => tileMapGenerator.WaterDecoPrefabsForRun;

    /// <summary>[로그라이크] 장식 자리가 _worldRect 안에 드는 깊은 물 칸(TileMapGenerator.GetDeepWaterCellsForRun).</summary>
    public void GetDeepWaterCellsForRun(Rect _worldRect, List<Vector3Int> _outCells) => tileMapGenerator.GetDeepWaterCellsForRun(_worldRect, _outCells);

    /// <summary>[로그라이크] 칸에 물 장식을 놓을 월드 위치(본편 물 장식과 같은 자리).</summary>
    public Vector3 GetWaterDecoWorldPosForRun(Vector3Int _cell) => tileMapGenerator.GetWaterDecoWorldPosForRun(_cell);

    /// <summary>[로그라이크] 바닥 타일 파도 연출이 다시 깔 시각 타일맵(TileMapGenerator.GetVisualTilemapsForRun).</summary>
    public void GetVisualTilemapsForRun(List<UnityEngine.Tilemaps.Tilemap> _floor, List<UnityEngine.Tilemaps.Tilemap> _water,
        List<UnityEngine.Tilemaps.Tilemap> _shore)
    {
        tileMapGenerator.GetVisualTilemapsForRun(_floor, _water, _shore);
    }

    // [로그라이크] 맵 생성 완료를 MapGeneratedSignal 대신 이 콜백으로 돌려받는 중인지.
    // 그 시그널을 받으면 InDungeonSystem이 던전 원정 흐름(포탈·NPC·카메라 하강)을 시작하므로 로그라이크는 피해 간다.
    private System.Action<List<Vector3>> roguelikeMapGeneratedCallback;

    /// <summary>
    /// [로그라이크 전용] 필드 맵을 만든다. 던전 진입(DungeonStarted)과 같은 순서로 생성하되, 완료 알림을
    /// 시그널이 아니라 _onGenerated 콜백으로만 보낸다. 본편 경로(DungeonReadySignal)는 바뀌지 않는다.
    /// </summary>
    public void GenerateRoguelikeField(ForestType _forestType, MapType _mapType, float _centerSafeZoneRadius,
        float _islandRadiusX, float _islandRadiusY, System.Action<List<Vector3>> _onGenerated,
        IslandShapeType _islandShape = IslandShapeType.Ellipse)
    {
        SetupForMapType(_forestType, _mapType);
        tileMapGenerator.SetCenterSafeZoneRadiusOverride(_centerSafeZoneRadius);
        tileMapGenerator.SetEllipseIslandOverride(_islandRadiusX, _islandRadiusY, _islandShape);

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

    /// <summary>
    /// [로그라이크 전용] 섬 한 화면(_viewRect, 월드)에 맞춰 구름·새 그림자를 깐다. GenerateRoguelikeField 뒤에 부른다.
    /// 구름은 화면 둘레에만 _cloudCnt개를 깔고 속도·불투명도를 로그라이크 값으로 쓴다. 본편 던전 경로는 바뀌지 않는다.
    /// </summary>
    public void SpawnRoguelikeEnvironmentObjs(Rect _viewRect, int _cloudCnt, float _cloudMinSpeed, float _cloudMaxSpeed, float _cloudAlpha)
    {
        if (environmentObjManager != null)
            environmentObjManager.SpawnRoguelikeEnvironmentObjs(_viewRect, _cloudCnt, _cloudMinSpeed, _cloudMaxSpeed, _cloudAlpha);
    }

    private void TilemapGenerated(List<Vector3> tilePositions)
    {
        if (null != roguelikeMapGeneratedCallback)
        {
            System.Action<List<Vector3>> callback = roguelikeMapGeneratedCallback;
            roguelikeMapGeneratedCallback = null;
            callback.Invoke(tilePositions);

            // 환경 오브젝트(구름·새 그림자)는 섬 화면을 아는 로그라이크가 SpawnRoguelikeEnvironmentObjs로 깐다.
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
        StopQuakeEffects();
        bDungeonMapActive = false;

        tileMapGenerator.ReleaseAllAnimatedObj();
        environmentObjManager.ReleaseAllObjs();
        environmentObjManager.SpawnTownEnvironmentObjs();
    }
}
