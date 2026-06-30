using UnityEngine;


[CreateAssetMenu(fileName = "Increase JackPot Amount", menuName = "Game/Skill Command/Increase JackPot Amount")]
public class SC_IncreaseJackPotAmount: SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logItemControllerCH.IncreaseJackPotAmount(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logItemControllerCH.IncreaseJackPotAmount(-amount);
    }
}
