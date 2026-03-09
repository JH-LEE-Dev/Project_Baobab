using UnityEngine;

public class GroundDataManager : MonoBehaviour, IGroundDataProvider
{
    private GroundPhysicsData dirtPhysicsData;
    private int dirtLayerMask;

    public void Initialize()
    {
        // Dirt 지형의 물리 데이터 초기화 (가속도, 감속도, 최대 속도)
        dirtPhysicsData = new GroundPhysicsData(8f, 8f, 2f);
        dirtLayerMask = LayerMask.GetMask("Dirt");
    }

    public GroundPhysicsData GetGroundPhysicsData(Vector3 _position)
    {
        // Physics2D.OverlapPoint를 사용하여 해당 위치의 레이어 확인
        Collider2D hit = Physics2D.OverlapPoint(_position, dirtLayerMask);

        if (hit != null)
        {
            // 현재는 Dirt만 존재하므로 Dirt 데이터 반환
            return dirtPhysicsData;
        }

        // 레이어가 감지되지 않아도 기본적으로 Dirt 데이터 반환 (현재 Dirt밖에 없으므로)
        return dirtPhysicsData;
    }
}
