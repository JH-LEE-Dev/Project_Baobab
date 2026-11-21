using UnityEngine;

[CreateAssetMenu(fileName = "Increase Repair Efficiency", menuName = "Game/Skill Command/Increase Repair Efficiency")]
public class SC_IncreaseRepairEfficiency: SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        // 수리 효율: 모루의 기본 초당 수리량을 amount%만큼 늘린다(amount 50 = 1.5배).
        _system.inDungeonObjManagerCH.IncreaseRepairRatePercent(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseRepairRatePercent(-amount);
    }
}