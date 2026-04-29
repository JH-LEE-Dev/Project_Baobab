using UnityEngine;

[CreateAssetMenu(fileName = "Stamina Consumption Reduction", menuName = "Game/Skill Command/Stamina")]
public class SC_Stamina : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.StaminaDecreaseAlpha(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.StaminaDecreaseAlpha(-amount);
    }
}
