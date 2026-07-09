using UnityEngine;

[CreateAssetMenu(fileName = "Spore Shield Regen Block", menuName = "Game/Skill Command/Spore Shield Regen Block")]
public class SC_SporeShieldRegenBlock : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseShieldRegenReduction(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseShieldRegenReduction(-amount);
    }
}
