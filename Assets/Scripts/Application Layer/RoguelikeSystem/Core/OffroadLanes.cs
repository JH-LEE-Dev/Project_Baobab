using UnityEngine;

/// <summary>
/// 오프로드 돌진 차선 계산. 섬(타원) 위를 가로지르는 직선 하나를 고르고, 그 직선이 섬 가장자리와 만나는 두 점을 구한다.
///
/// 차량 그림은 아이소 3/4 시점 한 방향(왼쪽 아래를 보는 모습)과 그 좌우 반전뿐이라, 아무 방향으로나 달리면 뒷걸음질처럼 보인다.
/// 그래서 진행 방향은 아이소 대각선 아래쪽 두 방향(왼쪽 아래 · 오른쪽 아래)으로만 고르고, 차선은 그 방향에 수직으로 펼쳐 섬 전체를 덮는다.
/// </summary>
public static class OffroadLanes
{
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

    /// <summary>
    /// 한 번에 _count대가 나갈 때 _index번째 차량의 수직 오프셋. 차선이 겹치지 않게 반폭의 ±_spread 안을 고르게 나누고,
    /// 각 칸 안에서 _jitter01(0~1)로 흔든다.
    /// </summary>
    public static float LaneOffset(int _index, int _count, float _halfWidth, float _spread, float _jitter01)
    {
        int count = Mathf.Max(1, _count);
        float span = 2f * _halfWidth * Mathf.Clamp01(_spread);
        float cell = span / count;
        return -span * 0.5f + cell * (Mathf.Clamp(_index, 0, count - 1) + Mathf.Clamp01(_jitter01));
    }
}
