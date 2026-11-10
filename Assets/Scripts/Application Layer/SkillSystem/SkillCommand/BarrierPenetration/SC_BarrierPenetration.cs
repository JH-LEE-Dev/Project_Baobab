using UnityEngine;

// 배리어 관통력 - 3스테이지 나무 배리어의 데미지 감소율(기본 100%)을 N%p 낮춘다.
[CreateAssetMenu(fileName = "Barrier Penetration", menuName = "Game/Skill Command/Barrier Penetration")]
public class SC_BarrierPenetration : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inDungeonObjManagerCH.IncreaseBarrierPenetration(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inDungeonObjManagerCH.IncreaseBarrierPenetration(-amount);
    }
}
