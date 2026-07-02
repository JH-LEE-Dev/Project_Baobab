using UnityEngine;

[CreateAssetMenu(fileName = "Repair", menuName = "Game/Skill Command/Repair")]
public class SC_Repair : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseRepairBoxCount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseRepairBoxCount(-amount);
    }
}
