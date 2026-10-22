using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 강아지 전용 길찾기 데이터. 마을 길찾기 데이터(TownTilemapDataProvider)를 그대로 쓰되, 나무 다리 칸만
/// 걸을 수 있는 칸으로 더한다.
///
/// 다리 밑은 물 그림(WaterTilemap)만 있고 땅 타일이 없으며, 플레이어가 건널 수 있도록 물 충돌 타일만
/// 빠져 있다. 마을 길찾기는 땅 타일이 있는 칸만 걸을 수 있다고 보므로 다리를 건너지 못한다.
/// 운반 NPC가 같은 데이터를 쓰므로 원본은 건드리지 않고 강아지 쪽에서만 감싼다.
/// </summary>
public class PuppyTilemapDataProvider : ITilemapDataProvider
{
    private readonly ITilemapDataProvider baseProvider;
    private readonly HashSet<Vector3Int> bridgeCells = new HashSet<Vector3Int>();

    public PuppyTilemapDataProvider(ITilemapDataProvider _baseProvider)
    {
        baseProvider = _baseProvider;
    }

    /// <summary>
    /// 마을 Grid가 새로 만들어질 때마다 다리 칸을 다시 찾는다. 물 그림은 있는데 물 충돌·땅·다른 충돌 타일이
    /// 없는 칸이 플레이어가 실제로 건너는 다리다.
    /// </summary>
    public void RefreshBridgeCells(TownTileManager _townTileManager)
    {
        bridgeCells.Clear();
        if (null == _townTileManager) return;

        Tilemap _water = _townTileManager.WaterTilemap;
        Tilemap _ground = _townTileManager.GroundTilemap;
        if (null == _water || null == _ground) return;

        BoundsInt _bounds = _water.cellBounds;
        for (int _x = _bounds.xMin; _x < _bounds.xMax; _x++)
        {
            for (int _y = _bounds.yMin; _y < _bounds.yMax; _y++)
            {
                Vector3Int _cell = new Vector3Int(_x, _y, 0);
                if (false == _water.HasTile(_cell) || true == _ground.HasTile(_cell)) continue;
                if (true == HasBlockingTile(_townTileManager, _cell)) continue;

                // 마을 길찾기 데이터와 같은 칸 좌표계(원점 보정 포함)로 바꿔 담는다.
                Vector3 _world = _ground.GetCellCenterWorld(_cell) + new Vector3(0f, _ground.cellSize.y * 0.5f, 0f);
                bridgeCells.Add(baseProvider.WorldToCell(_world));
            }
        }
    }

    private static bool HasBlockingTile(TownTileManager _townTileManager, Vector3Int _cell)
    {
        if (true == HasTile(_townTileManager.WaterColliderTilemap, _cell)) return true;
        if (true == HasTile(_townTileManager.ColliderTilemap, _cell)) return true;
        if (true == HasTile(_townTileManager.BuildingColliderTilemap, _cell)) return true;
        if (true == HasTile(_townTileManager.RockColliderTilemap, _cell)) return true;

        IReadOnlyList<Tilemap> _expansions = _townTileManager.BuildingColliderExpansionTilemaps;
        for (int i = 0; i < _expansions.Count; i++)
        {
            Tilemap _expansion = _expansions[i];
            if (null != _expansion && true == _expansion.gameObject.activeSelf && true == _expansion.HasTile(_cell)) return true;
        }

        return false;
    }

    private static bool HasTile(Tilemap _tilemap, Vector3Int _cell)
    {
        return null != _tilemap && _tilemap.HasTile(_cell);
    }

    public int GridWidth => baseProvider.GridWidth;
    public int GridHeight => baseProvider.GridHeight;

    public bool IsWalkable(Vector3Int _cellPos)
    {
        return baseProvider.IsWalkable(_cellPos) || bridgeCells.Contains(_cellPos);
    }

    public bool IsWaterTile(Vector3Int _cellPos) => baseProvider.IsWaterTile(_cellPos);
    public bool IsGrassTile(Vector3Int _cellPos) => baseProvider.IsGrassTile(_cellPos);
    public bool HasRockDeco(Vector3Int _cellPos) => baseProvider.HasRockDeco(_cellPos);
    public float GetHazardStaminaDrainPerSecond(Vector3Int _cellPos) => baseProvider.GetHazardStaminaDrainPerSecond(_cellPos);
    public float TreeHeatStaminaDamage => baseProvider.TreeHeatStaminaDamage;
    public Vector3Int WorldToCell(Vector3 _worldPos) => baseProvider.WorldToCell(_worldPos);
    public Vector3 CellToWorld(Vector3Int _cellPos) => baseProvider.CellToWorld(_cellPos);

    public List<Vector3> GetGrassTileWorldPositions() => baseProvider.GetGrassTileWorldPositions();
    public List<Vector3> GetDelayedGrassTileWorldPositions() => baseProvider.GetDelayedGrassTileWorldPositions();
    public List<Vector3> GetWalkableTileWorldPositions() => baseProvider.GetWalkableTileWorldPositions();
    public Vector3 GetPlayerSpawnPosition() => baseProvider.GetPlayerSpawnPosition();
    public Vector3 GetPortalSpawnPosition() => baseProvider.GetPortalSpawnPosition();
    public void SetTreeCollisionTile(Vector3 _worldPos) => baseProvider.SetTreeCollisionTile(_worldPos);
    public void ClearTreeCollisionTile(Vector3 _worldPos) => baseProvider.ClearTreeCollisionTile(_worldPos);
    public void BeginTreeCollisionTileBatch() => baseProvider.BeginTreeCollisionTileBatch();
    public void EndTreeCollisionTileBatch() => baseProvider.EndTreeCollisionTileBatch();
    public void BeginTreeSpawnTileBatch() => baseProvider.BeginTreeSpawnTileBatch();
    public void EndTreeSpawnTileBatch() => baseProvider.EndTreeSpawnTileBatch();
    public void ClearDecoTileForTree(Vector3 _worldPos) => baseProvider.ClearDecoTileForTree(_worldPos);
    public void RestoreDecoTileForTree(Vector3 _worldPos) => baseProvider.RestoreDecoTileForTree(_worldPos);
}
