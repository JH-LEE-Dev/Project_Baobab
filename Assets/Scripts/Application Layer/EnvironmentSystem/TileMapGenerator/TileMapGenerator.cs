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
    [Range(0f, 1f)]
    [SerializeField] private float outerWaterObjectDensity = 0.01f;
    private bool[] isMainland;

    [Header("중앙 보호 구역 설정")]
    [SerializeField] private float centerSafeZoneRadius = 15f;

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

    // ── 물 웅덩이별 균일 애니메이션 오브젝트 배치용 ──
    // 물 애니메이션 오브젝트(SpawnWaterAnimatedObj)를 타일 단위 독립 확률로 뽑으면
    // 작은 웅덩이는 운이 나빠 하나도 안 뽑히는 경우가 생긴다.
    // 그래서 연결된 물 덩어리(웅덩이) 단위로 목표 개수를 계산해 그 안에서만 랜덤 분배한다.
    private bool[] deepWaterTileFlags;
    private bool[] waterCompVisited;
    private Queue<int> waterCompQueue = new Queue<int>(2000);
    private List<int> pondInnerDeepBuffer = new List<int>(500);
    private List<int> pondOuterDeepBuffer = new List<int>(200);

    [Header("육지 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float sandDensity = 0.1f;
    [SerializeField, Range(0f, 1f)] private float grassDensity = 0.7f;

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
    private Tilemap collisionTilemap;
    private Tilemap decoTilemap;
    private Tilemap waterDecoTilemap;
    private Tilemap bloomWaterDecoTilemap;
    private Tilemap bloomDecoTilemap;
    private Tilemap waterStencilTilemap;
    private Tilemap groundStencilTilemap;
    private Tilemap waterTilemap;
    private Tilemap waterCornerTilemap;
    private Tilemap waterCollisionTilemap;
    private Tilemap rockCollisionTilemap;

    private Grid grid;

    // // 내부 의존성 및 캐싱 필드
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private float[] noiseValues;
    private TileBase[] groundTiles;
    private TileBase[] collisionTiles;
    private TileBase[] waterTiles;
    private TileBase[] waterCornerTiles;
    private TileBase[] waterCollisionTiles;
    private TileBase[] rockCollisionTiles;
    private TileBase[] decoTilesToApply;
    private TileBase[] bloomDecoTilesToApply;
    private TileBase[] waterDecoTilesToApply;
    private TileBase[] bloomWaterDecoTilesToApply;
    private TileBase[] waterStencilTiles;
    private TileBase[] groundStencilTiles;
    private int[] cellToIndex;
    private bool[] isShoreline;
    private float halfCellY;

    // // 최적화 캐싱 배열
    private Vector3[] worldPosMap;
    private WaitForSeconds delayYield;

    // // 재사용 컬렉션 (GC 최소화)
    private List<int> shorelineList = new List<int>(5000);
    private List<int> innerEdgesList = new List<int>(5000);
    private List<Vector3> grassPositions = new List<Vector3>(5000);
    private List<Vector3> delayedGrassPositions = new List<Vector3>(100);
    private List<Vector3> walkablePositions = new List<Vector3>(22500);

    private int playerIdx = -1;
    private int portalIdx = -1;

    // // 퍼블릭 초기화 및 제어 메서드

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

        worldPosMap = new Vector3[size];

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

        if (delayYield == null) delayYield = new WaitForSeconds(5f);

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
            animatedObjGenerator.SetPrefabs(stageTileData.AnimatedObjPrefabs, stageTileData.WaterAnimatedObjPrefabs, stageTileData.GrassStaticObjPrefabs, stageTileData.SandStaticObjPrefabs);
        }

        if (bloomDecoTilemap != null && stageTileData != null)
        {
            TilemapRenderer tr = bloomDecoTilemap.GetComponent<TilemapRenderer>();
            if (tr != null)
            {
                var mpb = new MaterialPropertyBlock();
                tr.GetPropertyBlock(mpb);
                mpb.SetFloat(HDRIntensityID, stageTileData.BloomDecoHDRIntensity);
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
        ApplyTiles();
        ApplyOuterWaterTiles();

        DeclareActiveTilesCntEvent?.Invoke(walkablePositions.Count, grassPositions.Count);
        TilemapGeneratedEvent?.Invoke(grassPositions);

        StopCoroutine(nameof(AddDelayedGrassPositions));
        StartCoroutine(nameof(AddDelayedGrassPositions));
    }

    private System.Collections.IEnumerator AddDelayedGrassPositions()
    {
        yield return delayYield;

        if (delayedGrassPositions.Count > 0)
        {
            grassPositions.AddRange(delayedGrassPositions);
            delayedGrassPositions.Clear();
        }
    }

    public Vector3 GetPlayerSpawnPosition() => GetWorldPos(playerIdx);

    public Vector3 GetPortalSpawnPosition() => GetWorldPos(portalIdx);

    public List<Vector3> GetGrassTileWorldPositions() => grassPositions;

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

        TileBase tile = groundTiles[_cellPos.x + _cellPos.y * width];
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
        collisionTilemap.SetTile(cellPos, stageTileData.TreeCollisionTile);

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

    public void ClearTreeCollisionTile(Vector3 _worldPos)
    {
        if (collisionTilemap == null) return;

        Vector3 adjustedPos = _worldPos;
        adjustedPos.y -= halfCellY;

        Vector3Int cellPos = collisionTilemap.WorldToCell(adjustedPos);
        collisionTilemap.SetTile(cellPos, null);

        if (cellPos.x < 0 || cellPos.x >= width || cellPos.y < 0 || cellPos.y >= height) return;

        int flatIdx = cellPos.x + cellPos.y * width;
        if (cellToIndex[flatIdx] == -1)
        {
            cellToIndex[flatIdx] = walkablePositions.Count;
            walkablePositions.Add(_worldPos);
        }
    }

    // // 프라이빗 로직 메서드

    private void GenerateNoiseMap()
    {
        float invWidth = 1f / width;
        float invHeight = 1f / height;
        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float safeRadiusSq = centerSafeZoneRadius * centerSafeZoneRadius;
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
                else if (distSq < safeRadiusSq)
                {
                    val = Mathf.Max(val, waterThreshold + 0.05f);
                }

                noiseValues[i] = val;
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

            if (IsWater(x + 1, y) || IsWater(x - 1, y) || IsWater(x, y + 1) || IsWater(x, y - 1))
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
        playerIdx = centerX + centerY * width;

        if (playerIdx < 0 || playerIdx >= size) playerIdx = 0;

        // 2. 포탈 스폰 위치 결정: 캐릭터 스폰 위치에서 오른쪽으로 2칸 떨어진 위치
        int portalX = centerX + 2;
        int portalY = centerY;

        if (portalX >= width) portalX = width - 1;

        portalIdx = portalX + portalY * width;

        //캐릭터 위치 좀 더 아래로 조정.
        centerX = width / 2;
        centerY = (height / 2) - 1;
        playerIdx = centerX + centerY * width;
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
        Array.Clear(waterDecoTilesToApply, 0, size);
        Array.Clear(bloomWaterDecoTilesToApply, 0, size);
        Array.Clear(waterStencilTiles, 0, size);
        Array.Clear(groundStencilTiles, 0, size);
        Array.Clear(deepWaterTileFlags, 0, size);

        grassPositions.Clear();
        delayedGrassPositions.Clear();
        walkablePositions.Clear();
        for (int i = 0; i < size; i++) cellToIndex[i] = -1;

        float totalDensity = sandDensity + grassDensity;
        float invTotal = totalDensity > 0 ? 1f / totalDensity : 0;
        float landRange = 1f - waterThreshold;

        float sandThreshold = waterThreshold + (landRange * (sandDensity * invTotal));

        Vector3 portalPos = GetPortalSpawnPosition();
        Vector3 playerPos = GetPlayerSpawnPosition();

        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float safeRadiusSq = centerSafeZoneRadius * centerSafeZoneRadius;
        
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

            bool inSafeZone = (distSq < safeRadiusSq);

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
                        if (stageTileData.WaterDecoTiles != null && stageTileData.WaterDecoTiles.Count > 0 && UnityEngine.Random.value < stageTileData.WaterDecoDensity)
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

                bool _isSand = isShoreline[i] || v < sandThreshold;
                if (_isSand)
                {
                    groundTiles[i] = (stageTileData != null && stageTileData.SandTiles != null && stageTileData.SandTiles.Count > 0)
                        ? stageTileData.SandTiles[UnityEngine.Random.Range(0, stageTileData.SandTiles.Count)]
                        : null;
                }
                else
                {
                    groundTiles[i] = (stageTileData != null && stageTileData.GrassTiles != null && stageTileData.GrassTiles.Count > 0)
                        ? stageTileData.GrassTiles[UnityEngine.Random.Range(0, stageTileData.GrassTiles.Count)]
                        : null;
                }

                bool _hasRockDeco = false;
                if (stageTileData != null)
                {
                    var staticPrefabs = _isSand ? stageTileData.SandStaticObjPrefabs : stageTileData.GrassStaticObjPrefabs;
                    float density = _isSand ? stageTileData.SandStaticObjDensity : stageTileData.GrassStaticObjDensity;

                    if (staticPrefabs != null && staticPrefabs.Count > 0 && UnityEngine.Random.value < density)
                    {
                        if (!inSafeZone && isMainland[i])
                        {
                            if (animatedObjGenerator != null)
                            {
                                if (_isSand)
                                    animatedObjGenerator.SpawnSandStaticObj(pos);
                                else
                                    animatedObjGenerator.SpawnGrassStaticObj(pos);
                            }
                            _hasRockDeco = true;
                            rockCollisionTiles[i] = stageTileData.TreeCollisionTile;
                        }
                    }
                }

                bool _hasAnimatedObj = false;
                if (false == _hasRockDeco && false == _isSand)
                {
                    if (animatedObjGenerator != null && stageTileData != null && UnityEngine.Random.value < stageTileData.AnimatedObjDensity)
                    {
                        animatedObjGenerator.SpawnAnimatedObj(pos);
                        _hasAnimatedObj = true;
                    }
                }

                bool _hasGroundDeco = false;
                if (false == _hasRockDeco && false == _hasAnimatedObj && stageTileData != null)
                {
                    float _groundDecoProb = _isSand ? stageTileData.SandDecoDensity : stageTileData.GroundDecoDensity;
                    float _bloomDecoProb = _isSand ? stageTileData.BloomSandDecoDensity : stageTileData.BloomGroundDecoDensity;

                    if (stageTileData.GroundDecoTiles != null && stageTileData.GroundDecoTiles.Count > 0 && UnityEngine.Random.value < _groundDecoProb)
                    {
                        decoTilesToApply[i] = stageTileData.GroundDecoTiles[UnityEngine.Random.Range(0, stageTileData.GroundDecoTiles.Count)];
                        _hasGroundDeco = true;
                    }
                    else if (stageTileData.BloomGroundDecoTiles != null && stageTileData.BloomGroundDecoTiles.Count > 0 && UnityEngine.Random.value < _bloomDecoProb)
                    {
                        bloomDecoTilesToApply[i] = stageTileData.BloomGroundDecoTiles[UnityEngine.Random.Range(0, stageTileData.BloomGroundDecoTiles.Count)];
                        _hasGroundDeco = true;
                    }
                }

                if (false == _isSand && false == _hasRockDeco && false == _hasAnimatedObj && false == _hasGroundDeco && stageTileData != null)
                {
                    if (stageTileData.GrassDecoTiles != null && stageTileData.GrassDecoTiles.Count > 0 && UnityEngine.Random.value < stageTileData.GrassDecoDensity)
                    {
                        decoTilesToApply[i] = stageTileData.GrassDecoTiles[UnityEngine.Random.Range(0, stageTileData.GrassDecoTiles.Count)];
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

        PlaceWaterAnimatedObjects(size, centerX, centerY, mapRadius);

        BoundsInt b = new BoundsInt(0, 0, 0, width, height, 1);
        groundTilemap.SetTilesBlock(b, groundTiles);
        collisionTilemap.SetTilesBlock(b, collisionTiles);
        if (waterTilemap != null) waterTilemap.SetTilesBlock(b, waterTiles);
        if (waterCornerTilemap != null) waterCornerTilemap.ClearAllTiles();
        if (waterCornerTilemap != null) waterCornerTilemap.SetTilesBlock(b, waterCornerTiles);
        if (waterCollisionTilemap != null) waterCollisionTilemap.SetTilesBlock(b, waterCollisionTiles);
        if (rockCollisionTilemap != null) rockCollisionTilemap.SetTilesBlock(b, rockCollisionTiles);
        decoTilemap.SetTilesBlock(b, decoTilesToApply);
        if (bloomDecoTilemap != null) bloomDecoTilemap.SetTilesBlock(b, bloomDecoTilesToApply);
        if (waterDecoTilemap != null) waterDecoTilemap.SetTilesBlock(b, waterDecoTilesToApply);
        if (bloomWaterDecoTilemap != null) bloomWaterDecoTilemap.SetTilesBlock(b, bloomWaterDecoTilesToApply);
        if (waterStencilTilemap != null) waterStencilTilemap.SetTilesBlock(b, waterStencilTiles);
        if (groundStencilTilemap != null) groundStencilTilemap.SetTilesBlock(b, groundStencilTiles);
    }

    // ── 웅덩이(연결된 물 덩어리) 단위 애니메이션 오브젝트 균일 분배 ──
    // 기존에는 깊은 물 타일마다 독립적으로 확률을 굴려 SpawnWaterAnimatedObj를 호출했기 때문에
    // 작은 웅덩이는 확률이 전부 빗나가면 애니메이션 오브젝트가 하나도 안 생길 수 있었다.
    // 물 타일을 4방향 연결 컴포넌트(웅덩이)로 묶은 뒤, 그 웅덩이에 속한 깊은 물 타일 수에 비례한
    // 목표 개수를 계산해서 웅덩이 안에서만 랜덤하게 뽑아 스폰한다.
    private void PlaceWaterAnimatedObjects(int _size, float _centerX, float _centerY, float _mapRadius)
    {
        if (animatedObjGenerator == null) return;

        float mapRadiusSq = _mapRadius * _mapRadius;
        float waterAnimatedObjDensity = stageTileData != null ? stageTileData.WaterAnimatedObjDensity : 0f;

        Array.Clear(waterCompVisited, 0, _size);

        for (int i = 0; i < _size; i++)
        {
            if (waterCompVisited[i] || waterTiles[i] == null) continue;

            pondInnerDeepBuffer.Clear();
            pondOuterDeepBuffer.Clear();
            waterCompQueue.Clear();
            waterCompQueue.Enqueue(i);
            waterCompVisited[i] = true;

            while (waterCompQueue.Count > 0)
            {
                int curr = waterCompQueue.Dequeue();
                int cx = curr % width;
                int cy = curr / width;

                if (deepWaterTileFlags[curr])
                {
                    float ddx = cx - _centerX;
                    float ddy = cy - _centerY;
                    bool inMainMap = (ddx * ddx + ddy * ddy) < mapRadiusSq;
                    (inMainMap ? pondInnerDeepBuffer : pondOuterDeepBuffer).Add(curr);
                }

                TryEnqueueWaterCompCell(cx + 1, cy);
                TryEnqueueWaterCompCell(cx - 1, cy);
                TryEnqueueWaterCompCell(cx, cy + 1);
                TryEnqueueWaterCompCell(cx, cy - 1);
            }

            SpawnDistributedWaterAnimatedObj(pondInnerDeepBuffer, waterAnimatedObjDensity);
            SpawnDistributedWaterAnimatedObj(pondOuterDeepBuffer, outerWaterObjectDensity);
        }
    }

    private void TryEnqueueWaterCompCell(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (waterCompVisited[idx] || waterTiles[idx] == null) return;

        waterCompVisited[idx] = true;
        waterCompQueue.Enqueue(idx);
    }

    // 웅덩이 크기 * 밀도의 기댓값을 정수부 + 소수부 확률적 반올림으로 변환하되,
    // 웅덩이가 존재하고(깊은 물 타일 1칸 이상) 밀도가 0보다 크면 최소 1개는 항상 보장한다.
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

    // _candidates(웅덩이에 속한 깊은 물 타일 목록)에서 부분 Fisher-Yates로 목표 개수만큼 뽑아
    // 해당 위치에 물 애니메이션 오브젝트를 스폰한다. _candidates는 호출마다 재사용되는 버퍼이므로
    // 여기서 순서를 바꿔도 안전하다.
    private void SpawnDistributedWaterAnimatedObj(List<int> _candidates, float _density)
    {
        int count = ComputeDistributedCount(_candidates.Count, _density);
        if (count <= 0) return;

        int n = _candidates.Count;
        for (int k = 0; k < count; k++)
        {
            int r = k + UnityEngine.Random.Range(0, n - k);
            int tmp = _candidates[k];
            _candidates[k] = _candidates[r];
            _candidates[r] = tmp;

            animatedObjGenerator.SpawnWaterAnimatedObj(GetWorldPos(_candidates[k]));
        }
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
        TileBase[] extWaterTiles = new TileBase[extSize];
        TileBase[] extWaterCollisionTiles = new TileBase[extSize];
        TileBase[] extWaterStencilTiles = new TileBase[extSize];
        TileBase[] extBloomWaterDecoTiles = new TileBase[extSize];
        TileBase[] extWaterDecoTiles = new TileBase[extSize];

        TileBase collisionTile = stageTileData.TreeCollisionTile;
        TileBase stencilTile = stageTileData.StencilTile;
        TileBase defaultWaterTile = stageTileData.WaterTile;

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

                if (animatedObjGenerator != null && UnityEngine.Random.value < outerWaterObjectDensity)
                {
                    Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);
                    Vector3 pos = groundTilemap.GetCellCenterWorld(cellPos) + new Vector3(0, halfCellY, 0);
                    animatedObjGenerator.SpawnWaterAnimatedObj(pos);
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
        if (waterTilemap != null) waterTilemap.SetTilesBlock(extBounds, extWaterTiles);
        if (waterCollisionTilemap != null) waterCollisionTilemap.SetTilesBlock(extBounds, extWaterCollisionTiles);
        if (waterStencilTilemap != null) waterStencilTilemap.SetTilesBlock(extBounds, extWaterStencilTiles);
        if (waterDecoTilemap != null) waterDecoTilemap.SetTilesBlock(extBounds, extWaterDecoTiles);
        if (bloomWaterDecoTilemap != null) bloomWaterDecoTilemap.SetTilesBlock(extBounds, extBloomWaterDecoTiles);
    }
}
