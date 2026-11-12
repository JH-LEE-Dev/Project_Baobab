using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 물가 지도. 맵을 만들 때 칸마다 "물과 땅의 경계에서 몇 칸 떨어졌는지"(물 쪽·땅 쪽 모두)를 재어 둔다.
/// 값은 1 + 경계까지 칸 수(최대 255)다. 경계에 맞닿은 칸(땅과 맞닿은 물 칸, 물과 맞닿은 땅 칸)이 0칸이다.
/// 반딧불(StageFireflies)이 물가 자리를 고를 때 읽는다.
/// 물 깊이(WaterDepthSettings)를 켠 스테이지는 같은 값을 작은 텍스처 한 장으로 전역에 넘겨,
/// 물 타일 셰이더(WaterDepth.hlsl)가 물가에서 멀수록 물을 깊은 색으로 칠한다.
/// 맵을 만들 때 한 번 훑고 끝나며 매 프레임 하는 일은 없다. 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// 이 지도를 쓰는 연출이 켜지지 않았으면 비운다.
/// </summary>
public static class StageShoreMap
{
    private const int MaxDistance = 254;

    private static readonly int TextureID = Shader.PropertyToID("_ShoreMapTex");
    // x = 켜짐(0/1)
    private static readonly int AID = Shader.PropertyToID("_ShoreMapA");
    // 월드 → 칸 좌표 변환(2x2 역행렬: m00, m01, m10, m11)
    private static readonly int MapAID = Shader.PropertyToID("_ShoreMapMapA");
    // xy = 칸 (0,0) 가운데의 월드 위치, zw = 텍스처 왼쪽 아래 칸 번호
    private static readonly int MapBID = Shader.PropertyToID("_ShoreMapMapB");
    // xy = 텍스처 크기(칸 수)
    private static readonly int MapCID = Shader.PropertyToID("_ShoreMapMapC");

    private static byte[] shoreBytes;
    private static Texture2D shoreTexture;
    private static readonly Queue<int> queue = new Queue<int>();

    // 지도가 꺼져 있으면 grid가 null이다.
    private static Tilemap grid;
    private static BoundsInt mapBounds;

    // // 퍼블릭 제어 메서드
    /// <summary>
    /// _waterTiles는 _bounds 범위의 물 타일 배열(x 먼저, 인덱스 = x + y * 너비)이고, 비어 있지 않은 칸을 물로 본다.
    /// _gridWidth·_gridHeight는 원본 맵 그리드 크기다. _groundTiles는 그리드 크기의 바닥 타일 배열(인덱스 = x + y * 그리드 너비)이고,
    /// 바닥 타일이 깔린 칸만 땅으로 본다. 물도 바닥도 없는 칸(세상 끝 원 밖의 허공)은 그리드 안이어도 경계를 만들지 않는다.
    /// </summary>
    public static void Apply(StageTileDataSO _tileData, TileBase[] _waterTiles, BoundsInt _bounds, int _gridWidth, int _gridHeight, TileBase[] _groundTiles, Tilemap _grid)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        if (null == _tileData || false == IsNeeded(_tileData) || null == _waterTiles || null == _grid
            || null == _groundTiles || _groundTiles.Length < _gridWidth * _gridHeight
            || width <= 0 || height <= 0 || _waterTiles.Length < width * height)
        {
            Clear();
            return;
        }

        if (null == shoreBytes || shoreBytes.Length != width * height)
        {
            shoreBytes = new byte[width * height];
        }

        MeasureShoreDistance(_waterTiles, _bounds, _gridWidth, _gridHeight, _groundTiles);
        grid = _grid;
        mapBounds = _bounds;

        if (IsTextureNeeded(_tileData))
        {
            UploadTexture(width, height);
        }
        else
        {
            Shader.SetGlobalVector(AID, Vector4.zero);
        }
    }

    public static void Clear()
    {
        grid = null;
        Shader.SetGlobalVector(AID, Vector4.zero);
        ReleaseTexture();
    }

    /// <summary>
    /// 월드 위치가 속한 칸이 물과 땅의 경계에서 몇 칸 떨어졌는지. 지도가 꺼져 있거나 지도 밖이면 false.
    /// </summary>
    public static bool TryGetShoreDistance(Vector3 _worldPosition, out int _cells)
    {
        _cells = 0;
        if (null == grid || null == shoreBytes)
        {
            return false;
        }

        Vector3Int cell = grid.WorldToCell(_worldPosition);
        int x = cell.x - mapBounds.xMin;
        int y = cell.y - mapBounds.yMin;
        if (x < 0 || y < 0 || x >= mapBounds.size.x || y >= mapBounds.size.y)
        {
            return false;
        }

        byte value = shoreBytes[x + y * mapBounds.size.x];
        if (0 == value)
        {
            return false;
        }

        _cells = value - 1;
        return true;
    }

    // 반딧불이나 물 깊이를 켠 스테이지만 지도가 필요하다.
    private static bool IsNeeded(StageTileDataSO _tileData)
    {
        FireflySettings fireflies = _tileData.Fireflies;
        return (null != fireflies && fireflies.enabled && null != fireflies.material) || IsTextureNeeded(_tileData);
    }

    // 물 깊이를 켠 스테이지만 셰이더용 텍스처가 필요하다.
    private static bool IsTextureNeeded(StageTileDataSO _tileData)
    {
        WaterDepthSettings depth = _tileData.WaterDepth;
        return null != depth && depth.enabled;
    }

    // 텍스처를 지우고 전역 바인딩도 푼다. 에디터 맵 미리보기(편집 모드)에서도 불리므로 그때는 즉시 지운다.
    private static void ReleaseTexture()
    {
        if (null == shoreTexture)
        {
            return;
        }

        Shader.SetGlobalTexture(TextureID, null);
        if (Application.isPlaying)
        {
            Object.Destroy(shoreTexture);
        }
        else
        {
            Object.DestroyImmediate(shoreTexture);
        }
        shoreTexture = null;
    }

    // 칸마다 값(1 + 경계까지 칸 수)을 그대로 담은 텍스처를 만들어 전역으로 넘긴다. 칸 사이는 부드럽게 이어지도록 보간한다.
    private static void UploadTexture(int _width, int _height)
    {
        Vector3 origin = grid.GetCellCenterWorld(Vector3Int.zero);
        Vector2 axisX = grid.GetCellCenterWorld(new Vector3Int(1, 0, 0)) - origin;
        Vector2 axisY = grid.GetCellCenterWorld(new Vector3Int(0, 1, 0)) - origin;
        float determinant = axisX.x * axisY.y - axisY.x * axisX.y;
        if (Mathf.Abs(determinant) < 0.000001f)
        {
            Shader.SetGlobalVector(AID, Vector4.zero);
            return;
        }

        if (null == shoreTexture || shoreTexture.width != _width || shoreTexture.height != _height)
        {
            ReleaseTexture();

            shoreTexture = new Texture2D(_width, _height, TextureFormat.R8, false, true);
            shoreTexture.name = "ShoreMap";
            shoreTexture.filterMode = FilterMode.Bilinear;
            shoreTexture.wrapMode = TextureWrapMode.Clamp;
            shoreTexture.hideFlags = HideFlags.DontSave;
        }

        shoreTexture.SetPixelData(shoreBytes, 0);
        shoreTexture.Apply(false, false);

        Shader.SetGlobalTexture(TextureID, shoreTexture);
        Shader.SetGlobalVector(MapAID, new Vector4(axisY.y / determinant, -axisY.x / determinant, -axisX.y / determinant, axisX.x / determinant));
        Shader.SetGlobalVector(MapBID, new Vector4(origin.x, origin.y, mapBounds.xMin, mapBounds.yMin));
        Shader.SetGlobalVector(MapCID, new Vector4(_width, _height, 0f, 0f));
        Shader.SetGlobalVector(AID, new Vector4(1f, 0f, 0f, 0f));
    }

    // 물과 땅이 4방향으로 맞닿은 칸을 거리 0으로 두고 8방향으로 넓혀 가며 경계까지 칸 수를 잰다(1 + 거리, 최대 255).
    // 땅은 그리드 안에서 바닥 타일이 깔린 칸이다. 물도 바닥도 없는 칸(맵 끝 너머, 세상 끝 원 밖의 허공)은 경계를 만들지 않는다.
    // 끝내 안 닿는 칸은 최대값.
    private static void MeasureShoreDistance(TileBase[] _waterTiles, BoundsInt _bounds, int _gridWidth, int _gridHeight, TileBase[] _groundTiles)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        queue.Clear();

        for (int i = 0; i < shoreBytes.Length; i++)
        {
            shoreBytes[i] = 255;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int kind = CellKind(_waterTiles, _bounds, x, y, _gridWidth, _gridHeight, _groundTiles);
                if (0 == kind)
                {
                    continue;
                }

                // 반대 종류(물 ↔ 땅)와 4방향으로 맞닿았으면 경계 칸
                int opposite = 1 == kind ? 2 : 1;
                if (opposite == CellKind(_waterTiles, _bounds, x + 1, y, _gridWidth, _gridHeight, _groundTiles)
                    || opposite == CellKind(_waterTiles, _bounds, x - 1, y, _gridWidth, _gridHeight, _groundTiles)
                    || opposite == CellKind(_waterTiles, _bounds, x, y + 1, _gridWidth, _gridHeight, _groundTiles)
                    || opposite == CellKind(_waterTiles, _bounds, x, y - 1, _gridWidth, _gridHeight, _groundTiles))
                {
                    int index = x + y * width;
                    shoreBytes[index] = 1;
                    queue.Enqueue(index);
                }
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            byte next = (byte)Mathf.Min(shoreBytes[index] + 1, MaxDistance + 1);
            if (next >= 255)
            {
                continue;
            }

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
                    // 먼저 닿은 쪽이 가장 가까운 경계다.
                    if (255 != shoreBytes[neighbor])
                    {
                        continue;
                    }

                    shoreBytes[neighbor] = next;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    // 0 = 범위 밖이거나 물도 바닥도 없는 칸, 1 = 물, 2 = 땅(그리드 안에서 바닥 타일이 깔린 칸)
    private static int CellKind(TileBase[] _waterTiles, BoundsInt _bounds, int _x, int _y, int _gridWidth, int _gridHeight, TileBase[] _groundTiles)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        if (_x < 0 || _y < 0 || _x >= width || _y >= height)
        {
            return 0;
        }

        if (null != _waterTiles[_x + _y * width])
        {
            return 1;
        }

        int cellX = _x + _bounds.xMin;
        int cellY = _y + _bounds.yMin;
        bool bInsideGrid = cellX >= 0 && cellY >= 0 && cellX < _gridWidth && cellY < _gridHeight;
        return bInsideGrid && null != _groundTiles[cellX + cellY * _gridWidth] ? 2 : 0;
    }
}
