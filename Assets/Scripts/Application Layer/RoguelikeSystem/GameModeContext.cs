/// <summary>
/// 지금 세션이 로그라이크 모드인지. BootStrap만 켜고 끈다.
///
/// [본편 영향 없음] 본편 경로에서는 이 값이 한 번도 true가 되지 않는다. 메인메뉴에서 로그라이크로
/// 들어갈 때(BootStrap.GoToRoguelikeFromMainMenu)만 켜지고, 메인메뉴로 돌아오는 순간 꺼진다.
/// 본편 코드가 이 값을 보는 곳은 전부 "true일 때만 다르게 동작"하는 가드다.
///
/// [왜 정적 플래그인가] 저장 차단(SaveManager)처럼 GameInstaller 바깥에 사는 객체도 봐야 하고,
/// 본편 생성자·Initialize 시그니처를 바꾸지 않고 가드를 넣기 위해서다. 판정 외의 용도로 쓰지 않는다.
/// </summary>
public static class GameModeContext
{
    public static bool IsRoguelike { get; private set; }

    public static void EnterRoguelike()
    {
        IsRoguelike = true;
    }

    public static void ExitRoguelike()
    {
        IsRoguelike = false;

        // 로그라이크가 본편 전투 코드에 걸어 둔 정적 덮어쓰기·구독을 여기서도 한 번 더 지운다.
        // 정상 경로에서는 RoguelikeSystem.Release가 이미 지웠지만, 해제가 예외로 중간에 끊겨도
        // 본편으로 넘어가기 전(메인메뉴)에 반드시 기본값이 되도록 보장한다.
        RunCombatOverrides.ResetAll();
        RunCombatHooks.ClearAll();
        RoguelikeModalInput.Reset();
    }
}
