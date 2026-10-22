using UnityEngine;

/// <summary>
/// 기본 도끼 휘두름으로만 맞는 대상(로그라이크 스텔라의 별 - 기획서 3-3 "별은 기본적으로 도끼(기본 공격)로만 칠 수 있다").
/// 회전베기가 발동한 휘두름에서도 360도 전체가 아니라 평소 휘두름 각도 안에 있을 때만 맞는다.
/// 나무와 "가장 가까운 대상"을 다투지 않는다 - 휘두름 범위(인디케이터) 안이면 강조되고, 휘두르면 나무 타격과 함께 늘 맞는다(기획서 v2.5 6-1).
/// 본편 대상은 이 인터페이스를 갖지 않아 판정이 예전과 같다.
/// </summary>
public interface IBasicAxeOnlyTarget
{
    /// <summary>도끼 범위 안에 들어와 강조(나무의 아웃라인과 같은 역할)를 켜거나 끈다. 같은 상태로 다시 불려도 아무 일도 하지 않아야 한다.</summary>
    void SetAxeHighlighted(bool _bHighlighted);

    /// <summary>
    /// 도끼 판정 모양의 윗끝(바닥 위치 IStaticCollidable.Position 기준). 판정은 바닥 위치에서 이 점까지 이은 막대에 IStaticCollidable.Radius를
    /// 두른 모양이고, 그 어느 부분이라도 휘두름 범위에 들어오면 맞는다(기획서 v2.8 "범위 안의 별은 무조건 타격"). 떠 있는 그림의 중심을 넣는다.
    /// </summary>
    Vector2 AxeHitTopOffset { get; }
}
