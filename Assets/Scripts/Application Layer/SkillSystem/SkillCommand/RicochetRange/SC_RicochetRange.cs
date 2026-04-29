using UnityEngine;

[CreateAssetMenu(fileName = "Ricochet Range", menuName = "Game/Skill Command/Ricochet Range")]
public class SC_RicochetRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRicochetRange(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRicochetRange(-amount);
    }
}
