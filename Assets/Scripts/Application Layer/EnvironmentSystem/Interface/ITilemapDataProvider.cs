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

    // 나무를 한꺼번에 정리할 때(던전 전환 등) 쓰는 배치 구간. Begin~End 사이의
    // ClearTreeCollisionTile은 Tilemap 쓰기만 미뤄뒀다가 End에서 한 번에 반영한다.
    // cellToIndex/walkablePositions 같은 부기는 배치 여부와 무관하게 즉시 반영되므로,
    // 호출하는 쪽에서 보이는 상태는 배치를 쓰든 안 쓰든 동일하다.
    void BeginTreeCollisionTileBatch();
    void EndTreeCollisionTileBatch();

    // 길찾기 지원
    int GridWidth { get; }
    int GridHeight { get; }
    bool IsWalkable(Vector3Int _cellPos);
    bool IsWaterTile(Vector3Int _cellPos);
    bool IsGrassTile(Vector3Int _cellPos);
    bool HasRockDeco(Vector3Int _cellPos);
    float GetHazardStaminaDrainPerSecond(Vector3Int _cellPos);
    float TreeHeatStaminaDamage { get; }
    Vector3Int WorldToCell(Vector3 _worldPos);
    Vector3 CellToWorld(Vector3Int _cellPos);
}
