using UnityEngine;

[CreateAssetMenu(fileName = "Stamina Recovery Boost", menuName = "Game/Skill Command/Stamina Recovery Boost")]
public class SC_StaminaRecoveryBoost : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.StaminaIncreaseAlpha(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.StaminaIncreaseAlpha(-amount);
    }
}
