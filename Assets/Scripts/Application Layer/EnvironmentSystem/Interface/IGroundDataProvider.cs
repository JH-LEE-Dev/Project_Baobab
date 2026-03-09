using UnityEngine;
public interface IGroundDataProvider
{
        
    public GroundPhysicsData GetGroundPhysicsData(Vector3 _position);
}
