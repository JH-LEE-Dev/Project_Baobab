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

    [Header("중앙 보호 구역 설정")]
    [SerializeField] private float centerSafeZoneRadius = 15f;

    [Header("육지 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float sandDensity = 0.1f;
    [SerializeField, Range(0f, 1f)] private float grassDensity = 0.7f;
    [SerializeField, Range(0f, 1f)] private float rockDensity = 0.2f;

    [Header("타일 에셋")]
    [SerializeField] private TileBase waterTile;
    [SerializeField] private TileBase waterTile_BorderRU;
    [SerializeField] private TileBase waterTile_BorderRD;
    [SerializeField] private TileBase waterTile_BorderLU;
    [SerializeField] private TileBase waterTile_BorderLD;
    [SerializeField] private TileBase waterTile_BorderRU_RD;
    [SerializeField] private TileBase waterTile_BorderRU_LU;
    [SerializeField] private TileBase waterTile_BorderRU_LD;
    [SerializeField] private TileBase waterTile_BorderRD_LU;
    [SerializeField] private TileBase waterTile_BorderRD_LD;
    [SerializeField] private TileBase waterTile_BorderLU_LD;
    [SerializeField] private TileBase waterTile_BorderRU_RD_LU;
    [SerializeField] private TileBase waterTile_BorderRU_RD_LD;
    [SerializeField] private TileBase waterTile_BorderRU_LU_LD;
    [SerializeField] private TileBase waterTile_BorderRD_LU_LD;
    [SerializeField] private TileBase waterTile_BorderAll;

    [SerializeField] private TileBase sandTile;
    [SerializeField] private TileBase grassTile;
    [SerializeField] private TileBase mountainTile;
    [SerializeField] private TileBase treeCollisionTile;
    [SerializeField] private List<TileBase> decoTiles;
    [SerializeField] private TileBase stencilTile;
    [SerializeField] private TileBase groundStencilTile;

    // // 외부 의존성
    private Tilemap groundTilemap;
    private Tilemap collisionTilemap;
    private Tilemap decoTilemap;
    private Tilemap waterStencilTilemap;
    private Tilemap groundStencilTilemap;
    private Tilemap waterTilemap;
    private Tilemap waterCollisionTilemap;

    private Grid grid;

    // // 내부 의존성 및 캐싱 필드
    private float[] noiseValues;
    private TileBase[] groundTiles;
    private TileBase[] collisionTiles;
    private TileBase[] waterTiles;
    private TileBase[] waterCollisionTiles;
    private TileBase[] decoTilesToApply;
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

    public void InitializeMapData()
    {
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
            else if (maps[i].name == "WaterStencilTilemap") waterStencilTilemap = maps[i];
            else if (maps[i].name == "GroundStencilTilemap") groundStencilTilemap = maps[i];
            else if (maps[i].name == "WaterTilemap") waterTilemap = maps[i];
            else if (maps[i].name == "WaterColliderTilemap") waterCollisionTilemap = maps[i];
        }

        int size = width * height;
        noiseValues = new float[size];
        groundTiles = new TileBase[size];
        collisionTiles = new TileBase[size];
        waterTiles = new TileBase[size];
        waterCollisionTiles = new TileBase[size];
        decoTilesToApply = new TileBase[size];
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

        if (seed == 0) seed = UnityEngine.Random.Range(1, 100000);
    }

    public void GenerateMap()
    {
        if (groundTilemap == null || collisionTilemap == null || decoTilemap == null) return;

        groundTilemap.ClearAllTiles();
        collisionTilemap.ClearAllTiles();
        decoTilemap.ClearAllTiles();
        if (waterTilemap != null) waterTilemap.ClearAllTiles();
        if (waterCollisionTilemap != null) waterCollisionTilemap.ClearAllTiles();
        if (waterStencilTilemap != null) waterStencilTilemap.ClearAllTiles();
        if (groundStencilTilemap != null) groundStencilTilemap.ClearAllTiles();

        GenerateNoiseMap();
        DetermineSpawns();
        ApplyTiles();

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

    public bool IsWalkable(Vector3Int _cellPos)
    {
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return false;
        return cellToIndex[_cellPos.x + _cellPos.y * width] != -1;
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
        if (collisionTilemap == null || treeCollisionTile == null) return;

        Vector3 adjustedPos = _worldPos;
        adjustedPos.y -= halfCellY;

        Vector3Int cellPos = collisionTilemap.WorldToCell(adjustedPos);
        collisionTilemap.SetTile(cellPos, treeCollisionTile);

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
        float radiusSq = centerSafeZoneRadius * centerSafeZoneRadius;

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

                // 중앙 보호 구역 내에는 물이 생기지 않도록 보정
                float dx = x - centerX;
                if (dx * dx + dy * dy < radiusSq)
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

        // 1. 플레이어 스폰 위치를 맵의 중앙(안전 구역의 정중앙)으로 설정
        int centerX = width / 2;
        int centerY = height / 2;
        playerIdx = centerX + centerY * width;

        if (playerIdx < 0 || playerIdx >= size) playerIdx = 0;

        // 2. 포탈 스폰 위치 결정: 캐릭터 스폰 위치에서 오른쪽으로 2칸 떨어진 위치
        int portalX = centerX + 2;
        int portalY = centerY;

        if (portalX >= width) portalX = width - 1;

        portalIdx = portalX + portalY * width;
    }

    private void ApplyTiles()
    {
        int size = width * height;
        Array.Clear(groundTiles, 0, size);
        Array.Clear(collisionTiles, 0, size);
        Array.Clear(waterTiles, 0, size);
        Array.Clear(waterCollisionTiles, 0, size);
        Array.Clear(decoTilesToApply, 0, size);
        Array.Clear(waterStencilTiles, 0, size);
        Array.Clear(groundStencilTiles, 0, size);

        grassPositions.Clear();
        delayedGrassPositions.Clear();
        walkablePositions.Clear();
        for (int i = 0; i < size; i++) cellToIndex[i] = -1;

        float totalDensity = sandDensity + grassDensity + rockDensity;
        float invTotal = totalDensity > 0 ? 1f / totalDensity : 0;
        float landRange = 1f - waterThreshold;

        float sandThreshold = waterThreshold + (landRange * (sandDensity * invTotal));
        float grassThreshold = sandThreshold + (landRange * (grassDensity * invTotal));

        Vector3 portalPos = GetPortalSpawnPosition();
        Vector3 playerPos = GetPlayerSpawnPosition();

        float centerX = width * 0.5f;
        float centerY = height * 0.5f;
        float radiusSq = centerSafeZoneRadius * centerSafeZoneRadius;

        for (int i = 0; i < size; i++)
        {
            float v = noiseValues[i];
            int x = i % width;
            int y = i / width;

            float dx = x - centerX;
            float dy = y - centerY;
            bool inSafeZone = (dx * dx + dy * dy < radiusSq);

            if (v < waterThreshold)
            {
                waterTiles[i] = GetWaterTile(x, y);
                waterCollisionTiles[i] = treeCollisionTile;
                waterStencilTiles[i] = stencilTile;
            }
            else
            {
                groundStencilTiles[i] = groundStencilTile;
                Vector3 pos = GetWorldPos(i);

                cellToIndex[i] = walkablePositions.Count;
                walkablePositions.Add(pos);

                if (isShoreline[i] || v < sandThreshold)
                {
                    groundTiles[i] = sandTile;
                }
                else if (v < grassThreshold)
                {
                    groundTiles[i] = grassTile;

                    if (decoTiles != null && decoTiles.Count > 0 && UnityEngine.Random.value < 0.3f)
                    {
                        decoTilesToApply[i] = decoTiles[UnityEngine.Random.Range(0, decoTiles.Count)];
                    }

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
                else
                {
                    groundTiles[i] = mountainTile;
                }
            }
        }

        BoundsInt b = new BoundsInt(0, 0, 0, width, height, 1);
        groundTilemap.SetTilesBlock(b, groundTiles);
        collisionTilemap.SetTilesBlock(b, collisionTiles);
        if (waterTilemap != null) waterTilemap.SetTilesBlock(b, waterTiles);
        if (waterCollisionTilemap != null) waterCollisionTilemap.SetTilesBlock(b, waterCollisionTiles);
        decoTilemap.SetTilesBlock(b, decoTilesToApply);
        if (waterStencilTilemap != null) waterStencilTilemap.SetTilesBlock(b, waterStencilTiles);
        if (groundStencilTilemap != null) groundStencilTilemap.SetTilesBlock(b, groundStencilTiles);
    }

    private TileBase GetWaterTile(int _x, int _y)
    {
        int mask = 0;
        if (IsLand(_x + 1, _y)) mask |= 1;  // RU
        if (IsLand(_x, _y - 1)) mask |= 2;  // RD
        if (IsLand(_x, _y + 1)) mask |= 4;  // LU
        if (IsLand(_x - 1, _y)) mask |= 8;  // LD

        switch (mask)
        {
            case 1: return waterTile_BorderRU;
            case 2: return waterTile_BorderRD;
            case 3: return waterTile_BorderRU_RD;
            case 4: return waterTile_BorderLU;
            case 5: return waterTile_BorderRU_LU;
            case 6: return waterTile_BorderRD_LU;
            case 7: return waterTile_BorderRU_RD_LU;
            case 8: return waterTile_BorderLD;
            case 9: return waterTile_BorderRU_LD;
            case 10: return waterTile_BorderRD_LD;
            case 11: return waterTile_BorderRU_RD_LD;
            case 12: return waterTile_BorderLU_LD;
            case 13: return waterTile_BorderRU_LU_LD;
            case 14: return waterTile_BorderRD_LU_LD;
            case 15: return waterTile_BorderAll;
            default: return waterTile;
        }
    }

    private bool IsLand(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return false;
        return noiseValues[_x + _y * width] >= waterThreshold;
    }

    private bool IsWater(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return false;
        return noiseValues[_x + _y * width] < waterThreshold;
    }

    private Vector3 GetWorldPos(int _idx)
    {
        if (_idx < 0 || _idx >= worldPosMap.Length) return Vector3.zero;
        return worldPosMap[_idx];
    }
}
