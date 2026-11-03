using UnityEngine;

/// <summary>
/// 불씨 획득량 증가 - 불씨를 주울 때 오르는 과열 수치가 amount(%)만큼 늘어난다. 레벨마다 amount가 더해진다.
/// </summary>
[CreateAssetMenu(fileName = "Overheat Gain Bonus", menuName = "Game/Skill Command/Overheat Gain Bonus")]
public class SC_OverheatGainBonus : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseOverheatGainBonus(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseOverheatGainBonus(-amount);
    }
}
