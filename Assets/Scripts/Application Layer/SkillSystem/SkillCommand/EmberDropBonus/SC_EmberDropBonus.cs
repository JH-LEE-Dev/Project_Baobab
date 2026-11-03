using UnityEngine;

/// <summary>
/// 불씨 드랍량 증가 - 나무를 벌목했을 때(희귀나무 등급 변화 포함) 떨어지는 불씨 수가 amount(%)만큼 늘어난다.
/// 소수점 아래는 그 확률로 1개 더 떨어진다. 레벨마다 amount가 더해진다.
/// </summary>
[CreateAssetMenu(fileName = "Ember Drop Bonus", menuName = "Game/Skill Command/Ember Drop Bonus")]
public class SC_EmberDropBonus : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseEmberDrop(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseEmberDrop(-amount);
    }
}
