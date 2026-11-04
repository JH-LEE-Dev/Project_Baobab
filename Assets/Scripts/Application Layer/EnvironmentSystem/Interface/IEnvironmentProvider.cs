using UnityEngine;

public interface IEnvironmentProvider
{
    public IShadowDataProvider shadowDataProvider { get; }
    public IGroundDataProvider groundDataProvider { get; }
    public ITilemapDataProvider tilemapDataProvider { get; }
    public IPathfindGridProvider pathfindGridProvider { get; }
    public IDensityProvider densityProvider { get; }

    /// <summary>
    /// 지진 바닥 물결 연출을 _center에서 시작한다. 던전 맵이 없거나 화면에 바닥이 없으면 false.
    /// _strikeRadius(바닥 평면 거리)를 넘기면 그 피해 범위 안이 크게 출렁이고 안쪽 나무가 튄다(0이면 범위 표시 없음).
    /// </summary>
    public bool PlayQuakeRipple(Vector3 _center, float _strikeRadius = 0f);
    /// <summary>지진 물결 앞머리 속도(바닥 평면 거리/초 - 세로 차이를 두 배로 편 거리).</summary>
    public float QuakeRippleFrontSpeed { get; }
    /// <summary>시작한 지진 물결이 아직 준비 중(몇 프레임)이라 퍼지기 전. 이게 끝난 다음 프레임부터 앞머리가 퍼진다.</summary>
    public bool IsQuakeRipplePreparing { get; }
    /// <summary>지진에 맞은 나무(루트 트랜스폼)를 알린다. 지진 물결이 도는 동안이면 그 나무가 위로 한 번 튀어 보인다(그리는 동안만).</summary>
    public void NotifyQuakeStruck(Transform _target);
}
