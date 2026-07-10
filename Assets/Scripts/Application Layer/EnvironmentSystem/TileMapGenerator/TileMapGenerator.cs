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
    [SerializeField] private float scale = 25f;
    [SerializeField] private int seed;
    [SerializeField] private float waterThreshold = 0.38f;

    [Header("외부 물 타일 설정")]
    [SerializeField] private int outerWaterDepth = 25;

    [Header("중앙 보호 구역 설정")]
    [SerializeField] private float centerSafeZoneRadius = 15f;

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
        float mapRadius = Mathf.Min(width, height) * 0.5f;
        float mapRadiusSq = mapRadius * mapRadius;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            float yCoord = (y + 0.5f) * invHeight * scale + seed;
            float dy = y - centerY;

            for (int x = 0; x < width; x++)
            {
                int i = x + rowOffset;
                float xCoord = (x + 0.5f) * invWidth * scale + seed;
                float val = Mathf.PerlinNoise(xCoord, yCoord);

                float dx = x - centerX;
                
                // 타원 밖은 완전한 물로 설정하여, 기존 ApplyTiles에서 자연스럽게 shoreline이 형성되도록 함
                if (dx * dx + dy * dy > mapRadiusSq)
                {
                    val = 0f;
                }
                // 중앙 보호 구역 내에는 물이 생기지 않도록 보정
                else if (dx * dx + dy * dy < safeRadiusSq)
                {
                    val = Mathf.Max(val, waterThreshold + 0.05f);
                }

                noiseValues[i] = val;
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
        
        float mapRadius = Mathf.Min(width, height) * 0.5f;
        float mapRadiusSq = mapRadius * mapRadius;

        for (int i = 0; i < size; i++)
        {
            int x = i % width;
            int y = i / width;
            float dx = x - centerX;
            float dy = y - centerY;
            bool inSafeZone = (dx * dx + dy * dy < safeRadiusSq);

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

                if (_isDeepWater)
                {
                    if (stageTileData != null)
                    {
                        if (stageTileData.WaterDecoTiles != null && stageTileData.WaterDecoTiles.Count > 0 && UnityEngine.Random.value < stageTileData.WaterDecoDensity)
                        {
                            waterDecoTilesToApply[i] = stageTileData.WaterDecoTiles[UnityEngine.Random.Range(0, stageTileData.WaterDecoTiles.Count)];
                        }
                    }

                    if (animatedObjGenerator != null && stageTileData != null && UnityEngine.Random.value < stageTileData.WaterAnimatedObjDensity)
                    {
                        Vector3 pos = GetWorldPos(i);
                        animatedObjGenerator.SpawnWaterAnimatedObj(pos);
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
                        if (!inSafeZone)
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
                    if (!inSafeZone)
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
        float mapRadius = Mathf.Min(width, height) * 0.5f;
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

                if (animatedObjGenerator != null && UnityEngine.Random.value < stageTileData.WaterAnimatedObjDensity)
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
