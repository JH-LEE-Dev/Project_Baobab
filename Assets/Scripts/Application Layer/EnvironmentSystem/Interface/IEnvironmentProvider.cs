using UnityEngine;

public interface IEnvironmentProvider
{
    public IShadowDataProvider shadowDataProvider { get; }
    
    /// <summary>
    /// 특정 위치의 지형 물리 데이터를 반환합니다.
    /// </summary>
    /// <param name="_position">체크할 위치</param>
    /// <returns>지형 물리 데이터</returns>
    public GroundPhysicsData GetGroundPhysicsData(Vector3 _position);
}
