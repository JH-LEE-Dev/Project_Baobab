using UnityEngine;

// 드론 치명타 적용 - 드론 레이저(주 대상·연쇄 전이)가 도끼 치명타 확률·배율을 그대로 계승한다(부메랑 치명타 적용과 같은 방식).
[CreateAssetMenu(fileName = "Drone Critical", menuName = "Game/Skill Command/Drone Critical")]
public class SC_DroneCritical : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateDroneCritical(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateDroneCritical(false);
    }
}
