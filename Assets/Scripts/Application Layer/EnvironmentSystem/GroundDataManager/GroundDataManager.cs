using UnityEngine;

public class GroundDataManager : MonoBehaviour, IGroundDataProvider
{
    private float dirtAcceleration = 35f;
    private float dirtDeceleration = 20f;
    private float dirtMaxSpeed = 2.2f;

    private GroundPhysicsData dirtPhysicsData;

    public void Initialize()
    {
        RefreshDirtPhysicsData();
    }

    /// <summary>
    /// 지형별 물리 계수를 돌려준다. 지금은 지형에 관계없이 항상 같은 값이다.
    ///
    /// 예전엔 여기서 "Dirt" 레이어를 Physics2D.OverlapPoint로 찍어봤지만, 흙 전용 물리 데이터
    /// 세트가 끝내 만들어지지 않아 맞든 틀리든 같은 값을 돌려주고 있었다(아래 주석 참고).
    /// 그런데 이 메서드는 Character.FixedUpdate에서 매 물리 스텝마다 불리므로, 결과를 버리는
    /// 물리 쿼리가 초당 50회씩 돌고 있었다. 분기가 없는 지금은 쿼리도 필요 없다.
    ///
    /// 지형별로 실제로 다르게 만들 때는 흙용 GroundPhysicsData를 하나 더 두고,
    /// 그때 레이어 판정(LayerMask.GetMask("Dirt") + OverlapPoint)을 다시 넣으면 된다.
    /// </summary>
    public GroundPhysicsData GetGroundPhysicsData(Vector3 _position)
    {
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
