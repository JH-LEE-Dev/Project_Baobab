using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오프로드 돌진 차선 계산. 섬(타원 또는 마름모, IslandShapeType) 위를 가로지르는 직선 하나를 고르고, 그 직선이 섬 가장자리와 만나는 두 점을 구한다.
///
/// 차량 그림은 아이소 3/4 시점 한 방향(왼쪽 아래를 보는 모습)과 그 좌우 반전뿐이라, 아무 방향으로나 달리면 뒷걸음질처럼 보인다.
/// 그래서 진행 방향은 아이소 대각선 아래쪽 두 방향(왼쪽 아래 · 오른쪽 아래)으로만 고르고, 차선은 그 방향에 수직으로 펼쳐 섬 전체를 덮는다.
/// </summary>
public static class OffroadLanes
{
    /// <summary>오프로드 차선 하나. 타일 줄 중심(수직 오프셋 offset)을 지나고, entry는 그 줄의 첫 육지 타일 중심(소환 위치), exit는 마지막 육지 타일 중심이다.</summary>
    public struct Lane
    {
        public bool bRight;
        public float offset;
        public Vector2 entry;
        public Vector2 exit;
    }

    /// <summary>차선이 지나는 타일을 읽는 창구(필드 타일맵). 칸 변환은 게임의 타일 위치 규칙(나무가 서는 자리)을 따른다.</summary>
    public interface ILandTiles
    {
        Vector3Int WorldToCell(Vector3 _worldPos);
        Vector3 CellToWorld(Vector3Int _cellPos);
        bool IsWaterTile(Vector3Int _cellPos);
    }

    /// <summary>아이소 타일의 가로:세로 비(2:1)를 따르는 아래쪽 대각선. _bRight면 오른쪽 아래, 아니면 왼쪽 아래.</summary>
    public static Vector2 DownDiagonal(bool _bRight)
    {
        return new Vector2(_bRight ? 2f : -2f, -1f).normalized;
    }

    /// <summary>진행 방향 _dir에 수직인 축으로 본 타원의 반폭(차선을 이 안에서 고르면 섬과 반드시 만난다).</summary>
    public static float HalfWidth(float _radiusX, float _radiusY, Vector2 _dir)
    {
        Vector2 perp = new Vector2(-_dir.y, _dir.x);
        return Mathf.Sqrt(_radiusX * _radiusX * perp.x * perp.x + _radiusY * _radiusY * perp.y * perp.y);
    }

    /// <summary>
    /// 중심 _center, 반지름 (_radiusX, _radiusY)인 타원을 진행 방향 _dir, 수직 오프셋 _offset인 직선이 지나는 진입점·이탈점.
    /// 직선이 타원과 만나지 않으면 false.
    /// </summary>
    public static bool TryGetLane(Vector2 _center, float _radiusX, float _radiusY, Vector2 _dir, float _offset, out Vector2 _entry, out Vector2 _exit)
    {
        _entry = _exit = _center;
        if (_radiusX <= 0f || _radiusY <= 0f || _dir.sqrMagnitude < 0.0001f) return false;

        Vector2 dir = _dir.normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 origin = perp * _offset; // 타원 중심 기준

        // ((ox + t·dx)/rx)² + ((oy + t·dy)/ry)² = 1 을 t에 대해 푼다.
        float irx2 = 1f / (_radiusX * _radiusX);
        float iry2 = 1f / (_radiusY * _radiusY);
        float a = dir.x * dir.x * irx2 + dir.y * dir.y * iry2;
        float b = 2f * (origin.x * dir.x * irx2 + origin.y * dir.y * iry2);
        float c = origin.x * origin.x * irx2 + origin.y * origin.y * iry2 - 1f;

        float disc = b * b - 4f * a * c;
        if (disc <= 0f) return false;

        float sqrt = Mathf.Sqrt(disc);
        float t1 = (-b - sqrt) / (2f * a);
        float t2 = (-b + sqrt) / (2f * a);

        _entry = _center + origin + dir * t1;
        _exit = _center + origin + dir * t2;
        return true;
    }

    /// <summary>섬 모양 _shape에 맞춘 HalfWidth. 마름모는 진행 방향에 수직인 축으로 본 꼭짓점의 가장 먼 거리다.</summary>
    public static float HalfWidth(IslandShapeType _shape, float _radiusX, float _radiusY, Vector2 _dir)
    {
        if (IslandShapeType.Diamond != _shape) return HalfWidth(_radiusX, _radiusY, _dir);

        Vector2 perp = new Vector2(-_dir.y, _dir.x);
        return Mathf.Max(_radiusX * Mathf.Abs(perp.x), _radiusY * Mathf.Abs(perp.y));
    }

    /// <summary>섬 모양 _shape에 맞춘 TryGetLane. 마름모는 네 변(반평면)으로 직선을 잘라 진입점·이탈점을 구한다.</summary>
    public static bool TryGetLane(IslandShapeType _shape, Vector2 _center, float _radiusX, float _radiusY, Vector2 _dir, float _offset, out Vector2 _entry, out Vector2 _exit)
    {
        if (IslandShapeType.Diamond != _shape) return TryGetLane(_center, _radiusX, _radiusY, _dir, _offset, out _entry, out _exit);

        _entry = _exit = _center;
        if (_radiusX <= 0f || _radiusY <= 0f || _dir.sqrMagnitude < 0.0001f) return false;

        Vector2 dir = _dir.normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 origin = perp * _offset; // 마름모 중심 기준

        // 변마다 sx·x/rx + sy·y/ry ≤ 1 (sx, sy = ±1). 직선 origin + t·dir를 넣으면 a·t ≤ b 꼴이 된다.
        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;
        for (int i = 0; i < 4; i++)
        {
            float sx = 0 == (i & 1) ? 1f : -1f;
            float sy = 0 == (i & 2) ? 1f : -1f;
            float a = sx * dir.x / _radiusX + sy * dir.y / _radiusY;
            float b = 1f - (sx * origin.x / _radiusX + sy * origin.y / _radiusY);

            if (Mathf.Abs(a) < 1e-6f)
            {
                if (b < 0f) return false;
                continue;
            }

            float t = b / a;
            if (a > 0f) tMax = Mathf.Min(tMax, t);
            else tMin = Mathf.Max(tMin, t);
        }

        if (tMin >= tMax) return false;

        _entry = _center + origin + dir * tMin;
        _exit = _center + origin + dir * tMax;
        return true;
    }

    /// <summary>
    /// 진행 방향 _dir(아이소 아래 대각선)을 따라 놓인 타일 줄 사이의 수직 간격. 타일 한 칸이 _cellSize(아이소 마름모 가로 x 세로)일 때
    /// 격자 축은 (x/2, y/2)와 (−x/2, y/2)이고, 2:1 대각선은 그중 한 축과 평행하므로 나머지 축을 수직 방향에 투영한 길이다(칸 (1, 0.5)이면 1/√5).
    /// </summary>
    public static float RowSpacing(Vector2 _dir, Vector2 _cellSize)
    {
        Vector2 dir = _dir.sqrMagnitude > 0.0001f ? _dir.normalized : Vector2.right;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 axisA = new Vector2(_cellSize.x * 0.5f, _cellSize.y * 0.5f);
        Vector2 axisB = new Vector2(-_cellSize.x * 0.5f, _cellSize.y * 0.5f);
        return Mathf.Max(Mathf.Abs(Vector2.Dot(axisA, perp)), Mathf.Abs(Vector2.Dot(axisB, perp)));
    }

    /// <summary>
    /// 타일 줄 중심을 지나는 차선 오프셋(섬 중심 기준 수직 거리)들의 번호 범위. 오프셋 = _phase + k × _spacing이고,
    /// |오프셋| ≤ 반폭 × _spread 인 k만 고른다(_spread 1이면 가장자리 줄까지). 줄이 하나도 없으면 false.
    /// </summary>
    public static bool TryGetRowRange(float _halfWidth, float _spread, float _spacing, float _phase, out int _kMin, out int _kMax)
    {
        _kMin = 0;
        _kMax = -1;
        if (_spacing <= 0.0001f) return false;

        // 실제 타일 줄은 해석적 섬 중심에서 최대 반 줄 어긋날 수 있다(섬 중심이 칸 중심보다 반 칸 아래). 가장자리 줄이 빠지지 않게
        // 반 줄(+부동소수 여유)만큼 넓혀 고르고, 줄에 실제 육지가 있는지는 CollectLandLanes가 타일로 확인한다.
        float limit = Mathf.Max(0f, _halfWidth) * Mathf.Clamp01(_spread) + _spacing * 0.51f;
        _kMin = Mathf.CeilToInt((-limit - _phase) / _spacing);
        _kMax = Mathf.FloorToInt((limit - _phase) / _spacing);
        return _kMin <= _kMax;
    }

    /// <summary>
    /// 섬을 가로지르는 오프로드 차선을 모두 모은다(기획서 v3.0 11-13 6-1). 두 진행 방향마다 타일 줄 중심을 지나는 차선을 가장자리 줄까지
    /// 훑어, 그 줄의 첫 육지 타일 중심을 소환 위치로, 마지막 육지 타일 중심을 멈추는 곳으로 정한다. 육지가 한 칸뿐인 줄은 차가 물 위로 나가므로 뺀다.
    /// _tiles가 없으면 해석적 섬 경계를 그대로 쓴다. 섬 지형만 보므로 필드가 바뀔 때 한 번 계산해 두면 된다.
    /// </summary>
    public static void CollectLandLanes(ILandTiles _tiles, IslandShapeType _shape, Vector2 _center, float _radiusX, float _radiusY,
        Vector2 _cellSize, float _spread, List<Lane> _lanes)
    {
        _lanes.Clear();

        // 타일 줄 위상: 섬 중심에 가장 가까운 칸 중심이 지나는 줄이 기준이다(타일이 없으면 섬 중심).
        Vector2 rowOrigin = _center;
        if (null != _tiles) rowOrigin = _tiles.CellToWorld(_tiles.WorldToCell(_center));
        // 진행 방향 한 칸의 길이(칸 (1, 0.5)이면 √5/4)
        float cellStep = Mathf.Sqrt(_cellSize.x * _cellSize.x + _cellSize.y * _cellSize.y) * 0.5f;

        for (int side = 0; side < 2; side++)
        {
            bool bRight = 1 == side;
            Vector2 dir = DownDiagonal(bRight);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float halfWidth = HalfWidth(_shape, _radiusX, _radiusY, dir);
            float spacing = RowSpacing(dir, _cellSize);
            float phase = Vector2.Dot(rowOrigin - _center, perp);
            if (false == TryGetRowRange(halfWidth, _spread, spacing, phase, out int kMin, out int kMax)) continue;

            // 섬 끝 줄은 마름모 변 위나 바깥에 걸려 해석적 교차가 흔들리므로 경계는 살짝 안쪽 직선으로 구하고, 훑기는 실제 줄 위로 옮겨서 한다.
            float queryLimit = Mathf.Max(0f, halfWidth - spacing * 0.05f);

            for (int k = kMin; k <= kMax; k++)
            {
                float offset = phase + k * spacing;
                if (null == _tiles && Mathf.Abs(offset) > halfWidth) continue;

                float queryOffset = Mathf.Clamp(offset, -queryLimit, queryLimit);
                if (false == TryGetLane(_shape, _center, _radiusX, _radiusY, dir, queryOffset, out Vector2 roughEntry, out Vector2 roughExit)) continue;

                Vector2 shift = perp * (offset - queryOffset);
                Vector2 entry = roughEntry + shift;
                Vector2 exit = roughExit + shift;
                if (null != _tiles && false == TryGetLandSpan(_tiles, _center, dir, perp, offset, spacing, cellStep, entry, exit, out entry, out exit)) continue;
                if (Vector2.Dot(exit - entry, dir) < cellStep * 0.5f) continue;

                _lanes.Add(new Lane { bRight = bRight, offset = offset, entry = entry, exit = exit });
            }
        }
    }

    /// <summary>
    /// 오프셋 _offset 타일 줄을 진행 방향 _dir로 훑어 첫 육지 타일 중심(_entry)과 마지막 육지 타일 중심(_exit)을 찾는다.
    /// _from · _to(그 줄 위의 대략적인 섬 경계)에서 두 칸 바깥부터 한 칸에 네 번씩 훑는다.
    /// </summary>
    private static bool TryGetLandSpan(ILandTiles _tiles, Vector2 _center, Vector2 _dir, Vector2 _perp, float _offset, float _rowSpacing,
        float _cellStep, Vector2 _from, Vector2 _to, out Vector2 _entry, out Vector2 _exit)
    {
        _exit = _to;
        float sample = Mathf.Max(0.01f, _cellStep * 0.25f);
        float margin = _cellStep * 2f;
        float length = Vector2.Distance(_from, _to) + margin * 2f;
        int samples = Mathf.CeilToInt(length / sample);

        bool bEntry = false;
        _entry = _from;
        Vector2 start = _from - _dir * margin;
        for (int i = 0; i <= samples && false == bEntry; i++) bEntry = TryGetRowLandTile(_tiles, _center, start + _dir * (sample * i), _perp, _offset, _rowSpacing, out _entry);
        if (false == bEntry) return false;

        bool bExit = false;
        Vector2 end = _to + _dir * margin;
        for (int i = 0; i <= samples && false == bExit; i++) bExit = TryGetRowLandTile(_tiles, _center, end - _dir * (sample * i), _perp, _offset, _rowSpacing, out _exit);

        return bExit && Vector2.Dot(_exit - _entry, _dir) >= 0f;
    }

    /// <summary>_position이 놓인 칸이 육지이고 그 칸 중심이 이 타일 줄 위면 칸 중심을 돌려준다(줄 경계의 샘플이 이웃 줄 칸을 집으면 버린다).</summary>
    private static bool TryGetRowLandTile(ILandTiles _tiles, Vector2 _center, Vector2 _position, Vector2 _perp, float _offset, float _rowSpacing, out Vector2 _tileCenter)
    {
        Vector3Int cell = _tiles.WorldToCell(_position);
        _tileCenter = _tiles.CellToWorld(cell);
        if (_tiles.IsWaterTile(cell)) return false;

        return Mathf.Abs(Vector2.Dot(_tileCenter - _center, _perp) - _offset) <= _rowSpacing * 0.5f;
    }
}
