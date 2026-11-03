using UnityEngine;

/// <summary>
/// 예열 - 숲에 들어와 움직일 수 있게 된 순간 과열 수치가 amount만큼 오른 채로 시작한다.
/// 불씨가 나오는 숲(4스테이지, "산불 조심"이 있으면 모든 숲)에서만 적용된다. 레벨마다 amount가 더해진다.
/// </summary>
[CreateAssetMenu(fileName = "Preheat", menuName = "Game/Skill Command/Preheat")]
public class SC_Preheat : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreasePreheat(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreasePreheat(-amount);
    }
}
