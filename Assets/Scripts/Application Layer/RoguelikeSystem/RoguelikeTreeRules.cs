using UnityEngine;

/// <summary>로그라이크 세션의 나무 규칙. 웨이브가 바뀔 때 HP와 나무 종류만 갱신한다.</summary>
public sealed class RoguelikeTreeRules : IRunTreeRules
{
    public bool bForceNormalGrade => true;
    public bool bDisableStageGimmicks => true;
    public bool bSuppressItemDrops => true;

    public float TreeHp { get; private set; }

    private TreeType mainType = TreeType.None;
    private TreeType previousType = TreeType.None;
    private float previousShare;

    /// <summary>웨이브 시작 전에 부른다. 나무 스탯은 풀에서 꺼낼 때 정해지므로 스폰 전에 바꿔야 한다.</summary>
    public void SetTreeHp(float _hp)
    {
        TreeHp = Mathf.Max(0f, _hp);
    }

    /// <summary>
    /// 이번 웨이브의 주력 나무 종류. 티어 안에서 종류가 바뀌는 웨이브는 _previousType이 _previousShare 비율로 섞인다(기획서 7-1).
    /// _mainType이 None이면 지역 분포를 그대로 쓴다.
    /// </summary>
    public void SetTreeTypes(TreeType _mainType, TreeType _previousType, float _previousShare)
    {
        mainType = _mainType;
        previousType = _previousType;
        previousShare = Mathf.Clamp01(_previousShare);
    }

    public TreeType PickTreeType(TreeType _densityType)
    {
        if (TreeType.None == mainType) return _densityType;
        if (TreeType.None != previousType && previousShare > 0f && Random.value < previousShare) return previousType;
        return mainType;
    }
}
