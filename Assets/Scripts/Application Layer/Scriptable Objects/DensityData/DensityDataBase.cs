using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Density Data Base", menuName = "Game/Density Data/Density Data Base")]
public class DensityDataBase : ScriptableObject
{
    public List<DensityData> densityDatas;

    public DensityData Get(MapType _mapType)
    {
        return densityDatas.Find(x => x.mapType == _mapType);
    }
}
