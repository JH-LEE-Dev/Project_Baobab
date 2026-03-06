
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
