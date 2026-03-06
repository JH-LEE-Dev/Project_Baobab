
public enum SceneType
{
    None,
    MainMenu,
    Town,
    Dungeon,
    MAX,
}

public struct SceneChangeData
{
    public SceneType currentScene;
    public SceneType prevScene;
    public SceneChangeData(SceneType _currentScene, SceneType _prevScene)
    {
        currentScene = _currentScene;
        prevScene = _prevScene;
    }
}