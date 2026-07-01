using UnityEngine;

[CreateAssetMenu(fileName = "StaminaRecover", menuName = "Game/Skill Command/StaminaRecover")]
public class SC_StaminaRecover : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseStaminaRecoverAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseStaminaRecoverAmount(-amount);
    }
}