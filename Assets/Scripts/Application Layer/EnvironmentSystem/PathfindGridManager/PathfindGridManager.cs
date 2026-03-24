using UnityEngine;
using System.Collections.Generic;

public class PathfindGridManager : MonoBehaviour, IPathfindGridProvider
{
    // 점유된 타일 정보를 저장 (GC 최소화를 위해 HashSet 사용)
    private readonly HashSet<Vector3Int> occupiedTiles = new HashSet<Vector3Int>(512);

    public bool IsOccupied(Vector3Int _cellPos)
    {
        return occupiedTiles.Contains(_cellPos);
    }

    public bool Occupy(Vector3Int _cellPos)
    {
        return occupiedTiles.Add(_cellPos);
    }

    public void Release(Vector3Int _cellPos)
    {
        occupiedTiles.Remove(_cellPos);
    }

    // 씬 전환이나 초기화 시 호출 가능
    public void ClearAllOccupancy()
    {
        occupiedTiles.Clear();
    }
}
