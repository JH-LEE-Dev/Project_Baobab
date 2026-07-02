using UnityEngine;

[CreateAssetMenu(fileName = "OffroadContainer Range Increase", menuName = "Game/Skill Command/OffroadContainer Range Increase")]
public class SC_OffroadContainerRangeIncrease : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.offroadContainerCH.ColliderRangeIncrease(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.ColliderRangeIncrease(-amount);
    }
}


