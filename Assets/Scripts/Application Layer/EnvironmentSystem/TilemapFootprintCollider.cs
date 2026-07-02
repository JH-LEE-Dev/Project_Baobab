using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 로컬 Tilemap이 덮는 타일들을 IEnvironmentProvider의 던전 길찾기 그리드에
/// 이동 불가 타일로 등록/해제하는 재사용 가능한 헬퍼.
/// 자기 발밑 타일을 길찾기에서 막아야 하는 오브젝트(차량, RepairBox 등)가 공통으로 사용한다.
/// </summary>
public class TilemapFootprintCollider
{
    private readonly Tilemap footprintTilemap;
    private IEnvironmentProvider environmentProvider;
    private readonly List<Vector3> registeredPositions = new List<Vector3>(16);
    private bool bRegistered;

    public TilemapFootprintCollider(Tilemap _footprintTilemap)
    {
        footprintTilemap = _footprintTilemap;
    }

    public void SetEnvironmentProvider(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    /// <summary>
    /// 현재 위치 기준으로 타일들을 등록합니다. 이미 등록된 것이 있다면 먼저 해제하고 다시 등록합니다.
    /// (오브젝트가 재배치되어도 안전하게 다시 호출할 수 있습니다.)
    /// </summary>
    public void Register()
    {
        Clear();

        if (footprintTilemap == null || environmentProvider?.tilemapDataProvider == null) return;

        ITilemapDataProvider dungeonTilemap = environmentProvider.tilemapDataProvider;

        foreach (Vector3Int localCell in footprintTilemap.cellBounds.allPositionsWithin)
        {
            if (!footprintTilemap.HasTile(localCell)) continue;

            Vector3 worldCenter = footprintTilemap.GetCellCenterWorld(localCell);
            // 로컬 타일맵의 셀 좌표를 던전 타일맵의 셀 좌표로 변환한 뒤 다시 월드 좌표로 되돌려서
            // SetTreeCollisionTile이 기대하는 좌표 규칙(halfCellY 보정 포함)에 정확히 맞춘다.
            Vector3Int dungeonCell = dungeonTilemap.WorldToCell(worldCenter);
            Vector3 dungeonWorldPos = dungeonTilemap.CellToWorld(dungeonCell);

            dungeonTilemap.SetTreeCollisionTile(dungeonWorldPos);
            registeredPositions.Add(dungeonWorldPos);
        }

        bRegistered = true;
    }

    public void Clear()
    {
        if (!bRegistered || environmentProvider?.tilemapDataProvider == null) return;

        ITilemapDataProvider dungeonTilemap = environmentProvider.tilemapDataProvider;
        for (int i = 0; i < registeredPositions.Count; i++)
        {
            dungeonTilemap.ClearTreeCollisionTile(registeredPositions[i]);
        }
        registeredPositions.Clear();

        bRegistered = false;
    }
}
