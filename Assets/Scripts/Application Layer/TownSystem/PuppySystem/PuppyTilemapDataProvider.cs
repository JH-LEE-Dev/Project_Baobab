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
        int _sizeX = _bounds.xMax - _bounds.xMin;
        int _sizeY = _bounds.yMax - _bounds.yMin;
        if (0 >= _sizeX || 0 >= _sizeY) return;

        // 칸마다 HasTile(= 그 칸의 타일 에셋 != null)을 부르던 것을, 같은 범위(z = 0 한 장)를 타일맵마다 한 번에 읽어 같은 검사를 한다.
        // 블록 배열은 x가 가장 빨리 돈다(index = (x - xMin) + (y - yMin) * sizeX). 비활성 확장 타일맵은 원래도 검사하지 않았다.
        BoundsInt _block = new BoundsInt(_bounds.xMin, _bounds.yMin, 0, _sizeX, _sizeY, 1);
        int _count = _sizeX * _sizeY;
        TileBase[] _waterTiles = ReadBlock(_water, _block, _count, ref waterBlock);
        TileBase[] _groundTiles = ReadBlock(_ground, _block, _count, ref groundBlock);
        TileBase[] _waterColliderTiles = ReadBlock(_townTileManager.WaterColliderTilemap, _block, _count, ref waterColliderBlock);
        TileBase[] _colliderTiles = ReadBlock(_townTileManager.ColliderTilemap, _block, _count, ref colliderBlock);
        TileBase[] _buildingColliderTiles = ReadBlock(_townTileManager.BuildingColliderTilemap, _block, _count, ref buildingColliderBlock);
        TileBase[] _rockColliderTiles = ReadBlock(_townTileManager.RockColliderTilemap, _block, _count, ref rockColliderBlock);

        IReadOnlyList<Tilemap> _expansions = _townTileManager.BuildingColliderExpansionTilemaps;
        int _expansionBlockCount = 0;
        for (int i = 0; i < _expansions.Count; i++)
        {
            Tilemap _expansion = _expansions[i];
            if (null == _expansion || false == _expansion.gameObject.activeSelf) continue;

            if (expansionBlocks.Count <= _expansionBlockCount) expansionBlocks.Add(null);
            TileBase[] _buffer = expansionBlocks[_expansionBlockCount];
            expansionBlocks[_expansionBlockCount] = ReadBlock(_expansion, _block, _count, ref _buffer);
            _expansionBlockCount++;
        }

        for (int _x = _bounds.xMin; _x < _bounds.xMax; _x++)
        {
            for (int _y = _bounds.yMin; _y < _bounds.yMax; _y++)
            {
                int _index = (_x - _bounds.xMin) + (_y - _bounds.yMin) * _sizeX;
                if (null == _waterTiles[_index] || null != _groundTiles[_index]) continue;
                if (true == HasBlockingTile(_index, _waterColliderTiles, _colliderTiles, _buildingColliderTiles, _rockColliderTiles, _expansionBlockCount)) continue;

                // 마을 길찾기 데이터와 같은 칸 좌표계(원점 보정 포함)로 바꿔 담는다.
                Vector3Int _cell = new Vector3Int(_x, _y, 0);
                Vector3 _world = _ground.GetCellCenterWorld(_cell) + new Vector3(0f, _ground.cellSize.y * 0.5f, 0f);
                bridgeCells.Add(baseProvider.WorldToCell(_world));
            }
        }
    }

    // 다리 칸 검사용 타일 블록 버퍼(마을 Grid가 새로 만들어질 때만 채우고, 다음 번에 다시 쓴다).
    private TileBase[] waterBlock;
    private TileBase[] groundBlock;
    private TileBase[] waterColliderBlock;
    private TileBase[] colliderBlock;
    private TileBase[] buildingColliderBlock;
    private TileBase[] rockColliderBlock;
    private readonly List<TileBase[]> expansionBlocks = new List<TileBase[]>();

    // 타일맵이 없으면 null(= 모든 칸 타일 없음)을 돌려준다.
    private static TileBase[] ReadBlock(Tilemap _tilemap, BoundsInt _block, int _count, ref TileBase[] _buffer)
    {
        if (null == _tilemap) return null;

        if (null == _buffer || _buffer.Length < _count) _buffer = new TileBase[_count];
        _tilemap.GetTilesBlockNonAlloc(_block, _buffer);
        return _buffer;
    }

    private bool HasBlockingTile(int _index, TileBase[] _waterCollider, TileBase[] _collider, TileBase[] _buildingCollider, TileBase[] _rockCollider, int _expansionBlockCount)
    {
        if (true == HasTile(_waterCollider, _index)) return true;
        if (true == HasTile(_collider, _index)) return true;
        if (true == HasTile(_buildingCollider, _index)) return true;
        if (true == HasTile(_rockCollider, _index)) return true;

        for (int i = 0; i < _expansionBlockCount; i++)
        {
            if (null != expansionBlocks[i][_index]) return true;
        }

        return false;
    }

    private static bool HasTile(TileBase[] _tiles, int _index)
    {
        return null != _tiles && null != _tiles[_index];
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
