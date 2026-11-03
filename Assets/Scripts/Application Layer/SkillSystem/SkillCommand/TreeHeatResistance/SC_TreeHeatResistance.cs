using UnityEngine;

/// <summary>
/// 열기 내성 강화 - 나무가 내뿜는 열기의 피로도 피해와 기절(넉백 경직) 시간이 amount(%)만큼 줄어든다.
/// 레벨마다 amount가 더해진다(합계는 100%에서 막힌다).
/// </summary>
[CreateAssetMenu(fileName = "Tree Heat Resistance", menuName = "Game/Skill Command/Tree Heat Resistance")]
public class SC_TreeHeatResistance : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.characterStatCH.IncreaseTreeHeatResistance(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.characterStatCH.IncreaseTreeHeatResistance(-amount);
    }
}
