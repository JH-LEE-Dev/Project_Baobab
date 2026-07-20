using UnityEngine;

[CreateAssetMenu(fileName = "Remote Deposit", menuName = "Game/Skill Command/Remote Deposit")]
public class SC_RemoteDeposit : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logProcessingSystemCH.SetRemoteDeposit(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logProcessingSystemCH.SetRemoteDeposit(false);
    }
}
