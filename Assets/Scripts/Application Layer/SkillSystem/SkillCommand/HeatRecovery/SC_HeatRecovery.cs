using UnityEngine;

[CreateAssetMenu(fileName = "Heat Recovery", menuName = "Game/Skill Command/Heat Recovery")]
public class SC_HeatRecovery : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseHeatRecoveryAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseHeatRecoveryAmount(-amount);
    }
}
