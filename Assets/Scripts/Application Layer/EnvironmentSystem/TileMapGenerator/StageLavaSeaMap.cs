using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 섬을 둘러싼 용암 바다 지도. 맵을 만들 때 칸마다 "섬 밖과 이어진 바다인지, 바다면 해안에서 몇 칸 떨어졌는지"를 담은
/// 작은 텍스처를 한 장 만들어 전역으로 넘긴다(바다가 아니면 0, 바다면 1 + 해안까지 칸 수, 최대 255).
/// 물 타일 셰이더가 이 지도를 읽어 바다 쪽 거품 빈도(WaterBubbles.hlsl)와 바다 온도 무늬·굳은 껍질(WaterMagma.hlsl)을 정한다.
/// 맵을 만들 때 한 번 훑고 끝나며 매 프레임 하는 일은 없다. 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// 이 지도를 쓰는 연출이 하나도 켜지지 않았거나 로그라이크면 전역값을 0으로 비워 예전과 똑같이 그린다.
/// </summary>
public static class StageLavaSeaMap
{
    private static readonly int TextureID = Shader.PropertyToID("_LavaSeaTex");
    // x = 켜짐(0/1)
    private static readonly int AID = Shader.PropertyToID("_LavaSeaA");
    // 월드 → 칸 좌표 변환(2x2 역행렬: m00, m01, m10, m11)
    private static readonly int MapAID = Shader.PropertyToID("_LavaSeaMapA");
    // xy = 칸 (0,0) 가운데의 월드 위치, zw = 텍스처 왼쪽 아래 칸 번호
    private static readonly int MapBID = Shader.PropertyToID("_LavaSeaMapB");
    // xy = 텍스처 크기(칸 수)
    private static readonly int MapCID = Shader.PropertyToID("_LavaSeaMapC");

    private const int MaxDistance = 254;

    private static Texture2D seaTexture;
    private static byte[] seaBytes;
    private static readonly Queue<int> queue = new Queue<int>();

    // // 퍼블릭 제어 메서드
    /// <summary>
    /// _waterTiles는 _bounds 범위의 물 타일 배열(x 먼저, 인덱스 = x + y * 너비)이고, 비어 있지 않은 칸을 물로 본다.
    /// _gridWidth·_gridHeight는 원본 맵 그리드 크기다. 그리드 테두리 칸이나 그리드 밖의 물에서 4방향으로 이어진 물을 바다로 본다.
    /// 해안은 그리드 안의 빈 칸(땅)이다. 그리드 밖 빈 칸(맵 끝 너머)은 해안으로 치지 않는다.
    /// </summary>
    public static void Apply(StageTileDataSO _tileData, TileBase[] _waterTiles, BoundsInt _bounds, int _gridWidth, int _gridHeight, Tilemap _grid)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        if (null == _tileData || false == IsNeeded(_tileData) || null == _waterTiles || null == _grid
            || GameModeContext.IsRoguelike || width <= 0 || height <= 0 || _waterTiles.Length < width * height)
        {
            Clear();
            return;
        }

        Vector3 origin = _grid.GetCellCenterWorld(Vector3Int.zero);
        Vector2 axisX = _grid.GetCellCenterWorld(new Vector3Int(1, 0, 0)) - origin;
        Vector2 axisY = _grid.GetCellCenterWorld(new Vector3Int(0, 1, 0)) - origin;
        float determinant = axisX.x * axisY.y - axisY.x * axisX.y;
        if (Mathf.Abs(determinant) < 0.000001f)
        {
            Clear();
            return;
        }

        PrepareTexture(width, height);
        MarkSea(_waterTiles, _bounds, _gridWidth, _gridHeight);
        MeasureShoreDistance(_waterTiles, _bounds, _gridWidth, _gridHeight);
        seaTexture.SetPixelData(seaBytes, 0);
        seaTexture.Apply(false, false);

        Shader.SetGlobalTexture(TextureID, seaTexture);
        Shader.SetGlobalVector(MapAID, new Vector4(axisY.y / determinant, -axisY.x / determinant, -axisX.y / determinant, axisX.x / determinant));
        Shader.SetGlobalVector(MapBID, new Vector4(origin.x, origin.y, _bounds.xMin, _bounds.yMin));
        Shader.SetGlobalVector(MapCID, new Vector4(width, height, 0f, 0f));
        Shader.SetGlobalVector(AID, new Vector4(1f, 0f, 0f, 0f));
    }

    public static void Clear()
    {
        Shader.SetGlobalVector(AID, Vector4.zero);
    }

    // 바다 쪽 거품 빈도를 낮췄거나 바다 온도 무늬를 켠 스테이지만 지도가 필요하다.
    private static bool IsNeeded(StageTileDataSO _tileData)
    {
        WaterBubbleSettings bubbles = _tileData.WaterBubbles;
        WaterMagmaSettings magma = _tileData.WaterMagma;
        bool bBubbles = null != bubbles && bubbles.enabled && bubbles.seaFrequency < 1f;
        bool bMagma = null != magma && magma.enabled && magma.seaEnabled;
        return bBubbles || bMagma;
    }

    private static void PrepareTexture(int _width, int _height)
    {
        if (null != seaTexture && seaTexture.width == _width && seaTexture.height == _height)
        {
            return;
        }

        if (null != seaTexture)
        {
            Object.Destroy(seaTexture);
        }

        // 바다 여부는 칸 가운데를 정확히 읽고(가운데에서는 보간해도 제 값), 해안 거리는 칸 사이를 부드럽게 잇도록 보간한다.
        seaTexture = new Texture2D(_width, _height, TextureFormat.R8, false, true);
        seaTexture.name = "LavaSeaMap";
        seaTexture.filterMode = FilterMode.Bilinear;
        seaTexture.wrapMode = TextureWrapMode.Clamp;
        seaTexture.hideFlags = HideFlags.DontSave;
        seaBytes = new byte[_width * _height];
    }

    // 그리드 테두리·그리드 밖의 물 칸에서 시작해 4방향으로 이어진 물을 모두 바다(255, 거리는 아래에서 정한다)로 칠한다. 나머지는 0.
    private static void MarkSea(TileBase[] _waterTiles, BoundsInt _bounds, int _gridWidth, int _gridHeight)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        System.Array.Clear(seaBytes, 0, seaBytes.Length);
        queue.Clear();

        for (int y = 0; y < height; y++)
        {
            int cellY = y + _bounds.yMin;
            for (int x = 0; x < width; x++)
            {
                int cellX = x + _bounds.xMin;
                bool bOnOrOutsideGridEdge = cellX <= 0 || cellY <= 0 || cellX >= _gridWidth - 1 || cellY >= _gridHeight - 1;
                if (bOnOrOutsideGridEdge)
                {
                    TryMarkSea(_waterTiles, x, y, width, height);
                }
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            TryMarkSea(_waterTiles, x + 1, y, width, height);
            TryMarkSea(_waterTiles, x - 1, y, width, height);
            TryMarkSea(_waterTiles, x, y + 1, width, height);
            TryMarkSea(_waterTiles, x, y - 1, width, height);
        }
    }

    private static void TryMarkSea(TileBase[] _waterTiles, int _x, int _y, int _width, int _height)
    {
        if (_x < 0 || _y < 0 || _x >= _width || _y >= _height)
        {
            return;
        }

        int index = _x + _y * _width;
        if (0 != seaBytes[index] || null == _waterTiles[index])
        {
            return;
        }

        seaBytes[index] = 255;
        queue.Enqueue(index);
    }

    // 땅(그리드 안의 빈 칸)과 8방향으로 맞닿은 바다 칸을 거리 0으로 두고 8방향으로 넓혀 가며 해안까지 칸 수를 잰다.
    // 값은 1 + 거리(최대 255)로 담는다. 땅에 닿지 않는 바다(맵 끝 너머만 닿는 칸)는 넓혀 가며 정해지고, 끝내 안 닿으면 최대값.
    private static void MeasureShoreDistance(TileBase[] _waterTiles, BoundsInt _bounds, int _gridWidth, int _gridHeight)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        queue.Clear();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = x + y * width;
                if (0 == seaBytes[index])
                {
                    continue;
                }

                if (TouchesLand(_waterTiles, _bounds, x, y, _gridWidth, _gridHeight))
                {
                    seaBytes[index] = 1;
                    queue.Enqueue(index);
                }
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            byte next = (byte)Mathf.Min(seaBytes[index] + 1, MaxDistance + 1);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    int neighbor = nx + ny * width;
                    // 아직 255(미정)인 바다 칸만 채운다. 먼저 닿은 쪽이 가장 가까운 해안이다.
                    if (255 != seaBytes[neighbor] || next >= 255)
                    {
                        continue;
                    }

                    seaBytes[neighbor] = next;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private static bool TouchesLand(TileBase[] _waterTiles, BoundsInt _bounds, int _x, int _y, int _gridWidth, int _gridHeight)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = _x + dx;
                int ny = _y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    continue;
                }

                int cellX = nx + _bounds.xMin;
                int cellY = ny + _bounds.yMin;
                bool bInsideGrid = cellX >= 0 && cellY >= 0 && cellX < _gridWidth && cellY < _gridHeight;
                if (bInsideGrid && null == _waterTiles[nx + ny * width])
                {
                    return true;
                }
            }
        }

        return false;
    }
}
