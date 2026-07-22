using UnityEngine;

[CreateAssetMenu(fileName = "Overheat Consumption Reduction", menuName = "Game/Skill Command/Overheat Consumption Reduction")]
public class SC_OverheatConsumptionReduction : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseOverheatConsumptionReduction(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseOverheatConsumptionReduction(-amount);
    }
}
