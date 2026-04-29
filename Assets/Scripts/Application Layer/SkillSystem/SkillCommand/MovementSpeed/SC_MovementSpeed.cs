using UnityEngine;

[CreateAssetMenu(fileName = "Movement Speed", menuName = "Game/Skill Command/Movement Speed")]
public class SC_MovementSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseMovementSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseMovementSpeed(-amount);
    }
}
