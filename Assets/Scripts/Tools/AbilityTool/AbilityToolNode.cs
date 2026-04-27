using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class AbilityToolLevelCostEntry
{
    public int level = 1;
    public MoneyType moneyType = MoneyType.Coin;
    public int amount = 0;
}

[Serializable]
public class AbilityToolParentLink
{
    public AbilityToolNode parentNode;
    public bool usePivot;
    public Vector2Int pivotGrid;
}

public class AbilityToolNode : MonoBehaviour
{
    private static readonly Color DefaultBaseColor = Color.white;
    private static readonly Color SelectedBaseColor = new Color32(0, 255, 0, 255);

    [Header("Node Data")]
    [SerializeField] private SkillType skillType = SkillType.None;
    [SerializeField] private string displayName;
    [SerializeField] [TextArea(2, 5)] private string description;
    [SerializeField] private int maxLevel = 1;
    [SerializeField] private List<AbilityToolLevelCostEntry> levelCosts = new List<AbilityToolLevelCostEntry>();
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private List<AbilityToolParentLink> parentLinks = new List<AbilityToolParentLink>();

    [Header("UI References")]
    [SerializeField] private Image abilityBaseImage;

    public SkillType SkillType => skillType;
    public string DisplayName => displayName;
    public string Description => description;
    public int MaxLevel => maxLevel;
    public List<AbilityToolLevelCostEntry> LevelCosts => levelCosts;
    public Vector2Int GridPosition => gridPosition;
    public RectTransform RectTransform => transform as RectTransform;
    public List<AbilityToolParentLink> ParentLinks => parentLinks;

    public void ApplyDefinition(AbilityNodeDefinitionJson _definition, SkillType _skillType, float _gridCellSize)
    {
        if (_definition == null)
            return;

        skillType = _skillType;
        displayName = _definition.displayName;
        description = _definition.description;
        maxLevel = Mathf.Max(_definition.maxLevel, 1);
        gridPosition = new Vector2Int(_definition.gridX, _definition.gridY);
        levelCosts = ConvertLevelCosts(_definition.levelCosts);
        parentLinks.Clear();

        ApplyAnchoredPosition(_gridCellSize);
        gameObject.name = $"AbilityToolNode_{skillType}_{gridPosition.x}_{gridPosition.y}";
    }

    // 툴에서 지정한 그리드 좌표를 저장하고 실제 UI 위치에 반영한다.
    public void SetGridPosition(Vector2Int _gridPosition, float _gridCellSize)
    {
        gridPosition = _gridPosition;
        ApplyAnchoredPosition(_gridCellSize);
        gameObject.name = $"AbilityToolNode_{gridPosition.x}_{gridPosition.y}";
    }

    // 현재 그리드 좌표를 UI 기준 anchoredPosition으로 바꿔 반영한다.
    public void ApplyAnchoredPosition(float _gridCellSize)
    {
        RectTransform rectTransform = RectTransform;
        if (rectTransform == null)
            return;

        Vector2 anchoredPosition = new Vector2(gridPosition.x * _gridCellSize, gridPosition.y * _gridCellSize);
        rectTransform.anchoredPosition = new Vector2(
            Mathf.Round(anchoredPosition.x),
            Mathf.Round(anchoredPosition.y));
    }

    // 연결 모드에서 선택된 노드인지 시각적으로 표시한다.
    public void SetSelectedVisual(bool _selected)
    {
        if (abilityBaseImage == null)
            return;

        abilityBaseImage.color = _selected ? SelectedBaseColor : DefaultBaseColor;
    }

    // 지정한 부모 노드 연결을 추가하거나 기존 연결을 갱신한다.
    public void AddOrUpdateParentLink(AbilityToolNode _parentNode, bool _usePivot, Vector2Int _pivotGrid)
    {
        if (_parentNode == null || _parentNode == this)
            return;

        for (int i = 0; i < parentLinks.Count; i++)
        {
            AbilityToolParentLink link = parentLinks[i];
            if (link == null || link.parentNode != _parentNode)
                continue;

            link.usePivot = _usePivot;
            link.pivotGrid = _pivotGrid;
            return;
        }

        parentLinks.Add(new AbilityToolParentLink
        {
            parentNode = _parentNode,
            usePivot = _usePivot,
            pivotGrid = _pivotGrid
        });
    }

    // 특정 부모 노드와의 연결을 제거한다.
    public void RemoveParentLink(AbilityToolNode _parentNode)
    {
        for (int i = parentLinks.Count - 1; i >= 0; i--)
        {
            AbilityToolParentLink link = parentLinks[i];
            if (link == null || link.parentNode != _parentNode)
                continue;

            parentLinks.RemoveAt(i);
        }
    }

    // 삭제된 노드를 참조하는 모든 부모 연결을 제거한다.
    public void RemoveNullOrTargetParentLinks(AbilityToolNode _targetNode)
    {
        for (int i = parentLinks.Count - 1; i >= 0; i--)
        {
            AbilityToolParentLink link = parentLinks[i];
            if (link == null || link.parentNode == null || link.parentNode == _targetNode)
                parentLinks.RemoveAt(i);
        }
    }

    private List<AbilityToolLevelCostEntry> ConvertLevelCosts(AbilityLevelCostJson[] _levelCosts)
    {
        List<AbilityToolLevelCostEntry> result = new List<AbilityToolLevelCostEntry>();
        if (_levelCosts == null)
            return result;

        for (int i = 0; i < _levelCosts.Length; i++)
        {
            AbilityLevelCostJson levelCost = _levelCosts[i];
            if (levelCost == null)
                continue;

            MoneyType moneyType = MoneyType.None;
            if (string.IsNullOrWhiteSpace(levelCost.moneyType) == false)
                Enum.TryParse(levelCost.moneyType, true, out moneyType);

            result.Add(new AbilityToolLevelCostEntry
            {
                level = levelCost.level,
                moneyType = moneyType,
                amount = levelCost.amount
            });
        }

        return result;
    }
}
