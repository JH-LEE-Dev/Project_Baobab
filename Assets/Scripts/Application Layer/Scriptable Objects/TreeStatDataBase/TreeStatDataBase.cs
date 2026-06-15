using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Tree Stat Data Base", menuName = "Game/Objects/Tree Stat Data Base")]
public class TreeStatDataBase : ScriptableObject
{
    public List<TreeStatData> treeStatDatas;

    public TreeStatData Get(TreeType _type)
    {
        return treeStatDatas.Find(x => x.treeType == _type);
    }
}

[Serializable]
public struct TreeStatData
{
    public TreeType treeType;
    public float hp;
    public float sp;
    public float spRegen;
    public SPRegenStrategySO regenStrategy;
}

[Serializable]
public struct TreeGradeStatMultiplierData
{
    public TreeGrade treeGrade;
    public float hpMultiplier;
    public float dropMultiplier;
}