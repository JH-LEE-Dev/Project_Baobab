using UnityEngine;

[CreateAssetMenu(fileName = "Efficient Movement", menuName = "Game/Skill Command/Efficient Movement")]
public class SC_EfficientMovement : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseSpeedWhileAction(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseSpeedWhileAction(-amount);
    }
}
