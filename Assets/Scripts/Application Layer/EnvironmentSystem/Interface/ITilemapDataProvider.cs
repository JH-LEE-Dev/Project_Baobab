using UnityEngine;
using System.Collections.Generic;

public interface ITilemapDataProvider
{
    List<Vector3> GetGrassTileWorldPositions();

    // 스폰 지점(플레이어/포탈) 바로 옆이라 진입 직후에는 나무를 심지 않는 칸들.
    // 위 GetGrassTileWorldPositions()에는 들어 있지 않으며, 진입 후 일정 시간이 지나면
    // InDungeonObjectManager가 이 목록을 스폰 후보에 합류시킨다.
    List<Vector3> GetDelayedGrassTileWorldPositions();

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

    // 나무가 들어선 칸의 데코 타일 처리. 데코 배치(ApplyTiles)는 나무 스폰보다 먼저 끝나고
    // 나무 후보 칸은 바위 데코만 걸러내므로, 잔디/그라운드 데코가 깔린 칸에도 나무가 들어선다.
    // Clear가 걷어낸 타일은 보관해 뒀다가 Restore가 같은 칸에 되돌린다 - 그냥 버리면 나무가
    // 자라고 죽기를 반복하는 동안 데코가 영구히 줄어들어 맵이 점점 휑해진다.
    // Restore도 Begin~End 배치 구간에서는 Tilemap 쓰기를 End까지 미룬다.
    void ClearDecoTileForTree(Vector3 _worldPos);
    void RestoreDecoTileForTree(Vector3 _worldPos);

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
