using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;

public class TileMapGenerator : MonoBehaviour, ITilemapDataProvider
{
    public event Action<List<Vector3>> TilemapGeneratedEvent;

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

    private Tilemap groundTilemap;
    private Tilemap collisionTilemap;
    private Grid grid;

    private List<int> largestBlob = new List<int>();
    private List<Vector3> grassPositions = new List<Vector3>();
    private float[] noiseValues;
    private int playerIdx = -1, portalIdx = -1;

    public void InitializeMapData()
    {
        if (grid == null) grid = Instantiate(gridPrefab, transform.position, Quaternion.identity).GetComponent<Grid>();
        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>();
        foreach (var m in maps)
        {
            if (m.name == "GroundTilemap") groundTilemap = m;
            else if (m.name == "ColliderTilemap") collisionTilemap = m;
        }
        noiseValues = new float[width * height];
        if (seed == 0) seed = UnityEngine.Random.Range(1, 100000);
    }

    public Vector3 GetPlayerSpawnPosition() => GetWorldPos(playerIdx);
    public Vector3 GetPortalSpawnPosition() => GetWorldPos(portalIdx);
    public List<Vector3> GetGrassTileWorldPositions() => grassPositions;

    public void GenerateMap()
    {
        if (!groundTilemap || !collisionTilemap) return;
        groundTilemap.ClearAllTiles();
        collisionTilemap.ClearAllTiles();

        // 1. 노이즈 생성
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float val = Mathf.PerlinNoise((x + 0.5f) / width * scale + seed, (y + 0.5f) / height * scale + seed);
                if (useIslandPrevention)
                {
                    float nx = x / (width - 1f) * 2f - 1f, ny = y / (height - 1f) * 2f - 1f;
                    val -= EvaluateFalloff(Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)));
                }
                noiseValues[x + y * width] = val;
            }
        }

        RemoveIslands();
        DetermineSpawns();

        // 2. 타일 배치 및 Grass 좌표 수집
        TileBase[] gArr = new TileBase[noiseValues.Length], cArr = new TileBase[noiseValues.Length];
        grassPositions.Clear();
        float sandT = waterThreshold + 0.1f, mountT = 0.7f;

        for (int i = 0; i < noiseValues.Length; i++)
        {
            float v = noiseValues[i];
            if (v < waterThreshold) { cArr[i] = waterTile; }
            else
            {
                if (v < sandT) gArr[i] = sandTile;
                else if (v < mountT)
                {
                    gArr[i] = grassTile;
                    Vector3 pos = GetWorldPos(i);
                    if ((pos - GetPlayerSpawnPosition()).sqrMagnitude > 2.25f && (pos - GetPortalSpawnPosition()).sqrMagnitude > 2.25f)
                        grassPositions.Add(pos);
                }
                else gArr[i] = mountainTile;
            }
        }
        BoundsInt b = new BoundsInt(0, 0, 0, width, height, 1);
        groundTilemap.SetTilesBlock(b, gArr);
        collisionTilemap.SetTilesBlock(b, cArr);
        TilemapGeneratedEvent?.Invoke(grassPositions);
    }

    private void DetermineSpawns()
    {
        List<int> edges = new List<int>();
        foreach (int i in largestBlob)
        {
            int x = i % width, y = i / width;
            if (IsWater(x + 1, y) || IsWater(x - 1, y) || IsWater(x, y + 1) || IsWater(x, y - 1)) edges.Add(i);
        }
        portalIdx = edges.Count > 0 ? edges[UnityEngine.Random.Range(0, edges.Count)] : (largestBlob.Count > 0 ? largestBlob[0] : -1);
        
        playerIdx = -1;
        int px = portalIdx % width, py = portalIdx / width;
        for (int r = 3; r <= 20 && playerIdx == -1; r++)
        {
            for (int dx = -r; dx <= r && playerIdx == -1; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    // 포탈로부터 최소 3타일 거리를 유지
                    if (Mathf.Abs(dx) < 3 && Mathf.Abs(dy) < 3) continue;

                    int x = px + dx, y = py + dy;
                    if (x < 0 || x >= width || y < 0 || y >= height) continue;

                    int idx = x + y * width;
                    if (noiseValues[idx] >= waterThreshold)
                    { playerIdx = idx; break; }
                }
            }
        }
        if (playerIdx == -1) playerIdx = portalIdx;
    }

    private bool IsWater(int x, int y) => x < 0 || x >= width || y < 0 || y >= height || noiseValues[x + y * width] < waterThreshold;

    private Vector3 GetWorldPos(int i) => i < 0 ? Vector3.zero : groundTilemap.GetCellCenterWorld(new Vector3Int(i % width, i / width, 0)) + new Vector3(0, grid.cellSize.y * 0.5f, 0);

    private void RemoveIslands()
    {
        bool[] vis = new bool[noiseValues.Length];
        largestBlob.Clear();
        for (int i = 0; i < noiseValues.Length; i++)
        {
            if (noiseValues[i] < waterThreshold || vis[i]) continue;
            List<int> curr = new List<int>();
            Queue<int> q = new Queue<int>();
            q.Enqueue(i); vis[i] = true;
            while (q.Count > 0)
            {
                int c = q.Dequeue(); curr.Add(c);
                int cx = c % width, cy = c / width;
                int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                for (int j = 0; j < 4; j++)
                {
                    int nx = cx + dx[j], ny = cy + dy[j], ni = nx + ny * width;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && !vis[ni] && noiseValues[ni] >= waterThreshold)
                    { vis[ni] = true; q.Enqueue(ni); }
                }
            }
            if (curr.Count > largestBlob.Count) largestBlob = curr;
        }
        vis = new bool[noiseValues.Length];
        foreach (int i in largestBlob) vis[i] = true;
        for (int i = 0; i < noiseValues.Length; i++) if (noiseValues[i] >= waterThreshold && !vis[i]) noiseValues[i] = waterThreshold - 0.05f;
    }

    private float EvaluateFalloff(float v) { float p = Mathf.Pow(v, falloffA); return p / (p + Mathf.Pow(falloffB - falloffB * v, falloffA)); }
}
