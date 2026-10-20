/// <summary>
/// InDungeonObjectManager에 끼워 넣는 "판 단위 나무 규칙". 로그라이크 모드만 구현해서 넣는다.
///
/// [본편 영향 없음] 본편에서는 아무도 SetRunTreeRules를 부르지 않아 규칙이 항상 null이고,
/// 규칙을 보는 분기는 전부 "null이 아닐 때만" 동작한다. 기본 경로의 계산 순서와 값은 그대로다.
/// </summary>
public interface IRunTreeRules
{
    /// <summary>true면 등급 확률을 굴리지 않고 항상 Normal 등급으로 만든다(보석 회생으로 처치 수가 꼬이지 않게).</summary>
    bool bForceNormalGrade { get; }

    /// <summary>나무의 절대 HP. 0 이하면 나무 종류의 기본 HP를 쓴다. 웨이브 구간 표 값 × 언리밋 누적 배율.</summary>
    float TreeHp { get; }

    /// <summary>스폰할 나무 종류. 정하지 않으면 _densityType(지역 분포로 뽑은 종류)을 그대로 돌려준다.</summary>
    TreeType PickTreeType(TreeType _densityType);

    /// <summary>true면 실드·열기 같은 스테이지 고유 기믹을 끈다(기획서 7-1 "스테이지 고유 기믹은 사용하지 않는다").</summary>
    bool bDisableStageGimmicks { get; }

    /// <summary>true면 원목·원석·전리품(구제 드랍 포함)을 떨어뜨리지 않는다. 로그라이크의 보상은 골드뿐이다.</summary>
    bool bSuppressItemDrops { get; }
}
