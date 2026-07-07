using UnityEngine;

[CreateAssetMenu(fileName = "Boomerang Damage", menuName = "Game/Skill Command/Boomerang Damage")]
public class SC_BoomerangDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseBoomerangDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseBoomerangDamage(-amount);
    }
}
