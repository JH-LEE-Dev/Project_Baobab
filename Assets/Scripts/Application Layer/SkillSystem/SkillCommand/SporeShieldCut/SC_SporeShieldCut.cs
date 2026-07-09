using UnityEngine;

[CreateAssetMenu(fileName = "Spore Shield Cut", menuName = "Game/Skill Command/Spore Shield Cut")]
public class SC_SporeShieldCut : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseShieldDamageMultiplier(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseShieldDamageMultiplier(-amount);
    }
}
