using UnityEngine;

[CreateAssetMenu(fileName = "Fluffy Forest OverGrowth", menuName = "Game/Skill Command/Fluffy Forest OverGrowth")]
public class SC_FluffyForestOverGrowth : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.densityCH.IncreaseTreeDensity(MapType.FluffySporeForest, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseTreeDensity(MapType.FluffySporeForest, -amount);
    }
}

