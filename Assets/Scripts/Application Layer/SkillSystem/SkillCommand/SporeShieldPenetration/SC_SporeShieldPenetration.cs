using UnityEngine;

[CreateAssetMenu(fileName = "Spore Shield Penetration", menuName = "Game/Skill Command/Spore Shield Penetration")]
public class SC_SporeShieldPenetration : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseShieldPenetration(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseShieldPenetration(-amount);
    }
}
