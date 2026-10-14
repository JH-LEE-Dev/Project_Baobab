
public enum SceneType
{
    None,
    MainMenu,
    Town,
    DungeonScene,
    MAX,

    // 로그라이크 모드 전용 씬. 기존 값(특히 MAX)의 정수값이 바뀌지 않도록 MAX 뒤에 붙였다.
    Roguelike,
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