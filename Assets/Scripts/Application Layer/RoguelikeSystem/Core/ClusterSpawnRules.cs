using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 웨이브의 나무 생성 위치 규칙(기획서 v2.3 3-1 "생성 위치"). 위치 목록만 다루는 순수 규칙이라 EditMode 테스트로 검증한다.
///   - 군집: 새 나무는 군집 중심 반경 안의 빈칸에 심는다. 반경 안이 다 차면 반경을 넓혀 가며 찾는다.
///   - 플레이어 바로 옆 금지: 플레이어 금지 반경 안의 칸은 고르지 않는다(이동속도·범위 카드의 가치 유지).
///   - 허탕 방지: 필드에 나무가 있는데 플레이어에서 가장 가까운 나무까지 거리가 R을 넘으면, 그 나무 쪽 군집에 우선 심는다.
/// 거리는 등각 보정 거리(세로를 두 배로 본다)로 잰다. 섬과 도끼 판정이 같은 공식을 쓴다.
/// </summary>
public static class ClusterSpawnRules
{
    private const int ExpandSteps = 4;
    private const float ExpandFactor = 1.6f;

    /// <summary>등각 보정 거리의 제곱.</summary>
    public static float IsoDistanceSq(Vector3 _a, Vector3 _b)
    {
        float dx = _a.x - _b.x;
        float dy = (_a.y - _b.y) * 2f;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// _free 중 _center에서 반경 _radius 안이고 _player에서 _exclusion 밖인 칸 하나의 인덱스를 고른다.
    /// 반경 안에 없으면 반경을 넓혀 가며 찾고, 끝까지 없으면 금지 반경 밖 아무 칸. 금지 반경 밖에 칸이 하나도 없으면 -1
    /// (플레이어 바로 옆에는 심지 않는다 - 이번 스폰은 건너뛴다).
    /// </summary>
    public static int PickIndex(IReadOnlyList<Vector3> _free, Vector3 _center, float _radius, Vector3 _player, float _exclusion, System.Random _rng, List<int> _scratch)
    {
        if (null == _free || 0 == _free.Count) return -1;

        float exclusionSq = Mathf.Max(0f, _exclusion) * Mathf.Max(0f, _exclusion);
        float radius = Mathf.Max(0.1f, _radius);

        for (int step = 0; step <= ExpandSteps; step++)
        {
            float radiusSq = radius * radius;
            _scratch.Clear();

            for (int i = 0; i < _free.Count; i++)
            {
                if (IsoDistanceSq(_free[i], _center) > radiusSq) continue;
                if (IsoDistanceSq(_free[i], _player) < exclusionSq) continue;
                _scratch.Add(i);
            }

            if (_scratch.Count > 0) return _scratch[_rng.Next(_scratch.Count)];
            radius *= ExpandFactor;
        }

        _scratch.Clear();
        for (int i = 0; i < _free.Count; i++)
        {
            if (IsoDistanceSq(_free[i], _player) >= exclusionSq) _scratch.Add(i);
        }

        if (_scratch.Count > 0) return _scratch[_rng.Next(_scratch.Count)];
        return -1;
    }

    /// <summary>
    /// 허탕 방지 기준점. _trees 중 _player에서 가장 가까운 나무까지 거리가 _gapDistance를 넘으면 그 나무 위치를 _anchor로 돌려주고 true.
    /// 나무가 없거나 가까운 나무가 있으면 false(평소 군집 중심을 쓴다).
    /// </summary>
    public static bool TryGetGapAnchor(IReadOnlyList<Vector3> _trees, Vector3 _player, float _gapDistance, out Vector3 _anchor)
    {
        _anchor = default;
        if (null == _trees || 0 == _trees.Count || _gapDistance <= 0f) return false;

        int nearest = NearestIndex(_trees, _player, out float nearestSq);
        if (nearest < 0 || nearestSq <= _gapDistance * _gapDistance) return false;

        _anchor = _trees[nearest];
        return true;
    }

    /// <summary>_points 중 _from에서 가장 가까운 점의 인덱스(등각 보정 거리). 없으면 -1.</summary>
    public static int NearestIndex(IReadOnlyList<Vector3> _points, Vector3 _from, out float _distanceSq)
    {
        _distanceSq = float.MaxValue;
        int result = -1;
        if (null == _points) return result;

        for (int i = 0; i < _points.Count; i++)
        {
            float d = IsoDistanceSq(_points[i], _from);
            if (d >= _distanceSq) continue;

            _distanceSq = d;
            result = i;
        }

        return result;
    }

    /// <summary>
    /// 웨이브 시작 초기 나무 수(기획서 v2.3 3-1 "초기 나무"): 초당 스폰 × _seconds, 반올림. 임계치 T보다 항상 낮고 목표 처치 수를 넘지 않는다.
    /// </summary>
    public static int InitialTreeCount(float _spawnInterval, float _seconds, int _threshold, int _killTarget)
    {
        if (_spawnInterval <= 0f || _seconds <= 0f) return 0;

        int count = Mathf.RoundToInt(_seconds / _spawnInterval);
        count = Mathf.Min(count, Mathf.Max(0, _threshold - 1));
        return Mathf.Clamp(count, 0, Mathf.Max(0, _killTarget));
    }
}
