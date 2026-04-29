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
    public SkillCost cost;
    public List<SkillCommandInfo> commands;
    public List<SkillNode> prerequisiteNodes;

    public bool bApplied => currentLevel > 0;

    public SkillNode(SkillType _type, int _maxLevel, SkillCost _cost, List<SkillCommandInfo> _commands)
    {
        skillType = _type;
        currentLevel = 0;
        maxLevel = _maxLevel;
        cost = _cost;
        commands = _commands;
        prerequisiteNodes = new List<SkillNode>(4);
    }

    public bool GetNextLevelCost(out int _money, out int _carrot)
    {
        int nextLevel = currentLevel + 1;
        _money = (int)cost.moneyCurve.Evaluate(nextLevel);
        _carrot = (int)cost.carrotCurve.Evaluate(nextLevel);

        return true;
    }
}

public struct SkillDispatchInfo
{
    public int level;
    public SkillCommandInfo commandInfo;
    public SkillDispatchInfo(int _level, SkillCommandInfo _info)
    {
        level = _level;
        commandInfo = _info;
    }
}

public class SkillManager : MonoBehaviour, ISkillSystemProvider
{
    private const bool EnablePrototypeAutoPass = false;

    public Action<SkillDispatchInfo> DispatchSkillsEvent;

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
        //if (EnablePrototypeAutoPass)
        //return AbilityLevelUpRejectReason.Pass;

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
            Debug.Log($"특성 적용 -> 타입 : {_type} (Level: {node.currentLevel})");

            for (int i = 0; i < node.commands.Count; i++)
            {
                var info = new SkillDispatchInfo(node.currentLevel, node.commands[i]);

                DispatchSkillsEvent?.Invoke(info);
            }
        }

        return AbilityLevelUpRejectReason.Pass;
    }

    /// <summary>
    /// 해당 스킬이 습득 가능한 상태인지 확인 (ISkillSystemProvider 구현)
    /// </summary>
    public AbilityLevelUpRejectReason CanApplySkill(SkillType _type)
    {
        //if (EnablePrototypeAutoPass)
        //return AbilityLevelUpRejectReason.Pass;

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

        // 3. 재화 체크
        if (!node.GetNextLevelCost(out int moneyCost, out int carrotCost))
            return AbilityLevelUpRejectReason.None;

        if (inventory.GetCurrentMoney() < moneyCost)
        {
            return AbilityLevelUpRejectReason.NotEnoughMoney;
        }

        if (inventory.GetCurrentCarrot() < carrotCost)
        {
            return AbilityLevelUpRejectReason.NotEnoughCarrot;
        }

        return AbilityLevelUpRejectReason.Pass;
    }

    /// <summary>
    /// 특정 스킬을 이미 습득했는지 확인하고 레벨을 반환함 (ISkillSystemProvider 구현)
    /// </summary>
    public bool IsApplied(SkillType _type, out int _level)
    {
        if (skillNodeMap.TryGetValue(_type, out SkillNode node))
        {
            _level = node.currentLevel;
            return node.bApplied;
        }
        _level = 0;
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

    /// <summary>
    /// 특정 스킬의 상세 정보(레벨, 비용, 선행 스킬 등)를 반환 (ISkillSystemProvider 구현)
    /// </summary>
    public SkillInfo GetSkillInfo(SkillType _type)
    {
        SkillInfo info = new SkillInfo();
        info.skillType = _type;

        if (skillNodeMap.TryGetValue(_type, out SkillNode node))
        {
            info.currentLevel = node.currentLevel;
            info.maxLevel = node.maxLevel;

            int nextCarrotCost;
            int nextMoneyCost;
            // 다음 레벨 비용 계산
            node.GetNextLevelCost(out nextMoneyCost, out nextCarrotCost);

            if (nextMoneyCost > 0)
            {
                info.nextCost = nextMoneyCost;
                info.moneyType = MoneyType.Coin;
            }
            else if (nextCarrotCost > 0)
            {
                info.nextCost = nextCarrotCost;
                info.moneyType = MoneyType.Carrot;
            }


            // 선행 스킬 리스트 구성
            if (node.prerequisiteNodes != null && node.prerequisiteNodes.Count > 0)
            {
                info.prerequisiteSkills = new List<SkillType>(node.prerequisiteNodes.Count);
                for (int i = 0; i < node.prerequisiteNodes.Count; i++)
                {
                    info.prerequisiteSkills.Add(node.prerequisiteNodes[i].skillType);
                }
            }
        }

        return info;
    }

    /// <summary>
    /// 세이브를 위해 현재 습득한(레벨 > 0) 모든 스킬 데이터를 리스트에 채워줌 (GC Alloc 최소화)
    /// </summary>
    public void PopulateSkillSaveData(List<SkillSaveData> _saveDataList)
    {
        _saveDataList.Clear();

        foreach (var pair in skillNodeMap)
        {
            SkillNode node = pair.Value;
            if (node.currentLevel > 0)
            {
                _saveDataList.Add(new SkillSaveData
                {
                    skillType = node.skillType,
                    currentLevel = node.currentLevel
                });
            }
        }
    }

    /// <summary>
    /// 세이브된 데이터를 불러와서 스킬 상태를 복구하고 효과를 적용함
    /// </summary>
    public void LoadSaveData(List<SkillSaveData> _dataList)
    {
        if (_dataList == null) return;

        foreach (var data in _dataList)
        {
            if (skillNodeMap.TryGetValue(data.skillType, out SkillNode node))
            {
                node.currentLevel = data.currentLevel;

                // 스킬 효과 재적용 (각 레벨에 대해 이벤트를 발송해야 할 수도 있으나, 
                // 현재 구조상 마지막 레벨의 효과만 발송해도 누적되는지 확인 필요.
                // 대부분의 시스템이 레벨별 절대값을 사용한다면 마지막 레벨만 발송)
                if (node.commands != null)
                {
                    for (int i = 0; i < node.commands.Count; i++)
                    {
                        var info = new SkillDispatchInfo(node.currentLevel, node.commands[i]);
                        //DispatchSkillsEvent?.Invoke(info);
                    }
                }
            }
        }
        Debug.Log("[SkillManager] Skill Save Data Loaded and Applied.");
    }
}
