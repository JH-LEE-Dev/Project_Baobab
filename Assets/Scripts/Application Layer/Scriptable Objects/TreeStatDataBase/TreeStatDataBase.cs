using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Tree Stat Data Base", menuName = "Game/Objects/Tree Stat Data Base")]
public class TreeStatDataBase : ScriptableObject
{
    public List<TreeStatData> treeStatDatas;

    [Header("데모 빌드 전용 HP 오버라이드")]
    [Tooltip("BuildInfo.IsDemo가 true일 때만 적용됩니다. 여기 등록된 TreeType은 기본 hp 대신 이 값을 사용합니다.")]
    public List<TreeDemoHpOverrideData> demoHpOverrides;

    public TreeStatData Get(TreeType _type)
    {
        TreeStatData data = treeStatDatas.Find(x => x.treeType == _type);

        if (BuildInfo.IsDemo && demoHpOverrides != null)
        {
            int overrideIndex = demoHpOverrides.FindIndex(x => x.treeType == _type);
            if (overrideIndex >= 0)
            {
                data.hp = demoHpOverrides[overrideIndex].hp;
            }
        }

        return data;
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
public struct TreeDemoHpOverrideData
{
    public TreeType treeType;
    public float hp;
}

[Serializable]
public struct TreeGradeStatMultiplierData
{
    public TreeGrade treeGrade;
    public float hpMultiplier;
    public float dropMultiplier;
}