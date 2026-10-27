using UnityEngine;

/// <summary>
/// 원목 보험 증서 - 탈진으로 유실되는 원목 중 amount(%)만큼을 운반 상자에 담는다.
/// 랜덤이 아니라 가방 슬롯마다 그 비율만큼이며, 가치가 높은 원목부터 상자에 자리가 나는 만큼 담긴다.
/// 레벨마다 amount가 더해진다(합계는 100%에서 막힌다).
/// </summary>
[CreateAssetMenu(fileName = "Log Insurance", menuName = "Game/Skill Command/Log Insurance")]
public class SC_LogInsurance : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.offroadContainerCH.IncreaseLogInsurancePercent(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.IncreaseLogInsurancePercent(-amount);
    }
}
