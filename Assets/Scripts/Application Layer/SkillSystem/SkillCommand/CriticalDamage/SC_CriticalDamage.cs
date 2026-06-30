using UnityEngine;

[CreateAssetMenu(fileName = "Critical Damage", menuName = "Game/Skill Command/Critical Damage")]
public class SC_CriticalDamage : SkillCommand
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