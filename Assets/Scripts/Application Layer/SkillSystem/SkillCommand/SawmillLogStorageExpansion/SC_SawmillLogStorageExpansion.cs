using UnityEngine;

[CreateAssetMenu(fileName = "Sawmill Log Storage Expansion", menuName = "Game/Skill Command/Sawmill Log Storage Expansion")]
public class SC_SawmillLogStorageExpansion : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.containerCH.ExpandContainerSlotCnt(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.containerCH.ExpandContainerSlotCnt(-amount);
    }
}
