using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;

public class TileMapGenerator : MonoBehaviour, ITilemapDataProvider
{
    // // 이벤트
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

    // // 내부 의존성
    private Tilemap groundTilemap;
    private Grid grid;

    // // 최적화를 위한 재사용 컬렉션 (GC Alloc 방지)
    private bool[] visited;
    private bool[] isEdgeFlag;
    private Queue<int> bfsQueue;
    private List<int> currentBlob;
    private List<int> largestBlob;
    private List<int> edgeIndices;
    private List<Vector3> grassTileWorldPositions;
    private float[] noiseValues;
    private TileBase[] groundArray;
    private TileBase[] collisionArray;
    private Vector3 halfCellHeightOffset;

    // // 스폰 데이터
    private int playerSpawnIndex = -1;
    private int portalSpawnIndex = -1;

    public void InitializeMapData()
    {
        if (grid == null)
        {
            grid = Instantiate(gridPrefab, transform.position, Quaternion.identity).GetComponent<Grid>();
        }

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>();
        Tilemap collisionTilemap = null;
        foreach (var map in maps)
        {
            if (map.name == "GroundTilemap") groundTilemap = map;
            else if (map.name == "ColliderTilemap") collisionTilemap = map;
        }

        int totalSize = width * height;
        visited = new bool[totalSize];
        isEdgeFlag = new bool[totalSize];
        bfsQueue = new Queue<int>(totalSize);
        currentBlob = new List<int>(totalSize);
        largestBlob = new List<int>(totalSize);
        edgeIndices = new List<int>(totalSize / 10);
        grassTileWorldPositions = new List<Vector3>(totalSize);
        noiseValues = new float[totalSize];
        groundArray = new TileBase[totalSize];
        collisionArray = new TileBase[totalSize];

        // 타일 세로 크기의 절반 오프셋 캐싱
        halfCellHeightOffset = new Vector3(0, grid.cellSize.y * 0.5f, 0);

        if (seed == 0) seed = UnityEngine.Random.Range(1, 100000);
    }

    public Vector3 GetPlayerSpawnPosition() => GetWorldPosByIndex(playerSpawnIndex);
    public Vector3 GetPortalSpawnPosition() => GetWorldPosByIndex(portalSpawnIndex);
    public List<Vector3> GetGrassTileWorldPositions() => grassTileWorldPositions;

    public void GenerateMap()
    {
        if (groundTilemap == null) return;

        groundTilemap.ClearAllTiles();
        // ColliderTilemap은 groundTilemap과 동일한 구조의 자식이므로 함께 정리 (필요 시 캐싱 가능)
        Tilemap collisionTilemap = grid.transform.Find("ColliderTilemap")?.GetComponent<Tilemap>();
        if (collisionTilemap != null) collisionTilemap.ClearAllTiles();

        float invWidth = 1f / width;
        float invHeight = 1f / height;
        float invWidthMinusOne = 1f / (width - 1);
        float invHeightMinusOne = 1f / (height - 1);

        // 1단계: 노이즈 생성
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            float yCoord = (float)y * invHeight * scale + seed;
            float ny = (float)y * invHeightMinusOne * 2f - 1f;

            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x * invWidth * scale + seed;
                float noiseValue = Mathf.PerlinNoise(xCoord, yCoord);

                if (useIslandPrevention)
                {
                    float nx = (float)x * invWidthMinusOne * 2f - 1f;
                    float distance = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                    noiseValue -= EvaluateFalloff(distance);
                }
                noiseValues[x + rowOffset] = noiseValue;
            }
        }

        RemoveIslands();
        DetermineSpawnPoints();

        // 2단계: 타일 배치 및 Grass 좌표 수집 (최적화 루프)
        grassTileWorldPositions.Clear();
        Vector3 playerPos = GetPlayerSpawnPosition();
        Vector3 portalPos = GetPortalSpawnPosition();
        const float excludeRadiusSqr = 1.5f * 1.5f;

        int currX = 0;
        int currY = 0;
        for (int i = 0; i < noiseValues.Length; i++)
        {
            float val = noiseValues[i];
            if (val < waterThreshold)
            {
                collisionArray[i] = waterTile;
                groundArray[i] = null;
            }
            else
            {
                collisionArray[i] = null;
                TileBase tile = GetTileByNoise(val);
                groundArray[i] = tile;

                if (tile == grassTile)
                {
                    // % 와 / 연산 대신 현재 추적 중인 좌표 사용
                    Vector3 worldPos = groundTilemap.GetCellCenterWorld(new Vector3Int(currX, currY, 0)) - halfCellHeightOffset;
                    if ((worldPos - playerPos).sqrMagnitude >= excludeRadiusSqr && 
                        (worldPos - portalPos).sqrMagnitude >= excludeRadiusSqr)
                    {
                        grassTileWorldPositions.Add(worldPos);
                    }
                }
            }

            // 인덱스 기반 좌표 추적 (나눗셈 제거)
            currX++;
            if (currX >= width) { currX = 0; currY++; }
        }

        BoundsInt area = new BoundsInt(0, 0, 0, width, height, 1);
        groundTilemap.SetTilesBlock(area, groundArray);
        if (collisionTilemap != null) collisionTilemap.SetTilesBlock(area, collisionArray);

        TilemapGeneratedEvent?.Invoke(grassTileWorldPositions);
    }

    private void DetermineSpawnPoints()
    {
        edgeIndices.Clear();
        Array.Clear(isEdgeFlag, 0, isEdgeFlag.Length);

        int blobCount = largestBlob.Count;
        for (int i = 0; i < blobCount; i++)
        {
            int idx = largestBlob[i];
            int cx = idx % width;
            int cy = idx / width;

            if (IsWaterOrEdge(cx + 1, cy) || IsWaterOrEdge(cx - 1, cy) ||
                IsWaterOrEdge(cx, cy + 1) || IsWaterOrEdge(cx, cy - 1))
            {
                edgeIndices.Add(idx);
                isEdgeFlag[idx] = true;
            }
        }

        if (edgeIndices.Count == 0)
        {
            playerSpawnIndex = blobCount > 0 ? largestBlob[0] : -1;
            portalSpawnIndex = playerSpawnIndex;
            return;
        }

        // 1. 포탈 위치를 먼저 결정 (해안가 중 랜덤)
        portalSpawnIndex = edgeIndices[UnityEngine.Random.Range(0, edgeIndices.Count)];
        int potX = portalSpawnIndex % width;
        int potY = portalSpawnIndex / width;

        // 2. 포탈 인근에서 가장 가까운 Grass 타일을 찾아 캐릭터 위치로 결정
        playerSpawnIndex = -1;
        
        // 탐색 범위를 0(포탈 위치)부터 반경 10까지 점진적으로 확대하며 탐색
        for (int r = 0; r <= 10 && playerSpawnIndex == -1; r++)
        {
            for (int dx = -r; dx <= r && playerSpawnIndex == -1; dx++)
            {
                int tx = potX + dx;
                if (tx < 0 || tx >= width) continue;

                for (int dy = -r; dy <= r; dy++)
                {
                    int ty = potY + dy;
                    if (ty < 0 || ty >= height) continue;

                    int tIdx = tx + ty * width;
                    float val = noiseValues[tIdx];

                    // 해당 타일이 메인 대륙에 있고(RemoveIslands에서 걸러짐), Grass 타일 조건에 맞는지 확인
                    if (val >= waterThreshold + 0.1f && val < 0.7f)
                    {
                        playerSpawnIndex = tIdx;
                        break;
                    }
                }
            }
        }

        // 만약 인근에 Grass 타일이 전혀 없다면 (매우 드문 지형), 안전책으로 포탈 위치를 사용
        if (playerSpawnIndex == -1) playerSpawnIndex = portalSpawnIndex;
    }

    private bool IsWaterOrEdge(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return true;
        return noiseValues[_x + _y * width] < waterThreshold;
    }

    private Vector3 GetWorldPosByIndex(int _index)
    {
        if (_index < 0 || groundTilemap == null) return Vector3.zero;
        return groundTilemap.GetCellCenterWorld(new Vector3Int(_index % width, _index / width, 0)) - halfCellHeightOffset;
    }

    private void RemoveIslands()
    {
        int totalSize = width * height;
        Array.Clear(visited, 0, totalSize);
        largestBlob.Clear();

        for (int i = 0; i < totalSize; i++)
        {
            if (noiseValues[i] < waterThreshold || visited[i]) continue;
            currentBlob.Clear();
            bfsQueue.Clear();
            bfsQueue.Enqueue(i);
            visited[i] = true;

            while (bfsQueue.Count > 0)
            {
                int curr = bfsQueue.Dequeue();
                currentBlob.Add(curr);
                int cx = curr % width;
                int cy = curr / width;
                CheckNeighbor(cx + 1, cy);
                CheckNeighbor(cx - 1, cy);
                CheckNeighbor(cx, cy + 1);
                CheckNeighbor(cx, cy - 1);
            }

            if (currentBlob.Count > largestBlob.Count)
            {
                largestBlob.Clear();
                largestBlob.AddRange(currentBlob);
            }
        }

        Array.Clear(visited, 0, totalSize);
        for (int i = 0; i < largestBlob.Count; i++) visited[largestBlob[i]] = true;

        float failValue = waterThreshold - 0.05f;
        for (int i = 0; i < totalSize; i++)
        {
            if (noiseValues[i] >= waterThreshold && !visited[i]) noiseValues[i] = failValue;
        }
    }

    private void CheckNeighbor(int _x, int _y)
    {
        if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
        int idx = _x + _y * width;
        if (!visited[idx] && noiseValues[idx] >= waterThreshold)
        {
            visited[idx] = true;
            bfsQueue.Enqueue(idx);
        }
    }

    private float EvaluateFalloff(float _value)
    {
        float vPow = Mathf.Pow(_value, falloffA);
        return vPow / (vPow + Mathf.Pow(falloffB - falloffB * _value, falloffA));
    }

    private TileBase GetTileByNoise(float _value)
    {
        if (_value < waterThreshold + 0.1f) return sandTile;
        if (_value < 0.7f) return grassTile;
        return mountainTile;
    }
}
