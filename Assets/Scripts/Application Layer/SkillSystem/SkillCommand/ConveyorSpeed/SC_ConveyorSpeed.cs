using UnityEngine;

[CreateAssetMenu(fileName = "Conveyor Speed", menuName = "Game/Skill Command/Conveyor Speed")]
public class SC_ConveyorSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.logProcessingSystemCH.IncreaseConveyorSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logProcessingSystemCH.IncreaseConveyorSpeed(-amount);
    }
}
