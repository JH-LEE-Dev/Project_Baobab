using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임 스킬 데이터 노드 (트리 구조 관리)
/// </summary>
public class SkillNode
{
    public SkillType skillType;
    public bool bApplied;
    public List<SkillCommand<ICommandHandler>> commands;
    public List<SkillNode> prerequisiteNodes;

    public SkillNode(SkillType _type, bool _applied, List<SkillCommand<ICommandHandler>> _commands)
    {
        skillType = _type;
        bApplied = _applied;
        commands = _commands;
        prerequisiteNodes = new List<SkillNode>(4);
    }
}

public class SkillManager : MonoBehaviour, ISkillSystemProvider
{
    // 외부 의존성
    [SerializeField] private SkillDataBase skillDataBase;

    // 내부 의존성
    private Dictionary<SkillType, SkillNode> skillNodeMap;

    /// <summary>
    /// 스킬 매니저 초기화 및 스킬 트리 구축
    /// </summary>
    public void Initialize()
    {
        if (skillDataBase == null)
        {
            Debug.LogError("[SkillManager] SkillDataBase is null!");
            return;
        }

        int skillCount = skillDataBase.skills.Count;
        skillNodeMap = new Dictionary<SkillType, SkillNode>(skillCount);

        // 1단계: 모든 스킬 노드 생성
        for (int i = 0; i < skillCount; i++)
        {
            Skill skillData = skillDataBase.skills[i];
            
            // 중복 방지
            if (skillNodeMap.ContainsKey(skillData.skillType)) continue;

            SkillNode node = new SkillNode(skillData.skillType, skillData.bApplied, skillData.skillTypes);
            skillNodeMap.Add(skillData.skillType, node);
        }

        // 2단계: 선행 스킬 트리 구조 연결
        for (int i = 0; i < skillCount; i++)
        {
            Skill skillData = skillDataBase.skills[i];
            if (!skillNodeMap.TryGetValue(skillData.skillType, out SkillNode currentNode)) continue;

            List<SkillType> prerequisites = skillData.prerequisiteSkills;
            if (prerequisites == null) continue;

            for (int j = 0; j < prerequisites.Count; j++)
            {
                SkillType prereqType = prerequisites[j];
                if (skillNodeMap.TryGetValue(prereqType, out SkillNode prereqNode))
                {
                    currentNode.prerequisiteNodes.Add(prereqNode);
                }
            }
        }
    }

    /// <summary>
    /// 특정 스킬 습득 시도 (ISkillSystemProvider 구현)
    /// </summary>
    /// <param name="_type">습득할 스킬 타입</param>
    /// <returns>습득 성공 여부</returns>
    public bool TryApplySkill(SkillType _type)
    {
        if (!skillNodeMap.TryGetValue(_type, out SkillNode node)) return false;
        if (node.bApplied) return false;

        if (CanApplySkill(_type))
        {
            node.bApplied = true;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 해당 스킬이 습득 가능한 상태인지 확인 (ISkillSystemProvider 구현)
    /// </summary>
    /// <param name="_type">체크할 스킬 타입</param>
    /// <returns>습득 가능 여부</returns>
    public bool CanApplySkill(SkillType _type)
    {
        if (!skillNodeMap.TryGetValue(_type, out SkillNode node)) return false;
        if (node.bApplied) return false;

        List<SkillNode> prerequisites = node.prerequisiteNodes;
        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!prerequisites[i].bApplied)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 특정 스킬을 이미 습득했는지 확인 (ISkillSystemProvider 구현)
    /// </summary>
    /// <param name="_type">확인할 스킬 타입</param>
    /// <returns>습득 여부</returns>
    public bool IsApplied(SkillType _type)
    {
        if (skillNodeMap.TryGetValue(_type, out SkillNode node))
        {
            return node.bApplied;
        }
        return false;
    }

    /// <summary>
    /// 특정 스킬의 선행 스킬 노드 리스트를 반환 (ISkillSystemProvider 구현)
    /// </summary>
    /// <param name="_type">대상 스킬 타입</param>
    /// <returns>선행 노드 리스트</returns>
    public List<SkillNode> GetPrerequisites(SkillType _type)
    {
        if (skillNodeMap.TryGetValue(_type, out SkillNode node))
        {
            return node.prerequisiteNodes;
        }
        return null;
    }
}
