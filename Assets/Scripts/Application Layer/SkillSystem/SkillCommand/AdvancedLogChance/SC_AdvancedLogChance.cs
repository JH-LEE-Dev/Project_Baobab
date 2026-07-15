using UnityEngine;

[CreateAssetMenu(fileName = "Advanced Log Chance", menuName = "Game/Skill Command/Advanced Log Chance")]
public class SC_AdvancedLogChance : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logItemControllerCH.IncreaseDropProb(LogState.Advanced, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logItemControllerCH.IncreaseDropProb(LogState.Advanced, -amount);
    }
}
