using UnityEngine;

// 그림자 판정 타원(EnvironmentInteractionManager.IsUnderShadow)에 참여할 수 있는 오브젝트가 구현한다.
// TreeObj뿐 아니라 건물(Tent, ShopNPC 등)도 이 인터페이스를 구현하면 동일한 방식으로 그림자 판정 대상이 된다.
//
// 판정 타원은 Position을 원점으로 하는 "그림자 회전이 보정된 로컬 좌표계"에서
//   단축(로컬 X) 반경 = TopShadowRadius
//   장축(로컬 Y) 반경 = TopShadowRadius * 장축배율
// 로 만들어지며, 중심은 TopShadowOffset이다.
public interface IShadowCaster
{
    Vector2 Position { get; }
    float TopShadowRadius { get; }
    Vector2 TopShadowOffset { get; }

    // 장축 배율을 이 캐스터가 직접 지정한다. 0 이하를 반환하면 태양 각도에서 유도된 전역 배율을 사용한다.
    //
    // 나무는 위로 솟은 형태라 지면에 길게 늘어진 그림자가 생기므로 전역 배율(0 반환)이 맞지만,
    // 건물의 그림자 스프라이트는 길쭉하지 않고 뭉툭한 덩어리다. 전역 배율(약 3.45배)을 그대로 쓰면
    // 스프라이트를 한참 벗어나는 가늘고 긴 타원이 되어, 그림자에 닿지도 않았는데 어두워지는 문제가 생긴다.
    // 그래서 건물은 스프라이트에 맞는 자체 배율을 돌려준다.
    float ShadowLengthScaleOverride { get; }
}
