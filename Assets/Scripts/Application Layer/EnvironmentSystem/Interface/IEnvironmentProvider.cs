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
    /// _bReachIsStrike면 물결·나무 떨림이 피해 범위 끝에서 멈춘다(범위 밖은 출렁이지 않는다 - 연출 범위 = 피해 범위).
    /// _bHitStop이 false면 터지는 순간의 히트스톱만 건너뛴다(나머지 연출은 같다). _flashScale은 터지는 순간 섬광 세기 배율(1이면 그대로).
    /// </summary>
    public bool PlayQuakeRipple(Vector3 _center, float _strikeRadius = 0f, bool _bReachIsStrike = false, bool _bHitStop = true, float _flashScale = 1f);
    /// <summary>
    /// 지진 바닥 물결의 첫 준비를 _center 둘레에서 숨긴 채 미리 한 번 돌려 둔다(화면이 가려진 원정 시작 시점용).
    /// 게임을 켠 뒤 첫 지진의 시작 프레임들에 몰리던 준비를 옮길 뿐이고 화면·게임 상태는 바뀌지 않는다.
    /// </summary>
    public void PrewarmQuakeRipple(Vector3 _center);
    /// <summary>지진 물결 앞머리 속도(바닥 평면 거리/초 - 세로 차이를 두 배로 편 거리).</summary>
    public float QuakeRippleFrontSpeed { get; }
    /// <summary>시작한 지진 물결이 아직 퍼지기 전(복제층 준비 몇 프레임 + 터지기 전 전조). 이게 끝난 다음 프레임부터 앞머리가 퍼진다.</summary>
    public bool IsQuakeRipplePreparing { get; }
    /// <summary>지진에 맞은 나무(루트 트랜스폼)를 알린다. 지진 물결이 도는 동안이면 그 나무가 위로 한 번 튀어 보인다(그리는 동안만).</summary>
    public void NotifyQuakeStruck(Transform _target);
}
