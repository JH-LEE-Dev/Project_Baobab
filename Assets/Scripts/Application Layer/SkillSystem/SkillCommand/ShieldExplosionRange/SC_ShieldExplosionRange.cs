using UnityEngine;

[CreateAssetMenu(fileName = "Shield Explosion Range", menuName = "Game/Skill Command/Shield Explosion Range")]
public class SC_ShieldExplosionRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseShieldExplosionRange(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseShieldExplosionRange(-amount);
    }
}
