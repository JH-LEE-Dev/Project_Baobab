
using UnityEngine;

public struct DungeonReadySignal
{
    public DungeonData dungeonData;
    public DungeonReadySignal(DungeonData _dungeonData)
    {
        dungeonData = _dungeonData;
    }
}

public struct DungeonStartSignal
{
    public Vector3 characterPos;
    public DungeonStartSignal(Vector3 _characterPos)
    {
        characterPos = _characterPos;
    }
}