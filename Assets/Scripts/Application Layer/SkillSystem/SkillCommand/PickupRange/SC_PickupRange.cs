using UnityEngine;

[CreateAssetMenu(fileName = "Pickup Range", menuName = "Game/Skill Command/Pickup Range")]
public class SC_PickupRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreasePickupRange(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreasePickupRange(-amount);
    }
}
