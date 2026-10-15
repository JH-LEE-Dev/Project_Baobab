using System;

/// <summary>
/// 플레이어 공격(도끼·충격파·부메랑·드론)이 나무에 피해를 주는 구간 표시입니다.
///
/// 나무는 TakeDamage 호출 안에서 바로 쓰러지고 TreeDeadEvent도 그 안에서 동기로 나가므로, 쓰러지는 순간 이 구간이
/// 켜져 있는지로 "플레이어 공격으로 벌목했는지"를 알 수 있습니다. 폭발·별똥별·지속 피해처럼 코루틴으로 나중에
/// 들어오는 피해는 이 구간 밖이라 플레이어 공격으로 치지 않습니다.
/// 지금은 포자 대폭발("포자 포션")이 발동 조건으로 씁니다(InDungeonObjectManager.OnTreeDead).
///
/// 할당 없이 쓰도록 구조체로 둡니다. 피해를 주는 메서드 첫 줄에 이렇게 씁니다.
/// <code>using var _playerAttackScope = PlayerAttackDamageScope.Enter();</code>
/// </summary>
public readonly struct PlayerAttackDamageScope : IDisposable
{
    // 중첩될 수 있다(드론 레이저 → 같은 호출 안의 연쇄 공격). 0보다 크면 구간 안이다.
    private static int depth;

    public static bool IsActive => depth > 0;

    private readonly bool bEntered;

    private PlayerAttackDamageScope(bool _bEntered)
    {
        bEntered = _bEntered;
    }

    public static PlayerAttackDamageScope Enter()
    {
        depth++;
        return new PlayerAttackDamageScope(true);
    }

    public void Dispose()
    {
        if (bEntered && depth > 0) depth--;
    }
}
