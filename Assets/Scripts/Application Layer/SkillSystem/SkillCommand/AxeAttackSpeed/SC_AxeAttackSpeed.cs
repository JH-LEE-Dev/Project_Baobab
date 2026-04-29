using UnityEngine;

[CreateAssetMenu(fileName = "Axe Attack Speed", menuName = "Game/Skill Command/Axe Attack Speed")]
public class SC_AxeAttackSpeed : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseAxeAttackSpeed(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeAttackSpeed(-amount);
    }
}
