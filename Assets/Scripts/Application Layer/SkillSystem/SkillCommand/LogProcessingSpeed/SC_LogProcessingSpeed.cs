using UnityEngine;

[CreateAssetMenu(fileName = "Log Processing Speed", menuName = "Game/Skill Command/Log Processing Speed")]
public class SC_LogProcessingSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.cutterCH.IncreaseCutSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.cutterCH.IncreaseCutSpeed(-amount);
    }
}
