/// <summary>
/// 로그라이크 결과 화면에서 메인메뉴로 나왔는지 기억한다. 그때만 메인메뉴가 로그라이크 하위 메뉴를 연 상태로 나타난다
/// (바로 다시 시작하거나 기록을 볼 수 있게).
///
/// RoguelikeSystem이 결과 화면의 메인메뉴 선택으로 이탈을 시작할 때 켜고, BootStrap.SetupMainMenuScene이 메인메뉴를 세우며
/// 한 번 읽고 끈다. 로그라이크 세션은 그사이 해제되므로(GameInstaller.Release) 세션 바깥의 정적 값으로 둔다.
/// GameModeContext는 판정 전용이라 이 용도로 쓰지 않는다.
///
/// [본편 영향 없음] 본편 경로에서는 한 번도 켜지지 않는다. 꺼져 있으면 메인메뉴는 평소처럼 메인 버튼을 보여 준다.
/// </summary>
public static class RoguelikeMainMenuReturn
{
    private static bool bOpenRoguelikeMenu;

    public static void RequestOpenRoguelikeMenu()
    {
        bOpenRoguelikeMenu = true;
    }

    /// <summary>요청이 있었는지 돌려주고 끈다(한 번만).</summary>
    public static bool ConsumeOpenRoguelikeMenu()
    {
        bool bRequested = bOpenRoguelikeMenu;
        bOpenRoguelikeMenu = false;
        return bRequested;
    }
}
