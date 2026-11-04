/// <summary>
/// 마을(엔딩 출발) → 엔딩 씬으로 넘겨주는 값. 엔딩 씬에는 GameInstaller가 없어서 정적 값으로 건넨다.
/// </summary>
public static class EndingContext
{
    // 이번 엔딩에서 로그라이크 모드가 처음 해금됐는지. 소풍 장면에서 "로그라이크 모드 해제!" 문구를 띄울지 정한다.
    public static bool bShowRoguelikeUnlockMessage;

    // 엔딩을 마치고 메인메뉴로 돌아가는 중인지. 메인메뉴가 "아무 키나 누르세요" 문구를 엔딩용(계속 하려면…)으로 바꿔 띄운다.
    public static bool bReturningToMainMenu;
}
