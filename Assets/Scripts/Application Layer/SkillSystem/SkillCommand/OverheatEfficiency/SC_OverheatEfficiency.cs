using UnityEngine;

/// <summary>
/// 과열 효율 증가 - 과열 수치로 오르는 피해와 도끼 공격속도가 amount(%)만큼 커진다. 레벨마다 amount가 더해진다.
/// </summary>
[CreateAssetMenu(fileName = "Overheat Efficiency", menuName = "Game/Skill Command/Overheat Efficiency")]
public class SC_OverheatEfficiency : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseOverheatEfficiency(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseOverheatEfficiency(-amount);
    }
}
