using UnityEngine;

/// <summary>
/// 지형별 물리 속성을 정의하는 구조체 (GC 방지를 위해 struct 사용)
/// </summary>
public struct GroundPhysicsData
{
    public float acceleration;  // 가속도
    public float deceleration;  // 감속도 (마찰력)
    public float maxSpeed;      // 최대 속도

    public GroundPhysicsData(float _acceleration, float _deceleration, float _maxSpeed)
    {
        acceleration = _acceleration;
        deceleration = _deceleration;
        maxSpeed = _maxSpeed;
    }
}

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
