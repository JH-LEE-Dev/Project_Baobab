using UnityEngine;
using System;

public class PathfindGridManager : MonoBehaviour, IPathfindGridProvider
{
    private bool[] occupiedTiles;
    private int width;
    private int height;

    // // 퍼블릭 초기화 메서드 (인터페이스 아님)
    public void Initialize(int _width, int _height)
    {
        width = _width;
        height = _height;
        int size = width * height;

        if (occupiedTiles == null || occupiedTiles.Length != size)
        {
            occupiedTiles = new bool[size];
        }
        else
        {
            Array.Clear(occupiedTiles, 0, size);
        }
    }

    public bool IsOccupied(Vector3Int _cellPos)
    {
        if (occupiedTiles == null) return false;
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return true;

        return occupiedTiles[_cellPos.x + _cellPos.y * width];
    }

    public bool Occupy(Vector3Int _cellPos)
    {
        if (occupiedTiles == null) return false;
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return false;

        int index = _cellPos.x + _cellPos.y * width;
        if (occupiedTiles[index]) return false;

        occupiedTiles[index] = true;
        return true;
    }

    public void Release(Vector3Int _cellPos)
    {
        if (occupiedTiles == null) return;
        if (_cellPos.x < 0 || _cellPos.x >= width || _cellPos.y < 0 || _cellPos.y >= height) return;

        occupiedTiles[_cellPos.x + _cellPos.y * width] = false;
    }

    public void ClearAllOccupancy()
    {
        if (occupiedTiles != null)
        {
            Array.Clear(occupiedTiles, 0, occupiedTiles.Length);
        }
    }
}
