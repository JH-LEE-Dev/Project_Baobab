using UnityEngine;

[CreateAssetMenu(fileName = "Star Path Speed Boost", menuName = "Game/Skill Command/Star Path Speed Boost")]
public class SC_StarPathSpeedBoost : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseStarPathSpeedBoost(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseStarPathSpeedBoost(-amount);
    }
}
