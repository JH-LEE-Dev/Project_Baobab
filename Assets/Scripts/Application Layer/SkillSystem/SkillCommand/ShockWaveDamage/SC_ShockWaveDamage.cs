using UnityEngine;

[CreateAssetMenu(fileName = "Shock Wave Damage", menuName = "Game/Skill Command/Shock Wave Damage")]
public class SC_ShockWaveDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseShockWaveDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseShockWaveDamage(-amount);
    }
}
