using UnityEngine;

[CreateAssetMenu(fileName = "Shield Explosion Damage", menuName = "Game/Skill Command/Shield Explosion Damage")]
public class SC_ShieldExplosionDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseShieldExplosionDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseShieldExplosionDamage(-amount);
    }
}
