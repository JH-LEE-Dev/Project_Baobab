using UnityEngine;

[CreateAssetMenu(fileName = "Recovery Power", menuName = "Game/Skill Command/Recovery Power")]
public class SC_RecoveryPower : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseRecoveryPower(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseRecoveryPower(-amount);
    }
}
