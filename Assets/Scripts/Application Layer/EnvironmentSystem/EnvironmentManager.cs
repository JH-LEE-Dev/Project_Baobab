using UnityEngine;

public class EnvironmentManager : MonoBehaviour, IEnvironmentProvider
{
    //외부 의존성
    private IsometricShadowController isometricShadowController;

    //내부 의존성 (물리 데이터 캐싱)
    private GroundPhysicsData dirtPhysicsData;
    private int dirtLayerMask;

    public IShadowDataProvider shadowDataProvider => isometricShadowController;

    public void Initialize()
    {
        isometricShadowController = GetComponentInChildren<IsometricShadowController>();
        
        // Dirt 지형의 물리 데이터 초기화 (가속도, 감속도, 최대 속도)
        dirtPhysicsData = new GroundPhysicsData(8f, 8f, 2f);
        dirtLayerMask = LayerMask.GetMask("Dirt");
    }

    /// <summary>
    /// 특정 위치의 레이어를 체크하여 지형 물리 데이터를 반환합니다.
    /// </summary>
    /// <param name="_position">체크할 위치</param>
    /// <returns>해당 위치의 물리 데이터 (기본값은 Dirt)</returns>
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
