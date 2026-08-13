using System;
using System.Collections.Generic;
using UnityEngine;

public enum TreeGemType
{
    Gold,
    Diamond,
    Rainbow,
}

[Serializable]
public struct TreeGradeGemMapping
{
    public TreeGrade grade;
    public TreeGemType gemType;
}

/// <summary>
/// 나무 등급별로 어떤 보석 종류를 쓸지 정한다.
///
/// 색 자체는 여기서 정의하지 않는다. 종류별 머티리얼 세트가 TreeVisualComponent에 따로 있고,
/// 각 머티리얼에서 색·투명도·면 크기 등을 독립적으로 설정한다.
/// </summary>
[CreateAssetMenu(fileName = "Tree Gem Color Data Base", menuName = "Game/Objects/Tree Gem Color Data Base")]
public class TreeGemColorDataBase : ScriptableObject
{
    [Header("나무 등급 -> 보석 종류")]
    public List<TreeGradeGemMapping> gradeMappings;

#if UNITY_EDITOR
    // 색 비교용 디버그 스위치. 빌드에는 필드도 판정 코드도 포함되지 않으므로,
    // 켜 둔 채로 빌드해도 게임에 영향을 주지 않는다.
    [Header("디버그 - 등급 무시하고 한 종류로 고정 (에디터 전용)")]
    [Tooltip("켜면 등급과 무관하게 아래 종류로 강제한다. 플레이 중에 바꿔도 즉시 반영된다.")]
    public bool debugForceGemType;
    public TreeGemType debugGemType = TreeGemType.Diamond;
#endif

    /// <summary>
    /// 이 등급이 쓸 보석 종류를 찾는다.
    /// 매핑이 없으면 false를 돌려주고, 호출부가 기본 종류를 쓴다.
    /// </summary>
    public bool TryResolveGemType(TreeGrade _grade, out TreeGemType _gemType)
    {
#if UNITY_EDITOR
        if (debugForceGemType)
        {
            _gemType = debugGemType;
            return true;
        }
#endif

        if (gradeMappings != null)
        {
            for (int i = 0; i < gradeMappings.Count; i++)
            {
                if (gradeMappings[i].grade == _grade)
                {
                    _gemType = gradeMappings[i].gemType;
                    return true;
                }
            }
        }

        _gemType = default;
        return false;
    }

#if UNITY_EDITOR
    // 값을 바꾸면 이미 스폰된 나무들에도 즉시 반영한다. 플레이 중 색 비교용.
    //
    // 나무는 전부 런타임에 풀에서 스폰되므로 인스펙터로 참조를 걸어둘 대상이 없다.
    // 그래서 여기서는 탐색이 유일한 방법이다. OnValidate에서만, 즉 인스펙터를 만졌을 때만 돌고
    // 빌드에는 포함되지 않으므로 게임 성능과는 무관하다.
    // (같은 이유로 TreeObj.UpdateAllTreesInScene도 동일한 방식을 쓴다)
    private void OnValidate()
    {
        foreach (TreeObj tree in FindObjectsByType<TreeObj>(FindObjectsInactive.Exclude))
        {
            tree.RefreshGemVisual();
        }
    }
#endif
}
