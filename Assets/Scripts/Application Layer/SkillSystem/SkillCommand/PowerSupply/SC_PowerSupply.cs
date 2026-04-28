using UnityEngine;

[CreateAssetMenu(fileName = "Power Supply", menuName = "Game/Skill Command/Power Supply")]
public class SC_PowerSupply : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.cutterCH.SetPowerSupply(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.cutterCH.SetPowerSupply(false);
    }
}
