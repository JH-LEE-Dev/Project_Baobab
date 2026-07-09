using UnityEngine;

[CreateAssetMenu(fileName = "Star Mark Damage", menuName = "Game/Skill Command/Star Mark Damage")]
public class SC_StarMarkDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseStarMarkDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseStarMarkDamage(-amount);
    }
}
