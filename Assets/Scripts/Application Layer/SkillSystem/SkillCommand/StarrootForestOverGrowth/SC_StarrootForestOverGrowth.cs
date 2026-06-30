using UnityEngine;

[CreateAssetMenu(fileName = "Starroot Forest OverGrowth", menuName = "Game/Skill Command/Starroot Forest OverGrowth")]
public class SC_StarrootForestOverGrowth : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.densityCH.IncreaseTreeDensity(MapType.StarrootForest, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseTreeDensity(MapType.StarrootForest, -amount);
    }
}
