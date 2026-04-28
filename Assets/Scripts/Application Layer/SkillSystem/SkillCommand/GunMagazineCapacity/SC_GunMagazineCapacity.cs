using UnityEngine;

[CreateAssetMenu(fileName = "Gun Magazine Capacity", menuName = "Game/Skill Command/Gun Magazine Capacity")]
public class SC_GunMagazineCapacity : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseMagCap((int)amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseMagCap(-(int)amount);
    }
}
