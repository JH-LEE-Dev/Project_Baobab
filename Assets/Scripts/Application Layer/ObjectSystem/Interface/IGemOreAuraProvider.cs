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
/// 원석에 붙는 종류별 아우라를 빌려주는 쪽(GemOreItemController).
/// 원목의 ILogItemAuraProvider와 같은 구조이고, 열쇠만 LogState 대신 GemOreType이다.
/// </summary>
public interface IGemOreAuraProvider
{
    /// <summary>해당 종류의 아우라를 하나 꺼낸다. 등록된 프리팹이 없으면 null.</summary>
    ItemAuraEffectController GetAura(GemOreType _gemOreType);

    /// <summary>다 쓴 아우라를 돌려준다. 꺼낼 때와 같은 종류를 넘겨야 올바른 풀로 반환된다.</summary>
    void ReleaseAura(GemOreType _gemOreType, ItemAuraEffectController _aura);
}
