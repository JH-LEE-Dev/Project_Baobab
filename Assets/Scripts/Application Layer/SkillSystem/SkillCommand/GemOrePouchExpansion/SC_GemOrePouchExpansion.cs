using UnityEngine;

/// <summary>
/// 원석 주머니 확장 - 원석을 담을 수 있는 공간을 amount만큼 늘린다.
/// 황금/다이아/프리즘을 합친 총량이 이 한도를 넘지 못한다.
/// </summary>
[CreateAssetMenu(fileName = "Gem Ore Pouch Expansion", menuName = "Game/Skill Command/Gem Ore Pouch Expansion")]
public class SC_GemOrePouchExpansion : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.inventoryCH.IncreaseGemOrePouchCapacity(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.inventoryCH.IncreaseGemOrePouchCapacity(-amount);
    }
}
