using UnityEngine;

/// <summary>
/// 자동 적재 2 - 자동 적재의 전송 범위를 던전 전체로 넓힌다. 어디서든 가방의 원목이 운반 상자로 계속 날아가 담기고,
/// 상자에 자리가 없으면 가방에 남는다. 상자 UI·가방 자동 열림과 가방 <-> 상자 교체는 지금처럼 휴식 범위 안에서만이다.
/// </summary>
[CreateAssetMenu(fileName = "Auto Load Anywhere", menuName = "Game/Skill Command/Auto Load Anywhere")]
public class SC_AutoLoadAnywhere : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.offroadContainerCH.SetAutoLoadAnywhere(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.SetAutoLoadAnywhere(false);
    }
}
