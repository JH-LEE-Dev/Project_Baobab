using UnityEngine;
using System.Collections.Generic;

public interface ITilemapDataProvider
{
    List<Vector3> GetGrassTileWorldPositions();
    Vector3 GetPlayerSpawnPosition();
    Vector3 GetPortalSpawnPosition();
    void SetTreeCollisionTile(Vector3 _worldPos);
    void ClearTreeCollisionTile(Vector3 _worldPos);
}
