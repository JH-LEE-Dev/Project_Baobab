using UnityEngine;

[CreateAssetMenu(fileName = "LogCapacity Increase", menuName = "Game/Skill Command/LogCapacity Increase")]
public class SC_LogCapacityIncrease : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inventoryCH.LogCapacityIncrease(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inventoryCH.LogCapacityIncrease(-amount);
    }
}
