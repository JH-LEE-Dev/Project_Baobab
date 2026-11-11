using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;

public class TileMapGenerator : MonoBehaviour, ITilemapDataProvider
{
    public event Action<List<Vector3>> TilemapGeneratedEvent;
    public event Action<int, int> DeclareActiveTilesCntEvent;

    [Header("설정")]
    [SerializeField] private GameObject gridPrefab;
    [SerializeField] private int width = 150;
    [SerializeField] private int height = 150;
    [SerializeField] private float scale = 40f;
    [SerializeField] private int seed;
    [SerializeField] private float waterThreshold = 0.38f;

    [Header("외부 물 타일 설정")]
    [SerializeField] private int outerWaterDepth = 25;
    [SerializeField] private float baseMapRadius = 75f;
    // "바다" 오브젝트 밀도는 여기가 아니라 StageTileDataSO.WaterAnimatedObjSeaDensity /
    // WaterAnimatedOtherTypeObjSeaDensity에서 설정한다 (섬과 연결된 모든 물에 동일 적용,
    // 150x150 그리드 안의 바다 링과 ApplyOuterWaterTiles가 만드는 확장 바다 모두 포함).
    private bool[] isMainland;

    [Header("중앙 보호 구역 설정")]
    [SerializeField] private float centerSafeZoneRadius = 15f;
    // 본편 보호 구역 중심을 맵 중앙 칸에서 이만큼(칸 좌표) 옮긴다. 휴식 특성 모닥불이 놓이는 칸
    // (차량 StaminaRecoverCircle.centerOffset)에 맞춘 값이라, 모닥불 위치를 바꾸면 이 값도 같이 바꿔야 한다.
    // 로그라이크 타원 섬(bEllipseIsland)은 모닥불이 없으므로 예전처럼 맵 중앙 칸 그대로다.
    [SerializeField] private Vector2Int centerSafeZoneOffset = new Vector2Int(-1, -1);

    [Header("섬 내부 웅덩이 설정")]
    // 1 = 기존과 동일하게 생성되는 모든 웅덩이를 유지, 0 = 섬 내부 웅덩이를 전부 제거.
    // 해안선(외부 바다)과 연결되지 않은 고립된 물웅덩이만 대상으로 하므로 Shoreline 생성 로직에는 영향을 주지 않는다.
    [SerializeField, Range(0f, 1f)] private float innerPuddleDensity = 1f;
    // 이 칸 수 미만인 웅덩이는 innerPuddleDensity 설정과 무관하게 항상 잔디 타일로 메운다.
    [SerializeField] private int minPuddleTileCount = 4;
    private bool[] isOceanConnectedWater;
    private bool[] puddleVisited;
    private Queue<int> puddleQueue = new Queue<int>(2000);
    private List<int> puddleComponentBuffer = new List<int>(500);
    private List<int> puddleBorderBuffer = new List<int>(200);

    // ── 물 웅덩이별 물 장식 배치용 (PlaceWaterAnimatedObjects 참고) ──
    // 연결된 물 덩어리(웅덩이) 단위로 목표 개수를 계산해 그 안에서 자리를 고른다.
    // isOceanConnectedWater로 "바다(섬과 연결된 물)"와 "웅덩이(섬 내부 고립된 물)"를 구분해서
    // 서로 다른 밀도를 적용한다 — 거리(mapRadius) 기준이 아니라 실제 연결 여부 기준이다.
    private bool[] deepWaterTileFlags;
    private bool[] waterCompVisited;
    private Queue<int> waterCompQueue = new Queue<int>(2000);
    private List<int> waterCompCells = new List<int>(500);

    [Header("육지 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float sandDensity = 0.1f;
    [SerializeField, Range(0f, 1f)] private float grassDensity = 0.7f;
    // 해안선(Shoreline)이 아닌, 노이즈로 섬 내부에 흩뿌려진 모래 패치 중 이 칸 수 미만인 것은 항상 잔디로 메운다.
    // 해안선 모래에는 영향을 주지 않는다.
    [SerializeField] private int minSandPatchTileCount = 4;
    private bool[] forceGrassOverride;
    private bool[] sandPatchVisited;
    private Queue<int> sandPatchQueue = new Queue<int>(500);
    private List<int> sandPatchComponentBuffer = new List<int>(200);

    [System.Serializable]
    public struct MapTypeTileData
    {
        public MapType mapType;
        public StageTileDataSO tileData;
    }

    [Header("타일 데이터")]
    [SerializeField] private StageTileDataSO stageTileData;
    [SerializeField] private List<MapTypeTileData> mapTypeTileDatas;

    private AnimatedObjGenerator animatedObjGenerator;

    // // 외부 의존성
    private Tilemap groundTilemap;
    // 바닥 타일맵 렌더러의 원래 머티리얼(잔디 바람 머티리얼로 바꿔 끼웠다가 되돌릴 때 쓴다)
    private TilemapRenderer groundTilemapRenderer;
    private Material originalGroundMaterial;
    private Tilemap collisionTilemap;
    private Tilemap decoTilemap;
    // 바닥 장식 타일맵 렌더러의 원래 머티리얼(용암 빛 머티리얼로 바꿔 끼웠다가 되돌릴 때 쓴다)
    private TilemapRenderer decoTilemapRenderer;
    private Material originalDecoMaterial;
    private Tilemap waterDecoTilemap;
    private Tilemap bloomWaterDecoTilemap;
    private Tilemap bloomDecoTilemap;
    private TilemapRenderer bloomDecoTilemapRenderer;
    private Material originalBloomDecoMaterial;
    private Tilemap waterStencilTilemap;
    private Tilemap groundStencilTilemap;
    // 물·육지 스텐실 타일맵 렌더러의 원래 머티리얼(용암 위 그림자를 없애는 스테이지에서 바꿔 끼웠다가 되돌릴 때 쓴다)
    private TilemapRenderer waterStencilTilemapRenderer;
    private Material originalWaterStencilMaterial;
    private TilemapRenderer groundStencilTilemapRenderer;
    private Material originalGroundStencilMaterial;
    private Tilemap waterTilemap;
    private Tilemap waterCornerTilemap;
    private Tilemap waterCollisionTilemap;
    private Tilemap rockCollisionTilemap;

    private Grid grid;

    // // 내부 의존성 및 캐싱 필드
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private static readonly int HDRGlowOnlyID = Shader.PropertyToID("_HDRGlowOnly");
    private static readonly int HDRGlowRangeID = Shader.PropertyToID("_HDRGlowRange");
    private float[] noiseValues;
    private TileBase[] groundTiles;
    private TileBase[] collisionTiles;
    private TileBase[] waterTiles;
    private TileBase[] waterCornerTiles;
    private TileBase[] waterCollisionTiles;
    private TileBase[] rockCollisionTiles;
    private TileBase[] decoTilesToApply;
    private TileBase[] bloomDecoTilesToApply;
    // 나무가 들어서서 걷어낸 데코 타일 보관소. 나무가 사라지면 여기서 원래 타일을 되돌린다.
    // decoTilesToApply/bloomDecoTilesToApply는 "지금 타일맵에 깔려 있는 것"을 그대로 반영하므로,
    // 걷어낸 칸은 그쪽을 null로 비우고 원본을 이 배열로 옮긴다.
    private TileBase[] suppressedDecoTiles;
    private TileBase[] suppressedBloomDecoTiles;
    // UseGroundTilemapForDeco 스테이지에서 데코가 GroundTilemap의 타일을 대체한 칸의, 대체되기 전 타일.
    // IsGrassTile 같은 "이 칸이 잔디냐" 판정이 데코 타일을 보고 잔디가 아니라고 답하면 안 되기 때문에 남겨둔다.
    private TileBase[] decoReplacedGroundTiles;
    private TileBase[] waterDecoTilesToApply;
    private TileBase[] bloomWaterDecoTilesToApply;
    private TileBase[] waterStencilTiles;
    private TileBase[] groundStencilTiles;
    // 외부 바다 확장 영역 배열(5종, 약 41k칸) - 던전 진입마다 약 1.6MB를 새로 할당하지 않도록 크기가 같으면 재사용한다.
    private TileBase[] extWaterTilesCache;
    private TileBase[] extWaterCollisionTilesCache;
    private TileBase[] extWaterStencilTilesCache;
    private TileBase[] extBloomWaterDecoTilesCache;
    private TileBase[] extWaterDecoTilesCache;
    private int extTilesCacheSize = -1;
    // 셰이더 타일 애니메이션(켠 스테이지만). 켜지면 물·물가·스텐실 애니메이션 타일을 첫 프레임 그림 타일로 바꾼 배열(아래 버퍼)을 깐다.
    // 원본 배열(waterTiles 등)은 그대로 두어 물 판정·용암 빛 등 다른 코드가 보던 값이 바뀌지 않는다.
    private readonly StageTileFrameAnimation tileFrameAnimation = new StageTileFrameAnimation();
    private TileBase[] waterTilesFrameBuffer;
    private TileBase[] waterCornerTilesFrameBuffer;
    private TileBase[] waterStencilTilesFrameBuffer;
    private TileBase[] groundStencilTilesFrameBuffer;
    private TileBase[] extWaterTilesFrameBuffer;
    private TileBase[] extWaterStencilTilesFrameBuffer;
    private int[] cellToIndex;
    private bool[] isShoreline;
    private float halfCellY;

    // // 최적화 캐싱 배열
    private Vector3[] worldPosMap;
    // worldPosMap이 어떤 그리드 배치로 채워졌는지. 이 둘이 그대로면 다시 계산하지 않는다.
    private Vector3 worldPosMapOrigin;
    private Vector3 worldPosMapCellSize;
    private bool bWorldPosMapDirty = true;

    // // 재사용 컬렉션 (GC 최소화)
    private List<int> shorelineList = new List<int>(5000);
    private List<int> innerEdgesList = new List<int>(5000);
    private List<Vector3> grassPositions = new List<Vector3>(5000);
    private List<Vector3> delayedGrassPositions = new List<Vector3>(100);
    private List<Vector3> walkablePositions = new List<Vector3>(22500);

    private int playerIdx = -1;
    private int portalIdx = -1;

    // // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// [로그라이크 전용] 중앙 보호 구역 반경을 바꾼다. 로그라이크 필드는 플레이어 주변 작은 영역이라,
    /// 본편 값(15칸)으로는 필드 안에 나무를 심을 자리가 없다. 배열 크기와 무관한 값이라 그리드 구조는 그대로다.
    /// 로그라이크 세션의 인스톨러는 메인메뉴로 돌아갈 때 통째로 파괴되므로 본편에 값이 남지 않는다.
    /// </summary>
    public void SetCenterSafeZoneRadiusOverride(float _radius)
    {
        centerSafeZoneRadius = Mathf.Max(0f, _radius);
    }

    // [로그라이크 전용] 화면 기준 섬(타원 또는 마름모). 반지름이 0이면 꺼져 있다(본편).
    private float ellipseIslandRadiusX;
    private float ellipseIslandRadiusY;
    private IslandShapeType ellipseIslandShape = IslandShapeType.Ellipse;

    /// <summary>
    /// [로그라이크 전용] 노이즈 섬 대신 플레이어 시작 칸을 중심으로 한 섬(_shape: 타원 또는 마름모)을 만든다. 반지름은 월드 단위(화면 가로·세로)다.
    /// 섬 밖은 전부 물, 안은 전부 육지다(해안선·물 타일·장식은 기존 규칙 그대로 붙는다). 0 이하를 넣으면 꺼진다.
    /// 로그라이크 세션의 인스톨러는 메인메뉴로 돌아갈 때 파괴되므로 본편에 값이 남지 않는다.
    /// </summary>
    public void SetEllipseIslandOverride(float _radiusX, float _radiusY, IslandShapeType _shape = IslandShapeType.Ellipse)
    {
        bool bEnabled = _radiusX > 0f && _radiusY > 0f;
        ellipseIslandRadiusX = bEnabled ? _radiusX : 0f;
        ellipseIslandRadiusY = bEnabled ? _radiusY : 0f;
        ellipseIslandShape = bEnabled ? _shape : IslandShapeType.Ellipse;
    }

    private bool bEllipseIsland => ellipseIslandRadiusX > 0f && ellipseIslandRadiusY > 0f;

    /// <summary>셀 한 칸의 월드 크기(아이소메트릭 마름모의 가로·세로). 그리드가 없으면 (1, 0.5).</summary>
    public Vector2 CellSize => null != grid ? (Vector2)grid.cellSize : new Vector2(1f, 0.5f);

    /// <summary>[로그라이크 전용] 그리드 전체 칸 범위(0,0 ~ 가로·세로). 바닥 타일 파도 연출이 칸 단위로 다시 깔 범위를 고른다.</summary>
    public BoundsInt GridBoundsForRun => new BoundsInt(0, 0, 0, width, height, 1);

    /// <summary>[로그라이크 전용] 칸 중심의 월드 위치. 그리드가 없으면 zero.</summary>
    public Vector3 GetCellCenterWorldForRun(Vector3Int _cell) => null != grid ? grid.GetCellCenterWorld(_cell) : Vector3.zero;

    /// <summary>[로그라이크 전용] 지금 맵 종류의 물 장식 프리팹(본편 물에 뜨는 연잎·물결·용암 거품 등). 없으면 null.</summary>
    public List<DecoSpritePatternAnimator> WaterDecoPrefabsForRun => null != stageTileData ? stageTileData.WaterAnimatedObjPrefabs : null;

    /// <summary>
    /// [로그라이크 전용] 장식 자리(GetWaterDecoWorldPosForRun)가 _worldRect 안에 드는 깊은 물 칸을 모은다.
    /// 깊은 물은 둘레 8칸에 육지가 없는 물로, 본편 물 장식이 뜨는 칸과 같은 기준이다. 맵을 깐 뒤에 부른다.
    /// </summary>
    public void GetDeepWaterCellsForRun(Rect _worldRect, List<Vector3Int> _outCells)
    {
        _outCells.Clear();
        if (null == deepWaterTileFlags || null == worldPosMap) return;

        int size = Mathf.Min(deepWaterTileFlags.Length, worldPosMap.Length);
        for (int i = 0; i < size; i++)
        {
            if (false == deepWaterTileFlags[i]) continue;
            if (false == _worldRect.Contains(worldPosMap[i])) continue;

            _outCells.Add(new Vector3Int(i % width, i / width, 0));
        }
    }

    /// <summary>[로그라이크 전용] 칸에 물 장식을 놓을 월드 위치. 본편 물 장식과 같은 자리(칸 중심에서 반 칸 위)다.</summary>
    public Vector3 GetWaterDecoWorldPosForRun(Vector3Int _cell) => GetWorldPos(_cell.x + _cell.y * width);

    /// <summary>
    /// [로그라이크·본편 지진] 바닥 타일 파도 연출이 다시 깔 시각 타일맵. _floor는 바닥 블록(0번이 파도로 떨어지는 층), _water는 물 층
    /// (0번이 물 타일), _shore는 물 가장자리(해안선) 층이다. 지형 전환 파도는 한 칸의 세 묶음을 함께 띄운다.
    /// 본편 지진 물결(EnvironmentSystem.PlayQuakeRipple → QuakeTileRipple)도 같은 묶음을 복제층에 옮겨 그린다(읽기만 한다).
    /// 충돌 타일맵은 넣지 않는다(보이지 않고, 생성 결과가 곧바로 맞아야 한다).
    /// </summary>
    public void GetVisualTilemapsForRun(List<Tilemap> _floor, List<Tilemap> _water, List<Tilemap> _shore)
    {
        _floor.Clear();
        _water.Clear();
        _shore.Clear();

        if (null != groundTilemap) _floor.Add(groundTilemap);
        if (null != groundStencilTilemap) _floor.Add(groundStencilTilemap);
        if (null != decoTilemap) _floor.Add(decoTilemap);
        if (null != bloomDecoTilemap) _floor.Add(bloomDecoTilemap);

        if (null != waterTilemap) _water.Add(waterTilemap);
        if (null != waterStencilTilemap) _water.Add(waterStencilTilemap);
        if (null != waterDecoTilemap) _water.Add(waterDecoTilemap);
        if (null != bloomWaterDecoTilemap) _water.Add(bloomWaterDecoTilemap);

        if (null != waterCornerTilemap) _shore.Add(waterCornerTilemap);
    }

    public void SetupForMapType(MapType _mapType)
    {
        if (mapTypeTileDatas == null) return;

        for (int i = 0; i < mapTypeTileDatas.Count; i++)
        {
            if (mapTypeTileDatas[i].mapType == _mapType)
            {
                stageTileData = mapTypeTileDatas[i].tileData;
                return;
            }
        }
    }

    public void InitializeMapData()
    {
        // 공격 판정 루프 동안 모아 둔 나무 타일 쓰기를 루프가 끝날 때 반영하도록 등록한다(TreeTileWriteBatch 참고).
        if (null == cachedCombatTileFlush) cachedCombatTileFlush = FlushCombatTileBatch;
        TreeTileWriteBatch.Register(cachedCombatTileFlush);

        animatedObjGenerator = GetComponent<AnimatedObjGenerator>();
        if (animatedObjGenerator != null)
        {
            animatedObjGenerator.Initialize();
        }

        if (grid == null)
        {
            grid = Instantiate(gridPrefab, transform.position, Quaternion.identity).GetComponent<Grid>();
        }

        halfCellY = grid.cellSize.y * 0.5f;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>();
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i].name == "GroundTilemap") groundTilemap = maps[i];
            else if (maps[i].name == "ColliderTilemap") collisionTilemap = maps[i];
            else if (maps[i].name == "DecoTilemap") decoTilemap = maps[i];
            else if (maps[i].name == "BloomDecoTilemap") bloomDecoTilemap = maps[i];
            else if (maps[i].name == "WaterStencilTilemap") waterStencilTilemap = maps[i];
            else if (maps[i].name == "GroundStencilTilemap") groundStencilTilemap = maps[i];
            else if (maps[i].name == "WaterTilemap") waterTilemap = maps[i];
            else if (maps[i].name == "WaterDecoTilemap") waterDecoTilemap = maps[i];
            else if (maps[i].name == "BloomWaterDecoTilemap") bloomWaterDecoTilemap = maps[i];
            else if (maps[i].name == "WaterCornerTilemap") waterCornerTilemap = maps[i];
            else if (maps[i].name == "WaterColliderTilemap") waterCollisionTilemap = maps[i];
            else if (maps[i].name == "RockColliderTilemap" || maps[i].name == "RockCollisionTilemap") rockCollisionTilemap = maps[i];
        }

        int size = width * height;

        // 이 배열들은 던전에 들어올 때마다 다시 만들 필요가 없다. width/height가 직렬화 상수라
        // 크기가 변하지 않고, 내용도 전부 하류에서 초기화되기 때문이다.
        //   - TileBase 15종 + deepWaterTileFlags + cellToIndex : ApplyTiles()가 Array.Clear/-1 대입
        //   - noiseValues                                      : GenerateNoiseMap()이 전 칸을 덮어씀
        //   - isMainland / isShoreline                         : MarkMainland() / DetermineSpawns()
        //   - isOceanConnectedWater / puddleVisited            : ApplyInnerPuddleDensity()
        //   - forceGrassOverride / sandPatchVisited            : ApplySmallInlandSandPatches()
        //   - waterCompVisited                                 : PlaceWaterAnimatedObjects()
        // 190x190 기준 한 번에 약 5.2MB(TileBase 배열 15개만 4.3MB)라, 매번 새로 만들면 던전
        // 진입마다 그만큼이 그대로 가비지가 된다. 크기가 실제로 달라졌을 때만 다시 할당한다.
        if (noiseValues == null || noiseValues.Length != size)
        {
            noiseValues = new float[size];
            isMainland = new bool[size];
            groundTiles = new TileBase[size];
            collisionTiles = new TileBase[size];
            waterTiles = new TileBase[size];
            waterCornerTiles = new TileBase[size];
            waterCollisionTiles = new TileBase[size];
            rockCollisionTiles = new TileBase[size];
            decoTilesToApply = new TileBase[size];
            bloomDecoTilesToApply = new TileBase[size];
            suppressedDecoTiles = new TileBase[size];
            suppressedBloomDecoTiles = new TileBase[size];
            decoReplacedGroundTiles = new TileBase[size];
            waterDecoTilesToApply = new TileBase[size];
            bloomWaterDecoTilesToApply = new TileBase[size];
            waterStencilTiles = new TileBase[size];
            groundStencilTiles = new TileBase[size];
            cellToIndex = new int[size];
            for (int i = 0; i < size; i++) cellToIndex[i] = -1;
            isShoreline = new bool[size];
            isOceanConnectedWater = new bool[size];
            puddleVisited = new bool[size];
            deepWaterTileFlags = new bool[size];
            waterCompVisited = new bool[size];
            forceGrassOverride = new bool[size];
            sandPatchVisited = new bool[size];

            worldPosMap = new Vector3[size];
            bWorldPosMapDirty = true;
        }

        // worldPosMap은 그리드 배치에만 의존한다. 그리드는 씬 전환으로 파괴돼 던전마다 새로
        // 만들어지지만 언제나 같은 자리(transform.position)에 같은 셀 크기로 생성되므로 값이
        // 매번 똑같이 나온다. 그래도 그리드가 옮겨지는 경우까지 감수할 이유는 없으므로,
        // 기준점과 셀 크기를 기억해 두고 실제로 달라졌을 때만 다시 계산한다
        // (36,100번의 GetCellCenterWorld 네이티브 호출이다).
        Vector3 _gridOrigin = groundTilemap.CellToWorld(Vector3Int.zero);
        Vector3 _gridCellSize = grid.cellSize;
        if (bWorldPosMapDirty || _gridOrigin != worldPosMapOrigin || _gridCellSize != worldPosMapCellSize)
        {
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    int i = x + rowOffset;
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    worldPosMap[i] = groundTilemap.GetCellCenterWorld(cellPos) + new Vector3(0, halfCellY, 0);
                }
            }

            worldPosMapOrigin = _gridOrigin;
            worldPosMapCellSize = _gridCellSize;
            bWorldPosMapDirty = false;
        }

        seed = UnityEngine.Random.Range(1, 100000);
    }

    public void ReleaseAllAnimatedObj()
    {
        if (animatedObjGenerator != null)
        {
            animatedObjGenerator.ReleaseAllActive();
        }
    }

    public void GenerateMap()
    {
        if (groundTilemap == null || collisionTilemap == null || decoTilemap == null) return;

        ReleaseAllAnimatedObj();

        if (animatedObjGenerator != null && stageTileData != null)
        {
            animatedObjGenerator.SetPrefabs(stageTileData.AnimatedObjPrefabs, stageTileData.WaterAnimatedObjPrefabs, stageTileData.GrassStaticObjPrefabs, stageTileData.SandStaticObjPrefabs, stageTileData.ShorelineStaticObjPrefabs, stageTileData.ShorelineAnimatedObjPrefabs, stageTileData.WaterAnimatedOtherTypeObjPrefabs);
        }

        // DecoTilemap의 Y 오프셋은 스테이지마다 다르다(Stage1은 0, 나머지는 프리팹 값인 0.5).
        // Grid는 InitializeMapData가 한 번만 Instantiate해서 던전 사이에 재사용하므로, 값이 다른
        // 스테이지를 거쳐 들어와도 어긋나지 않도록 진입할 때마다 무조건 다시 써준다.
        if (stageTileData != null)
        {
            Vector3 decoLocalPos = decoTilemap.transform.localPosition;
            decoLocalPos.y = stageTileData.DecoTilemapYOffset;
            decoTilemap.transform.localPosition = decoLocalPos;
        }

        if (bloomDecoTilemap != null && stageTileData != null)
        {
            TilemapRenderer tr = bloomDecoTilemap.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                var mpb = new MaterialPropertyBlock();
                tr.GetPropertyBlock(mpb);
                mpb.SetFloat(HDRIntensityID, stageTileData.BloomDecoHDRIntensity);
                // Grid는 스테이지 사이에 재사용되므로 끈 스테이지도 매번 0을 써서 예전처럼 그림 전체에 HDR을 주게 한다.
                mpb.SetFloat(HDRGlowOnlyID, stageTileData.BloomDecoGlowOnly ? 1f : 0f);
                Vector2 glowRange = stageTileData.BloomDecoGlowRange;
                mpb.SetVector(HDRGlowRangeID, new Vector4(glowRange.x, glowRange.y, 0f, 0f));
                tr.SetPropertyBlock(mpb);
            }
        }

        if (bloomWaterDecoTilemap != null && stageTileData != null)
        {
            TilemapRenderer tr = bloomWaterDecoTilemap.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                var mpb = new MaterialPropertyBlock();
                tr.GetPropertyBlock(mpb);
                mpb.SetFloat(HDRIntensityID, stageTileData.BloomWaterDecoHDRIntensity);
                tr.SetPropertyBlock(mpb);
            }
        }

        if (waterTilemap != null && stageTileData != null)
        {
            TilemapRenderer tr = waterTilemap.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                var mpb = new MaterialPropertyBlock();
                tr.GetPropertyBlock(mpb);
                if (stageTileData.UseWaterTileBloom)
                {
                    mpb.SetFloat(HDRIntensityID, stageTileData.WaterTileHDRIntensity);
                }
                else
                {
                    mpb.SetFloat(HDRIntensityID, 1.0f);
                }
                tr.SetPropertyBlock(mpb);
            }
        }

        // 스테이지 분위기 연출(돌풍·물 반짝임) 셰이더 전역값. 렌더링만 바뀌고, 꺼진 스테이지는 0으로 비운다.
        StageAmbienceShaderGlobals.Apply(stageTileData);
        ApplyGroundMaterialForStage();
        ApplyDecoMaterialForStage();
        ApplyBloomDecoMaterialForStage();
        ApplyStencilMaterialsForStage();

        // 이전 던전의 초기 스폰 코루틴이 아직 돌던 중(스폰 배치가 열린 채)에 새 맵을 만드는 경우,
        // 모아둔 충돌 타일 심기/데코 걷어내기는 옛 맵에 대한 것이므로 새 맵 위에 반영되면 안 된다.
        // 예전(나무마다 즉시 SetTile)에는 아래 ClearAllTiles/ApplyTiles가 그 흔적을 덮어썼으므로, 같은 결과가 되도록 버린다.
        DiscardTreeSpawnTileBatch();

        groundTilemap.ClearAllTiles();
        collisionTilemap.ClearAllTiles();
        decoTilemap.ClearAllTiles();
        if (bloomDecoTilemap != null) bloomDecoTilemap.ClearAllTiles();
        if (waterTilemap != null) waterTilemap.ClearAllTiles();
        if (waterDecoTilemap != null) waterDecoTilemap.ClearAllTiles();
        if (bloomWaterDecoTilemap != null) bloomWaterDecoTilemap.ClearAllTiles();
        if (waterCollisionTilemap != null) waterCollisionTilemap.ClearAllTiles();
        if (rockCollisionTilemap != null) rockCollisionTilemap.ClearAllTiles();
        if (waterStencilTilemap != null) waterStencilTilemap.ClearAllTiles();
        if (groundStencilTilemap != null) groundStencilTilemap.ClearAllTiles();

        GenerateNoiseMap();
        DetermineSpawns();
        ApplySmallInlandSandPatches();
        bWaterDecoGridReady = false;
        ApplyTiles();
        ApplyOuterWaterTiles();

        // 인자 순서 주의: (잔디 타일 수, 걷기 가능 타일 수)다.
        // 나무는 잔디에만 심기고(TilemapGeneratedEvent가 넘기는 grassPositions가 곧 스폰 후보다)
        // 동물은 걷기 가능한 곳이면 어디든 돌아다니므로, DensityManager는 나무 예산을 grassTileCnt로,
        // 동물 예산을 walkableTilesCnt로 계산한다. 예전엔 이 두 인자가 뒤집혀 있어서 나무 수가
        // 잔디가 아니라 걷기 가능 타일(모래·해안선·안전구역까지 포함) 기준으로 잡혔고,
        // 그만큼(실측 약 1.24배) 설계보다 빽빽했다. 밀도 계수는 그 1.24를 흡수해 조정해 두었으므로
        // 여기 순서를 다시 건드리면 눈에 보이는 밀도가 함께 달라진다.
        DeclareActiveTilesCntEvent?.Invoke(grassPositions.Count, walkablePositions.Count);
        TilemapGeneratedEvent?.Invoke(grassPositions);

    }

    public Vector3 GetPlayerSpawnPosition() => GetWorldPos(playerIdx);

    public Vector3 GetPortalSpawnPosition() => GetWorldPos(portalIdx);

    public List<Vector3> GetGrassTileWorldPositions() => grassPositions;

    /// <summary>
    /// 스폰 지점(플레이어/포탈) 반경 안이라 진입 직후에는 나무를 심지 않는 칸들.
    /// 안전구역(centerSafeZoneRadius) 안쪽은 여기에도 들어오지 않는다 - 그쪽은 영구 배제다.
    ///
    /// 예전엔 이 목록을 5초 뒤 grassPositions에 합쳐서 개방하려 했는데, 유일한 소비자인
    /// SpawnInitialTrees가 던전 진입 시점에 grassPositions를 availablePositions로 이미
    /// 복사해 간 뒤라 아무도 다시 읽지 않았다(= 이 칸들이 영영 개방되지 않았다).
    /// 지금은 InDungeonObjectManager가 이 목록을 직접 받아 자기 스폰 후보에 합류시킨다.
    /// </summary>
    public List<Vector3> GetDelayedGrassTileWorldPositions() => delayedGrassPositions;

    public List<Vector3> GetWalkableTileWorldPositions() => walkablePositions;

    public int GridWidth => width;
    public int GridHeight => height;

    public bool IsWalkable(Vector3Int _cellPos)
    {
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return false;
        return cellToIndex[_cellPos.x + _cellPos.y * width] != -1;
    }

    public bool IsWaterTile(Vector3Int _cellPos)
    {
        return IsWater(_cellPos.x, _cellPos.y);
    }

    // 생성 시 각 셀에 배치된 실제 타일(groundTiles)이 StageTileData의 GrassTiles 목록에 속하는지로 판정한다
    // (모래/그 외 타일이면 false → 발소리 시스템 등에서 "일반 바닥"으로 취급).
    public bool IsGrassTile(Vector3Int _cellPos)
    {
        if (groundTiles == null) return false;
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return false;

        int flatIdx = _cellPos.x + _cellPos.y * width;

        // 데코가 그라운드 타일을 대체한 칸(UseGroundTilemapForDeco)은 대체 전 타일로 판정한다.
        // 그러지 않으면 Stage1에서 데코가 깔린 잔디 칸이 전부 "잔디 아님"이 되어 발소리가 흙으로 바뀐다.
        TileBase tile = decoReplacedGroundTiles[flatIdx] != null ? decoReplacedGroundTiles[flatIdx] : groundTiles[flatIdx];
        if (tile == null || stageTileData == null || stageTileData.GrassTiles == null) return false;

        return stageTileData.GrassTiles.Contains(tile);
    }

    // 상하좌우+대각선 8방향 오프셋. 매 프레임 새로 할당하지 않도록 static readonly로 한 번만 생성한다.
    private static readonly Vector3Int[] EightDirectionOffsets = new Vector3Int[]
    {
        new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
    };

    /// <summary>
    /// _cellPos의 8방향 이웃 중 물(용암 재스킨 포함) 타일이 있으면 stageTileData에 설정된
    /// 초당 스태미나 추가 소모량을 반환한다. 해당 스테이지에 설정값이 없으면(0) 검사 자체를 생략한다.
    /// </summary>
    public float GetHazardStaminaDrainPerSecond(Vector3Int _cellPos)
    {
        if (stageTileData == null) return 0f;

        float drainPerSecond = stageTileData.WaterHazardStaminaDrainPerSecond;
        if (drainPerSecond <= 0f) return 0f;

        for (int i = 0; i < EightDirectionOffsets.Length; i++)
        {
            Vector3Int neighborCell = _cellPos + EightDirectionOffsets[i];
            if (IsWater(neighborCell.x, neighborCell.y))
            {
                return drainPerSecond;
            }
        }

        return 0f;
    }

    public float TreeHeatStaminaDamage => stageTileData != null ? stageTileData.TreeHeatStaminaDamage : 0f;

    public bool HasRockDeco(Vector3Int _cellPos)
    {
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return false;
        return rockCollisionTiles[_cellPos.x + _cellPos.y * width] != null;
    }

    public Vector3Int WorldToCell(Vector3 _worldPos)
    {
        if (groundTilemap == null) return Vector3Int.zero;

        Vector3 adjustedPos = _worldPos;
        adjustedPos.y -= halfCellY;
        return groundTilemap.WorldToCell(adjustedPos);
    }

    public Vector3 CellToWorld(Vector3Int _cellPos)
    {
        if (groundTilemap == null) return Vector3.zero;
        return groundTilemap.GetCellCenterWorld(_cellPos) + new Vector3(0, halfCellY, 0);
    }

    public void SetTreeCollisionTile(Vector3 _worldPos)
    {
        if (collisionTilemap == null || stageTileData == null || stageTileData.TreeCollisionTile == null) return;

        Vector3 adjustedPos = _worldPos;
        adjustedPos.y -= halfCellY;

        Vector3Int cellPos = collisionTilemap.WorldToCell(adjustedPos);

        // 공격 판정 루프에서 모아 둔 지우기가 있으면 먼저 반영해 "지우고 → 심는" 순서를 지킨다
        FlushCombatTileWritesForOrdering();

        // 스폰 배치 중에는 Tilemap 쓰기만 미룬다. 아래 cellToIndex/walkablePositions 부기는
        // 배치 여부와 상관없이 지금 그대로 반영되므로 호출부가 보는 상태는 달라지지 않는다.
        if (true == bBatchingTreeTileSets)
        {
            pendingTreeTileSets.Add(cellPos);
        }
        else
        {
            collisionTilemap.SetTile(cellPos, stageTileData.TreeCollisionTile);
        }

        if (cellPos.x < 0 || cellPos.x >= width || cellPos.y < 0 || cellPos.y >= height) return;

        int flatIdx = cellPos.x + cellPos.y * width;
        int index = cellToIndex[flatIdx];

        if (index != -1)
        {
            int lastIdx = walkablePositions.Count - 1;
            Vector3 lastPos = walkablePositions[lastIdx];
            Vector3Int lastCellPos = WorldToCell(lastPos);

            walkablePositions[index] = lastPos;

            if (lastCellPos.x >= 0 && lastCellPos.x < width && lastCellPos.y >= 0 && lastCellPos.y < height)
            {
                cellToIndex[lastCellPos.x + lastCellPos.y * width] = index;
            }

            walkablePositions.RemoveAt(lastIdx);
            cellToIndex[flatIdx] = -1;
        }
    }

    /// <summary>
    /// 배치 구간 동안 지울 셀을 모아두는 버퍼. Tilemap.SetTile은 호출마다 네이티브 경계를
    /// 넘나들어서, 던전 정리처럼 수천 번을 한 프레임에 부르면 그것만으로 프레임이 눌린다.
    /// 좌표가 흩어져 있어 연속 영역을 요구하는 SetTilesBlock은 쓸 수 없고(사각 영역 안의
    /// 나무 아닌 충돌 타일까지 지워버린다), 임의 좌표를 받는 SetTiles로 한 번에 처리한다.
    /// </summary>
    private readonly List<Vector3Int> pendingTreeTileClears = new List<Vector3Int>(256);
    private bool bBatchingTreeTileClears = false;

    /// <summary>
    /// 초기 스폰 배치 구간 동안 심을 충돌 타일 셀과 걷어낼 데코 셀을 모아두는 버퍼.
    /// 수천 그루를 프레임 분산으로 심는 동안 나무마다 SetTile을 부르면 호출 비용과 TilemapCollider2D 재빌드 예약이
    /// 스폰 프레임마다 반복된다. 스폰 중에는 던전이 가려져 있고 캐릭터도 비활성이라(InDungeonSystem.OnTreesReady에서 켠다)
    /// 타일맵을 읽는 쪽이 없으므로 End에서 SetTiles 한 번으로 모아 반영해도 결과가 같다.
    /// </summary>
    private readonly List<Vector3Int> pendingTreeTileSets = new List<Vector3Int>(2560);
    private readonly List<Vector3Int> pendingDecoClearCells = new List<Vector3Int>(256);
    private readonly List<Vector3Int> pendingBloomDecoClearCells = new List<Vector3Int>(256);
    private bool bBatchingTreeTileSets = false;

    // 데코 복원도 같은 이유로 모아서 쓴다. 충돌 타일 지우기와 달리 되돌릴 타일이 칸마다 달라서
    // 좌표와 타일을 짝지어 보관한다(SetTiles가 두 배열을 같은 인덱스로 읽는다).
    private readonly List<Vector3Int> pendingDecoRestoreCells = new List<Vector3Int>(256);
    private readonly List<TileBase> pendingDecoRestoreTiles = new List<TileBase>(256);
    private readonly List<Vector3Int> pendingBloomDecoRestoreCells = new List<Vector3Int>(64);
    private readonly List<TileBase> pendingBloomDecoRestoreTiles = new List<TileBase>(64);

    public void BeginTreeSpawnTileBatch()
    {
        // 중첩 호출 방어: 이미 열려 있으면 모아둔 것을 먼저 반영하고 새로 연다
        if (true == bBatchingTreeTileSets) FlushTreeSpawnTileBatch();

        bBatchingTreeTileSets = true;
        pendingTreeTileSets.Clear();
        pendingDecoClearCells.Clear();
        pendingBloomDecoClearCells.Clear();
    }

    public void EndTreeSpawnTileBatch()
    {
        if (false == bBatchingTreeTileSets) return;

        bBatchingTreeTileSets = false;
        FlushTreeSpawnTileBatch();
    }

    // 모아둔 스폰 쓰기를 반영하지 않고 버린다(맵 재생성 직전 전용).
    private void DiscardTreeSpawnTileBatch()
    {
        bBatchingTreeTileSets = false;
        pendingTreeTileSets.Clear();
        pendingDecoClearCells.Clear();
        pendingBloomDecoClearCells.Clear();
    }

    // 스폰 배치에 모아둔 충돌 타일 심기와 데코 걷어내기를 Tilemap에 반영한다(배치 플래그는 건드리지 않는다).
    private void FlushTreeSpawnTileBatch()
    {
        int count = pendingTreeTileSets.Count;
        if (collisionTilemap != null && count > 0 && stageTileData != null && stageTileData.TreeCollisionTile != null)
        {
            // SetTiles는 positionArray.Length만큼 순회하므로 정확한 길이의 배열이어야 한다(정리 배치와 같은 이유로 진입당 1회 할당을 감수한다).
            Vector3Int[] cells = pendingTreeTileSets.ToArray();
            TileBase[] tiles = new TileBase[count];
            TileBase collisionTile = stageTileData.TreeCollisionTile;
            for (int i = 0; i < count; i++) tiles[i] = collisionTile;

            collisionTilemap.SetTiles(cells, tiles);
        }
        pendingTreeTileSets.Clear();

        FlushPendingDecoClears(decoTilemap, pendingDecoClearCells);
        FlushPendingDecoClears(bloomDecoTilemap, pendingBloomDecoClearCells);
    }

    private static void FlushPendingDecoClears(Tilemap _tilemap, List<Vector3Int> _cells)
    {
        int count = _cells.Count;
        if (_tilemap != null && count > 0)
        {
            // null로 채운 배열 = 해당 칸을 전부 지운다
            _tilemap.SetTiles(_cells.ToArray(), new TileBase[count]);
        }

        _cells.Clear();
    }

    public void BeginTreeCollisionTileBatch()
    {
        // 스폰 배치가 열려 있는 상태에서 정리가 시작되면 먼저 심어둔 것을 반영해 "심고 → 지우는" 순서를 지킨다
        if (true == bBatchingTreeTileSets) FlushTreeSpawnTileBatch();

        // 공격 판정 루프가 모아 둔 쓰기가 남아 있으면 아래 Clear로 버리지 않도록 먼저 반영한다(실제로는 겹칠 일이 없다 -
        // 던전 정리는 공격 판정 루프 밖에서만 일어난다).
        FlushCombatTileWritesForOrdering();

        bBatchingTreeTileClears = true;
        pendingTreeTileClears.Clear();
        pendingDecoRestoreCells.Clear();
        pendingDecoRestoreTiles.Clear();
        pendingBloomDecoRestoreCells.Clear();
        pendingBloomDecoRestoreTiles.Clear();
    }

    public void EndTreeCollisionTileBatch()
    {
        if (false == bBatchingTreeTileClears) return;

        bBatchingTreeTileClears = false;

        FlushPendingTreeTileClears();
    }

    /// <summary>
    /// 모아 둔 나무 충돌 타일 지우기와 데코 되돌리기를 Tilemap에 반영한다. 던전 정리 배치의 End와 공격 판정 루프의 끝이 같이 쓴다.
    /// 같은 타일맵에 대한 쓰기는 모은 순서 그대로 SetTiles 한 번에 들어가므로, 하나씩 SetTile 했을 때와 결과가 같다.
    /// </summary>
    private void FlushPendingTreeTileClears()
    {
        using var _profile = ChopProfilerMarkers.TreeTileFlush.Auto();

        int count = pendingTreeTileClears.Count;
        if (collisionTilemap != null && count > 0)
        {
            // SetTiles는 positionArray.Length만큼 순회하므로 정확한 길이의 배열이어야 한다(남는 칸의 옛 좌표까지 null로 밀면
            // 나무와 무관한 충돌 타일을 지운다). 공격 판정 루프마다 새로 할당하지 않도록 길이별로 재사용한다.
            Vector3Int[] cells = GetExactCellArray(pendingTreeTileClears);
            TileBase[] emptyTiles = GetExactEmptyTileArray(count);

            collisionTilemap.SetTiles(cells, emptyTiles);
        }
        pendingTreeTileClears.Clear();

        // 충돌 타일이 없거나 지울 게 없어도 데코 복원은 반드시 반영해야 하므로 위에서 early return하지 않는다.
        FlushPendingDecoRestores(decoTilemap, pendingDecoRestoreCells, pendingDecoRestoreTiles);
        FlushPendingDecoRestores(bloomDecoTilemap, pendingBloomDecoRestoreCells, pendingBloomDecoRestoreTiles);
    }

    private void FlushPendingDecoRestores(Tilemap _tilemap, List<Vector3Int> _cells, List<TileBase> _tiles)
    {
        if (_tilemap != null && _cells.Count > 0)
        {
            TileBase[] tiles = GetExactTileArray(_tiles);
            _tilemap.SetTiles(GetExactCellArray(_cells), tiles);
            // 다음에 같은 길이로 재사용될 때까지 타일 참조를 붙들고 있지 않게 비운다
            Array.Clear(tiles, 0, tiles.Length);
        }

        _cells.Clear();
        _tiles.Clear();
    }

    // ── 공격 판정 루프 묶음(TreeTileWriteBatch) ─────────────────────────────────────

    private Action cachedCombatTileFlush;

    private bool HasPendingTreeTileClears =>
        pendingTreeTileClears.Count > 0 || pendingDecoRestoreCells.Count > 0 || pendingBloomDecoRestoreCells.Count > 0;

    /// <summary>TreeTileWriteBatch의 가장 바깥 End에서 불린다. 던전 정리 배치가 열려 있으면 그쪽 End가 반영한다.</summary>
    private void FlushCombatTileBatch()
    {
        if (true == bBatchingTreeTileClears) return;

        FlushPendingTreeTileClears();
    }

    /// <summary>
    /// 공격 판정 루프가 모아 둔 지우기·되돌리기가 있으면 지금 반영한다. 같은 칸에 대한 다른 쓰기(나무 심기, 데코 걷어내기)가
    /// 그 뒤에 들어올 때 순서가 뒤집히지 않게 하기 위한 것이다. 던전 정리 배치 중에는 예전 동작 그대로 두기 위해 손대지 않는다.
    /// </summary>
    private void FlushCombatTileWritesForOrdering()
    {
        if (true == bBatchingTreeTileClears) return;
        if (false == HasPendingTreeTileClears) return;

        FlushPendingTreeTileClears();
    }

    // SetTiles에 넘길 정확한 길이의 배열을 길이별로 재사용한다(SetTiles는 넘긴 배열을 수정하지 않는다).
    // 공격 판정 루프 한 번에 쓰러지는 나무 수 정도의 작은 길이만 보관한다. 던전 정리(수천 칸)처럼 큰 길이는 드물고
    // 길이가 매번 달라 보관해 봐야 쌓이기만 하므로 예전처럼 그때그때 만든다.
    private const int MaxCachedExactArrayLength = 256;
    private readonly Dictionary<int, Vector3Int[]> exactCellArrays = new Dictionary<int, Vector3Int[]>();
    private readonly Dictionary<int, TileBase[]> exactTileArrays = new Dictionary<int, TileBase[]>();
    private readonly Dictionary<int, TileBase[]> exactEmptyTileArrays = new Dictionary<int, TileBase[]>();

    private Vector3Int[] GetExactCellArray(List<Vector3Int> _src)
    {
        int count = _src.Count;
        if (count > MaxCachedExactArrayLength) return _src.ToArray();

        if (false == exactCellArrays.TryGetValue(count, out Vector3Int[] arr))
        {
            arr = new Vector3Int[count];
            exactCellArrays.Add(count, arr);
        }

        _src.CopyTo(arr);
        return arr;
    }

    private TileBase[] GetExactTileArray(List<TileBase> _src)
    {
        int count = _src.Count;
        if (count > MaxCachedExactArrayLength) return _src.ToArray();

        if (false == exactTileArrays.TryGetValue(count, out TileBase[] arr))
        {
            arr = new TileBase[count];
            exactTileArrays.Add(count, arr);
        }

        _src.CopyTo(arr);
        return arr;
    }

    // 전부 null인 배열 = 해당 칸을 지운다. 내용을 쓰지 않으므로 그대로 재사용한다.
    private TileBase[] GetExactEmptyTileArray(int _count)
    {
        if (_count > MaxCachedExactArrayLength) return new TileBase[_count];

        if (false == exactEmptyTileArrays.TryGetValue(_count, out TileBase[] arr))
        {
            arr = new TileBase[_count];
            exactEmptyTileArrays.Add(_count, arr);
        }

        return arr;
    }

    private void OnDestroy()
    {
        if (null != cachedCombatTileFlush) TreeTileWriteBatch.Unregister(cachedCombatTileFlush);
        StageAmbienceShaderGlobals.Clear();
        tileFrameAnimation.End();
    }

    // 돌풍이 켜진 스테이지만 바닥 타일맵을 잔디 바람 머티리얼로 그리고, 그 밖에는 원래 머티리얼로 되돌린다(렌더링 전용).
    private void ApplyGroundMaterialForStage()
    {
        if (null == groundTilemapRenderer)
        {
            groundTilemapRenderer = groundTilemap.GetComponent<TilemapRenderer>();
            if (null == groundTilemapRenderer) return;
            originalGroundMaterial = groundTilemapRenderer.sharedMaterial;
        }

        Material overrideMaterial = StageAmbienceShaderGlobals.GetGroundMaterialOverride(stageTileData);
        Material target = null != overrideMaterial ? overrideMaterial : originalGroundMaterial;
        if (groundTilemapRenderer.sharedMaterial != target)
        {
            groundTilemapRenderer.sharedMaterial = target;
        }
    }

    // 용암 빛·숲의 숨결·결정 장식 숨쉬기가 켜진 스테이지만 바닥 장식(돌멩이 등) 타일맵을 그 머티리얼로 그리고, 그 밖에는 원래 머티리얼로 되돌린다(렌더링 전용).
    private void ApplyDecoMaterialForStage()
    {
        if (null == decoTilemapRenderer)
        {
            if (null == decoTilemap) return;
            decoTilemapRenderer = decoTilemap.GetComponent<TilemapRenderer>();
            if (null == decoTilemapRenderer) return;
            originalDecoMaterial = decoTilemapRenderer.sharedMaterial;
        }

        Material overrideMaterial = StageAmbienceShaderGlobals.GetDecoMaterialOverride(stageTileData);
        Material target = null != overrideMaterial ? overrideMaterial : originalDecoMaterial;
        if (decoTilemapRenderer.sharedMaterial != target)
        {
            decoTilemapRenderer.sharedMaterial = target;
        }
    }

    // 용암 위 나무 그림자를 없애는 스테이지만 물·육지 스텐실 타일맵을 그 머티리얼로 그리고, 그 밖에는 원래 머티리얼로 되돌린다(렌더링 전용).
    private void ApplyStencilMaterialsForStage()
    {
        if (null == waterStencilTilemapRenderer && null != waterStencilTilemap)
        {
            waterStencilTilemapRenderer = waterStencilTilemap.GetComponent<TilemapRenderer>();
            if (null != waterStencilTilemapRenderer) originalWaterStencilMaterial = waterStencilTilemapRenderer.sharedMaterial;
        }

        if (null == groundStencilTilemapRenderer && null != groundStencilTilemap)
        {
            groundStencilTilemapRenderer = groundStencilTilemap.GetComponent<TilemapRenderer>();
            if (null != groundStencilTilemapRenderer) originalGroundStencilMaterial = groundStencilTilemapRenderer.sharedMaterial;
        }

        // 한쪽 렌더러라도 없으면 바꾸지 않는다(물 쪽만 바뀌면 육지 위 그림자까지 사라진다).
        bool useOverride = StageAmbienceShaderGlobals.TryGetStencilMaterialOverrides(stageTileData, out Material waterStencil, out Material groundStencil)
            && null != waterStencilTilemapRenderer && null != groundStencilTilemapRenderer;

        SetSharedMaterial(waterStencilTilemapRenderer, useOverride ? waterStencil : originalWaterStencilMaterial);
        SetSharedMaterial(groundStencilTilemapRenderer, useOverride ? groundStencil : originalGroundStencilMaterial);
    }

    private static void SetSharedMaterial(TilemapRenderer _renderer, Material _material)
    {
        if (null != _renderer && null != _material && _renderer.sharedMaterial != _material)
        {
            _renderer.sharedMaterial = _material;
        }
    }

    // 꽃버섯 반짝임이 켜진 스테이지만 블룸 데코 타일맵을 반짝임 머티리얼로 그리고, 그 밖에는 원래 머티리얼로 되돌린다(렌더링 전용).
    private void ApplyBloomDecoMaterialForStage()
    {
        if (null == bloomDecoTilemapRenderer)
        {
            if (null == bloomDecoTilemap) return;
            bloomDecoTilemapRenderer = bloomDecoTilemap.GetComponent<TilemapRenderer>();
            if (null == bloomDecoTilemapRenderer) return;
            originalBloomDecoMaterial = bloomDecoTilemapRenderer.sharedMaterial;
        }

        Material overrideMaterial = StageAmbienceShaderGlobals.GetBloomDecoMaterialOverride(stageTileData);
        Material target = null != overrideMaterial ? overrideMaterial : originalBloomDecoMaterial;
        if (bloomDecoTilemapRenderer.sharedMaterial != target)
        {
            bloomDecoTilemapRenderer.sharedMaterial = target;
        }
    }

    public void ClearTreeCollisionTile(Vector3 _worldPos)
    {
        if (collisionTilemap == null) return;

        Vector3 adjustedPos = _worldPos;
        adjustedPos.y -= halfCellY;

        Vector3Int cellPos = collisionTilemap.WorldToCell(adjustedPos);

        // 스폰 배치 구간 안에서 지우기가 들어오면 모아둔 심기를 먼저 반영해 순서를 지킨다
        if (true == bBatchingTreeTileSets) FlushTreeSpawnTileBatch();

        // 배치 중에는 Tilemap 쓰기만 미룬다. 아래 cellToIndex/walkablePositions 부기는
        // 배치 여부와 상관없이 지금 그대로 반영되므로 호출부가 보는 상태는 달라지지 않는다.
        // 던전 정리 배치뿐 아니라 공격 판정 루프(TreeTileWriteBatch)도 같은 버퍼에 모은다.
        if (true == bBatchingTreeTileClears || true == TreeTileWriteBatch.IsActive)
        {
            pendingTreeTileClears.Add(cellPos);
        }
        else
        {
            collisionTilemap.SetTile(cellPos, null);
        }

        if (cellPos.x < 0 || cellPos.x >= width || cellPos.y < 0 || cellPos.y >= height) return;

        int flatIdx = cellPos.x + cellPos.y * width;
        if (cellToIndex[flatIdx] == -1)
        {
            cellToIndex[flatIdx] = walkablePositions.Count;
            walkablePositions.Add(_worldPos);
        }
    }

    /// <summary>
    /// 나무가 들어선 칸의 데코 타일을 걷어낸다. 데코는 ApplyTiles가 나무 스폰보다 먼저 깔고,
    /// 나무 후보 칸(grassPositions)은 바위 데코(_hasRockDeco)만 걸러내므로 잔디/그라운드 데코가
    /// 있는 칸에도 나무가 들어선다 - 그대로 두면 나무 밑동에 데코가 겹쳐 보인다.
    ///
    /// 걷어낸 타일은 버리지 않고 suppressed 배열에 옮겨 두고, 나무가 사라질 때
    /// RestoreDecoTileForTree가 되돌린다. 버리면 나무가 자라고 죽기를 반복하는 동안 잔디
    /// 데코가 단조롭게 줄어들어 오래 플레이한 맵만 휑해진다.
    ///
    /// 데코와 블룸 데코는 ApplyTiles에서 한 칸에 둘 중 하나만 깔리므로 실제로 타일맵을 건드리는
    /// 것은 많아야 한 번이고, 데코가 없는 칸이면 아예 쓰지 않는다.
    ///
    /// UseGroundTilemapForDeco 스테이지(Stage1)에서는 데코가 그라운드 타일 자체가 되어
    /// decoTilesToApply가 비어 있으므로 이 경로를 타지 않는다. 의도한 동작이다 - 그라운드로 내려간
    /// 데코는 나무보다 아래에 그려져 밑동을 가리지 않으니 걷어낼 이유가 없고, 걷어내면 오히려
    /// 나무 밑만 다른 타일로 도려낸 자국이 남는다. 블룸 데코는 여전히 겹쳐 그려지므로 그대로 걷어낸다.
    /// </summary>
    public void ClearDecoTileForTree(Vector3 _worldPos)
    {
        Vector3Int cellPos = WorldToCell(_worldPos);
        if (cellPos.x < 0 || cellPos.x >= width || cellPos.y < 0 || cellPos.y >= height) return;

        int flatIdx = cellPos.x + cellPos.y * width;

        // 공격 판정 루프에서 모아 둔 데코 되돌리기가 있으면 먼저 반영해 "되돌리고 → 걷어내는" 순서를 지킨다
        FlushCombatTileWritesForOrdering();

        // 걷어낸 타일의 보관(suppressed*)은 배치 여부와 무관하게 즉시 한다. 미루면 그 사이 들어온 Restore가 되돌릴 타일을 모른다.
        if (decoTilesToApply[flatIdx] != null)
        {
            suppressedDecoTiles[flatIdx] = decoTilesToApply[flatIdx];
            decoTilesToApply[flatIdx] = null;
            if (true == bBatchingTreeTileSets) pendingDecoClearCells.Add(cellPos);
            else if (decoTilemap != null) decoTilemap.SetTile(cellPos, null);
        }

        if (bloomDecoTilesToApply[flatIdx] != null)
        {
            suppressedBloomDecoTiles[flatIdx] = bloomDecoTilesToApply[flatIdx];
            bloomDecoTilesToApply[flatIdx] = null;
            if (true == bBatchingTreeTileSets) pendingBloomDecoClearCells.Add(cellPos);
            else if (bloomDecoTilemap != null) bloomDecoTilemap.SetTile(cellPos, null);
        }
    }

    /// <summary>
    /// ClearDecoTileForTree가 걷어낸 데코를 같은 칸에 되돌린다. 걷어낸 적 없는 칸이면 아무것도 하지 않으므로
    /// 나무가 없던 자리에 대고 불러도 안전하다(던전 재생성 직후처럼 suppressed가 비어 있는 경우 포함).
    /// </summary>
    public void RestoreDecoTileForTree(Vector3 _worldPos)
    {
        Vector3Int cellPos = WorldToCell(_worldPos);
        if (cellPos.x < 0 || cellPos.x >= width || cellPos.y < 0 || cellPos.y >= height) return;

        // 스폰 배치 구간 안에서 되돌리기가 들어오면 모아둔 걷어내기를 먼저 반영해 "걷어내고 → 되돌리는" 순서를 지킨다
        if (true == bBatchingTreeTileSets) FlushTreeSpawnTileBatch();

        int flatIdx = cellPos.x + cellPos.y * width;

        TileBase deco = suppressedDecoTiles[flatIdx];
        if (deco != null)
        {
            suppressedDecoTiles[flatIdx] = null;
            decoTilesToApply[flatIdx] = deco;

            if (true == bBatchingTreeTileClears || true == TreeTileWriteBatch.IsActive)
            {
                pendingDecoRestoreCells.Add(cellPos);
                pendingDecoRestoreTiles.Add(deco);
            }
            else if (decoTilemap != null)
            {
                decoTilemap.SetTile(cellPos, deco);
            }
        }

        TileBase bloomDeco = suppressedBloomDecoTiles[flatIdx];
        if (bloomDeco != null)
        {
            suppressedBloomDecoTiles[flatIdx] = null;
            bloomDecoTilesToApply[flatIdx] = bloomDeco;

            if (true == bBatchingTreeTileClears || true == TreeTileWriteBatch.IsActive)
            {
                pendingBloomDecoRestoreCells.Add(cellPos);
                pendingBloomDecoRestoreTiles.Add(bloomDeco);
            }
            else if (bloomDecoTilemap != null)
            {
                bloomDecoTilemap.SetTile(cellPos, bloomDeco);
            }
        }
    }

    // // 프라이빗 로직 메서드

    private void GenerateNoiseMap()
    {
        // [로그라이크] 타원 섬. 본편은 반지름이 0이라 이 분기를 타지 않는다.
        if (ellipseIslandRadiusX > 0f && ellipseIslandRadiusY > 0f)
        {
            GenerateEllipseIslandMap();
            return;
        }

        float invWidth = 1f / width;
        float invHeight = 1f / height;
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float safeRadiusSq = centerSafeZoneRadius * centerSafeZoneRadius;
        // 해안선은 맵 중앙 기준 그대로, 보호 구역만 모닥불 칸 기준이다(centerSafeZoneOffset 주석 참고).
        float safeCenterX = centerX + centerSafeZoneOffset.x;
        float safeCenterY = centerY + centerSafeZoneOffset.y;
        float mapRadius = baseMapRadius;
        float mapRadiusSq = mapRadius * mapRadius;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            float yCoord = (y + 0.5f) * invHeight * scale + seed;
            float dy = y - centerY;

            for (int x = 0; x < width; x++)
            {
                int i = x + rowOffset;
                // --- Domain Warping + FBM 기법 (심화 파편화) ---
                // 1타일 단위의 부자연스러운 노이즈(파편)를 없애고, 
                // 물감이나 대리석 마블링처럼 유기적이고 부드러운 형태의 작은 지형들을 생성합니다.
                
                // 지형이 뭉치지 않고 훨씬 잘게 쪼개지도록 스케일을 2.5배로 크게 올립니다.
                float xCoord = (x + 0.5f) * invWidth * scale + seed;

                float nx = (x + 0.5f) * invWidth * (scale * 2.5f);
                float ny = (y + 0.5f) * invHeight * (scale * 2.5f);
                
                // 좌표계를 구부리기 위한 워프(Warp) 노이즈 추출
                float warpX = Mathf.PerlinNoise(nx * 0.5f + 11.1f, ny * 0.5f + 11.1f) - 0.5f;
                float warpY = Mathf.PerlinNoise(nx * 0.5f + 22.2f, ny * 0.5f + 22.2f) - 0.5f;
                
                // 워핑된 기본 지형 노이즈
                float baseVal = Mathf.PerlinNoise(nx + warpX * 1.5f + seed, ny + warpY * 1.5f + seed);
                
                // 테두리를 미세하게 깎아내어 뭉침을 방지하기 위한 디테일 노이즈 (약한 FBM)
                float detailVal = Mathf.PerlinNoise(nx * 2f + seed, ny * 2f + seed);
                
                // 최종 지형 (기본 85% + 디테일 15%로 합성하여 부드럽고 촘촘한 파편화 달성)
                float val = (baseVal * 0.85f) + (detailVal * 0.15f);

                float dx = x - centerX;
                float distSq = dx * dx + dy * dy;
                
                float angle = Mathf.Atan2(dy, dx);
                // 1. 큰 파동 (해안선의 큼직한 굴곡)
                float coast1 = (Mathf.PerlinNoise(angle * 4f, seed * 0.01f) - 0.5f) * 20f;
                // 2. 작은 파동 (해안선의 자잘한 울퉁불퉁함)
                float coast2 = (Mathf.PerlinNoise(angle * 10f, seed * 0.02f) - 0.5f) * 10f;
                // 3. 2D 맵 노이즈를 이용한 경계면 파편화 (장식용 섬 생성)
                float islandNoiseRaw = Mathf.PerlinNoise(xCoord * 2.5f + 100f, yCoord * 2.5f + 100f);
                float islandNoise = 0f;
                // 노이즈 값이 0.65 이상일 때만 급격히 확장시켜, 육지와 분리된 장식용 섬들이 흩뿌려지듯 생성되게 함
                if (islandNoiseRaw > 0.65f)
                {
                    islandNoise = (islandNoiseRaw - 0.65f) * 60f; 
                }
                
                float adjustedRadius = mapRadius + coast1 + coast2 + islandNoise;
                
                // 배열 바깥으로 너무 튀어나가지 않도록 최대 확장치 제한 (안전 장치)
                adjustedRadius = Mathf.Min(adjustedRadius, mapRadius + 22f);
                
                if (distSq > adjustedRadius * adjustedRadius)
                {
                    val = 0f;
                }
                else
                {
                    float sdx = x - safeCenterX;
                    float sdy = y - safeCenterY;
                    if (sdx * sdx + sdy * sdy < safeRadiusSq)
                    {
                        val = Mathf.Max(val, waterThreshold + 0.05f);
                    }
                }

                noiseValues[i] = val;
            }
        }

        ApplyInnerPuddleDensity();
        MarkMainland();
    }

    /// <summary>
    /// [로그라이크 전용] 플레이어 시작 칸(DetermineSpawns와 같은 칸)을 중심으로 화면에서 완벽한 타원(또는 마름모, ellipseIslandShape)이 되는 섬을 찍는다.
    ///
    /// 아이소메트릭 셀 (x, y)의 월드 위치는 ((x - y) × 셀 가로/2, (x + y) × 셀 세로/2)다. 중심 칸과의 차이를 정수 셀 좌표로
    /// 구해 이 식으로 바꾸면, 화면 좌우 반전(셀 x·y 교환)과 상하 반전(부호 교환과 x·y 교환)에 대응하는 칸이 정확히 같은
    /// 값을 받는다. 부동소수 월드 좌표(worldPosMap)를 쓰지 않는 이유가 이것이다 - 경계에 걸린 칸이 한쪽에서만 뒤집히지 않는다.
    ///
    /// 섬 안은 잔디 값(1)으로 채워 내부 웅덩이·모래 얼룩이 생기지 않는다. 해안선 모래는 isShoreline이 따로 붙인다.
    /// </summary>
    private void GenerateEllipseIslandMap()
    {
        int centerCellX = width / 2;
        int centerCellY = (height / 2) - 1;

        Vector2 cell = CellSize;
        float halfW = cell.x * 0.5f;
        float halfH = cell.y * 0.5f;
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            int oy = y - centerCellY;

            for (int x = 0; x < width; x++)
            {
                int ox = x - centerCellX;
                float wx = (ox - oy) * halfW;
                float wy = (ox + oy) * halfH;

                bool bInside = IslandShape.Contains(ellipseIslandShape, wx, wy, ellipseIslandRadiusX, ellipseIslandRadiusY);
                noiseValues[x + rowOffset] = bInside ? 1f : 0f;
            }
        }

        ApplyInnerPuddleDensity();
        MarkMainland();
    }

    // ── 섬 내부 웅덩이 빈도 조절 ──
    // Shoreline(외부 바다와 맞닿은 해안선)을 만드는 위 로직은 전혀 건드리지 않고,
    // 외부 바다와 연결되지 않은 "고립된 물웅덩이"만 컴포넌트 단위로 찾아
    // innerPuddleDensity 확률로 유지하거나 육지로 메워버린다.
    private void ApplyInnerPuddleDensity()
    {
        // innerPuddleDensity가 1이어도 minPuddleTileCount 미만의 자투리 웅덩이는 항상 제거해야 하므로
        // 여기서는 조기 종료하지 않는다.
        int size = width * height;
        Array.Clear(isOceanConnectedWater, 0, size);
        Array.Clear(puddleVisited, 0, size);
        puddleQueue.Clear();

        // 1. 그리드 테두리와 맞닿은 물을 시작점으로 외부 바다 영역을 BFS로 표시
        for (int x = 0; x < width; x++)
        {
            TryEnqueueOceanWater(x, 0);
            TryEnqueueOceanWater(x, height - 1);
        }
        for (int y = 0; y < height; y++)
        {
            TryEnqueueOceanWater(0, y);
            TryEnqueueOceanWater(width - 1, y);
        }

        while (puddleQueue.Count > 0)
        {
            int curr = puddleQueue.Dequeue();
            int cx = curr % width;
            int cy = curr / width;

            TryEnqueueOceanWater(cx + 1, cy);
            TryEnqueueOceanWater(cx - 1, cy);
            TryEnqueueOceanWater(cx, cy + 1);
            TryEnqueueOceanWater(cx, cy - 1);
        }

        // 2. 외부 바다와 연결되지 않은 물(=섬 내부 웅덩이)을 컴포넌트 단위로 찾아 확률적으로 제거
        float fillValue = waterThreshold + (1f - waterThreshold) * 0.5f;

        for (int i = 0; i < size; i++)
        {
            if (puddleVisited[i]) continue;
            if (noiseValues[i] >= waterThreshold || isOceanConnectedWater[i]) continue;

            puddleComponentBuffer.Clear();
            puddleBorderBuffer.Clear();
            puddleQueue.Clear();
            puddleQueue.Enqueue(i);
            puddleVisited[i] = true;

            while (puddleQueue.Count > 0)
            {
                int curr = puddleQueue.Dequeue();
                puddleComponentBuffer.Add(curr);

                int cx = curr % width;
                int cy = curr / width;

                TryEnqueuePuddleCell(cx + 1, cy);
                TryEnqueuePuddleCell(cx - 1, cy);
                TryEnqueuePuddleCell(cx, cy + 1);
                TryEnqueuePuddleCell(cx, cy - 1);

                CollectPuddleBorderCell(cx + 1, cy);
                CollectPuddleBorderCell(cx - 1, cy);
                CollectPuddleBorderCell(cx, cy + 1);
                CollectPuddleBorderCell(cx, cy - 1);
            }

            bool isTooSmall = puddleComponentBuffer.Count < minPuddleTileCount;
            bool keepPuddle = !isTooSmall && UnityEngine.Random.value < innerPuddleDensity;
            if (keepPuddle) continue; // 이 웅덩이는 유지

            for (int k = 0; k < puddleComponentBuffer.Count; k++)
            {
                noiseValues[puddleComponentBuffer[k]] = fillValue;
            }

            // 웅덩이 테두리로 인해 sandThreshold 밑으로 깔려 있던 육지 칸(예전 모래 테두리)도
            // 같이 잔디 값으로 밀어서, 웅덩이를 지운 자리에 모래 "도넛" 자국이 남지 않도록 한다.
            for (int k = 0; k < puddleBorderBuffer.Count; k++)
            {
                int borderIdx = puddleBorderBuffer[k];
                if (noiseValues[borderIdx] < fillValue)
                {
                    noiseValues[borderIdx] = fillValue;
                }
            }
        }
    }

    private void TryEnqueueOceanWater(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (isOceanConnectedWater[idx]) return;
        if (noiseValues[idx] >= waterThreshold) return;

        isOceanConnectedWater[idx] = true;
        puddleQueue.Enqueue(idx);
    }

    private void TryEnqueuePuddleCell(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (puddleVisited[idx]) return;
        if (noiseValues[idx] >= waterThreshold || isOceanConnectedWater[idx]) return;

        puddleVisited[idx] = true;
        puddleQueue.Enqueue(idx);
    }

    // 웅덩이 컴포넌트에 맞닿은 육지 칸(모래 테두리가 남을 수 있는 칸)을 수집한다.
    // 중복으로 여러 번 들어와도 이후 덮어쓰기가 멱등적이라 문제 없다.
    private void CollectPuddleBorderCell(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (noiseValues[idx] < waterThreshold) return; // 물 칸은 border가 아님

        puddleBorderBuffer.Add(idx);
    }

    private System.Collections.Generic.Queue<int> floodFillQueue = new System.Collections.Generic.Queue<int>(10000);

    private void MarkMainland()
    {
        int size = width * height;
        System.Array.Clear(isMainland, 0, size);
        
        int centerX = width / 2;
        int centerY = height / 2;
        
        floodFillQueue.Clear();
        int startIdx = centerX + centerY * width;
        
        if (noiseValues[startIdx] >= waterThreshold)
        {
            floodFillQueue.Enqueue(startIdx);
            isMainland[startIdx] = true;
        }

        while (floodFillQueue.Count > 0)
        {
            int curr = floodFillQueue.Dequeue();
            int cx = curr % width;
            int cy = curr / width;

            // Right
            if (cx < width - 1)
            {
                int nIdx = curr + 1;
                if (!isMainland[nIdx] && noiseValues[nIdx] >= waterThreshold)
                {
                    isMainland[nIdx] = true;
                    floodFillQueue.Enqueue(nIdx);
                }
            }
            // Left
            if (cx > 0)
            {
                int nIdx = curr - 1;
                if (!isMainland[nIdx] && noiseValues[nIdx] >= waterThreshold)
                {
                    isMainland[nIdx] = true;
                    floodFillQueue.Enqueue(nIdx);
                }
            }
            // Up
            if (cy < height - 1)
            {
                int nIdx = curr + width;
                if (!isMainland[nIdx] && noiseValues[nIdx] >= waterThreshold)
                {
                    isMainland[nIdx] = true;
                    floodFillQueue.Enqueue(nIdx);
                }
            }
            // Down
            if (cy > 0)
            {
                int nIdx = curr - width;
                if (!isMainland[nIdx] && noiseValues[nIdx] >= waterThreshold)
                {
                    isMainland[nIdx] = true;
                    floodFillQueue.Enqueue(nIdx);
                }
            }
        }
    }

    private void DetermineSpawns()
    {
        int size = width * height;
        Array.Clear(isShoreline, 0, size);
        shorelineList.Clear();
        innerEdgesList.Clear();

        for (int i = 0; i < size; i++)
        {
            if (noiseValues[i] < waterThreshold) continue;

            int x = i % width;
            int y = i / width;

            if (IsWater(x + 1, y) || IsWater(x - 1, y) || IsWater(x, y + 1) || IsWater(x, y - 1) ||
                IsWater(x + 1, y + 1) || IsWater(x + 1, y - 1) || IsWater(x - 1, y + 1) || IsWater(x - 1, y - 1))
            {
                isShoreline[i] = true;
                shorelineList.Add(i);
            }
        }

        ReadOnlySpan<int> dx = stackalloc int[] { 1, -1, 0, 0 };
        ReadOnlySpan<int> dy = stackalloc int[] { 0, 0, 1, -1 };

        for (int i = 0; i < size; i++)
        {
            if (noiseValues[i] < waterThreshold || isShoreline[i]) continue;

            int x = i % width;
            int y = i / width;

            for (int j = 0; j < 4; j++)
            {
                int nx = x + dx[j];
                int ny = y + dy[j];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (isShoreline[nx + ny * width])
                    {
                        innerEdgesList.Add(i);
                        break;
                    }
                }
            }
        }

        // 1. 플레이어 스폰 위치를 맵의 중앙(안전 구역의 정중앙)에서 한 칸 아래로 설정
        int centerX = width / 2;
        int centerY = (height / 2);

        // 2. 포탈 스폰 위치 결정: 캐릭터 스폰 위치에서 오른쪽으로 2칸 떨어진 위치
        int portalX = centerX + 2;
        int portalY = centerY;

        if (portalX >= width) portalX = width - 1;

        portalIdx = portalX + portalY * width;

        //캐릭터 위치 좀 더 아래로 조정.
        centerX = width / 2;
        centerY = (height / 2) - 1;
        playerIdx = centerX + centerY * width;

        // 경계 검사는 반드시 최종 값에 걸어야 한다. 예전엔 이 검사가 위쪽 임시 대입과
        // 이 최종 대입 사이에 있어서, 실제로 쓰이는 값은 한 번도 검사받지 않았다.
        if (playerIdx < 0 || playerIdx >= size) playerIdx = 0;
        if (portalIdx < 0 || portalIdx >= size) portalIdx = 0;
    }

    // ApplyTiles()에서 sandThreshold를 계산하는 로직과 동일. 모래 패치 판정에도 같은 기준을 써야 하므로
    // 별도 메서드로 분리해 재사용한다.
    private float ComputeSandThreshold()
    {
        float totalDensity = sandDensity + grassDensity;
        float invTotal = totalDensity > 0 ? 1f / totalDensity : 0;
        float landRange = 1f - waterThreshold;

        return waterThreshold + (landRange * (sandDensity * invTotal));
    }

    // ── 해안선이 아닌 작은 내륙 모래 패치를 잔디로 메우기 ──
    // 노이즈 때문에 섬 내부에 흩뿌려지는 모래 조각들 중, 해안선(Shoreline)과 무관하게 생기는
    // 작은 패치는 minSandPatchTileCount 칸 미만이면 어색해 보이므로 잔디로 대체한다.
    // 해안선 모래는 이 로직의 대상이 아니므로 그대로 유지된다.
    private void ApplySmallInlandSandPatches()
    {
        int size = width * height;
        Array.Clear(forceGrassOverride, 0, size);

        if (minSandPatchTileCount <= 0) return;

        Array.Clear(sandPatchVisited, 0, size);
        float sandThreshold = ComputeSandThreshold();

        for (int i = 0; i < size; i++)
        {
            if (sandPatchVisited[i]) continue;
            if (!IsInlandSandCandidate(i, sandThreshold)) continue;

            sandPatchComponentBuffer.Clear();
            sandPatchQueue.Clear();
            sandPatchQueue.Enqueue(i);
            sandPatchVisited[i] = true;

            while (sandPatchQueue.Count > 0)
            {
                int curr = sandPatchQueue.Dequeue();
                sandPatchComponentBuffer.Add(curr);

                int cx = curr % width;
                int cy = curr / width;

                TryEnqueueSandPatchCell(cx + 1, cy, sandThreshold);
                TryEnqueueSandPatchCell(cx - 1, cy, sandThreshold);
                TryEnqueueSandPatchCell(cx, cy + 1, sandThreshold);
                TryEnqueueSandPatchCell(cx, cy - 1, sandThreshold);
            }

            if (sandPatchComponentBuffer.Count < minSandPatchTileCount)
            {
                for (int k = 0; k < sandPatchComponentBuffer.Count; k++)
                {
                    forceGrassOverride[sandPatchComponentBuffer[k]] = true;
                }
            }
        }
    }

    private bool IsInlandSandCandidate(int _idx, float _sandThreshold)
    {
        if (isShoreline[_idx]) return false;

        float v = noiseValues[_idx];
        return v >= waterThreshold && v < _sandThreshold;
    }

    private void TryEnqueueSandPatchCell(int _x, int _y, float _sandThreshold)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (sandPatchVisited[idx]) return;
        if (!IsInlandSandCandidate(idx, _sandThreshold)) return;

        sandPatchVisited[idx] = true;
        sandPatchQueue.Enqueue(idx);
    }

    /// <summary>
    /// 데코 타일을 어느 타일맵 배열에 넣을지 정한다.
    ///
    /// 기본은 GroundTilemap 위에 겹쳐 그리는 DecoTilemap이고, _bReplaceGround(Stage1)일 때는
    /// GroundTilemap의 타일을 아예 대체한다. 대체한 칸은 원래 그라운드 타일을
    /// decoReplacedGroundTiles에 남겨 IsGrassTile이 계속 잔디로 판정하게 한다.
    ///
    /// 블룸 데코는 이 경로를 타지 않는다. 겹쳐 그리는 것이 목적이라 BloomDecoTilemap에 그대로 간다.
    /// </summary>
    private void PlaceDecoTile(int _idx, TileBase _decoTile, bool _bReplaceGround)
    {
        if (_decoTile == null) return;

        if (false == _bReplaceGround)
        {
            decoTilesToApply[_idx] = _decoTile;
            return;
        }

        decoReplacedGroundTiles[_idx] = groundTiles[_idx];
        groundTiles[_idx] = _decoTile;
    }

    private void ApplyTiles()
    {
        int size = width * height;
        Array.Clear(groundTiles, 0, size);
        Array.Clear(collisionTiles, 0, size);
        Array.Clear(waterTiles, 0, size);
        Array.Clear(waterCornerTiles, 0, size);
        Array.Clear(waterCollisionTiles, 0, size);
        Array.Clear(rockCollisionTiles, 0, size);
        Array.Clear(decoTilesToApply, 0, size);
        Array.Clear(bloomDecoTilesToApply, 0, size);
        Array.Clear(suppressedDecoTiles, 0, size);
        Array.Clear(suppressedBloomDecoTiles, 0, size);
        Array.Clear(decoReplacedGroundTiles, 0, size);
        Array.Clear(waterDecoTilesToApply, 0, size);
        Array.Clear(bloomWaterDecoTilesToApply, 0, size);
        Array.Clear(waterStencilTiles, 0, size);
        Array.Clear(groundStencilTiles, 0, size);
        Array.Clear(deepWaterTileFlags, 0, size);

        grassPositions.Clear();
        delayedGrassPositions.Clear();
        walkablePositions.Clear();
        for (int i = 0; i < size; i++) cellToIndex[i] = -1;

        float sandThreshold = ComputeSandThreshold();

        // Stage1처럼 데코가 그라운드를 대체하는 스테이지에서는 DecoTilemap을 아예 쓰지 않는다.
        // 칸마다 다시 물어볼 값이 아니라 루프 밖에서 한 번만 읽는다.
        bool bDecoReplacesGround = stageTileData != null && stageTileData.UseGroundTilemapForDeco;

        // [로그라이크] 타원 섬은 모든 티어에서 장식을 하나도 만들지 않는다(장식 타일·움직이는 장식 오브젝트·물 장식).
        // 바닥·물·해안선과 물 전체에 까는 블룸 층은 장식이 아니라 그대로 둔다. 본편은 bEllipseIsland가 항상 false라
        // 아래 조건들이 예전과 같고, 무작위 판정 순서도 바뀌지 않는다(&&의 앞 조건이 true라 뒤 판정이 그대로 실행된다).
        bool bSpawnDeco = false == bEllipseIsland;

        Vector3 portalPos = GetPortalSpawnPosition();
        Vector3 playerPos = GetPlayerSpawnPosition();

        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float safeRadiusSq = centerSafeZoneRadius * centerSafeZoneRadius;
        // 보호 구역만 모닥불 칸 기준(centerSafeZoneOffset 주석 참고). 로그라이크 타원 섬은 맵 중앙 그대로다.
        float safeCenterX = bEllipseIsland ? centerX : centerX + centerSafeZoneOffset.x;
        float safeCenterY = bEllipseIsland ? centerY : centerY + centerSafeZoneOffset.y;

        float mapRadius = baseMapRadius;
        float outerRadius = mapRadius + outerWaterDepth;
        float outerRadiusSq = outerRadius * outerRadius;

        for (int i = 0; i < size; i++)
        {
            int x = i % width;
            int y = i / width;
            float dx = x - centerX;
            float dy = y - centerY;
            float distSq = dx * dx + dy * dy;

            // 도화지(Width x Height)의 구석 부분(타원 반경을 벗어나는 곳)은 타일을 아예 생성하지 않음 (사각형 방지)
            if (distSq > outerRadiusSq) continue;

            float sdx = x - safeCenterX;
            float sdy = y - safeCenterY;
            bool inSafeZone = (sdx * sdx + sdy * sdy < safeRadiusSq);

            float v = noiseValues[i];

            if (v < waterThreshold)
            {
                waterTiles[i] = GetWaterTile(x, y);
                waterCornerTiles[i] = GetWaterCornerTile(x, y);
                waterCollisionTiles[i] = stageTileData != null ? stageTileData.TreeCollisionTile : null;
                waterStencilTiles[i] = stageTileData != null ? stageTileData.StencilTile : null;

                if (stageTileData != null && stageTileData.BloomWaterDecoTiles != null && stageTileData.BloomWaterDecoTiles.Count > 0)
                {
                    bool shouldPlaceBloom = true;
                    if (stageTileData.UseBloomWaterDecoDensity)
                    {
                        shouldPlaceBloom = UnityEngine.Random.value < stageTileData.BloomWaterDecoDensity;
                    }

                    if (shouldPlaceBloom)
                    {
                        bloomWaterDecoTilesToApply[i] = stageTileData.BloomWaterDecoTiles[UnityEngine.Random.Range(0, stageTileData.BloomWaterDecoTiles.Count)];
                    }
                }

                bool _isDeepWater = true;
                for (int _dy = -1; _dy <= 1; _dy++)
                {
                    for (int _dx = -1; _dx <= 1; _dx++)
                    {
                        if (_dx == 0 && _dy == 0) continue;
                        if (IsLand(x + _dx, y + _dy))
                        {
                            _isDeepWater = false;
                            break;
                        }
                    }
                    if (!_isDeepWater) break;
                }

                deepWaterTileFlags[i] = _isDeepWater;

                if (_isDeepWater)
                {
                    if (stageTileData != null)
                    {
                        if (bSpawnDeco && stageTileData.WaterDecoTiles != null && stageTileData.WaterDecoTiles.Count > 0 && UnityEngine.Random.value < stageTileData.WaterDecoDensity)
                        {
                            waterDecoTilesToApply[i] = stageTileData.WaterDecoTiles[UnityEngine.Random.Range(0, stageTileData.WaterDecoTiles.Count)];
                        }
                    }
                }
            }
            else
            {
                if (isShoreline[i])
                {
                    groundStencilTiles[i] = stageTileData != null ? stageTileData.GroundStencilTile : null;
                }
                Vector3 pos = GetWorldPos(i);

                cellToIndex[i] = walkablePositions.Count;
                walkablePositions.Add(pos);

                bool _isShoreline = isShoreline[i];
                bool _isSand = _isShoreline || (v < sandThreshold && !forceGrassOverride[i]);
                if (_isSand)
                {
                    groundTiles[i] = (stageTileData != null && stageTileData.SandTiles != null && stageTileData.SandTiles.Count > 0)
                        ? stageTileData.SandTiles[UnityEngine.Random.Range(0, stageTileData.SandTiles.Count)]
                        : null;
                }
                else
                {
                    // [로그라이크] 타원 섬은 풀 타일 한 종류(목록의 첫 칸)만 쓴다. 본편은 기존처럼 무작위 변형.
                    bool bSingleGrass = bEllipseIsland;
                    groundTiles[i] = (stageTileData != null && stageTileData.GrassTiles != null && stageTileData.GrassTiles.Count > 0)
                        ? stageTileData.GrassTiles[bSingleGrass ? 0 : UnityEngine.Random.Range(0, stageTileData.GrassTiles.Count)]
                        : null;
                }

                bool _hasRockDeco = false;
                if (stageTileData != null)
                {
                    List<StaticObj> staticPrefabs;
                    float density;

                    if (_isShoreline)
                    {
                        staticPrefabs = stageTileData.ShorelineStaticObjPrefabs;
                        density = stageTileData.ShorelineStaticObjDensity;
                    }
                    else if (_isSand)
                    {
                        staticPrefabs = stageTileData.SandStaticObjPrefabs;
                        density = stageTileData.SandStaticObjDensity;
                    }
                    else
                    {
                        staticPrefabs = stageTileData.GrassStaticObjPrefabs;
                        density = stageTileData.GrassStaticObjDensity;
                    }

                    // [로그라이크] 충돌 장식(바위 등 StaticObj + 충돌 타일)도 장식이라 만들지 않는다(bSpawnDeco 참고).
                    if (bSpawnDeco && staticPrefabs != null && staticPrefabs.Count > 0 && UnityEngine.Random.value < density)
                    {
                        if (!inSafeZone && isMainland[i])
                        {
                            if (animatedObjGenerator != null)
                            {
                                if (_isShoreline)
                                    animatedObjGenerator.SpawnShorelineStaticObj(pos);
                                else if (_isSand)
                                    animatedObjGenerator.SpawnSandStaticObj(pos);
                                else
                                    animatedObjGenerator.SpawnGrassStaticObj(pos);
                            }
                            _hasRockDeco = true;
                            rockCollisionTiles[i] = stageTileData.TreeCollisionTile;
                        }
                    }
                }

                // Shoreline은 StaticObj와 마찬가지로 전용 밀도/프리팹을 쓰고, Sand(비Shoreline)는 기존처럼
                // 애니메이션 오브젝트를 스폰하지 않는다. _hasRockDeco로 이미 배제했으므로 같은 타일에
                // Static과 Animated가 겹쳐 생성되지 않는다.
                bool _hasAnimatedObj = false;
                if (bSpawnDeco && false == _hasRockDeco)
                {
                    if (_isShoreline)
                    {
                        if (animatedObjGenerator != null && stageTileData != null && UnityEngine.Random.value < stageTileData.ShorelineAnimatedObjDensity)
                        {
                            animatedObjGenerator.SpawnShorelineAnimatedObj(pos);
                            _hasAnimatedObj = true;
                        }
                    }
                    else if (false == _isSand)
                    {
                        if (animatedObjGenerator != null && stageTileData != null && UnityEngine.Random.value < stageTileData.AnimatedObjDensity)
                        {
                            animatedObjGenerator.SpawnAnimatedObj(pos);
                            _hasAnimatedObj = true;
                        }
                    }
                }

                bool _hasGroundDeco = false;
                if (bSpawnDeco && false == _hasRockDeco && false == _hasAnimatedObj && stageTileData != null)
                {
                    float _groundDecoProb = _isSand ? stageTileData.SandDecoDensity : stageTileData.GroundDecoDensity;
                    float _bloomDecoProb = _isSand ? stageTileData.BloomSandDecoDensity : stageTileData.BloomGroundDecoDensity;

                    if (stageTileData.GroundDecoTiles != null && stageTileData.GroundDecoTiles.Count > 0 && UnityEngine.Random.value < _groundDecoProb)
                    {
                        PlaceDecoTile(i, stageTileData.GroundDecoTiles[UnityEngine.Random.Range(0, stageTileData.GroundDecoTiles.Count)], bDecoReplacesGround);
                        _hasGroundDeco = true;
                    }
                    else if (stageTileData.BloomGroundDecoTiles != null && stageTileData.BloomGroundDecoTiles.Count > 0 && UnityEngine.Random.value < _bloomDecoProb)
                    {
                        bloomDecoTilesToApply[i] = stageTileData.BloomGroundDecoTiles[UnityEngine.Random.Range(0, stageTileData.BloomGroundDecoTiles.Count)];
                        _hasGroundDeco = true;
                    }
                }

                if (bSpawnDeco && false == _isSand && false == _hasRockDeco && false == _hasAnimatedObj && false == _hasGroundDeco && stageTileData != null)
                {
                    if (stageTileData.GrassDecoTiles != null && stageTileData.GrassDecoTiles.Count > 0 && UnityEngine.Random.value < stageTileData.GrassDecoDensity)
                    {
                        PlaceDecoTile(i, stageTileData.GrassDecoTiles[UnityEngine.Random.Range(0, stageTileData.GrassDecoTiles.Count)], bDecoReplacesGround);
                    }
                    else if (stageTileData.BloomGrassDecoTiles != null && stageTileData.BloomGrassDecoTiles.Count > 0 && UnityEngine.Random.value < stageTileData.BloomGrassDecoDensity)
                    {
                        bloomDecoTilesToApply[i] = stageTileData.BloomGrassDecoTiles[UnityEngine.Random.Range(0, stageTileData.BloomGrassDecoTiles.Count)];
                    }
                }

                if (false == _isSand && false == _hasRockDeco)
                {
                    if (!inSafeZone && isMainland[i])
                    {
                        if ((pos - portalPos).sqrMagnitude > 2.25f && (pos - playerPos).sqrMagnitude > 2.25f)
                        {
                            grassPositions.Add(pos);
                        }
                        else
                        {
                            delayedGrassPositions.Add(pos);
                        }
                    }
                }
            }
        }

        if (bSpawnDeco) PlaceWaterAnimatedObjects(size);
        // 물가 갈대(스테이지 데이터에 켠 스테이지만). 물 장식을 다 놓은 뒤 그 자리를 피해 놓는다.
        if (bSpawnDeco) PlaceShoreReeds(size);
        // 버섯 포자 뿜기(스테이지 데이터에 켠 스테이지만, 로그라이크는 끈다). 렌더링 전용.
        StageMushroomSpores.Apply(bSpawnDeco && null != stageTileData ? stageTileData.MushroomSpores : null, animatedObjGenerator, "DungeonScene");
        // 장식 숨쉬기(꺼진 스테이지·로그라이크는 아무것도 만들지 않는다)
        StageDecoBreath.Apply(bSpawnDeco && null != stageTileData ? stageTileData.DecoBreath : null, animatedObjGenerator, "DungeonScene");
        // 용암 불씨(꺼진 스테이지·로그라이크는 아무것도 만들지 않는다). 렌더링 전용.
        StageLavaEmbers.Apply(bSpawnDeco && null != stageTileData ? stageTileData.LavaEmbers : null, waterTilemap, "DungeonScene");
        // 용암 기둥(꺼진 스테이지·로그라이크는 아무것도 만들지 않는다). 렌더링 전용.
        StageLavaSpouts.Apply(bSpawnDeco && null != stageTileData ? stageTileData.LavaSpouts : null, waterTilemap, "DungeonScene");
        // 데코 증기(꺼진 스테이지·로그라이크는 아무것도 만들지 않는다). 렌더링 전용.
        StageDecoSteam.Apply(bSpawnDeco && null != stageTileData ? stageTileData.DecoSteam : null, bloomDecoTilemap, "DungeonScene");
        // 물가·풀숲 반딧불(꺼진 스테이지·로그라이크는 아무것도 만들지 않는다). 물가는 맵 생성 끝의 물가 지도로 고른다. 렌더링 전용.
        StageFireflies.Apply(bSpawnDeco && null != stageTileData ? stageTileData.Fireflies : null, decoTilemap, "DungeonScene");

        // 셰이더 타일 애니메이션(켠 스테이지만, 로그라이크는 끈다). 꺼지면 아래 Convert가 원본 배열을 그대로 돌려준다. 렌더링 전용.
        tileFrameAnimation.Begin(null != stageTileData && stageTileData.ShaderTileAnimation, "DungeonScene", new[]
        {
            new StageTileFrameAnimation.Layer { tilemap = waterTilemap, tiles = waterTiles, extra = null != stageTileData ? stageTileData.WaterTile : null },
            new StageTileFrameAnimation.Layer { tilemap = waterCornerTilemap, tiles = waterCornerTiles },
            new StageTileFrameAnimation.Layer { tilemap = waterStencilTilemap, tiles = waterStencilTiles, extra = null != stageTileData ? stageTileData.StencilTile : null },
            new StageTileFrameAnimation.Layer { tilemap = groundStencilTilemap, tiles = groundStencilTiles },
        });

        BoundsInt b = new BoundsInt(0, 0, 0, width, height, 1);
        groundTilemap.SetTilesBlock(b, groundTiles);
        collisionTilemap.SetTilesBlock(b, collisionTiles);
        if (waterTilemap != null) waterTilemap.SetTilesBlock(b, tileFrameAnimation.Convert(waterTiles, ref waterTilesFrameBuffer));
        if (waterCornerTilemap != null) waterCornerTilemap.ClearAllTiles();
        if (waterCornerTilemap != null) waterCornerTilemap.SetTilesBlock(b, tileFrameAnimation.Convert(waterCornerTiles, ref waterCornerTilesFrameBuffer));
        if (waterCollisionTilemap != null) waterCollisionTilemap.SetTilesBlock(b, waterCollisionTiles);
        if (rockCollisionTilemap != null) rockCollisionTilemap.SetTilesBlock(b, rockCollisionTiles);
        decoTilemap.SetTilesBlock(b, decoTilesToApply);
        if (bloomDecoTilemap != null) bloomDecoTilemap.SetTilesBlock(b, bloomDecoTilesToApply);
        if (waterDecoTilemap != null) waterDecoTilemap.SetTilesBlock(b, waterDecoTilesToApply);
        if (bloomWaterDecoTilemap != null) bloomWaterDecoTilemap.SetTilesBlock(b, bloomWaterDecoTilesToApply);
        if (waterStencilTilemap != null) waterStencilTilemap.SetTilesBlock(b, tileFrameAnimation.Convert(waterStencilTiles, ref waterStencilTilesFrameBuffer));
        if (groundStencilTilemap != null) groundStencilTilemap.SetTilesBlock(b, tileFrameAnimation.Convert(groundStencilTiles, ref groundStencilTilesFrameBuffer));
    }

    // ── 웅덩이(연결된 물 덩어리) 단위 물 장식 배치 ──
    // 물 타일을 4방향 연결 컴포넌트(웅덩이)로 묶은 뒤, 그 웅덩이의 물 칸 수(바다는 깊은 물 칸 수)에 비례한
    // 목표 개수를 계산해서 웅덩이 안에서만 자리를 골라 스폰한다.
    // "바다"와 "웅덩이"는 거리(mapRadius)가 아니라 isOceanConnectedWater(섬과 물리적으로 연결됐는지)로
    // 구분한다 — 섬 외부를 채우는 바다와 섬 내부에 고립된 웅덩이가 실제로 다른 것이기 때문이다.
    //
    // 자리: 물 장식은 Objects 층이라 타일보다 위에 그려진다. 장식의 불투명 픽셀이 육지 타일이나 물가 그림
    // (물 타일 가장자리 거품·모서리 타일)에 걸치면 땅이나 물가 위에 떠 보인다. 예전에는 "둘레 8칸이 모두 물인 칸"의
    // 위 꼭짓점에만 놓아 이를 피했는데, 폭이 2~3칸인 웅덩이는 그런 칸이 없어 장식이 하나도 생기지 않았다
    // (190칸 맵 실측: 웅덩이 39개 중 20개). 지금은 웅덩이의 모든 물 칸을 후보로 두고, 장식 프리팹의 실제 모양
    // (모든 프레임의 불투명 픽셀, DecoSpritePatternAnimator.WaterFootprintRows)이 한 픽셀도 물가·육지 픽셀에
    // 닿지 않는 픽셀 단위 자리만 고른다(IsWaterDecoSpotClear).
    // 고르게 퍼지도록 후보 여럿 중 이미 놓인 장식에서 가장 먼 자리를 고른다(best-candidate).
    // 웅덩이는 최소 2개(서로 다른 종류)를 노리고, 무작위 후보가 모두 빗나가면 웅덩이 전체를 촘촘히 훑는다.
    // 종류는 웅덩이 안·맵 전체에서 덜 쓴 것부터 골라 골고루 나오게 한다(PlaceOneWaterDeco).
    // 장식이 들어갈 깨끗한 수면이 아예 없는 웅덩이(폭 1칸 물길 등)는 비워 둔다.
    private void PlaceWaterAnimatedObjects(int _size)
    {
        if (animatedObjGenerator == null || stageTileData == null) return;

        float waterAnimatedObjDensity = stageTileData.WaterAnimatedObjDensity;
        float waterAnimatedOtherTypeObjDensity = stageTileData.WaterAnimatedOtherTypeObjDensity;
        float waterAnimatedObjSeaDensity = stageTileData.WaterAnimatedObjSeaDensity;
        float waterAnimatedOtherTypeObjSeaDensity = stageTileData.WaterAnimatedOtherTypeObjSeaDensity;

        PrepareWaterDecoGrid();
        PrepareWaterDecoTypes();
        Array.Clear(waterCompVisited, 0, _size);
        mapPlacedWaterDecos.Clear();

        for (int i = 0; i < _size; i++)
        {
            if (waterCompVisited[i] || waterTiles[i] == null) continue;

            waterCompCells.Clear();
            waterCompQueue.Clear();
            waterCompQueue.Enqueue(i);
            waterCompVisited[i] = true;
            int deepCount = 0;

            while (waterCompQueue.Count > 0)
            {
                int curr = waterCompQueue.Dequeue();
                int cx = curr % width;
                int cy = curr / width;

                waterCompCells.Add(curr);
                if (deepWaterTileFlags[curr]) deepCount++;

                TryEnqueueWaterCompCell(cx + 1, cy);
                TryEnqueueWaterCompCell(cx - 1, cy);
                TryEnqueueWaterCompCell(cx, cy + 1);
                TryEnqueueWaterCompCell(cx, cy - 1);
            }

            // 한 컴포넌트는 모두 바다이거나 모두 웅덩이다(같은 4방향 연결로 묶이므로).
            // 웅덩이는 물 칸 전체 수를 기준으로 개수를 정한다. 장식은 픽셀 단위로 맨 수면 어디에나 놓이므로
            // 깊은 물 칸 수로 세면 폭이 좁은 웅덩이일수록 실제 넓이보다 훨씬 적게 나온다.
            bool bSea = isOceanConnectedWater[i];
            int poolSize = bSea ? deepCount : waterCompCells.Count;
            float densityA = bSea ? waterAnimatedObjSeaDensity : waterAnimatedObjDensity;
            float densityB = bSea ? waterAnimatedOtherTypeObjSeaDensity : waterAnimatedOtherTypeObjDensity;

            // 두 타입(A, B)의 목표 개수는 각각 독립적으로 계산한다 — 그래야 두 밀도가 서로의 실효 밀도를 깎아먹지 않는다.
            // 둘의 합이 풀 크기를 넘을 때만 B를 깎는다.
            int countA = ComputeDistributedCount(poolSize, densityA);
            int countB = ComputeDistributedCount(poolSize, densityB);
            if (countA + countB > poolSize) countB = Mathf.Max(0, poolSize - countA);

            // 웅덩이 하나에 한 종류만 보이지 않도록, 종류가 둘 이상이면 웅덩이마다 최소 2개(서로 다른 종류)를 노린다.
            // 자리가 없으면 들어가는 만큼만 놓는다.
            int pondMinCount = Mathf.Min(WaterDecoPondMinCount, waterDecoTypes.Count);
            if (false == bSea && countA > 0 && countA < pondMinCount) countA = pondMinCount;

            placedWaterDecos.Clear();
            if (waterDecoCompTypeUses.Length > 0) Array.Clear(waterDecoCompTypeUses, 0, waterDecoCompTypeUses.Length);

            for (int k = 0; k < countA; k++)
            {
                // 웅덩이의 최소 개수까지는 무작위 후보가 빗나가도 웅덩이 전체를 훑어 자리를 찾는다.
                bool bExhaustive = false == bSea && placedWaterDecos.Count < pondMinCount;
                if (false == PlaceOneWaterDeco(bSea, bExhaustive)) break;
            }

            bool bExhaustiveFallback = false == bSea;
            for (int k = 0; k < countB; k++)
            {
                if (false == TryFindWaterDecoSpot(GetFallbackWaterDecoFootprint(), bExhaustiveFallback && placedWaterDecos.Count == 0, out Vector2 pos)) break;

                AddPlacedWaterDeco(pos, GetFallbackWaterDecoFootprint());
                animatedObjGenerator.SpawnWaterAnimatedOtherTypeObj(pos);
            }
        }
    }

    // ── 물가 갈대(ShoreReedSettings) ──
    // 육지를 등진 물 칸(뒤쪽 +u 또는 +v 이웃이 육지이고, 앞쪽 -u·-v·대각선 앞 이웃은 물)에만 놓는다.
    // 화면 위쪽 물가 앞에 서므로, 앞쪽 육지 블록(반 칸 솟은 그림)에 갈대가 겹쳐 떠 보이지 않는다.
    // 웅덩이·바다마다 무리 수를 따로 정하고, 무리 자리는 서로 minClusterSpacing 이상 떨어진 물가 칸 중 고르게 퍼지도록 고른다.
    // 무리마다 clusterSizeMin~Max개를 이어진 물가 칸에 촘촘히 붙여 놓는다.
    // 물 장식(mapPlacedWaterDecos) 사각형과 겹치는 자리는 쓰지 않는다. 충돌이 없는 장식이라 게임 동작은 그대로다.
    private const float ShoreReedHalfWidth = 12f / 32f;
    private const float ShoreReedHalfHeight = 13f / 32f;
    // 무리 하나가 훑는 물가 칸 수 상한(무리가 물가를 따라 길게 늘어지지 않게)
    private const int ShoreReedMaxClusterCells = 8;
    // 물가선을 따라 갈대 자리를 흔드는 폭(칸 단위)
    private const float ShoreReedAlongJitter = 0.06f;
    // 두 갈대 그림이 겹쳐 한 덤불로 보이는 피봇 간격 한계(월드). 갈대 그림 폭은 약 0.66.
    private const float ShoreReedTouchX = 0.6f;
    private const float ShoreReedTouchY = 0.35f;
    // 무리 자리 칸 중심에서 무리 갈대가 닿을 수 있는 가장 먼 거리(월드, 넉넉히). 무리는 칸을 ShoreReedMaxClusterCells개까지
    // 잇고 한 칸 건너가 0.56월드 이하, 칸 안 갈대 자리 0.56·갈대 반폭 0.41을 더한 것보다 크게 잡는다.
    private const float ShoreReedClusterReach = ShoreReedMaxClusterCells * 0.6f + 1f;

    private readonly List<int> shoreReedCells = new List<int>(256);
    private readonly List<int> shoreReedClusterCells = new List<int>(ShoreReedMaxClusterCells);
    private readonly List<Vector2> shoreReedClusterSeeds = new List<Vector2>(64);
    private readonly List<Vector2> shoreReedClusterReeds = new List<Vector2>(16);
    private readonly List<PlacedWaterDeco> shoreReedNearbyDecos = new List<PlacedWaterDeco>(16);
    private bool[] shoreReedEligible = Array.Empty<bool>();

    private void PlaceShoreReeds(int _size)
    {
        if (animatedObjGenerator == null || stageTileData == null || GameModeContext.IsRoguelike) return;

        ShoreReedSettings settings = stageTileData.ShoreReeds;
        if (null == settings || false == settings.enabled || null == settings.prefab || (settings.clusterDensity <= 0f && settings.pondClusterDensity <= 0f)) return;

        if (false == bWaterDecoGridReady) PrepareWaterDecoGrid();

        if (shoreReedEligible.Length != _size) shoreReedEligible = new bool[_size];
        else Array.Clear(shoreReedEligible, 0, _size);

        bool bAnyEligible = false;
        for (int i = 0; i < _size; i++)
        {
            if (waterTiles[i] == null) continue;
            if (false == settings.includeSea && isOceanConnectedWater[i]) continue;

            int x = i % width;
            int y = i / width;
            if (false == IsLand(x + 1, y) && false == IsLand(x, y + 1)) continue;
            if (IsLand(x - 1, y) || IsLand(x, y - 1) || IsLand(x - 1, y - 1)) continue;

            shoreReedEligible[i] = true;
            bAnyEligible = true;
        }
        if (false == bAnyEligible) return;

        bool bFlip = false;
        WindGustSettings wind = stageTileData.WindGust;
        if (settings.flipToWind && null != wind && wind.enabled) bFlip = wind.direction.x > 0f;

        // 물 장식처럼 웅덩이(4방향으로 이어진 물 덩어리)·바다마다 따로 무리 수를 정한다. 바다는 물가 칸 수 * clusterDensity,
        // 웅덩이는 물 칸 수 * pondClusterDensity를 확률적으로 반올림하고 최소 1무리를 노리므로(ComputeDistributedCount),
        // 갈대가 큰 바다에만 몰리지 않고 작은 웅덩이에도 서며, 큰 웅덩이일수록 무리가 늘어난다.
        float spacingSq = settings.minClusterSpacing * settings.minClusterSpacing;
        shoreReedClusterSeeds.Clear();
        Array.Clear(waterCompVisited, 0, _size);
        for (int i = 0; i < _size; i++)
        {
            if (waterCompVisited[i] || waterTiles[i] == null) continue;

            waterCompQueue.Clear();
            waterCompQueue.Enqueue(i);
            waterCompVisited[i] = true;
            shoreReedCells.Clear();
            int waterCellCount = 0;
            while (waterCompQueue.Count > 0)
            {
                int curr = waterCompQueue.Dequeue();
                int cx = curr % width;
                int cy = curr / width;
                waterCellCount++;
                if (shoreReedEligible[curr]) shoreReedCells.Add(curr);

                TryEnqueueWaterCompCell(cx + 1, cy);
                TryEnqueueWaterCompCell(cx - 1, cy);
                TryEnqueueWaterCompCell(cx, cy + 1);
                TryEnqueueWaterCompCell(cx, cy - 1);
            }
            if (shoreReedCells.Count == 0) continue;

            // 한 덩어리는 모두 바다이거나 모두 웅덩이다(물 장식과 같은 4방향 연결).
            int target = isOceanConnectedWater[i]
                ? ComputeDistributedCount(shoreReedCells.Count, settings.clusterDensity)
                : ComputeDistributedCount(waterCellCount, settings.pondClusterDensity);
            int placedClusters = 0;
            while (placedClusters < target && shoreReedCells.Count > 0)
            {
                // 무리 자리는 물 장식처럼 후보 여럿 중 이미 놓인 무리에서 가장 먼 칸(best-candidate)으로 골라 물가에 고르게 퍼뜨린다.
                // 무작위 후보가 모자라면 이 물의 남은 물가 칸 전체에서 고른다. 고른 칸은 무리를 못 놓아도 다시 쓰지 않는다.
                // 놓인 무리와 너무 가까운 칸은 무리가 늘기만 하니 다시 쓸 수 없으므로 보는 즉시 후보에서 뺀다
                // (그래야 목표를 못 채우는 물에서 같은 칸을 매번 다시 훑지 않는다).
                int bestIdx = -1;
                float bestScore = -1f;
                int valid = 0;
                for (int t = 0; t < WaterDecoMaxRandomTries && valid < WaterDecoCandidatesPerPick && shoreReedCells.Count > 0; t++)
                {
                    int idx = UnityEngine.Random.Range(0, shoreReedCells.Count);
                    float score = GetShoreReedSeedScore(shoreReedCells[idx]);
                    if (score < spacingSq)
                    {
                        if (bestIdx == shoreReedCells.Count - 1) bestIdx = idx;
                        RemoveShoreReedCandidateAt(idx);
                        continue;
                    }

                    valid++;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = idx;
                    }
                }
                if (bestIdx < 0)
                {
                    // 뒤에서부터 훑어, 뺀 자리에 들어오는 마지막 칸은 이미 본 칸이 되게 한다.
                    for (int idx = shoreReedCells.Count - 1; idx >= 0; idx--)
                    {
                        float score = GetShoreReedSeedScore(shoreReedCells[idx]);
                        if (score < spacingSq)
                        {
                            if (bestIdx == shoreReedCells.Count - 1) bestIdx = idx;
                            RemoveShoreReedCandidateAt(idx);
                            continue;
                        }
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = idx;
                        }
                    }
                }
                if (bestIdx < 0) break;

                int seed = shoreReedCells[bestIdx];
                RemoveShoreReedCandidateAt(bestIdx);

                if (PlaceShoreReedCluster(seed, settings, bFlip) > 0)
                {
                    shoreReedClusterSeeds.Add(GetShoreReedCellCenter(seed));
                    placedClusters++;
                }
            }
        }
    }

    // 후보 칸 하나를 뺀다(순서는 상관없어 마지막 칸을 그 자리로 옮긴다).
    private void RemoveShoreReedCandidateAt(int _idx)
    {
        int last = shoreReedCells.Count - 1;
        shoreReedCells[_idx] = shoreReedCells[last];
        shoreReedCells.RemoveAt(last);
    }

    // 칸 중심에서 이미 놓인 무리 자리까지 가장 가까운 거리의 제곱(놓인 무리가 없으면 아주 큰 값).
    private float GetShoreReedSeedScore(int _cell)
    {
        Vector2 pos = GetShoreReedCellCenter(_cell);
        float best = float.MaxValue;
        for (int k = 0; k < shoreReedClusterSeeds.Count; k++)
        {
            float d = (shoreReedClusterSeeds[k] - pos).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }

    // 무리 하나를 놓고 놓은 갈대 수를 돌려준다(못 놓으면 0).
    // seed에서 8방향으로 이어진 물가 칸을 가까운 순서로 모으며, 칸마다 물가선을 따라 갈대 두 개씩 촘촘히 붙여 놓는다
    // (갈대 그림 폭이 칸 변보다 넓어서 이웃 갈대와 겹쳐 한 덤불로 보인다). clusterSizeMin개를 못 채우는 자리
    // (물가가 짧거나 물 장식에 막힌 곳)에는 하나도 놓지 않는다 - 갈대가 하나 달랑 서 있지 않게.
    private int PlaceShoreReedCluster(int _seed, ShoreReedSettings _settings, bool _bFlip)
    {
        int minCount = Mathf.Max(1, Mathf.Min(_settings.clusterSizeMin, _settings.clusterSizeMax));
        int maxCount = Mathf.Max(minCount, _settings.clusterSizeMax);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);

        shoreReedClusterCells.Clear();
        shoreReedClusterCells.Add(_seed);
        shoreReedClusterReeds.Clear();
        GatherShoreReedNearbyDecos(GetShoreReedCellCenter(_seed));
        for (int head = 0; head < shoreReedClusterCells.Count && shoreReedClusterReeds.Count < count; head++)
        {
            int cell = shoreReedClusterCells[head];
            // 물 장식에 막힌 칸은 건너뛰고 그 칸 너머로도 잇지 않는다(덤불 중간이 비어 갈라져 보이지 않게).
            if (false == AddShoreReedSlots(cell, _settings)) continue;
            if (shoreReedClusterCells.Count >= ShoreReedMaxClusterCells) continue;

            int cx = cell % width;
            int cy = cell / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    // 화면 가로로 한 칸(1월드) 떨어진 대각선 이웃(+u-v, -u+v)은 갈대가 끊겨 보이므로 잇지 않는다.
                    if (dx == 0 && dy == 0) continue;
                    if (dx == -dy) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int n = nx + ny * width;
                    if (shoreReedEligible[n] && false == shoreReedClusterCells.Contains(n) && shoreReedClusterCells.Count < ShoreReedMaxClusterCells)
                    {
                        shoreReedClusterCells.Add(n);
                    }
                }
            }
        }

        if (shoreReedClusterReeds.Count < minCount) return 0;

        int placed = Mathf.Min(count, shoreReedClusterReeds.Count);
        for (int r = 0; r < placed; r++)
        {
            animatedObjGenerator.SpawnShoreReed(_settings.prefab, shoreReedClusterReeds[r], _settings.material, _bFlip);
        }
        return placed;
    }

    // 칸 하나의 물가선 위 갈대 자리 두 개를 shoreReedClusterReeds에 더한다. 하나라도 물 장식과 겹치면 둘 다 넣지 않고 false.
    // 뒤쪽 +u가 육지면 물가선은 v축을 따라 가므로 u는 육지 쪽으로 밀고 v로 벌린다(+v도 같은 방식). 둘 다 육지인 안쪽 모서리 칸은
    // 두 물가선에 하나씩 놓는다.
    private bool AddShoreReedSlots(int _cell, ShoreReedSettings _settings)
    {
        int x = _cell % width;
        int y = _cell / width;
        bool bLandU = IsLand(x + 1, y);
        bool bLandV = IsLand(x, y + 1);
        float lo = Mathf.Min(_settings.shoreOffsetRange.x, _settings.shoreOffsetRange.y);
        float hi = Mathf.Max(_settings.shoreOffsetRange.x, _settings.shoreOffsetRange.y);
        float half = Mathf.Max(0f, _settings.alongBankSpacing) * 0.5f;

        Vector2 first = default;
        for (int s = 0; s < 2; s++)
        {
            float along = (s == 0 ? -half : half) + UnityEngine.Random.Range(-ShoreReedAlongJitter, ShoreReedAlongJitter);
            float shore = UnityEngine.Random.Range(lo, hi);
            float du;
            float dv;
            if (bLandU && bLandV)
            {
                du = s == 0 ? shore : -half;
                dv = s == 0 ? -half : shore;
            }
            else if (bLandU)
            {
                du = shore;
                dv = along;
            }
            else
            {
                du = along;
                dv = shore;
            }

            Vector2 pos = GetShoreReedPosition(x + du, y + dv);
            if (false == IsShoreReedClearOfWaterDecos(pos)) return false;
            if (s == 0)
            {
                first = pos;
                continue;
            }

            // 덤불이 끊기지 않게: 두 갈대가 서로 겹치고, 이미 놓인 갈대가 있으면 그중 하나와 겹쳐야 한다(물가가 꺾이는 곳에서
            // 한 칸 건너 떨어진 갈대가 따로 서 보이지 않게). 이미 놓인 갈대와 겹치는 쪽을 먼저 넣어, 개수를 자를 때도 붙은 쪽이 남는다.
            if (false == AreShoreReedsTouching(first, pos)) return false;
            if (shoreReedClusterReeds.Count > 0)
            {
                bool bFirstTouches = IsTouchingPlacedShoreReed(first);
                if (false == bFirstTouches && false == IsTouchingPlacedShoreReed(pos)) return false;
                if (false == bFirstTouches)
                {
                    Vector2 tmp = first;
                    first = pos;
                    pos = tmp;
                }
            }
            shoreReedClusterReeds.Add(first);
            shoreReedClusterReeds.Add(pos);
        }
        return true;
    }

    private static bool AreShoreReedsTouching(Vector2 _a, Vector2 _b)
    {
        return Mathf.Abs(_a.x - _b.x) < ShoreReedTouchX && Mathf.Abs(_a.y - _b.y) < ShoreReedTouchY;
    }

    private bool IsTouchingPlacedShoreReed(Vector2 _pos)
    {
        for (int i = 0; i < shoreReedClusterReeds.Count; i++)
        {
            if (AreShoreReedsTouching(_pos, shoreReedClusterReeds[i])) return true;
        }
        return false;
    }

    private Vector2 GetShoreReedCellCenter(int _cell)
    {
        int x = _cell % width;
        int y = _cell / width;
        return waterGridOrigin + waterGridAxisU * x + waterGridAxisV * y;
    }

    // 칸 좌표(수면 중심 기준) 자리를 월드로 옮겨 장식 픽셀 격자에 맞춘다.
    private Vector2 GetShoreReedPosition(float _u, float _v)
    {
        Vector2 p = waterGridOrigin + waterGridAxisU * _u + waterGridAxisV * _v;
        float pw = grid.cellSize.x / TilePixelWidth;
        return new Vector2(Mathf.Round(p.x / pw) * pw, Mathf.Round(p.y / pw) * pw);
    }

    // 무리 하나가 닿을 수 있는 물 장식만 미리 골라 둔다(무리 자리 칸 중심에서 ShoreReedClusterReach 안).
    // 갈대 자리마다 맵의 물 장식 전체를 훑지 않게 한다.
    private void GatherShoreReedNearbyDecos(Vector2 _center)
    {
        shoreReedNearbyDecos.Clear();
        float reach = ShoreReedClusterReach;
        for (int i = 0; i < mapPlacedWaterDecos.Count; i++)
        {
            PlacedWaterDeco other = mapPlacedWaterDecos[i];
            bool bApartX = _center.x + reach <= other.position.x + other.min.x || other.position.x + other.max.x <= _center.x - reach;
            bool bApartY = _center.y + reach <= other.position.y + other.min.y || other.position.y + other.max.y <= _center.y - reach;
            if (false == bApartX && false == bApartY) shoreReedNearbyDecos.Add(other);
        }
    }

    private bool IsShoreReedClearOfWaterDecos(Vector2 _pos)
    {
        for (int i = 0; i < shoreReedNearbyDecos.Count; i++)
        {
            PlacedWaterDeco other = shoreReedNearbyDecos[i];
            bool bApartX = _pos.x + ShoreReedHalfWidth <= other.position.x + other.min.x || other.position.x + other.max.x <= _pos.x - ShoreReedHalfWidth;
            bool bApartY = _pos.y + ShoreReedHalfHeight <= other.position.y + other.min.y || other.position.y + other.max.y <= _pos.y - ShoreReedHalfHeight;
            if (false == bApartX && false == bApartY) return false;
        }
        return true;
    }

    private void TryEnqueueWaterCompCell(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (waterCompVisited[idx] || waterTiles[idx] == null) return;

        waterCompVisited[idx] = true;
        waterCompQueue.Enqueue(idx);
    }

    // 풀 크기(웅덩이는 물 칸 수, 바다는 깊은 물 칸 수) * 밀도의 기댓값을 정수부 + 소수부 확률적 반올림으로 변환하되,
    // 풀이 1칸 이상이고 밀도가 0보다 크면 최소 1개는 항상 보장한다.
    // (기댓값만 따르면 작은 웅덩이는 기댓값 자체가 1 미만이라 거의 항상 0개가 되어,
    //  타일별 독립 확률로 뽑던 기존 방식과 통계적으로 별 차이가 없었다.)
    private int ComputeDistributedCount(int _poolSize, float _density)
    {
        if (_poolSize <= 0 || _density <= 0f) return 0;

        float expected = _poolSize * _density;
        int intPart = Mathf.FloorToInt(expected);
        float frac = expected - intPart;
        if (UnityEngine.Random.value < frac) intPart++;

        if (intPart <= 0) intPart = 1;

        return Mathf.Min(intPart, _poolSize);
    }

    // ── 물가 픽셀 모델 ──
    // 칸 좌표(u, v): 물 칸 수면 중심이 0, 가장자리가 ±0.5. +u = x+1 이웃(화면 오른쪽 위), +v = y+1 이웃(화면 왼쪽 위).
    // 아래 값은 스테이지 1~4의 물·육지 타일 그림에서 잰 것이다(네 스테이지 모두 모양이 같다). 타일 그림을 바꾸면 다시 재야 한다.
    //  - 물 타일 윗면 마름모는 칸 중심보다 1px 위에 그려진다(WaterSurfaceOffsetPixels).
    //  - 뒤쪽(+u, +v) 이웃이 육지면 물 타일 가장자리 거품이 11/64칸 지점부터, 앞쪽(-u, -v)이면 15/64칸 지점부터 그려진다.
    //  - 대각선 이웃만 육지면 모서리 타일(WaterCornerTilemap, GetWaterCornerTile과 같은 조건)이 같은 깊이의 귀퉁이를 덮는다.
    //  - 육지 타일은 윗면이 반 칸(8px) 솟은 블록이고 앞에 있는 타일이 나중에 그려진다. 그래서 앞쪽(화면 아래) 육지 블록은
    //    뒤에 있는 물 칸을 덮고, 뒤쪽 육지 블록의 옆면은 물 타일에 가려진다.
    // 190칸 맵 전체를 실제로 렌더해 대조했을 때 이 모델이 물가로 판정하지 못한 픽셀은 경계 반올림 1px뿐이었고,
    // 그 1px은 ShoreEdgeEpsilon으로 물가 쪽에 넣는다(남는 쪽은 언제나 물가로 보는 쪽, 즉 안전한 쪽이다).
    private const int TilePixelWidth = 32;
    private const float WaterSurfaceOffsetPixels = 1f;
    private const float ShoreBackFoamStart = 11f / 64f;
    private const float ShoreFrontFoamStart = 15f / 64f;
    private const float ShoreEdgeEpsilon = 1f / 256f;

    // 장식 하나에 볼 유효 후보 수(많을수록 더 고르게 퍼진다)와 무작위 후보를 뽑는 최대 횟수.
    private const int WaterDecoCandidatesPerPick = 12;
    private const int WaterDecoMaxRandomTries = 64;
    // 무작위 후보가 모두 빗나갔을 때 한 칸을 훑는 격자(가로·세로 칸 수).
    private const int WaterDecoLatticeSteps = 8;
    // 장식끼리 붙어 보이지 않도록 띄우는 픽셀 수.
    private const float WaterDecoGapPixels = 2f;

    private sealed class WaterDecoFootprint
    {
        // 피벗 기준 월드 오프셋(픽셀 중심). 테두리 픽셀이 앞에 있어 걸리는 자리를 빨리 버린다.
        public Vector2[] points;
        // 피벗 기준 픽셀 가장자리까지의 범위(장식끼리 간격 판정용).
        public Vector2 min;
        public Vector2 max;
        public float pixelWorld;
    }

    private struct PlacedWaterDeco
    {
        public Vector2 position;
        public Vector2 min;
        public Vector2 max;
    }

    private readonly Dictionary<DecoSpritePatternAnimator, WaterDecoFootprint> waterDecoFootprints = new Dictionary<DecoSpritePatternAnimator, WaterDecoFootprint>(8);
    private WaterDecoFootprint fallbackWaterDecoFootprint;
    private readonly List<PlacedWaterDeco> placedWaterDecos = new List<PlacedWaterDeco>(16);
    // 이번 맵 안에 놓인 물 장식 전부(웅덩이마다 비우지 않는다). 물가 갈대가 이 자리를 피한다.
    private readonly List<PlacedWaterDeco> mapPlacedWaterDecos = new List<PlacedWaterDeco>(64);
    // 웅덩이마다 노리는 최소 장식 수(종류가 그보다 적으면 종류 수만큼).
    private const int WaterDecoPondMinCount = 2;

    // 중복을 뺀 A 타입 프리팹과 종류별 누적 개수(웅덩이·바다 따로, 맵마다 비움), 지금 웅덩이에서 종류별로 놓은 수.
    private readonly List<DecoSpritePatternAnimator> waterDecoTypes = new List<DecoSpritePatternAnimator>(8);
    private int[] waterDecoPondTypeTotals = Array.Empty<int>();
    private int[] waterDecoSeaTypeTotals = Array.Empty<int>();
    private int[] waterDecoCompTypeUses = Array.Empty<int>();
    private bool[] waterDecoTypeTried = Array.Empty<bool>();

    // 칸마다 이웃 8칸의 육지 여부(아래 비트). 물 타일이 없는 칸은 WaterDecoCellBlocked.
    private const int LandPosU = 1 << 0;
    private const int LandPosV = 1 << 1;
    private const int LandNegU = 1 << 2;
    private const int LandNegV = 1 << 3;
    private const int LandPosUPosV = 1 << 4;
    private const int LandPosUNegV = 1 << 5;
    private const int LandNegUNegV = 1 << 6;
    private const int LandNegUPosV = 1 << 7;
    private const ushort WaterDecoCellBlocked = 1 << 8;
    private ushort[] waterDecoCellMasks = Array.Empty<ushort>();
    // 이번 맵에서 PrepareWaterDecoGrid·PrepareWaterDecoTypes를 했는지(맵 밖 바다 장식이 다시 하지 않도록). GenerateMap이 비운다.
    private bool bWaterDecoGridReady;

    private Vector2 waterGridOrigin;
    private Vector2 waterGridAxisU;
    private Vector2 waterGridAxisV;
    private float waterGridInvDet;

    // 칸 (0,0)의 수면 중심과 칸 한 개씩의 월드 이동(worldPosMap이 같은 그리드에서 나오므로 그대로 쓴다).
    private void PrepareWaterDecoGrid()
    {
        float pixelWorld = grid.cellSize.x / TilePixelWidth;
        waterGridAxisU = worldPosMap[1] - worldPosMap[0];
        waterGridAxisV = worldPosMap[width] - worldPosMap[0];
        waterGridOrigin = (Vector2)worldPosMap[0] + new Vector2(0f, WaterSurfaceOffsetPixels * pixelWorld - halfCellY);
        waterGridInvDet = 1f / (waterGridAxisU.x * waterGridAxisV.y - waterGridAxisU.y * waterGridAxisV.x);
        bWaterDecoGridReady = true;

        // 픽셀마다 이웃 8칸의 육지 여부를 다시 묻지 않도록 칸마다 한 번만 계산해 둔다.
        // 물 타일이 없는 칸은 막힌 칸, 이웃에 육지가 하나도 없는 칸(0)은 칸 안 어느 픽셀이든 맨 수면이다.
        int size = width * height;
        if (waterDecoCellMasks.Length != size) waterDecoCellMasks = new ushort[size];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                if (waterTiles[i] == null)
                {
                    waterDecoCellMasks[i] = WaterDecoCellBlocked;
                    continue;
                }

                int mask = 0;
                if (IsLand(x + 1, y)) mask |= LandPosU;
                if (IsLand(x, y + 1)) mask |= LandPosV;
                if (IsLand(x - 1, y)) mask |= LandNegU;
                if (IsLand(x, y - 1)) mask |= LandNegV;
                if (IsLand(x + 1, y + 1)) mask |= LandPosUPosV;
                if (IsLand(x + 1, y - 1)) mask |= LandPosUNegV;
                if (IsLand(x - 1, y - 1)) mask |= LandNegUNegV;
                if (IsLand(x - 1, y + 1)) mask |= LandNegUPosV;
                waterDecoCellMasks[i] = (ushort)mask;
            }
        }
    }

    private Vector2 WorldToWaterGrid(Vector2 _delta)
    {
        return new Vector2(
            (_delta.x * waterGridAxisV.y - _delta.y * waterGridAxisV.x) * waterGridInvDet,
            (waterGridAxisU.x * _delta.y - waterGridAxisU.y * _delta.x) * waterGridInvDet);
    }

    // _landMask: 이웃 8칸 중 육지인 칸(PrepareWaterDecoGrid가 칸마다 미리 계산한 값).
    private static bool IsShorePixel(int _landMask, float _du, float _dv)
    {
        float back = ShoreBackFoamStart - ShoreEdgeEpsilon;
        float front = ShoreFrontFoamStart - ShoreEdgeEpsilon;

        bool landPosU = (_landMask & LandPosU) != 0;
        bool landPosV = (_landMask & LandPosV) != 0;
        bool landNegU = (_landMask & LandNegU) != 0;
        bool landNegV = (_landMask & LandNegV) != 0;

        // 물 타일 가장자리 거품(GetWaterTile)
        if (landPosU && _du >= back) return true;
        if (landPosV && _dv >= back) return true;
        if (landNegU && _du <= -front) return true;
        if (landNegV && _dv <= -front) return true;

        // 모서리 타일(GetWaterCornerTile)
        bool landPosUPosV = (_landMask & LandPosUPosV) != 0;
        bool landPosUNegV = (_landMask & LandPosUNegV) != 0;
        bool landNegUNegV = (_landMask & LandNegUNegV) != 0;
        bool landNegUPosV = (_landMask & LandNegUPosV) != 0;
        if (landPosUPosV && false == landPosU && false == landPosV && _du >= back && _dv >= back) return true;
        if (landPosUNegV && false == landPosU && false == landNegV && _du >= back && _dv <= -back) return true;
        if (landNegUNegV && false == landNegU && false == landNegV && _du <= -front && _dv <= -front) return true;
        if (landNegUPosV && false == landNegU && false == landPosV && _du <= -back && _dv >= back) return true;

        // 앞쪽(같은 높이 포함) 육지 블록
        if (landNegU && IsCoveredByLandBlock(_du + 1f, _dv)) return true;
        if (landNegV && IsCoveredByLandBlock(_du, _dv + 1f)) return true;
        if (landNegUNegV && IsCoveredByLandBlock(_du + 1f, _dv + 1f)) return true;
        if (landPosUNegV && IsCoveredByLandBlock(_du - 1f, _dv + 1f)) return true;
        if (landNegUPosV && IsCoveredByLandBlock(_du + 1f, _dv - 1f)) return true;

        return false;
    }

    // 육지 블록 그림은 칸 마름모를 반 칸 위(+u, +v로 0.5)부터 반 칸 아래(-0.5)까지 쓸어내린 육각형이다.
    // (_a, _b)는 육지 칸 중심 기준 좌표. 어떤 t∈[-0.5, 0.5]에서 |_a - t| ≤ 0.5, |_b - t| ≤ 0.5이면 덮인다.
    private static bool IsCoveredByLandBlock(float _a, float _b)
    {
        float lo = Mathf.Max(Mathf.Max(_a, _b) - 0.5f, -0.5f);
        float hi = Mathf.Min(Mathf.Min(_a, _b) + 0.5f, 0.5f);
        return lo <= hi + ShoreEdgeEpsilon;
    }

    // 스테이지의 A 타입 프리팹을 중복 없이 모은다(목록에 같은 프리팹이 두 번 있어도 한 종류로 본다)
    // 그리고 종류별 누적 개수를 비운다. 맵을 만들 때마다 한 번 부른다.
    private void PrepareWaterDecoTypes()
    {
#if UNITY_EDITOR
        // 에디터에서는 프레임을 바꾸면 프리팹 모양이 다시 재지므로(DecoSpritePatternAnimator.OnValidate) 맵마다 새로 읽는다.
        waterDecoFootprints.Clear();
#endif
        waterDecoTypes.Clear();
        List<DecoSpritePatternAnimator> prefabs = stageTileData.WaterAnimatedObjPrefabs;
        if (prefabs != null)
        {
            for (int k = 0; k < prefabs.Count; k++)
            {
                if (prefabs[k] != null && false == waterDecoTypes.Contains(prefabs[k])) waterDecoTypes.Add(prefabs[k]);
            }
        }

        int typeCount = waterDecoTypes.Count;
        if (waterDecoPondTypeTotals.Length != typeCount)
        {
            waterDecoPondTypeTotals = new int[typeCount];
            waterDecoSeaTypeTotals = new int[typeCount];
            waterDecoCompTypeUses = new int[typeCount];
            waterDecoTypeTried = new bool[typeCount];
        }
        else
        {
            Array.Clear(waterDecoPondTypeTotals, 0, typeCount);
            Array.Clear(waterDecoSeaTypeTotals, 0, typeCount);
        }
    }

    // A 타입(물 장식 프리팹) 하나를 놓는다. 종류가 골고루 나오도록, 이 웅덩이에서 덜 쓴 종류 → 맵 전체(웅덩이끼리,
    // 바다끼리)에서 덜 쓴 종류 순으로 먼저 시도하고, 같으면 무작위로 고른다. 고른 종류가 들어갈 자리가 없으면
    // (좁은 웅덩이에 큰 물결 등) 다음 순서의 종류를 시도한다.
    private bool PlaceOneWaterDeco(bool _bSea, bool _bExhaustiveFallback)
    {
        int typeCount = waterDecoTypes.Count;
        if (typeCount == 0) return false;

        int[] totals = _bSea ? waterDecoSeaTypeTotals : waterDecoPondTypeTotals;
        Array.Clear(waterDecoTypeTried, 0, typeCount);

        for (int attempt = 0; attempt < typeCount; attempt++)
        {
            int pick = -1;
            int ties = 0;
            for (int k = 0; k < typeCount; k++)
            {
                if (waterDecoTypeTried[k]) continue;
                if (pick < 0
                    || waterDecoCompTypeUses[k] < waterDecoCompTypeUses[pick]
                    || (waterDecoCompTypeUses[k] == waterDecoCompTypeUses[pick] && totals[k] < totals[pick]))
                {
                    pick = k;
                    ties = 1;
                }
                else if (waterDecoCompTypeUses[k] == waterDecoCompTypeUses[pick] && totals[k] == totals[pick])
                {
                    ties++;
                    if (UnityEngine.Random.Range(0, ties) == 0) pick = k;
                }
            }
            waterDecoTypeTried[pick] = true;

            DecoSpritePatternAnimator prefab = waterDecoTypes[pick];
            WaterDecoFootprint footprint = GetWaterDecoFootprint(prefab);
            if (false == TryFindWaterDecoSpot(footprint, _bExhaustiveFallback, out Vector2 pos)) continue;

            AddPlacedWaterDeco(pos, footprint);
            animatedObjGenerator.SpawnWaterAnimatedObj(pos, prefab);
            waterDecoCompTypeUses[pick]++;
            totals[pick]++;
            return true;
        }
        return false;
    }

    // waterCompCells(지금 웅덩이) 안에서 _footprint가 맨 수면에만 닿고 이미 놓인 장식과 떨어진 자리를 찾는다.
    private bool TryFindWaterDecoSpot(WaterDecoFootprint _footprint, bool _bExhaustiveFallback, out Vector2 _pos)
    {
        _pos = default;
        if (waterCompCells.Count == 0) return false;

        bool bFound = false;
        float bestScore = -1f;
        int valid = 0;

        for (int t = 0; t < WaterDecoMaxRandomTries && valid < WaterDecoCandidatesPerPick; t++)
        {
            int cell = waterCompCells[UnityEngine.Random.Range(0, waterCompCells.Count)];
            Vector2 candidate = GetWaterDecoCandidate(cell, UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f, _footprint);
            if (false == IsWaterDecoSpotClear(candidate, _footprint)) continue;

            valid++;
            float score = DistanceToPlacedWaterDecos(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                _pos = candidate;
                bFound = true;
            }
        }

        if (bFound || false == _bExhaustiveFallback) return bFound;

        // 무작위 후보가 모두 빗나갔다(좁은 웅덩이). 웅덩이 전체를 격자로 훑어, 맞는 자리 중 이미 놓인 장식에서
        // 가장 먼 자리를 고른다(같은 거리면 무작위 - 아직 놓인 장식이 없으면 맞는 자리 전체에서 고르게 뽑힌다).
        int tieCount = 0;
        float step = 1f / WaterDecoLatticeSteps;
        for (int c = 0; c < waterCompCells.Count; c++)
        {
            int cell = waterCompCells[c];
            for (int iu = 0; iu < WaterDecoLatticeSteps; iu++)
            {
                for (int iv = 0; iv < WaterDecoLatticeSteps; iv++)
                {
                    Vector2 candidate = GetWaterDecoCandidate(cell, (iu + 0.5f) * step - 0.5f, (iv + 0.5f) * step - 0.5f, _footprint);
                    if (false == IsWaterDecoSpotClear(candidate, _footprint)) continue;

                    float score = DistanceToPlacedWaterDecos(candidate);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        tieCount = 1;
                        _pos = candidate;
                    }
                    else if (score == bestScore)
                    {
                        tieCount++;
                        if (UnityEngine.Random.Range(0, tieCount) == 0) _pos = candidate;
                    }
                }
            }
        }
        return tieCount > 0;
    }

    // 칸 수면 중심에서 (_du, _dv)칸 떨어진 점을 장식 픽셀 격자에 맞춘다(타일 픽셀과 장식 픽셀이 어긋나지 않게).
    private Vector2 GetWaterDecoCandidate(int _cell, float _du, float _dv, WaterDecoFootprint _footprint)
    {
        int x = _cell % width;
        int y = _cell / width;
        Vector2 p = waterGridOrigin + waterGridAxisU * (x + _du) + waterGridAxisV * (y + _dv);
        float pw = _footprint.pixelWorld;
        return new Vector2(Mathf.Round(p.x / pw) * pw, Mathf.Round(p.y / pw) * pw);
    }

    private bool IsWaterDecoSpotClear(Vector2 _pos, WaterDecoFootprint _footprint)
    {
        float gap = WaterDecoGapPixels * _footprint.pixelWorld;
        for (int i = 0; i < placedWaterDecos.Count; i++)
        {
            PlacedWaterDeco other = placedWaterDecos[i];
            bool bApartX = _pos.x + _footprint.max.x + gap <= other.position.x + other.min.x || other.position.x + other.max.x + gap <= _pos.x + _footprint.min.x;
            bool bApartY = _pos.y + _footprint.max.y + gap <= other.position.y + other.min.y || other.position.y + other.max.y + gap <= _pos.y + _footprint.min.y;
            if (false == bApartX && false == bApartY) return false;
        }

        // 장식 사각형이 걸치는 칸이 모두 이웃에 육지가 없는 물 칸이면 픽셀을 하나씩 볼 필요가 없다(넓은 수면 대부분).
        if (IsWaterDecoRectOnOpenWater(_pos, _footprint)) return true;

        // 그리드 밖(맵 밖 확장 물)은 보지 않는다 — 맵 안 장식은 그리드 안에만 놓는다.
        return AreWaterDecoPixelsOnOpenWater(_pos, _footprint, -1f);
    }

    // 픽셀마다 그 점이 물가·육지가 아닌 맨 수면인지 본다. 물 타일이 깔린 칸만 수면이고, 육지 칸과 맵 바깥 원 너머처럼
    // 타일이 없는 칸(빈 공간)은 걸린 것으로 본다. 그리드 밖 칸은 _outerRadiusSq가 0 이상이면 확장 물 타원
    // (ApplyOuterWaterTiles와 같은 조건, 물가 없는 깊은 물) 안일 때만 수면이고, 음수면 걸린 것으로 본다.
    // 자리마다 수천 번 도는 곳이라 함수 호출과 필드 읽기를 줄이려고 칸 좌표 변환(WorldToWaterGrid)을 풀어 썼다.
    private bool AreWaterDecoPixelsOnOpenWater(Vector2 _pos, WaterDecoFootprint _footprint, float _outerRadiusSq)
    {
        Vector2[] points = _footprint.points;
        float originX = waterGridOrigin.x;
        float originY = waterGridOrigin.y;
        float axisUX = waterGridAxisU.x;
        float axisUY = waterGridAxisU.y;
        float axisVX = waterGridAxisV.x;
        float axisVY = waterGridAxisV.y;
        float invDet = waterGridInvDet;
        ushort[] masks = waterDecoCellMasks;
        int gridWidth = width;
        int gridHeight = height;
        for (int i = 0; i < points.Length; i++)
        {
            float dx = (_pos.x + points[i].x) - originX;
            float dy = (_pos.y + points[i].y) - originY;
            float gu = (dx * axisVY - dy * axisVX) * invDet;
            float gv = (axisUX * dy - axisUY * dx) * invDet;
            int cx = Mathf.RoundToInt(gu);
            int cy = Mathf.RoundToInt(gv);
            if (cx < 0 || cx >= gridWidth || cy < 0 || cy >= gridHeight)
            {
                if (_outerRadiusSq < 0f) return false;
                float ox = cx - gridWidth * 0.5f;
                float oy = cy - gridHeight * 0.5f;
                if (ox * ox + oy * oy > _outerRadiusSq) return false;
                continue;
            }

            int mask = masks[cx + cy * gridWidth];
            if (mask == 0) continue;
            if (mask == WaterDecoCellBlocked) return false;
            if (IsShorePixel(mask, gu - cx, gv - cy)) return false;
        }
        return true;
    }

    // 장식 사각형(픽셀 가장자리 기준)을 칸 좌표로 옮긴 평행사변형의 경계 상자 안 칸이 모두 열린 물(마스크 0)인가.
    // 장식의 모든 픽셀은 이 상자 안 칸으로 반올림되므로(반올림은 단조), 참이면 픽셀별 판정도 모두 참이다.
    private bool IsWaterDecoRectOnOpenWater(Vector2 _pos, WaterDecoFootprint _footprint)
    {
        Vector2 origin = _pos - waterGridOrigin;
        Vector2 a = WorldToWaterGrid(origin + _footprint.min);
        Vector2 b = WorldToWaterGrid(origin + new Vector2(_footprint.max.x, _footprint.min.y));
        Vector2 c = WorldToWaterGrid(origin + new Vector2(_footprint.min.x, _footprint.max.y));
        Vector2 d = WorldToWaterGrid(origin + _footprint.max);

        int u0 = Mathf.RoundToInt(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)));
        int u1 = Mathf.RoundToInt(Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)));
        int v0 = Mathf.RoundToInt(Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)));
        int v1 = Mathf.RoundToInt(Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)));
        if (u0 < 0 || v0 < 0 || u1 >= width || v1 >= height) return false;

        for (int v = v0; v <= v1; v++)
        {
            int row = v * width;
            for (int u = u0; u <= u1; u++)
            {
                if (waterDecoCellMasks[row + u] != 0) return false;
            }
        }
        return true;
    }

    // 맵 밖 바다(ApplyOuterWaterTiles) 칸 꼭짓점 자리에 들어가는 물 장식 프리팹을 고른다. 종류는 중복 없이 같은 확률로,
    // 무작위로 고른 종류부터 차례로 보고 모양이 물 위에만 놓이는 첫 종류를 쓴다. 들어가는 종류가 없으면 null.
    private DecoSpritePatternAnimator PickOuterWaterDecoPrefab(Vector2 _pos, float _outerRadiusSq)
    {
        int typeCount = waterDecoTypes.Count;
        if (typeCount == 0) return null;

        int start = UnityEngine.Random.Range(0, typeCount);
        for (int k = 0; k < typeCount; k++)
        {
            DecoSpritePatternAnimator prefab = waterDecoTypes[(start + k) % typeCount];
            if (AreWaterDecoPixelsOnOpenWater(_pos, GetWaterDecoFootprint(prefab), _outerRadiusSq)) return prefab;
        }
        return null;
    }

    // 칸 좌표 거리(아이소메트릭 화면에서 고르게 보이는 거리). 놓인 장식이 없으면 최대값.
    private float DistanceToPlacedWaterDecos(Vector2 _pos)
    {
        float best = float.MaxValue;
        for (int i = 0; i < placedWaterDecos.Count; i++)
        {
            float d = WorldToWaterGrid(_pos - placedWaterDecos[i].position).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }

    private void AddPlacedWaterDeco(Vector2 _pos, WaterDecoFootprint _footprint)
    {
        PlacedWaterDeco placed = new PlacedWaterDeco { position = _pos, min = _footprint.min, max = _footprint.max };
        placedWaterDecos.Add(placed);
        mapPlacedWaterDecos.Add(placed);
    }

    private WaterDecoFootprint GetWaterDecoFootprint(DecoSpritePatternAnimator _prefab)
    {
        if (waterDecoFootprints.TryGetValue(_prefab, out WaterDecoFootprint footprint)) return footprint;

        Vector3Int[] rows = _prefab.WaterFootprintRows;
        float ppu = _prefab.WaterFootprintPixelsPerUnit;
        if (rows == null || rows.Length == 0 || ppu <= 0f)
        {
            // 모양을 잰 적이 없으면 첫 프레임 사각형 전체를 모양으로 본다(넉넉한 쪽).
            Sprite sprite = _prefab.FirstFrame;
            rows = null != sprite ? BuildRectFootprintRows(sprite) : null;
            ppu = null != sprite ? sprite.pixelsPerUnit : TilePixelWidth;
        }

        footprint = null != rows ? BuildWaterDecoFootprint(rows, ppu, _prefab.transform.localScale) : GetFallbackWaterDecoFootprint();
        waterDecoFootprints.Add(_prefab, footprint);
        return footprint;
    }

    // 모양을 모르는 B 타입(AnimatedObj) 물 오브젝트용. 가장 큰 물 장식(물결)과 같은 20x13px 사각형으로 본다.
    private WaterDecoFootprint GetFallbackWaterDecoFootprint()
    {
        if (null != fallbackWaterDecoFootprint) return fallbackWaterDecoFootprint;

        var rows = new Vector3Int[13];
        for (int i = 0; i < rows.Length; i++) rows[i] = new Vector3Int(i - 7, -10, 9);
        fallbackWaterDecoFootprint = BuildWaterDecoFootprint(rows, TilePixelWidth, Vector3.one);
        return fallbackWaterDecoFootprint;
    }

    private static Vector3Int[] BuildRectFootprintRows(Sprite _sprite)
    {
        int w = Mathf.RoundToInt(_sprite.rect.width);
        int h = Mathf.RoundToInt(_sprite.rect.height);
        int pivotX = Mathf.RoundToInt(_sprite.pivot.x);
        int pivotY = Mathf.RoundToInt(_sprite.pivot.y);

        var rows = new Vector3Int[h];
        for (int y = 0; y < h; y++) rows[y] = new Vector3Int(y - pivotY, -pivotX, w - 1 - pivotX);
        return rows;
    }

    // (y, xMin, xMax) 가로줄을 픽셀 중심 월드 오프셋으로 바꾼다. 테두리 픽셀(4방향 이웃 중 모양 밖이 있는 픽셀)을 먼저 둔다.
    private static WaterDecoFootprint BuildWaterDecoFootprint(Vector3Int[] _rows, float _ppu, Vector3 _scale)
    {
        var filled = new HashSet<Vector2Int>();
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int r = 0; r < _rows.Length; r++)
        {
            Vector3Int row = _rows[r];
            for (int x = row.y; x <= row.z; x++) filled.Add(new Vector2Int(x, row.x));
            minX = Mathf.Min(minX, row.y);
            maxX = Mathf.Max(maxX, row.z);
            minY = Mathf.Min(minY, row.x);
            maxY = Mathf.Max(maxY, row.x);
        }

        var edge = new List<Vector2>(filled.Count);
        var inner = new List<Vector2>(filled.Count);
        float sx = _scale.x / _ppu;
        float sy = _scale.y / _ppu;
        foreach (Vector2Int p in filled)
        {
            bool bEdge = false == filled.Contains(new Vector2Int(p.x + 1, p.y)) || false == filled.Contains(new Vector2Int(p.x - 1, p.y))
                      || false == filled.Contains(new Vector2Int(p.x, p.y + 1)) || false == filled.Contains(new Vector2Int(p.x, p.y - 1));
            Vector2 offset = new Vector2((p.x + 0.5f) * sx, (p.y + 0.5f) * sy);
            (bEdge ? edge : inner).Add(offset);
        }
        edge.AddRange(inner);

        return new WaterDecoFootprint
        {
            points = edge.ToArray(),
            min = new Vector2(minX * sx, minY * sy),
            max = new Vector2((maxX + 1) * sx, (maxY + 1) * sy),
            pixelWorld = 1f / _ppu,
        };
    }

    private TileBase GetWaterTile(int _x, int _y)
    {
        if (stageTileData == null) return null;

        int mask = 0;
        if (IsLand(_x + 1, _y)) mask |= 1;  // RU
        if (IsLand(_x, _y - 1)) mask |= 2;  // RD
        if (IsLand(_x, _y + 1)) mask |= 4;  // LU
        if (IsLand(_x - 1, _y)) mask |= 8;  // LD

        switch (mask)
        {
            case 1: return stageTileData.WaterTileBorderRU;
            case 2: return stageTileData.WaterTileBorderRD;
            case 3: return stageTileData.WaterTileBorderRURD;
            case 4: return stageTileData.WaterTileBorderLU;
            case 5: return stageTileData.WaterTileBorderRULU;
            case 6: return stageTileData.WaterTileBorderRDLU;
            case 7: return stageTileData.WaterTileBorderRURDLU;
            case 8: return stageTileData.WaterTileBorderLD;
            case 9: return stageTileData.WaterTileBorderRULD;
            case 10: return stageTileData.WaterTileBorderRDLD;
            case 11: return stageTileData.WaterTileBorderRURDLD;
            case 12: return stageTileData.WaterTileBorderLULD;
            case 13: return stageTileData.WaterTileBorderRULULD;
            case 14: return stageTileData.WaterTileBorderRDLULD;
            case 15: return stageTileData.WaterTileBorderAll;
            default: return stageTileData.WaterTile;
        }
    }

    private TileBase GetWaterCornerTile(int _x, int _y)
    {
        if (stageTileData == null) return null;

        int cornerMask = 0;
        if (IsLand(_x + 1, _y + 1) && !IsLand(_x + 1, _y) && !IsLand(_x, _y + 1)) cornerMask |= 1;  // U
        if (IsLand(_x + 1, _y - 1) && !IsLand(_x + 1, _y) && !IsLand(_x, _y - 1)) cornerMask |= 2;  // R
        if (IsLand(_x - 1, _y - 1) && !IsLand(_x, _y - 1) && !IsLand(_x - 1, _y)) cornerMask |= 4;  // D
        if (IsLand(_x - 1, _y + 1) && !IsLand(_x, _y + 1) && !IsLand(_x - 1, _y)) cornerMask |= 8;  // L

        switch (cornerMask)
        {
            case 1: return stageTileData.WaterTileCornerU;
            case 2: return stageTileData.WaterTileCornerR;
            case 3: return stageTileData.WaterTileCornerUR;
            case 4: return stageTileData.WaterTileCornerD;
            case 5: return stageTileData.WaterTileCornerUD;
            case 6: return stageTileData.WaterTileCornerRD;
            case 7: return stageTileData.WaterTileCornerURD;
            case 8: return stageTileData.WaterTileCornerL;
            case 9: return stageTileData.WaterTileCornerUL;
            case 10: return stageTileData.WaterTileCornerRL;
            case 11: return stageTileData.WaterTileCornerURL;
            case 12: return stageTileData.WaterTileCornerDL;
            case 13: return stageTileData.WaterTileCornerUDL;
            case 14: return stageTileData.WaterTileCornerRDL;
            case 15: return stageTileData.WaterTileCornerAll;
            default: return null;
        }
    }

    private bool IsLand(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return false;
        return noiseValues[_x + _y * width] >= waterThreshold;
    }

    private bool IsWater(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return true; // 맵 밖은 모두 확장된 물 영역
        return noiseValues[_x + _y * width] < waterThreshold;
    }

    private Vector3 GetWorldPos(int _idx)
    {
        if (_idx < 0 || _idx >= worldPosMap.Length) return Vector3.zero;
        return worldPosMap[_idx];
    }

    // ── 외부 물 타일 확장 ──

    private void ApplyOuterWaterTiles()
    {
        if (stageTileData == null) return;

        float cX = width * 0.5f;
        float cY = height * 0.5f;
        float mapRadius = baseMapRadius;
        float outerRadius = mapRadius + outerWaterDepth;
        float outerRadiusSq = outerRadius * outerRadius;

        int extMinX = Mathf.FloorToInt(cX - outerRadius) - 1;
        int extMinY = Mathf.FloorToInt(cY - outerRadius) - 1;
        int extMaxX = Mathf.CeilToInt(cX + outerRadius) + 2;  // exclusive
        int extMaxY = Mathf.CeilToInt(cY + outerRadius) + 2;  // exclusive

        int extW = extMaxX - extMinX;
        int extH = extMaxY - extMinY;
        int extSize = extW * extH;

        // 확장 영역 타일 배열 (Corner 제외, Corner는 원본 범위 150x150에서만 ApplyTiles를 통해 적용됨)
        // 크기가 같으면 재사용하고 비운다(새 배열과 똑같이 전부 null에서 시작해야 continue로 건너뛴 칸이 비어 있다).
        if (extTilesCacheSize != extSize)
        {
            extWaterTilesCache = new TileBase[extSize];
            extWaterCollisionTilesCache = new TileBase[extSize];
            extWaterStencilTilesCache = new TileBase[extSize];
            extBloomWaterDecoTilesCache = new TileBase[extSize];
            extWaterDecoTilesCache = new TileBase[extSize];
            extTilesCacheSize = extSize;
        }
        else
        {
            Array.Clear(extWaterTilesCache, 0, extSize);
            Array.Clear(extWaterCollisionTilesCache, 0, extSize);
            Array.Clear(extWaterStencilTilesCache, 0, extSize);
            Array.Clear(extBloomWaterDecoTilesCache, 0, extSize);
            Array.Clear(extWaterDecoTilesCache, 0, extSize);
        }

        TileBase[] extWaterTiles = extWaterTilesCache;
        TileBase[] extWaterCollisionTiles = extWaterCollisionTilesCache;
        TileBase[] extWaterStencilTiles = extWaterStencilTilesCache;
        TileBase[] extBloomWaterDecoTiles = extBloomWaterDecoTilesCache;
        TileBase[] extWaterDecoTiles = extWaterDecoTilesCache;

        TileBase collisionTile = stageTileData.TreeCollisionTile;
        TileBase stencilTile = stageTileData.StencilTile;
        TileBase defaultWaterTile = stageTileData.WaterTile;

        // 맵 밖 바다 장식도 모양 판정을 쓴다. 맵 안 물 장식을 만들지 않은 맵(로그라이크 타원 섬)이면 여기서 준비한다.
        if (animatedObjGenerator != null && false == bWaterDecoGridReady)
        {
            PrepareWaterDecoGrid();
            PrepareWaterDecoTypes();
        }

        for (int ey = 0; ey < extH; ey++)
        {
            int worldY = ey + extMinY;
            for (int ex = 0; ex < extW; ex++)
            {
                int worldX = ex + extMinX;
                int extIdx = ex + ey * extW;

                // 원본 맵 150x150 범위 내는 기존 ApplyTiles가 완벽히 처리했으므로 데이터 복사만 수행
                if (worldX >= 0 && worldX < width && worldY >= 0 && worldY < height)
                {
                    int origIdx = worldX + worldY * width;
                    extWaterTiles[extIdx] = waterTiles[origIdx];
                    extWaterCollisionTiles[extIdx] = waterCollisionTiles[origIdx];
                    extWaterStencilTiles[extIdx] = waterStencilTiles[origIdx];
                    extWaterDecoTiles[extIdx] = waterDecoTilesToApply[origIdx];
                    extBloomWaterDecoTiles[extIdx] = bloomWaterDecoTilesToApply[origIdx];
                    continue;
                }

                float dx = worldX - cX;
                float dy = worldY - cY;
                float distSq = dx * dx + dy * dy;

                if (distSq > outerRadiusSq) continue;

                // 원본 맵 바깥(150x150 밖) 타원 내부는 모두 완전한 깊은 물
                extWaterTiles[extIdx] = defaultWaterTile;
                extWaterCollisionTiles[extIdx] = collisionTile;
                extWaterStencilTiles[extIdx] = stencilTile;

                if (stageTileData.WaterDecoTiles != null && stageTileData.WaterDecoTiles.Count > 0
                    && UnityEngine.Random.value < stageTileData.WaterDecoDensity)
                {
                    extWaterDecoTiles[extIdx] = stageTileData.WaterDecoTiles[
                        UnityEngine.Random.Range(0, stageTileData.WaterDecoTiles.Count)];
                }

                if (animatedObjGenerator != null)
                {
                    // 각 타입 확률을 그대로 보존하면서 겹치지 않게 하려면, 독립적으로 두 번 굴리면 안 된다
                    // (그러면 OtherType의 실제 확률이 (1-물고기밀도)×OtherType밀도로 줄어든다).
                    // 대신 랜덤값 하나를 굴려서 [0, 물고기밀도) / [물고기밀도, 물고기밀도+OtherType밀도) /
                    // 나머지 세 구간으로 나눈다 — 이러면 둘 다 설정값 그대로의 확률을 갖고 서로 배타적이다.
                    // (둘의 합이 1을 넘으면 물리적으로 둘 다 보장할 수 없어 OtherType 쪽이 줄어든다.)
                    float fishDensity = stageTileData.WaterAnimatedObjSeaDensity;
                    float otherDensity = stageTileData.WaterAnimatedOtherTypeObjSeaDensity;
                    float roll = UnityEngine.Random.value;

                    // 장식 모양이 물 타일 바깥(타원 끝 너머 빈 공간)이나 맵 안 물가에 걸치는 자리는 건너뛴다.
                    if (roll < fishDensity)
                    {
                        Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);
                        Vector3 pos = groundTilemap.GetCellCenterWorld(cellPos) + new Vector3(0, halfCellY, 0);
                        DecoSpritePatternAnimator prefab = PickOuterWaterDecoPrefab(pos, outerRadiusSq);
                        if (null != prefab) animatedObjGenerator.SpawnWaterAnimatedObj(pos, prefab);
                    }
                    else if (roll < fishDensity + otherDensity)
                    {
                        Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);
                        Vector3 pos = groundTilemap.GetCellCenterWorld(cellPos) + new Vector3(0, halfCellY, 0);
                        if (AreWaterDecoPixelsOnOpenWater(pos, GetFallbackWaterDecoFootprint(), outerRadiusSq)) animatedObjGenerator.SpawnWaterAnimatedOtherTypeObj(pos);
                    }
                }

                if (stageTileData.BloomWaterDecoTiles != null && stageTileData.BloomWaterDecoTiles.Count > 0)
                {
                    bool shouldPlaceBloom = true;
                    if (stageTileData.UseBloomWaterDecoDensity)
                    {
                        shouldPlaceBloom = UnityEngine.Random.value < stageTileData.BloomWaterDecoDensity;
                    }
                    if (shouldPlaceBloom)
                    {
                        extBloomWaterDecoTiles[extIdx] = stageTileData.BloomWaterDecoTiles[
                            UnityEngine.Random.Range(0, stageTileData.BloomWaterDecoTiles.Count)];
                    }
                }
            }
        }

        BoundsInt extBounds = new BoundsInt(extMinX, extMinY, 0, extW, extH, 1);
        if (waterTilemap != null) waterTilemap.SetTilesBlock(extBounds, tileFrameAnimation.Convert(extWaterTiles, ref extWaterTilesFrameBuffer));
        if (waterCollisionTilemap != null) waterCollisionTilemap.SetTilesBlock(extBounds, extWaterCollisionTiles);
        if (waterStencilTilemap != null) waterStencilTilemap.SetTilesBlock(extBounds, tileFrameAnimation.Convert(extWaterStencilTiles, ref extWaterStencilTilesFrameBuffer));
        if (waterDecoTilemap != null) waterDecoTilemap.SetTilesBlock(extBounds, extWaterDecoTiles);
        if (bloomWaterDecoTilemap != null) bloomWaterDecoTilemap.SetTilesBlock(extBounds, extBloomWaterDecoTiles);

        // 용암 빛 번짐(꺼진 스테이지·로그라이크는 전역값을 비운다). 맵 안팎 물(용암) 칸이 모두 정해진 뒤에 만든다. 렌더링 전용.
        StageLavaGlow.Apply(stageTileData.LavaGlow, extWaterTiles, extBounds, waterTilemap);
        // 용암 바다 지도(바다 여부·해안 거리). 바다 쪽 거품 빈도·바다 온도 무늬가 쓴다. 쓰는 연출이 없거나 로그라이크면 전역값을 비운다. 렌더링 전용.
        StageLavaSeaMap.Apply(stageTileData, extWaterTiles, extBounds, width, height, waterTilemap);
        // 물가 지도(물과 땅의 경계까지 칸 수). 반딧불이 물가를 고를 때 쓴다. 쓰는 연출이 없거나 로그라이크면 비운다. 렌더링 전용.
        StageShoreMap.Apply(stageTileData, extWaterTiles, extBounds, width, height, waterTilemap);
    }
}
