using UnityEngine;

public interface IEnvironmentProvider
{
    public IShadowDataProvider shadowDataProvider { get; }
    public IGroundDataProvider groundDataProvider { get; }
    public ITilemapDataProvider tilemapDataProvider { get; }
    public IPathfindGridProvider pathfindGridProvider { get; }
    public IDensityProvider densityProvider { get; }

    /// <summary>지진 바닥 물결 연출을 _center에서 시작한다. 던전 맵이 없거나 화면에 바닥이 없으면 false.</summary>
    public bool PlayQuakeRipple(Vector3 _center);
    /// <summary>지진 물결 앞머리 속도(바닥 평면 거리/초 - 세로 차이를 두 배로 편 거리).</summary>
    public float QuakeRippleFrontSpeed { get; }
}
