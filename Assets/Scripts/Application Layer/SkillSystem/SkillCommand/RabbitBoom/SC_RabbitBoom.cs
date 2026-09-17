// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  당근 아이템 / 토끼 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: 호출부가 없고 커맨드 2종(CarrotBundle / RabbitBoom)도 미배선입니다.
//
// ⚠ 혼동 주의: 여기서 말하는 "당근"은 던전에 떨어지는 당근 아이템과 토끼 기능입니다.
//    스킬 비용 통화인 carrot(InventoryManager.carrot / SaveData.carrot /
//    IMoneyData.carrot / SkillManager)은 이 기능과 무관하게 계속 쓰입니다.
//    같이 지우면 세이브 포맷이 깨집니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: ICarrotItemCH가 ICommandHandleSystem과 SkillDispatcher에
//   남아 있고, 위 통화 carrot과 이름이 겹쳐 일괄 삭제가 위험합니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
using UnityEngine;

[CreateAssetMenu(fileName = "Rabbit Boom", menuName = "Game/Skill Command/Rabbit Boom")]
public class SC_RabbitBoom : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        PrintDebug();
        _system.densityCH.IncreaseRabbitDensity(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseRabbitDensity(-amount);
    }
}
