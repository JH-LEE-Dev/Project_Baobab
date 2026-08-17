using UnityEngine;

[CreateAssetMenu(fileName = "Perfect Log Chance", menuName = "Game/Skill Command/Perfect Log Chance")]
public class SC_PerfectLogChance : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseTreeGradeProb(TreeGrade.Perfect, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseTreeGradeProb(TreeGrade.Perfect, -amount);
    }
}
