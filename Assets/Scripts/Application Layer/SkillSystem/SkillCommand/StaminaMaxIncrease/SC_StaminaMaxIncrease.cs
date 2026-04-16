using UnityEngine;

[CreateAssetMenu(fileName = "Increase Max Stamina", menuName = "Game/Skill Command/Increase Max Stamina")]
public class SC_StaminaMaxIncrease : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseMaxStamina(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseMaxStamina(-amount);
    }
}
