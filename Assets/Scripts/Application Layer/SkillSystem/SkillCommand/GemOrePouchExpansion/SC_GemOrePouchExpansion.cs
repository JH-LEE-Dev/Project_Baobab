// ─────────────────────────────────────────────────────────────────────────────
// [죽은 코드] 원석 / 용광로 계열.
//
// SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라서 이 특성이 늘리는 주머니 한도는
// 아무 의미가 없다. 원석이 게임에 들어오는 입구가 막혀 있어 담길 원석 자체가 없고,
// HUD도 원석 칸을 숨긴다. 커맨드 배선과 핸들러는 정상이므로 스위치를 다시 켜면 그대로 동작한다.
//
// 버그 / 회귀 검토 대상에서 제외한다. 동작하지 않는 코드라 여기서 나오는 지적은 의미가 없다.
// 되살릴 때는 Assets/Scripts/Global/System.cs 의 그 스위치를 true 로 바꾸면 되고,
// 그 시점에 이 헤더도 같이 지워야 한다.
// ─────────────────────────────────────────────────────────────────────────────
// ─────────────────────────────────────────────────────────────────────────────
// [죽은 코드] 원석 / 용광로 계열.
//
// SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라서 이 파일의 코드는 한 줄도 실행되지
// 않는다. 원석이 게임에 들어오는 입구(InDungeonObjectManager.OnTreeDead)가 막혀 있어
// 원석 아이템이 생성되지 않고, 용광로도 열리지 않는다.
//
// 버그 / 회귀 검토 대상에서 제외한다. 동작하지 않는 코드라 여기서 나오는 지적은 의미가 없다.
// 되살릴 때는 Assets/Scripts/Global/System.cs 의 그 스위치를 true 로 바꾸면 되고,
// 그 시점에 이 헤더들도 같이 지워야 한다.
// ─────────────────────────────────────────────────────────────────────────────

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
