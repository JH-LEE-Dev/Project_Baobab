using UnityEngine;

[CreateAssetMenu(fileName = "Gun Reload Speed", menuName = "Game/Skill Command/Gun Reload Speed")]
public class SC_GunReloadSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseReloadSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseReloadSpeed(-amount);
    }
}
