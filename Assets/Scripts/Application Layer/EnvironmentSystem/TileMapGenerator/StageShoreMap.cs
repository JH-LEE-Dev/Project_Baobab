using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 물가 지도. 맵을 만들 때 칸마다 "물과 땅의 경계에서 몇 칸 떨어졌는지"(물 쪽·땅 쪽 모두)를 재어 둔다.
/// 값은 1 + 경계까지 칸 수(최대 255)다. 경계에 맞닿은 칸(땅과 맞닿은 물 칸, 물과 맞닿은 땅 칸)이 0칸이다.
/// 반딧불(StageFireflies)이 물가 자리를 고를 때 읽는다.
/// 맵을 만들 때 한 번 훑고 끝나며 매 프레임 하는 일은 없다. 렌더링만 바꾸고 게임 동작에는 영향이 없다.
/// 이 지도를 쓰는 연출이 켜지지 않았거나 로그라이크면 비운다.
/// </summary>
public static class StageShoreMap
{
    private const int MaxDistance = 254;

    private static byte[] shoreBytes;
    private static readonly Queue<int> queue = new Queue<int>();

    // 지도가 꺼져 있으면 grid가 null이다.
    private static Tilemap grid;
    private static BoundsInt mapBounds;

    // // 퍼블릭 제어 메서드
    /// <summary>
    /// _waterTiles는 _bounds 범위의 물 타일 배열(x 먼저, 인덱스 = x + y * 너비)이고, 비어 있지 않은 칸을 물로 본다.
    /// _gridWidth·_gridHeight는 원본 맵 그리드 크기다. 그리드 밖 빈 칸(맵 끝 너머)은 땅으로 치지 않는다.
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

        if (null == shoreBytes || shoreBytes.Length != width * height)
        {
            shoreBytes = new byte[width * height];
        }

        MeasureShoreDistance(_waterTiles, _bounds, _gridWidth, _gridHeight);
        grid = _grid;
        mapBounds = _bounds;
    }

    public static void Clear()
    {
        grid = null;
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

    // 반딧불을 켠 스테이지만 지도가 필요하다.
    private static bool IsNeeded(StageTileDataSO _tileData)
    {
        FireflySettings fireflies = _tileData.Fireflies;
        return null != fireflies && fireflies.enabled && null != fireflies.material;
    }

    // 물과 땅이 4방향으로 맞닿은 칸을 거리 0으로 두고 8방향으로 넓혀 가며 경계까지 칸 수를 잰다(1 + 거리, 최대 255).
    // 땅은 그리드 안의 빈 칸이다. 그리드 밖 빈 칸(맵 끝 너머)은 경계를 만들지 않는다. 끝내 안 닿는 칸은 최대값.
    private static void MeasureShoreDistance(TileBase[] _waterTiles, BoundsInt _bounds, int _gridWidth, int _gridHeight)
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
                int kind = CellKind(_waterTiles, _bounds, x, y, _gridWidth, _gridHeight);
                if (0 == kind)
                {
                    continue;
                }

                // 반대 종류(물 ↔ 땅)와 4방향으로 맞닿았으면 경계 칸
                int opposite = 1 == kind ? 2 : 1;
                if (opposite == CellKind(_waterTiles, _bounds, x + 1, y, _gridWidth, _gridHeight)
                    || opposite == CellKind(_waterTiles, _bounds, x - 1, y, _gridWidth, _gridHeight)
                    || opposite == CellKind(_waterTiles, _bounds, x, y + 1, _gridWidth, _gridHeight)
                    || opposite == CellKind(_waterTiles, _bounds, x, y - 1, _gridWidth, _gridHeight))
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

    // 0 = 범위 밖이거나 그리드 밖 빈 칸, 1 = 물, 2 = 땅(그리드 안 빈 칸)
    private static int CellKind(TileBase[] _waterTiles, BoundsInt _bounds, int _x, int _y, int _gridWidth, int _gridHeight)
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
        return bInsideGrid ? 2 : 0;
    }
}
