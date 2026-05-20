using UnityEngine;

[CreateAssetMenu(fileName = "Wooden Transport Box", menuName = "Game/Skill Command/Wooden Transport Box")]
public class SC_WoodenTransportBox : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.ExpandInventorySlotCnt(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.ExpandInventorySlotCnt(-amount);
    }
}
