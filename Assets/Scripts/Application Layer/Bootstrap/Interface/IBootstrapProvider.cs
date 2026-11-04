
public interface IBootStrapProvider
{
    void GoToMainMenuScene();

    void GoToTownScene(bool _bNewGame);

    /// <summary>던전 → 마을 귀환 전용. 새 게임/이어하기 어느 쪽도 아니다. (BootStrap 구현부 주석 참고)</summary>
    void ReturnToTownScene();
    void GoToDungeonFromMainMenu();
    public void GoToOtherScene(MapType _mapType, ForestType _forestType);

    /// <summary>메인메뉴 → 로그라이크 모드. 본편 세이브를 읽지도 지우지도 않는다.</summary>
    void GoToRoguelikeFromMainMenu();

    /// <summary>마을 → 엔딩 씬. 메인메뉴로 나갈 때처럼 GameInstaller를 해제하고 넘어간다.</summary>
    void GoToEndingScene();
}
