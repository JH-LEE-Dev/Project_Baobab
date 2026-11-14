using UnityEngine;

[CreateAssetMenu(fileName = "Increase Repair Efficiency", menuName = "Game/Skill Command/Increase Repair Efficiency")]
public class SC_IncreaseRepairEfficiency: SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        // 수리 효율: 모루의 초당 수리량에 최대 내구도의 amount%를 더한다(amount 1 = 1%/s).
        _system.inDungeonObjManagerCH.IncreaseRepairRatePercent(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseRepairRatePercent(-amount);
    }
}