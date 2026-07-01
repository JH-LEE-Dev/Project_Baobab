using UnityEngine;

[CreateAssetMenu(fileName = "Topgrade Assessment", menuName = "Game/Skill Command/Topgrade Assessment")]
public class SC_TopgradeAssessment : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logEvaluatorCH.IncreaseTopgradeAssessmentChance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logEvaluatorCH.IncreaseTopgradeAssessmentChance(-amount);
    }
}

