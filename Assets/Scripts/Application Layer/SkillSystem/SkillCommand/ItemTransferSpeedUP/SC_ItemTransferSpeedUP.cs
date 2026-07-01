using UnityEngine;

[CreateAssetMenu(fileName = "Item Transfer Speed Up", menuName = "Game/Skill Command/Item Transfer Speed Up")]
public class SC_ItemTransferSpeedUp : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.offroadContainerCH.ItemTransferSpeedUP(amount);
        _system.containerCH.ItemTransferSpeedUP(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.ItemTransferSpeedUP(-amount);
        _system.containerCH.ItemTransferSpeedUP(-amount);
    }
}

