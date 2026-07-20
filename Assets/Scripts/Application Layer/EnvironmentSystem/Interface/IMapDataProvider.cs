using UnityEngine;

public interface IMapDataProvider
{
    public MapEnvironmentDatabase GetMapEnvironmentDatabase();
    public void MarkUnlocked(MapType mapType, ForestType forestType);
    public void MarkUnlockAnimationPlayed(MapType mapType, ForestType forestType);
    public void MarkMapAsRead(MapType mapType, ForestType forestType);

    public void MarkMapUnlocked(MapType mapType);
    public void MarkMapUnlockAnimationPlayed(MapType mapType);
    public void MarkMapLevelAsRead(MapType mapType);
}
