using UnityEngine;

[CreateAssetMenu(fileName = "Overheat Permanent", menuName = "Game/Skill Command/Overheat Permanent")]
public class SC_OverheatPermanent : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateOverheatPermanent(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateOverheatPermanent(false);
    }
}
