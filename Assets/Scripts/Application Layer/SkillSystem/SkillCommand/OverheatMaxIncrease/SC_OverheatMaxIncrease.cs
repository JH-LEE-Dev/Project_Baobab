using UnityEngine;

/// <summary>
/// 과열 최대치 증가 - 과열 수치의 최대치가 amount만큼 오른다. 레벨마다 amount가 더해진다.
/// </summary>
[CreateAssetMenu(fileName = "Overheat Max Increase", menuName = "Game/Skill Command/Overheat Max Increase")]
public class SC_OverheatMaxIncrease : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseOverheatMaxValue(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseOverheatMaxValue(-amount);
    }
}
