using UnityEngine;

/// <summary>
/// 동시 가공 - 제재소가 한 번에 가공하는 원목 수를 amount개 늘린다. 같은 종류·등급의 원목일 때만 묶이고(모자라면 있는 만큼),
/// 벨트 위 그림은 1개다. 묶인 원목은 평가 때 원목마다 따로 값과 특등품 확률이 적용된다.
/// 가공되면 특등품 평가와 같은 연출로 묶인 원목 수 + 특등품 당첨 수만큼의 목재가 커터에서 튀어나온다.
/// </summary>
[CreateAssetMenu(fileName = "Simultaneous Processing", menuName = "Game/Skill Command/Simultaneous Processing")]
public class SC_SimultaneousProcessing : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.logProcessingSystemCH.IncreaseSimultaneousProcessing(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.logProcessingSystemCH.IncreaseSimultaneousProcessing(-amount);
    }
}
