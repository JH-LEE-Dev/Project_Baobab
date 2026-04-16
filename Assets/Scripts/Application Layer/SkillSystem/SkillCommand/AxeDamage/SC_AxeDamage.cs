using UnityEngine;

[CreateAssetMenu(fileName = "Axe Damage", menuName = "Game/Skill Command/Axe Damage")]
public class SC_AxeDamage : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeDamage(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeDamage(-amount);
    }
}
