using UnityEngine;
using System.Collections.Generic;

public struct MapGeneratedSignal
{
    public List<Vector3> grassTilePositions;
    public MapGeneratedSignal(List<Vector3> _grassTilePositions)
    {
        grassTilePositions = _grassTilePositions;
    }
}