using UnityEngine;

public class GroundDataManager : MonoBehaviour, IGroundDataProvider
{
    private float dirtAcceleration = 35f;
    private float dirtDeceleration = 20f;
    private float dirtMaxSpeed = 2.2f;

    private GroundPhysicsData dirtPhysicsData;
    private int dirtLayerMask;

    public void Initialize()
    {
        RefreshDirtPhysicsData();
        dirtLayerMask = LayerMask.GetMask("Dirt");
    }

    public GroundPhysicsData GetGroundPhysicsData(Vector3 _position)
    {
        Collider2D hit = Physics2D.OverlapPoint(_position, dirtLayerMask);

        if (hit != null)
        {
            return dirtPhysicsData;
        }

        return dirtPhysicsData;
    }

    private void OnValidate()
    {
        RefreshDirtPhysicsData();
    }

    private void RefreshDirtPhysicsData()
    {
        // 정현아 수치 수정했다. (위 값은 풀, 땅 등 Default Ground 마찰력)
        dirtPhysicsData = new GroundPhysicsData(dirtAcceleration, dirtDeceleration, dirtMaxSpeed);
    }
}
