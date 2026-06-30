using UnityEngine;

[CreateAssetMenu(fileName = "Final Attack", menuName = "Game/Skill Command/Final Attack")]
public class SC_FinalAttack : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.SetFinalAttackHealthPercent(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.SetFinalAttackHealthPercent(1f);
    }
}