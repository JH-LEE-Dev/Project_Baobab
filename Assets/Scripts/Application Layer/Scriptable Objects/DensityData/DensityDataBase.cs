using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "Density Data Base", menuName = "Game/Density Data/Density Data Base")]
public class MapDensityDataBase : ScriptableObject
{
    public List<MapDensityData> densityDatas;

    public DensityData Get(MapType _mapType, ForestType _ForestType)
    {
        var mapDensityData = densityDatas.Find(x => x.mapType == _mapType);

        return mapDensityData.densityData.Find(x => x.forestType == _ForestType);
    }
}
