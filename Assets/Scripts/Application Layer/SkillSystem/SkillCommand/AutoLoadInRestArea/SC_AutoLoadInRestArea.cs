using UnityEngine;

/// <summary>
/// 자동 적재 - 던전에서 휴식 범위 안에 있으면 가방의 원목이 운반 상자로 계속 알아서 담긴다.
/// 상자가 가득 찼을 때의 교체는 지금처럼 수동(교체 키)이다.
/// </summary>
[CreateAssetMenu(fileName = "Auto Load In Rest Area", menuName = "Game/Skill Command/Auto Load In Rest Area")]
public class SC_AutoLoadInRestArea : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.offroadContainerCH.SetAutoLoadInRestArea(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.offroadContainerCH.SetAutoLoadInRestArea(false);
    }
}
