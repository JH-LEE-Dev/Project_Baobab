using UnityEngine;

[CreateAssetMenu(fileName = "Log Value", menuName = "Game/Skill Command/Log Value")]
public class SC_LogValue : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logEvaluatorCH.IncreaseLogValueMultiplier(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logEvaluatorCH.IncreaseLogValueMultiplier(-amount);
    }
}
