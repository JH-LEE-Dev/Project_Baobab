using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임 스킬 데이터 노드 (트리 구조 및 레벨 관리)
/// </summary>
public class SkillNode
{
    public SkillType skillType;
    public int currentLevel;
    public int maxLevel;
    public List<SkillLevelCost> costs;
    public List<SkillCommand<ICommandHandler>> commands;
    public List<SkillNode> prerequisiteNodes;

    public bool bApplied => currentLevel > 0;

    public SkillNode(SkillType _type, int _maxLevel, List<SkillLevelCost> _costs, List<SkillCommand<ICommandHandler>> _commands)
    {
        skillType = _type;
        currentLevel = 0;
        maxLevel = _maxLevel;
        costs = _costs;
        commands = _commands;
        prerequisiteNodes = new List<SkillNode>(4);
    }

    public bool GetNextLevelCost(out int _money, out int _carrot)
    {
        _money = 0;
        _carrot = 0;

        if (costs == null) return false;
        
        int nextLevel = currentLevel + 1;
        for (int i = 0; i < costs.Count; i++)
        {
            if (costs[i].level == nextLevel)
            {
                _money = costs[i].moneyCost;
                _carrot = costs[i].carrotCost;
                return true;
            }
        }
        return false;
    }
}

public class SkillManager : MonoBehaviour, ISkillSystemProvider
{
    private const bool EnablePrototypeAutoPass = false;

    public Action<SkillCommand<ICommandHandler>> DispatchSkillsEvent;

    // 외부 의존성
    [SerializeField] private SkillDataBase skillDataBase;

    private IInventoryForSkill inventory;

    // 내부 의존성
    private Dictionary<SkillType, SkillNode> skillNodeMap;

    /// <summary>
    /// 스킬 매니저 초기화 및 스킬 트리 구축
    /// </summary>
    public void Initialize(IInventoryForSkill _inventory)
    {
        inventory = _inventory;

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

            SkillNode node = new SkillNode(
                skillData.skillType, 
                skillData.maxLevel, 
                skillData.cost, 
                skillData.skillTypes
            );
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
    public AbilityLevelUpRejectReason TryApplySkill(SkillType _type)
    {
        if (EnablePrototypeAutoPass)
            return AbilityLevelUpRejectReason.Pass;

        AbilityLevelUpRejectReason reason = CanApplySkill(_type);
        if (reason != AbilityLevelUpRejectReason.Pass) return reason;

        if (!skillNodeMap.TryGetValue(_type, out SkillNode node)) 
            return AbilityLevelUpRejectReason.None;

        if (!node.GetNextLevelCost(out int moneyCost, out int carrotCost))
            return AbilityLevelUpRejectReason.None;
        
        // 1. 재화 체크 (Money)
        if (inventory.GetCurrentMoney() < moneyCost)
        {
            return AbilityLevelUpRejectReason.NotEnoughMoney;
        }

        // 2. 재화 체크 (Carrot)
        if (inventory.GetCurrentCarrot() < carrotCost)
        {
            return AbilityLevelUpRejectReason.NotEnoughCarrot;
        }

        // 재화 차감
        if (moneyCost > 0) inventory.DecreaseMoney(moneyCost);
        if (carrotCost > 0) inventory.DecreaseCarrot(carrotCost);

        // 레벨업
        node.currentLevel++;

        // 스킬 적용 이벤트 발생 (등록된 모든 커맨드 발송)
        if (node.commands != null)
        {
            for (int i = 0; i < node.commands.Count; i++)
            {
                DispatchSkillsEvent?.Invoke(node.commands[i]);
            }
        }

        return AbilityLevelUpRejectReason.Pass;
    }

    /// <summary>
    /// 해당 스킬이 습득 가능한 상태인지 확인 (ISkillSystemProvider 구현)
    /// </summary>
    public AbilityLevelUpRejectReason CanApplySkill(SkillType _type)
    {
        if (EnablePrototypeAutoPass)
            return AbilityLevelUpRejectReason.Pass;

        if (!skillNodeMap.TryGetValue(_type, out SkillNode node)) 
            return AbilityLevelUpRejectReason.None;

        // 1. 최대 레벨 체크
        if (node.currentLevel >= node.maxLevel)
        {
            return AbilityLevelUpRejectReason.MaxLevel;
        }

        // 2. 선행 스킬 습득 체크
        List<SkillNode> prerequisites = node.prerequisiteNodes;
        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!prerequisites[i].bApplied)
            {
                return AbilityLevelUpRejectReason.None; // 선행 스킬 미습득
            }
        }

        return AbilityLevelUpRejectReason.Pass;
    }

    /// <summary>
    /// 특정 스킬을 이미 습득했는지 확인 (ISkillSystemProvider 구현)
    /// </summary>
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
    public List<SkillNode> GetPrerequisites(SkillType _type)
    {
        if (skillNodeMap.TryGetValue(_type, out SkillNode node))
        {
            return node.prerequisiteNodes;
        }
        return null;
    }
}
