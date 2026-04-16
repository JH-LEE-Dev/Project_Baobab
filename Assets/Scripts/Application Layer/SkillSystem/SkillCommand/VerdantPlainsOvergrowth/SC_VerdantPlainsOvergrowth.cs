using UnityEngine;

[CreateAssetMenu(fileName = "Verdant Plains Overgrowth", menuName = "Game/Skill Command/Verdant Plains Overgrowth")]
public class SC_VerdantPlainsOvergrowth : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseTreeDensity(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseTreeDensity(-amount);
    }
}
