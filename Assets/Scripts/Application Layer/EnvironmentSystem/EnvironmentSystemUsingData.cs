using UnityEngine;

public enum WeatherType
{
    Normal,
    Rain,
}

public enum EnvironmentObjType
{
    None,
    Cloud,
    BirdShadow,
}

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

public struct WaterPuddleData
{
    public Vector3 center;
    public float range;

    public WaterPuddleData(Vector3 _center, float _range)
    {
        center = _center;
        range = _range;
    }
}
