using UnityEngine;

[CreateAssetMenu(fileName = "Constellation Damage", menuName = "Game/Skill Command/Constellation Damage")]
public class SC_ConstellationDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseConstellationDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseConstellationDamage(-amount);
    }
}
