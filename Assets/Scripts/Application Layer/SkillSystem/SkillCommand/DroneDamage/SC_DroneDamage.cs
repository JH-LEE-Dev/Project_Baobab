using UnityEngine;

[CreateAssetMenu(fileName = "Drone Damage", menuName = "Game/Skill Command/Drone Damage")]
public class SC_DroneDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseDroneDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseDroneDamage(-amount);
    }
}
