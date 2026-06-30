using UnityEngine;

[CreateAssetMenu(fileName = "Critical Chance", menuName = "Game/Skill Command/Critical Chance")]
public class SC_CriticalChance : SkillCommand
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