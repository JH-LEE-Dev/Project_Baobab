using UnityEngine;

[CreateAssetMenu(fileName = "Gun Attack Speed", menuName = "Game/Skill Command/Gun Attack Speed")]
public class SC_GunAttackSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRifleAttackSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRifleAttackSpeed(-amount);
    }
}
