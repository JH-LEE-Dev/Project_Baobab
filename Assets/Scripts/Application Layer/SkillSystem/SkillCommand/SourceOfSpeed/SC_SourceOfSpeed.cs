using UnityEngine;

[CreateAssetMenu(fileName = "SourceOfSpeed", menuName = "Game/Skill Command/SourceOfSpeed")]
public class SC_SourceOfSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseSourceOfSpeedAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseSourceOfSpeedAmount(-amount);
    }
}
