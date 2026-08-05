
public interface IBootStrapProvider
{
    void GoToMainMenuScene();

    void GoToTownScene(bool _bNewGame);
    void GoToDungeonFromMainMenu();
    public void GoToOtherScene(MapType _mapType, ForestType _forestType);
}
