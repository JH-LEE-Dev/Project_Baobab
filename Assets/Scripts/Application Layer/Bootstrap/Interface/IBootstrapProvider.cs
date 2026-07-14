
public interface IBootStrapProvider
{
    void GoToMainMenuScene();

    void GoToTownScene(bool _bNewGame);
    public void GoToOtherScene(MapType _mapType, ForestType _forestType);
}
