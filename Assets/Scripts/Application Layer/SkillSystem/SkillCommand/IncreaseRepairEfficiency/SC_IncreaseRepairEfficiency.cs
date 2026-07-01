using UnityEngine;

[CreateAssetMenu(fileName = "Increase Repair Efficiency", menuName = "Game/Skill Command/Increase Repair Efficiency")]
public class SC_IncreaseRepairEfficiency: SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseRepairAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseRepairAmount(-amount);
    }
}