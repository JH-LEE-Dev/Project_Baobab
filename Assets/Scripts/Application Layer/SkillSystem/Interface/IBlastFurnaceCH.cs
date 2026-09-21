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

/// <summary>
/// 용광로 관련 특성이 건드리는 창구.
///   - IncreaseFurnaceCount : "용광로" 특성. 용광로를 한 대씩 추가한다(황금 -> 다이아 -> 프리즘 순).
///   - IncreaseProcessingSpeed : "용광로 가속" 특성. 가공 속도를 N% 올린다.
/// </summary>
public interface IBlastFurnaceCH
{
    public void IncreaseFurnaceCount(float _amount);
    public void IncreaseProcessingSpeed(float _percent);
}
