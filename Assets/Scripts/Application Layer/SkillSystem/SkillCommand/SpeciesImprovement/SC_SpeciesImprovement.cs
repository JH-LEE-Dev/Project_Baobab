using UnityEngine;

/// <summary>
/// 수종 개량 - 획득하는 모든 원목이 해당 지역에서 가장 가치가 높은 수종으로 변환된다.
/// (예: 1-3에서 소나무 원목을 먹으면 인벤토리에는 자작나무 원목으로 들어온다)
/// </summary>
[CreateAssetMenu(fileName = "Species Improvement", menuName = "Game/Skill Command/Species Improvement")]
public class SC_SpeciesImprovement : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logItemControllerCH.SetSpeciesImprovement(true);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logItemControllerCH.SetSpeciesImprovement(false);
    }
}
