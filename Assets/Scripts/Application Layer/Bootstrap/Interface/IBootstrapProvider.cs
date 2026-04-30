
public interface IBootStrapProvider
{
    void GoToMainMenuScene();

    void GoToTownScene(bool _bNewGame);
    void GoToOtherScene(string _sceneName, MapType _mapType);
}
