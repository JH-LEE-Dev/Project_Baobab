using UnityEngine;

[CreateAssetMenu(fileName = "Ricochet Damage", menuName = "Game/Skill Command/Ricochet Damage")]
public class SC_RicochetDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseRicochetDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRicochetDamage(-amount);
    }
}
