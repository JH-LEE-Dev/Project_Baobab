using UnityEngine;

[CreateAssetMenu(fileName = "Repair", menuName = "Game/Skill Command/Repair")]
public class SC_Repair : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        // 수리 특성은 숲 입장 시 차량 옆 모루를 해금한다(수치는 쓰지 않는다).
        _system.inDungeonObjManagerCH.SetRepairBoxUnlocked(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.SetRepairBoxUnlocked(false);
    }
}
