using UnityEngine;

[CreateAssetMenu(fileName = "Increase JackPot Chance", menuName = "Game/Skill Command/Increase JackPot Chance")]
public class SC_IncreaseJackPotChance: SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logItemControllerCH.IncreaseJackPotChance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logItemControllerCH.IncreaseJackPotChance(-amount);
    }
}
