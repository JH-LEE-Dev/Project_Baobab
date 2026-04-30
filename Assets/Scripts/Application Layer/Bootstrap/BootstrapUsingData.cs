
public enum SceneType
{
    None,
    MainMenu,
    Town,
    DungeonScene,
    MAX,
}

public struct SceneChangeData
{
    public SceneType currentScene;
    public SceneType prevScene;
    public MapType mapType;
    public SceneChangeData(SceneType _currentScene, SceneType _prevScene, MapType _mapType)
    {
        currentScene = _currentScene;
        prevScene = _prevScene;
        mapType = _mapType;
    }
}