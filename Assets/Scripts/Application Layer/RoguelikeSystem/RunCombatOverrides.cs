/// <summary>
/// 본편 전투 코드에 하드코딩된 상수 중 로그라이크 카드가 바꿔야 하는 값의 덮어쓰기 창구.
///
/// [본편 영향 없음] 모든 값의 기본값은 "덮어쓰지 않음"이고, 읽는 쪽은 기본값일 때 원래 상수를 그대로 쓴다.
/// 로그라이크 세션만 값을 넣고, 세션이 끝나면(RoguelikeSystem.Release) ResetAll로 되돌린다.
/// 메인메뉴를 거치지 않으면 본편에 들어갈 수 없으므로 본편 플레이 중에는 항상 기본값이다.
///
/// 충격파 각도처럼 인스턴스 필드로 이미 열려 있는 값은 여기 두지 않고 생성 직후 그 인스턴스에 직접 넣는다.
/// </summary>
public static class RunCombatOverrides
{
    /// <summary>회전베기 발동에 필요한 휘두름 수. 0 이하면 AttackComponent의 기본 상수(3)를 쓴다.</summary>
    public static int WhirlwindSwingInterval { get; private set; }

    public static void SetWhirlwindSwingInterval(int _interval)
    {
        WhirlwindSwingInterval = _interval;
    }

    public static void ResetAll()
    {
        WhirlwindSwingInterval = 0;
    }
}
