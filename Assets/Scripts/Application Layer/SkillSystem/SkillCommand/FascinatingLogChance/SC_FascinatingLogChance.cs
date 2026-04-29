using UnityEngine;

[CreateAssetMenu(fileName = "Fascinating Log Chance", menuName = "Game/Skill Command/Fascinating Log Chance")]
public class SC_FascinatingLogChance : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logItemCH.IncreaseDropProb(LogState.Fascinating, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logItemCH.IncreaseDropProb(LogState.Fascinating, -amount);
    }
}
