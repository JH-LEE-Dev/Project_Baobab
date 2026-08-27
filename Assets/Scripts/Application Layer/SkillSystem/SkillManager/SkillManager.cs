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

    public bool GetNextLevelCost(out long _money, out long _carrot)
    {
        int nextLevel = currentLevel + 1;
        _money = EvaluateCost(cost.moneyCurve, nextLevel);
        _carrot = EvaluateCost(cost.carrotCurve, nextLevel);

        return true;
    }

    private static long EvaluateCost(ProgressionCurve _curve, int _targetLevel)
    {
        float value = _curve.Evaluate(_targetLevel);
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0L;

        return (long)Math.Round(value, MidpointRounding.AwayFromZero);
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
    public event Action<SkillDispatchInfo> SkillValuePreviewRequestEvent;
    public event Action<int> PrestigeLevelIncreasedEvent;
    public Action<SkillDispatchInfo> DispatchSkillsEvent;

    // 외부 의존성
    [SerializeField] private SkillDataBase skillDataBase;

    private IInventoryForSkill inventory;

    // 내부 의존성
    private Dictionary<SkillType, SkillNode> skillNodeMap;
    [SerializeField] private int prestigeLevel = 0;
    [SerializeField] private int skillExperience = 0;
    [Tooltip("프레스티지 레벨 구간별 필요 경험치 (0-1, 1-2 등). 인덱스 초과 시 마지막 값을 사용합니다.")]
    [SerializeField] private int[] experienceToLevelUp = new int[] { 40 };

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

        if (!node.GetNextLevelCost(out long moneyCost, out long carrotCost))
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

        // 특성을 찍는 순간 (가벼운 진동)
        Rumble.Play(EHapticEvent.SkillPoint);

        // 경험치 증가 및 프레스티지 레벨업 처리
        skillExperience++;
        if (skillExperience >= GetPrestigeExpLimit())
        {
            skillExperience = 0;
            prestigeLevel++;

            // 프레스티지 레벨업은 특성 습득 진동보다 훨씬 세고 길어서, 같은 프레임에 겹쳐도
            // GamepadHaptics의 "더 강한 쪽이 이긴다" 규칙에 따라 이쪽 파형이 남는다.
            Rumble.Play(EHapticEvent.PrestigeLevelUp);

            PrestigeLevelIncreasedEvent?.Invoke(prestigeLevel);
        }

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
        if (!node.GetNextLevelCost(out long moneyCost, out long carrotCost))
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
    /// 비용(재화) 소모 없이 스킬을 습득하는 함수 (단, 선행 스킬 및 최대 레벨 조건은 확인합니다)
    /// </summary>
    public AbilityLevelUpRejectReason TryApplySkillWithoutCost(SkillType _type)
    {
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

        // 재화 차감 및 검사 로직 생략 (비용 없이 레벨업 진행)

        // 레벨업
        node.currentLevel++;

        // 특성을 찍는 순간 (가벼운 진동)
        Rumble.Play(EHapticEvent.SkillPoint);

        // 경험치 증가 및 프레스티지 레벨업 처리
        skillExperience++;
        if (skillExperience >= GetPrestigeExpLimit())
        {
            skillExperience = 0;
            prestigeLevel++;

            // 프레스티지 레벨업은 특성 습득 진동보다 훨씬 세고 길어서, 같은 프레임에 겹쳐도
            // GamepadHaptics의 "더 강한 쪽이 이긴다" 규칙에 따라 이쪽 파형이 남는다.
            Rumble.Play(EHapticEvent.PrestigeLevelUp);

            PrestigeLevelIncreasedEvent?.Invoke(prestigeLevel);
        }

        // 스킬 적용 이벤트 발생 (등록된 모든 커맨드 발송)
        if (node.commands != null)
        {
            Debug.Log($"특성 적용(무비용) -> 타입 : {_type} (Level: {node.currentLevel})");

            for (int i = 0; i < node.commands.Count; i++)
            {
                var info = new SkillDispatchInfo(node.currentLevel, node.commands[i]);
                DispatchSkillsEvent?.Invoke(info);
            }
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

            long nextCarrotCost;
            long nextMoneyCost;
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
    public void PopulateSkillSaveData(ref SkillTreeSaveData _saveData)
    {
        _saveData.prestigeLevel = prestigeLevel;
        _saveData.skillExperience = skillExperience;
        _saveData.skillSaveDatas.Clear();

        foreach (var pair in skillNodeMap)
        {
            SkillNode node = pair.Value;
            if (node.currentLevel > 0)
            {
                _saveData.skillSaveDatas.Add(new SkillSaveData
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
    public void LoadSaveData(SkillTreeSaveData _data)
    {
        prestigeLevel = _data.prestigeLevel;
        skillExperience = _data.skillExperience;

        if (_data.skillSaveDatas == null) return;

        foreach (var data in _data.skillSaveDatas)
        {
            if (skillNodeMap.TryGetValue(data.skillType, out SkillNode node))
            {
                node.currentLevel = data.currentLevel;

                // 스킬 효과 재적용 (각 레벨에 대해 이벤트를 발송해야 할 수도 있으나, 
                // 현재 구조상 마지막 레벨의 효과만 발송해도 누적되는지 확인 필요.
                // 대부분의 시스템이 레벨별 절대값을 사용한다면 마지막 레벨만 발송)
                if (node.commands != null)
                {
                    for (int lvl = 1; lvl <= node.currentLevel; lvl++)
                    {
                        for (int i = 0; i < node.commands.Count; i++)
                        {
                            var info = new SkillDispatchInfo(lvl, node.commands[i]);
                            DispatchSkillsEvent?.Invoke(info);
                        }
                    }
                }
            }
        }
        Debug.Log("[SkillManager] Skill Save Data Loaded and Applied.");
    }

    public int GetCurrentPrestigeLevel()
    {
        return prestigeLevel;
    }

    public int GetCurrentPrestigeExp()
    {
        return skillExperience;
    }

    public int GetPrestigeExpLimit()
    {
        if (experienceToLevelUp == null || experienceToLevelUp.Length == 0) return 40;
        int index = Mathf.Min(prestigeLevel, experienceToLevelUp.Length - 1);
        return experienceToLevelUp[index];
    }

    public void RequestSkillValuePreviewData(SkillType _type)
    {
        if (skillNodeMap.TryGetValue(_type, out SkillNode node))
        {
            if (node.commands != null)
            {
                int targetLevel = Mathf.Min(node.currentLevel + 1, node.maxLevel);
                for (int i = 0; i < node.commands.Count; i++)
                {
                    SkillDispatchInfo info = new SkillDispatchInfo(targetLevel, node.commands[i]);
                    SkillValuePreviewRequestEvent?.Invoke(info);
                }
            }
        }
    }
}
