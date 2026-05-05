
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
    public ForestType forestType;
    public MapType mapType;
    public SceneChangeData(SceneType _currentScene, SceneType _prevScene, ForestType _forestType, MapType _mapType)
    {
        currentScene = _currentScene;
        prevScene = _prevScene;
        mapType = _mapType;
        forestType = _forestType;
    }
}