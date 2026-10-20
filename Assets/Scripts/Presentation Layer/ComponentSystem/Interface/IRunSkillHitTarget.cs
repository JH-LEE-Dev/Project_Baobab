/// <summary>
/// 도끼 외의 공격(충격파·부메랑·드론 레이저·회전베기 360도)에도 맞을 수 있는 판 단위 대상(로그라이크 스텔라의 별 - 기획서 6-1 스텔라 III
/// "다른 액티브 스킬도 별을 공격할 수 있다"). 나무 충돌 레이어에 있어 각 스킬의 탐색에 걸리지만, 나무가 아니므로 피해 대신 맞았다고만 알린다.
///
/// [본편 영향 없음] 본편 대상은 이 인터페이스를 갖지 않고, RunCombatOverrides.bSkillsHitRunTargets도 본편에서는 항상 false다.
/// </summary>
public interface IRunSkillHitTarget
{
    /// <summary>지금 맞을 수 있는지(스텔라+드론의 별처럼 공격받지 않는 별은 false).</summary>
    bool bCanBeHitBySkill { get; }

    /// <summary>도끼가 아닌 공격에 맞았다.</summary>
    void NotifySkillHit();
}
