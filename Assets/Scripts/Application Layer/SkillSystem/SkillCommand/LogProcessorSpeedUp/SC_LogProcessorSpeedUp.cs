using UnityEngine;

[CreateAssetMenu(fileName = "LogProcessor SpeedUp", menuName = "Game/Skill Command/LogProcessor SpeedUp")]
public class SC_LogProcessorSpeedUp : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logProcessingSystemCH.LogProcessorSpeedUp(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logProcessingSystemCH.LogProcessorSpeedUp(-amount);
    }
}


