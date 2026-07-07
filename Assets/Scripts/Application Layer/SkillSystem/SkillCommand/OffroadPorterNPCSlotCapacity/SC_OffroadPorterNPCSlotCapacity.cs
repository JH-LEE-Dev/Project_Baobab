using UnityEngine;

[CreateAssetMenu(fileName = "Offroad Porter NPC Slot Capacity", menuName = "Game/Skill Command/Offroad Porter NPC Slot Capacity")]
public class SC_OffroadPorterNPCSlotCapacity : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.townUnitSpawnerCH.IncreaseOffroadPorterNPCSlotCapacity(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.townUnitSpawnerCH.IncreaseOffroadPorterNPCSlotCapacity(-amount);
    }
}
