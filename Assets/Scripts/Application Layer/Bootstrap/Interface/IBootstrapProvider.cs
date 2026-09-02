
public interface IBootStrapProvider
{
    void GoToMainMenuScene();

    void GoToTownScene(bool _bNewGame);

    /// <summary>던전 → 마을 귀환 전용. 새 게임/이어하기 어느 쪽도 아니다. (BootStrap 구현부 주석 참고)</summary>
    void ReturnToTownScene();
    void GoToDungeonFromMainMenu();
    public void GoToOtherScene(MapType _mapType, ForestType _forestType);
}
