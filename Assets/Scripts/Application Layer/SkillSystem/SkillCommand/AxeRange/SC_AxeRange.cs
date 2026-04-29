using UnityEngine;

[CreateAssetMenu(fileName = "Axe Range", menuName = "Game/Skill Command/Axe Range")]
public class SC_AxeRange : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseAxeRangeMultiplier(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseAxeRangeMultiplier(-amount);
    }
}
