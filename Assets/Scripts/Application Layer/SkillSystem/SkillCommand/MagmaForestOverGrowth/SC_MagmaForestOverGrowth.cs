using UnityEngine;

[CreateAssetMenu(fileName = "Magma Forest OverGrowth", menuName = "Game/Skill Command/Magma Forest OverGrowth")]
public class SC_MagmaForestOverGrowth : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.densityCH.IncreaseTreeDensity(MapType.MagmaForest, amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseTreeDensity(MapType.MagmaForest, -amount);
    }
}
