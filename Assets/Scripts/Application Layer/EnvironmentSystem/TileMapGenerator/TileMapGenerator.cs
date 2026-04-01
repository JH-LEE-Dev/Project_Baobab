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

    [Header("섬 방지 및 Falloff 설정")]
    [SerializeField] private bool useIslandPrevention = true;
    [SerializeField] private float falloffA = 3f;
    [SerializeField] private float falloffB = 6.5f;
    [SerializeField] private float waterThreshold = 0.38f;

    [Header("타일 에셋")]
    [SerializeField] private TileBase waterTile;
    [SerializeField] private TileBase sandTile;
    [SerializeField] private TileBase grassTile;
    [SerializeField] private TileBase mountainTile;
    [SerializeField] private TileBase treeCollisionTile;
    [SerializeField] private List<TileBase> decoTiles;

    // // 외부 의존성
    private Tilemap groundTilemap;
    private Tilemap collisionTilemap;
    private Tilemap decoTilemap;
    private Grid grid;

    // // 내부 의존성 및 캐싱 필드
    private float[] noiseValues;
    private TileBase[] groundTiles;
    private TileBase[] collisionTiles;
    private TileBase[] decoTilesToApply;
    private int[] cellToIndex; // Dictionary<Vector3Int, int> 대신 사용
    private bool[] visited;
    private bool[] isShoreline;
    private float halfCellY;

    // // 재사용 컬렉션 (GC 최소화)
    private List<int> largestBlob = new List<int>(22500);
    private List<int> currentBlob = new List<int>(22500);
    private List<int> shorelineList = new List<int>(5000);
    private List<int> innerEdgesList = new List<int>(5000);
    private List<Vector3> grassPositions = new List<Vector3>(5000);
    private List<Vector3> walkablePositions = new List<Vector3>(22500);
    private Queue<int> bfsQueue = new Queue<int>(22500);

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
            else if(maps[i].name == "DecoTilemap") decoTilemap = maps[i];
        }

        int size = width * height;
        noiseValues = new float[size];
        groundTiles = new TileBase[size];
        collisionTiles = new TileBase[size];
        decoTilesToApply = new TileBase[size];
        cellToIndex = new int[size];
        for (int i = 0; i < size; i++) cellToIndex[i] = -1;
        visited = new bool[size];
        isShoreline = new bool[size];

        if (seed == 0) seed = UnityEngine.Random.Range(1, 100000);
    }

    public void GenerateMap()
    {
        if (groundTilemap == null || collisionTilemap == null || decoTilemap == null) return;

        groundTilemap.ClearAllTiles();
        collisionTilemap.ClearAllTiles();
        decoTilemap.ClearAllTiles();

        GenerateNoiseMap();
        RemoveIslands();
        DetermineSpawns();
        ApplyTiles();

        DeclareActiveTilesCntEvent?.Invoke(walkablePositions.Count, grassPositions.Count);
        TilemapGeneratedEvent?.Invoke(grassPositions);
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

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            float yCoord = (y + 0.5f) * invHeight * scale + seed;
            float ny = y * (y - 1f != 0 ? 1f / (height - 1f) : 1f) * 2f - 1f;

            for (int x = 0; x < width; x++)
            {
                float xCoord = (x + 0.5f) * invWidth * scale + seed;
                float val = Mathf.PerlinNoise(xCoord, yCoord);

                if (useIslandPrevention)
                {
                    float nx = x * (x - 1f != 0 ? 1f / (width - 1f) : 1f) * 2f - 1f;
                    float dist = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                    val -= EvaluateFalloff(dist);
                }

                noiseValues[x + rowOffset] = val;
            }
        }
    }

    private void RemoveIslands()
    {
        int size = width * height;
        Array.Clear(visited, 0, size);
        largestBlob.Clear();

        ReadOnlySpan<int> dx = stackalloc int[] { 1, -1, 0, 0 };
        ReadOnlySpan<int> dy = stackalloc int[] { 0, 0, 1, -1 };

        for (int i = 0; i < size; i++)
        {
            if (noiseValues[i] < waterThreshold || visited[i]) continue;

            currentBlob.Clear();
            bfsQueue.Clear();

            bfsQueue.Enqueue(i);
            visited[i] = true;

            while (bfsQueue.Count > 0)
            {
                int c = bfsQueue.Dequeue();
                currentBlob.Add(c);

                int cx = c % width;
                int cy = c / width;

                for (int j = 0; j < 4; j++)
                {
                    int nx = cx + dx[j];
                    int ny = cy + dy[j];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        int ni = nx + ny * width;
                        if (!visited[ni] && noiseValues[ni] >= waterThreshold)
                        {
                            visited[ni] = true;
                            bfsQueue.Enqueue(ni);
                        }
                    }
                }
            }

            if (currentBlob.Count > largestBlob.Count)
            {
                largestBlob.Clear();
                largestBlob.AddRange(currentBlob);
            }
        }

        Array.Clear(visited, 0, size);
        for (int i = 0; i < largestBlob.Count; i++)
        {
            visited[largestBlob[i]] = true;
        }

        for (int i = 0; i < size; i++)
        {
            if (noiseValues[i] >= waterThreshold && !visited[i])
            {
                noiseValues[i] = waterThreshold - 0.05f;
            }
        }
    }

    private void DetermineSpawns()
    {
        int size = width * height;
        Array.Clear(isShoreline, 0, size);
        shorelineList.Clear();
        innerEdgesList.Clear();

        for (int i = 0; i < largestBlob.Count; i++)
        {
            int idx = largestBlob[i];
            int x = idx % width;
            int y = idx / width;

            if (IsWater(x + 1, y) || IsWater(x - 1, y) || IsWater(x, y + 1) || IsWater(x, y - 1))
            {
                isShoreline[idx] = true;
                shorelineList.Add(idx);
            }
        }

        ReadOnlySpan<int> dx = stackalloc int[] { 1, -1, 0, 0 };
        ReadOnlySpan<int> dy = stackalloc int[] { 0, 0, 1, -1 };

        for (int i = 0; i < largestBlob.Count; i++)
        {
            int idx = largestBlob[i];
            if (isShoreline[idx]) continue;

            int x = idx % width;
            int y = idx / width;

            for (int j = 0; j < 4; j++)
            {
                int nx = x + dx[j];
                int ny = y + dy[j];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (isShoreline[nx + ny * width])
                    {
                        innerEdgesList.Add(idx);
                        break;
                    }
                }
            }
        }

        if (innerEdgesList.Count > 0)
        {
            portalIdx = innerEdgesList[UnityEngine.Random.Range(0, innerEdgesList.Count)];
        }
        else if (shorelineList.Count > 0)
        {
            portalIdx = shorelineList[UnityEngine.Random.Range(0, shorelineList.Count)];
        }
        else
        {
            portalIdx = largestBlob.Count > 0 ? largestBlob[0] : -1;
        }

        playerIdx = portalIdx;
    }

    private void ApplyTiles()
    {
        int size = width * height;
        Array.Clear(groundTiles, 0, size);
        Array.Clear(collisionTiles, 0, size);
        Array.Clear(decoTilesToApply, 0, size);

        grassPositions.Clear();
        walkablePositions.Clear();
        for (int i = 0; i < size; i++) cellToIndex[i] = -1;

        float sandT = waterThreshold + 0.1f;
        float mountT = 0.7f;
        Vector3 portalPos = GetPortalSpawnPosition();

        for (int i = 0; i < size; i++)
        {
            float v = noiseValues[i];

            if (v < waterThreshold)
            {
                collisionTiles[i] = waterTile;
            }
            else
            {
                Vector3 pos = GetWorldPos(i);

                cellToIndex[i] = walkablePositions.Count;
                walkablePositions.Add(pos);

                if (v < sandT)
                {
                    groundTiles[i] = sandTile;
                }
                else if (v < mountT)
                {
                    groundTiles[i] = grassTile;
                    if ((pos - portalPos).sqrMagnitude > 2.25f)
                    {
                        grassPositions.Add(pos);

                        // 30% 확률로 Deco 타일 배치
                        if (decoTiles != null && decoTiles.Count > 0 && UnityEngine.Random.value < 0.3f)
                        {
                            decoTilesToApply[i] = decoTiles[UnityEngine.Random.Range(0, decoTiles.Count)];
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
        decoTilemap.SetTilesBlock(b, decoTilesToApply);
    }

    private bool IsWater(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return true;
        return noiseValues[_x + _y * width] < waterThreshold;
    }

    private Vector3 GetWorldPos(int _idx)
    {
        if (_idx < 0) return Vector3.zero;

        Vector3Int cellPos = new Vector3Int(_idx % width, _idx / width, 0);
        return groundTilemap.GetCellCenterWorld(cellPos) + new Vector3(0, halfCellY, 0);
    }

    private float EvaluateFalloff(float _v)
    {
        float p = Mathf.Pow(_v, falloffA);
        float q = Mathf.Pow(falloffB - falloffB * _v, falloffA);
        return p / (p + q);
    }
}
