using UnityEngine;

[CreateAssetMenu(fileName = "SourceOfStamina", menuName = "Game/Skill Command/SourceOfStamina")]
public class SC_SourceOfStamina : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseSourceOfStaminaRecoverAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseSourceOfStaminaRecoverAmount(-amount);
    }
}

