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

    /// <summary>
    /// 나무 스탯을 돌려줍니다.
    ///
    /// [데모 오버라이드의 방향]
    /// treeStatDatas가 <b>정식 값</b>이고, demoHpOverrides는 데모 빌드에서만 그 위에 hp를 덮어씁니다.
    /// 즉 정식은 오버라이드 목록을 아예 보지 않으므로, 데모를 따로 잡아도 정식 밸런스는 건드려지지 않습니다.
    /// 반대로 넣지 마십시오 — 데모 값을 treeStatDatas에 적어두면 정식이 그 값을 그대로 물고 나갑니다.
    ///
    /// [에디터에서 밸런스를 볼 때 주의]
    /// 디파인이 없는 상태가 데모이므로(BuildInfo), <b>에디터 기본 실행은 데모로 판정됩니다.</b>
    /// 오버라이드에 값이 들어 있으면 에디터에서 보이는 hp는 데모 값입니다.
    /// 정식 수치를 확인하려면 BAOBAB_FULL_RELEASE 를 켜고 재컴파일해야 합니다.
    ///
    /// [빈 목록은 "덮어쓸 것 없음"입니다]
    /// 목록이 비어 있으면 데모도 정식과 같은 hp로 돕니다. 고장이 아니라 아직 안 정한 상태입니다.
    ///
    /// 주의: treeStatDatas에 없는 TreeType을 물으면 Find가 default(hp 0)를 <b>조용히</b> 돌려줍니다.
    /// 나무 종류를 추가할 때 이 목록도 함께 채워야 합니다.
    /// </summary>
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