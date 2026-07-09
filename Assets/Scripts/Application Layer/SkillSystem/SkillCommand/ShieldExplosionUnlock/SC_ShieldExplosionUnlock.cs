using UnityEngine;

[CreateAssetMenu(fileName = "Shield Explosion Unlock", menuName = "Game/Skill Command/Shield Explosion Unlock")]
public class SC_ShieldExplosionUnlock : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.UnlockShieldExplosion(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.UnlockShieldExplosion(false);
    }
}
