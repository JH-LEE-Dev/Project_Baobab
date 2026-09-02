using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// TownTileManager가 들고 있는 고정(정적) 타일맵을 바탕으로 PathFindComponent가 요구하는
/// ITilemapDataProvider를 구현한다. 절차적 던전(TileMapGenerator)과 달리 노이즈로 미리 계산된
/// 배열이 아니라, 이미 배치돼 있는 타일을 그때그때 직접 조회(HasTile)해서 판정한다.
///
/// 길찾기 가능 타일은 GroundTilemap에 타일이 있는 칸뿐이며, ColliderTilemap/BuildingColliderTilemap/
/// WaterColliderTilemap/RockColliderTilemap 중 하나라도 타일이 있으면 이동 불가로 취급한다.
/// 제재소 증설분(BuildingColliderTilemap_1/_2)은 실제로 증설되어 활성화된 것만 이동 불가로 본다.
/// </summary>
public class TownTilemapDataProvider : ITilemapDataProvider
{
    private readonly TownTileManager townTileManager;
    private readonly List<Tilemap> extraColliderTilemaps = new List<Tilemap>(4);

    private Tilemap groundTilemap;
    private float halfCellY;
    private Vector3Int originCell;

    public int GridWidth { get; private set; }
    public int GridHeight { get; private set; }

    public TownTilemapDataProvider(TownTileManager _townTileManager)
    {
        townTileManager = _townTileManager;
        RefreshBounds();
    }

    /// <summary>
    /// 오프로드 차량처럼 마을 Grid와 별개의 Grid에 있는(발밑 전용) ColliderTilemap을
    /// 길찾기 제외 대상으로 추가 등록한다. 이미 등록된 타일맵은 중복 등록하지 않는다.
    /// </summary>
    public void RegisterExtraColliderTilemap(Tilemap _tilemap)
    {
        if (_tilemap != null && !extraColliderTilemaps.Contains(_tilemap))
        {
            extraColliderTilemaps.Add(_tilemap);
        }
    }

    /// <summary>
    /// TownTileManager.CreateGrid() 이후(GroundTilemap 등이 실제로 배정된 뒤) 반드시 한 번 호출해서
    /// 그리드 크기/원점을 다시 계산해야 한다.
    /// </summary>
    public void RefreshBounds()
    {
        groundTilemap = townTileManager != null ? townTileManager.GroundTilemap : null;
        if (groundTilemap == null) return;

        groundTilemap.CompressBounds();
        BoundsInt bounds = groundTilemap.cellBounds;

        originCell = new Vector3Int(bounds.xMin, bounds.yMin, 0);
        GridWidth = Mathf.Max(1, bounds.size.x);
        GridHeight = Mathf.Max(1, bounds.size.y);
        halfCellY = groundTilemap.cellSize.y * 0.5f;
    }

    public bool IsWalkable(Vector3Int _cellPos)
    {
        if (groundTilemap == null) return false;
        if (_cellPos.x < 0 || _cellPos.x >= GridWidth || _cellPos.y < 0 || _cellPos.y >= GridHeight) return false;

        Vector3Int actualCell = _cellPos + originCell;

        if (!groundTilemap.HasTile(actualCell)) return false;
        if (HasAnyCollider(actualCell)) return false;

        return true;
    }

    // 록 데코를 포함한 모든 콜라이더 타일이 이미 IsWalkable에서 제외되므로 별도 처리가 필요 없다.
    public bool HasRockDeco(Vector3Int _cellPos) => false;

    // 마을에는 위험 지형(용암 등)이 없으므로 항상 0을 반환한다.
    public float GetHazardStaminaDrainPerSecond(Vector3Int _cellPos) => 0f;

    // 마을에는 열기를 내뿜는 나무가 없으므로 항상 0을 반환한다.
    public float TreeHeatStaminaDamage => 0f;

    public bool IsWaterTile(Vector3Int _cellPos)
    {
        if (townTileManager == null || townTileManager.WaterColliderTilemap == null) return false;

        Vector3Int actualCell = _cellPos + originCell;
        return townTileManager.WaterColliderTilemap.HasTile(actualCell);
    }

    // GroundTilemap에 실제 배치된 타일 애셋 이름으로 판정한다 (예: "Stage01_GrassTile").
    // Town의 타일 팔레트가 던전(StageTileDataSO)과 동일한 "Stage0X_GrassTile" 명명 규칙을 그대로 쓰고 있어
    // 별도 리스트 없이도 이름만으로 안정적으로 구분할 수 있다.
    public bool IsGrassTile(Vector3Int _cellPos)
    {
        if (groundTilemap == null) return false;

        Vector3Int actualCell = _cellPos + originCell;
        TileBase tile = groundTilemap.GetTile(actualCell);
        return tile != null && tile.name.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool HasAnyCollider(Vector3Int _actualCell)
    {
        if (HasMainGridTile(townTileManager.ColliderTilemap, _actualCell)) return true;
        if (HasMainGridTile(townTileManager.BuildingColliderTilemap, _actualCell)) return true;
        if (HasMainGridTile(townTileManager.WaterColliderTilemap, _actualCell)) return true;
        if (HasMainGridTile(townTileManager.RockColliderTilemap, _actualCell)) return true;
        if (HasActiveBuildingExpansionTile(_actualCell)) return true;

        if (extraColliderTilemaps.Count > 0)
        {
            Vector3 worldPos = groundTilemap.GetCellCenterWorld(_actualCell);
            for (int i = 0; i < extraColliderTilemaps.Count; i++)
            {
                Tilemap foreign = extraColliderTilemaps[i];
                if (foreign == null) continue;

                Vector3Int foreignCell = foreign.WorldToCell(worldPos);
                if (foreign.HasTile(foreignCell)) return true;
            }
        }

        return false;
    }

    // 제재소 증설분 건물 충돌 타일맵은 아직 증설되지 않아도 타일 데이터는 그대로 들어있고(GameObject만 꺼둔 상태)
    // Tilemap.HasTile은 비활성 오브젝트에서도 true를 돌려준다. 그래서 활성 여부를 반드시 함께 확인해,
    // 증설 전에는 길이 막히지 않도록 한다.
    private bool HasActiveBuildingExpansionTile(Vector3Int _actualCell)
    {
        IReadOnlyList<Tilemap> expansions = townTileManager.BuildingColliderExpansionTilemaps;
        for (int i = 0; i < expansions.Count; i++)
        {
            Tilemap expansion = expansions[i];
            if (expansion == null || !expansion.gameObject.activeSelf) continue;
            if (expansion.HasTile(_actualCell)) return true;
        }

        return false;
    }

    // 같은 Grid 아래 있는 타일맵끼리는 셀 좌표계가 동일하므로 별도 좌표 변환 없이 바로 조회한다.
    private bool HasMainGridTile(Tilemap _tilemap, Vector3Int _actualCell)
    {
        return _tilemap != null && _tilemap.HasTile(_actualCell);
    }

    public Vector3Int WorldToCell(Vector3 _worldPos)
    {
        if (groundTilemap == null) return Vector3Int.zero;

        Vector3 adjustedPos = _worldPos;
        adjustedPos.y -= halfCellY;

        Vector3Int actualCell = groundTilemap.WorldToCell(adjustedPos);
        return actualCell - originCell;
    }

    public Vector3 CellToWorld(Vector3Int _cellPos)
    {
        if (groundTilemap == null) return Vector3.zero;

        Vector3Int actualCell = _cellPos + originCell;
        return groundTilemap.GetCellCenterWorld(actualCell) + new Vector3(0, halfCellY, 0);
    }

    // 아래는 인터페이스 요구사항이지만 운반 NPC의 길찾기(FindPath/FindPathNear)에서는 사용하지 않는
    // 던전 전용 기능들(나무 심기, 잔디/걷기 가능 위치 목록 등)이라 안전한 기본값만 반환한다.
    public List<Vector3> GetGrassTileWorldPositions() => new List<Vector3>();
    public List<Vector3> GetDelayedGrassTileWorldPositions() => new List<Vector3>();
    public List<Vector3> GetWalkableTileWorldPositions() => new List<Vector3>();
    public Vector3 GetPlayerSpawnPosition() => groundTilemap != null ? groundTilemap.transform.position : Vector3.zero;
    public Vector3 GetPortalSpawnPosition() => GetPlayerSpawnPosition();
    public void SetTreeCollisionTile(Vector3 _worldPos) { }
    public void ClearTreeCollisionTile(Vector3 _worldPos) { }
    public void BeginTreeCollisionTileBatch() { }
    public void EndTreeCollisionTileBatch() { }
}
