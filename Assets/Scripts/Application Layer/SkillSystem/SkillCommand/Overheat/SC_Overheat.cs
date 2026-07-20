using UnityEngine;

[CreateAssetMenu(fileName = "Overheat", menuName = "Game/Skill Command/Overheat")]
public class SC_Overheat : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateOverheat(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateOverheat(false);
    }
}
