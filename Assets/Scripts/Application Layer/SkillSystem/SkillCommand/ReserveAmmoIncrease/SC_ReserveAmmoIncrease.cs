using UnityEngine;

[CreateAssetMenu(fileName = "Reserve Ammo Increase", menuName = "Game/Skill Command/Reserve Ammo Increase")]
public class SC_ReserveAmmoIncrease : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAmmoCap((int)amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAmmoCap(-(int)amount);
    }
}
