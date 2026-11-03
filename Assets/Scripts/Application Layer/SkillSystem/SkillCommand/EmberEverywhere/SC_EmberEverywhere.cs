using UnityEngine;

/// <summary>
/// 산불 조심 - 4스테이지가 아닌 숲에서도 불씨가 나와 과열 수치를 올릴 수 있다.
/// 나무 열기 같은 4스테이지의 다른 맵 규칙은 따라오지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "Ember Everywhere", menuName = "Game/Skill Command/Ember Everywhere")]
public class SC_EmberEverywhere : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.ActivateEmberEverywhere(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.ActivateEmberEverywhere(false);
    }
}
