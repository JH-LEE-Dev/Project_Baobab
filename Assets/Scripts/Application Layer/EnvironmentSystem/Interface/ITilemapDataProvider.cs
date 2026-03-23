using UnityEngine;
using System.Collections.Generic;

public interface ITilemapDataProvider
{
    List<Vector3> GetGrassTileWorldPositions();
    List<Vector3> GetWalkableTileWorldPositions();
    Vector3 GetPlayerSpawnPosition();
    Vector3 GetPortalSpawnPosition();
    void SetTreeCollisionTile(Vector3 _worldPos);
    void ClearTreeCollisionTile(Vector3 _worldPos);

    // 길찾기 지원
    bool IsWalkable(Vector3Int _cellPos);
    Vector3Int WorldToCell(Vector3 _worldPos);
    Vector3 CellToWorld(Vector3Int _cellPos);
}
