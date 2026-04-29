using UnityEngine;

[CreateAssetMenu(fileName = "Inventory Expansion", menuName = "Game/Skill Command/Inventory Expansion")]
public class SC_InventoryExpansion : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inventoryCH.ExpandInventorySlotCnt(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inventoryCH.ExpandInventorySlotCnt(-amount);
    }
}
