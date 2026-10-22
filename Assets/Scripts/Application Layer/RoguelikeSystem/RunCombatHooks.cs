using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 본편 전투 코드가 "이런 일이 있었다"만 알리는 정적 이벤트. 로그라이크 조합 카드(기획서 v2.5 6-2 조합 7종)와 스텔라 III가 듣는다.
///
/// [본편 영향 없음] 본편 세션에서는 구독자가 없어 호출이 null 검사 한 번으로 끝난다. 알리는 쪽은 게임 상태를 바꾸지 않는다.
/// 구독자는 이 이벤트 안에서 바로 피해를 주지 말고(본편 판정 루프 도중이라 목록이 바뀔 수 있다) 기록만 해 두었다가
/// 자기 Update에서 처리한다. 로그라이크 세션이 끝나면 ClearAll로 구독을 모두 끊는다.
/// </summary>
public static class RunCombatHooks
{
    /// <summary>도끼를 한 번 휘둘렀다(맞혔는지와 무관). 인자는 공격 판정 중심.</summary>
    public static event Action<Vector3> AxeSwungEvent;

    /// <summary>부메랑이 나무를 맞혔다. 인자는 맞은 나무 위치.</summary>
    public static event Action<Vector3> BoomerangHitTreeEvent;

    /// <summary>
    /// 드론 레이저가 나갔다. 첫 인자는 총구 → 주 대상 → 연쇄 대상 순의 레이저 지점(나무 꼭대기), 둘째 인자는 주 대상 → 연쇄 대상 순의
    /// 맞힌 나무 바닥 위치다. 둘 다 재사용 버퍼라 이벤트 안에서만 읽는다.
    /// </summary>
    public static event Action<IReadOnlyList<Vector3>, IReadOnlyList<Vector3>> DroneLaserEvent;

    /// <summary>충격파가 나무를 맞혔다(포자 충격파). 인자는 맞은 나무 위치.</summary>
    public static event Action<Vector3> ShockWaveHitTreeEvent;

    public static bool HasBoomerangHitTreeListener => null != BoomerangHitTreeEvent;

    public static bool HasDroneLaserListener => null != DroneLaserEvent;

    public static bool HasShockWaveHitTreeListener => null != ShockWaveHitTreeEvent;

    public static void NotifyAxeSwung(Vector3 _center)
    {
        AxeSwungEvent?.Invoke(_center);
    }

    public static void NotifyBoomerangHitTree(Vector3 _treePosition)
    {
        BoomerangHitTreeEvent?.Invoke(_treePosition);
    }

    public static void NotifyDroneLaser(IReadOnlyList<Vector3> _points, IReadOnlyList<Vector3> _hitGroundPositions)
    {
        DroneLaserEvent?.Invoke(_points, _hitGroundPositions);
    }

    public static void NotifyShockWaveHitTree(Vector3 _treePosition)
    {
        ShockWaveHitTreeEvent?.Invoke(_treePosition);
    }

    public static void ClearAll()
    {
        AxeSwungEvent = null;
        BoomerangHitTreeEvent = null;
        DroneLaserEvent = null;
        ShockWaveHitTreeEvent = null;
    }
}
