using UnityEngine;

[CreateAssetMenu(fileName = "Tree Vitamin", menuName = "Game/Skill Command/Tree Vitamin")]
public class SC_TreeVitamin : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseGrowthSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseGrowthSpeed(-amount);
    }
}
