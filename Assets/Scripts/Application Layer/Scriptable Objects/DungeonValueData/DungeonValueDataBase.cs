using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DungeonDataBase", menuName = "Game/Dungeon/DungeonDataBase")]
public class DungeonValueDataBase : ScriptableObject
{
    public List<DungeonData> dungeonDatas;
    public DungeonData GetDungeonData(MapType _mapType)
    {
        return dungeonDatas.Find(x => x.mapTypeType == _mapType);
    }
}

[Serializable]
public struct DungeonData
{
    public MapType mapTypeType;
    public List<TreeGradeProb> treeGradeProbs;
    public float staminaDecAmount;
    public float staminaIncAmount;
}