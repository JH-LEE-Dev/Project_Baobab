using UnityEngine;

[CreateAssetMenu(fileName = "Equipment Switch Speed", menuName = "Game/Skill Command/Equipment Switch Speed")]
public class SC_EquipmentSwitchSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseSwitchSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseSwitchSpeed(-amount);
    }
}
