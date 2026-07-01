using UnityEngine;

[CreateAssetMenu(fileName = "WideGreen Forest OverGrowth", menuName = "Game/Skill Command/WideGreen Forest OverGrowth")]
public class SC_WideGreenForestOverGrowth : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.densityCH.IncreaseTreeDensity(MapType.WideGreenForest, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseTreeDensity(MapType.WideGreenForest, -amount);
    }
}
