using UnityEngine;

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
