using UnityEngine;

[CreateAssetMenu(fileName = "Gun Damage", menuName = "Game/Skill Command/Gun Damage")]
public class SC_GunDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseGunDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseGunDamage(-amount);
    }
}
