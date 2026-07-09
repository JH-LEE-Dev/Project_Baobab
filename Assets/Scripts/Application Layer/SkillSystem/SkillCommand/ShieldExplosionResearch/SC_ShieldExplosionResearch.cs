using UnityEngine;

[CreateAssetMenu(fileName = "Shield Explosion Research", menuName = "Game/Skill Command/Shield Explosion Research")]
public class SC_ShieldExplosionResearch : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseShieldExplosionResearchChance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseShieldExplosionResearchChance(-amount);
    }
}
