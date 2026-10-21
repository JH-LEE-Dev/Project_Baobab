using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 웨이브의 나무 생성 규칙(기획서 v2.5 3-1 "초기 나무" · "스폰 속도 · 묶음" · "생성 위치"). 위치 목록만 다루는 순수 규칙이라 EditMode 테스트로 검증한다.
///   - 초기 나무: 현재 임계치 T의 절반. 목표 처치 수에 포함한다.
///   - 묶음: 묶음 주기 P마다 초당 스폰 × P그루를 한꺼번에 심는다. 소수는 다음 묶음으로 넘겨 평균 초당 스폰을 지킨다.
///   - 분산: 새 나무는 필드 곳곳의 빈칸에 무작위로 심되, 다른 나무와 최소 간격을 둔다(간격을 지킬 칸이 없으면 간격만 포기한다).
///   - 플레이어 바로 옆 금지: 플레이어 금지 반경 안의 칸은 고르지 않는다(이동속도·범위 카드의 가치 유지).
/// 거리는 등각 보정 거리(세로를 두 배로 본다)로 잰다. 섬과 도끼 판정이 같은 공식을 쓴다.
/// </summary>
public static class TreeSpawnRules
{
    /// <summary>등각 보정 거리의 제곱.</summary>
    public static float IsoDistanceSq(Vector3 _a, Vector3 _b)
    {
        float dx = _a.x - _b.x;
        float dy = (_a.y - _b.y) * 2f;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// _free 중 _player에서 _exclusion 밖이고 _occupied(지금 서 있는 나무 · 이번 묶음에 심은 나무)와 모두 _minSpacing 이상 떨어진 칸 하나의 인덱스를 고른다.
    /// 간격을 지키는 칸이 없으면 금지 반경 밖 아무 칸. 금지 반경 밖에 칸이 하나도 없으면 -1(플레이어 바로 옆에는 심지 않는다 - 이번 한 그루는 건너뛴다).
    /// </summary>
    public static int PickScatteredIndex(IReadOnlyList<Vector3> _free, IReadOnlyList<Vector3> _occupied, float _minSpacing, Vector3 _player, float _exclusion,
        System.Random _rng, List<int> _scratch)
    {
        if (null == _free || 0 == _free.Count) return -1;

        float exclusionSq = Mathf.Max(0f, _exclusion) * Mathf.Max(0f, _exclusion);
        float spacingSq = Mathf.Max(0f, _minSpacing) * Mathf.Max(0f, _minSpacing);
        int occupiedCount = null != _occupied ? _occupied.Count : 0;

        // 간격 검사에서 떨어진 칸은 두 번째 후보로 같은 목록 뒤쪽에 모은다(할당 없이 한 번만 돈다).
        _scratch.Clear();
        int spaced = 0;

        for (int i = 0; i < _free.Count; i++)
        {
            Vector3 pos = _free[i];
            if (IsoDistanceSq(pos, _player) < exclusionSq) continue;

            bool bSpaced = true;
            if (spacingSq > 0f)
            {
                for (int k = 0; k < occupiedCount; k++)
                {
                    if (IsoDistanceSq(pos, _occupied[k]) >= spacingSq) continue;
                    bSpaced = false;
                    break;
                }
            }

            if (bSpaced)
            {
                _scratch.Add(i);
                // 간격을 지키는 칸은 앞쪽에 모은다.
                int last = _scratch.Count - 1;
                (_scratch[spaced], _scratch[last]) = (_scratch[last], _scratch[spaced]);
                spaced++;
            }
            else
            {
                _scratch.Add(i);
            }
        }

        if (spaced > 0) return _scratch[_rng.Next(spaced)];
        if (_scratch.Count > 0) return _scratch[_rng.Next(_scratch.Count)];
        return -1;
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
    /// 웨이브 시작 초기 나무 수(기획서 v2.5 3-1 "초기 나무"): 현재 임계치 × _ratio(초안 0.5), 내림. T 40이면 20그루이고 웨이브 번호와 무관하다.
    /// 임계치 T보다 항상 낮고 목표 처치 수를 넘지 않는다.
    /// </summary>
    public static int InitialTreeCount(int _threshold, float _ratio, int _killTarget)
    {
        if (_threshold <= 0 || _ratio <= 0f) return 0;

        int count = Mathf.FloorToInt(_threshold * _ratio + 0.0001f);
        count = Mathf.Min(count, _threshold - 1);
        return Mathf.Clamp(count, 0, Mathf.Max(0, _killTarget));
    }

    /// <summary>
    /// 묶음 하나의 크기(기획서 v2.5 3-1 "스폰 속도 · 묶음"). 이월분 _carry에 초당 스폰 × _batchSeconds를 더하고 정수 부분을 돌려준다.
    /// 소수 부분은 _carry에 남아 다음 묶음으로 넘어간다. 정수 부분은 상한에 막혀 다 심지 못해도 돌려받지 않는다(넘치는 몫은 소멸).
    /// </summary>
    public static int TakeBatch(ref float _carry, float _spawnInterval, float _batchSeconds)
    {
        if (_spawnInterval <= 0f || _batchSeconds <= 0f) return 0;

        _carry += _batchSeconds / _spawnInterval;
        int count = Mathf.FloorToInt(_carry + 0.0001f);
        _carry = Mathf.Max(0f, _carry - count);
        return count;
    }
}
