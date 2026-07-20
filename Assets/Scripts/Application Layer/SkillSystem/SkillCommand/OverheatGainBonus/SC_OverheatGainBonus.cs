using UnityEngine;

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
