using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public class AbilityToolParentLink
{
    public AbilityToolNode parentNode;
    public bool usePivot;
    public Vector2Int pivotGrid;
}

[Serializable]
public class AbilityToolSkillCommandEntry
{
    public SkillCommandType skillCommandType = SkillCommandType.None;
    public ProgressionCurve amountCurve;
}

public class AbilityToolNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color DefaultBaseColor = Color.white;
    private static readonly Color SelectedBaseColor = new Color32(0, 255, 0, 255);
    private static readonly Color MoveSelectedBaseColor = new Color32(64, 160, 255, 255);

    [Header("Node Data")]
    [SerializeField] private SkillType skillType = SkillType.None;
    [SerializeField] private string displayName;
    [SerializeField] [TextArea(2, 5)] private string description;
    [SerializeField] private int maxLevel = 1;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private List<AbilityToolParentLink> parentLinks = new List<AbilityToolParentLink>();

    [Header("Logic Export Data")]
    [SerializeField] private ProgressionCurve moneyCurve;
    [SerializeField] private ProgressionCurve carrotCurve;
    [SerializeField] private List<AbilityToolSkillCommandEntry> skillCommands = new List<AbilityToolSkillCommandEntry>();

    [Header("UI References")]
    [SerializeField] private Image abilityBaseImage;
    [SerializeField] private Image abilityPictureImage;

    [Header("Default Visual")]
    [SerializeField] private Sprite defaultPictureSprite;

    private SkillType lastPictureSkillType = SkillType.None;
    private Sprite lastPictureSprite;
    private AbilityToolManager owner;

    public SkillType SkillType => skillType;
    public string DisplayName => displayName;
    public string Description => description;
    public int MaxLevel => maxLevel;
    public ProgressionCurve MoneyCurve => moneyCurve;
    public ProgressionCurve CarrotCurve => carrotCurve;
    public List<AbilityToolSkillCommandEntry> SkillCommands => skillCommands;
    public Vector2Int GridPosition => gridPosition;
    public RectTransform RectTransform => transform as RectTransform;
    public List<AbilityToolParentLink> ParentLinks => parentLinks;

    public void BindOwner(AbilityToolManager _owner)
    {
        owner = _owner;
    }

    public void ApplyDefinition(AbilityNodeDefinitionJson _definition, SkillType _skillType, float _gridCellSize)
    {
        if (_definition == null)
            return;

        skillType = _skillType;
        displayName = _definition.displayName;
        description = _definition.description;
        gridPosition = new Vector2Int(_definition.gridX, _definition.gridY);
        parentLinks.Clear();

        ApplyAnchoredPosition(_gridCellSize);
        gameObject.name = $"AbilityToolNode_{skillType}_{gridPosition.x}_{gridPosition.y}";
    }

    public void ApplyLogicData(int _maxLevel, ProgressionCurve _moneyCurve, ProgressionCurve _carrotCurve, List<AbilityToolSkillCommandEntry> _skillCommands)
    {
        maxLevel = Mathf.Max(_maxLevel, 1);
        moneyCurve = CloneCurve(_moneyCurve);
        carrotCurve = CloneCurve(_carrotCurve);
        skillCommands = CloneSkillCommands(_skillCommands);
    }

    public bool HasPictureRefreshRequest(Sprite _expectedSprite)
    {
        Sprite resolvedSprite = _expectedSprite != null ? _expectedSprite : defaultPictureSprite;
        return lastPictureSkillType != skillType || lastPictureSprite != resolvedSprite;
    }

    public void SetPicture(Sprite _sprite)
    {
        lastPictureSkillType = skillType;
        lastPictureSprite = _sprite != null ? _sprite : defaultPictureSprite;

        if (abilityPictureImage == null)
            return;

        abilityPictureImage.sprite = lastPictureSprite;
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

    // 이동 모드에서 선택된 노드인지 파란색 테두리로 표시한다.
    public void SetMoveSelectedVisual(bool _selected)
    {
        if (abilityBaseImage == null)
            return;

        abilityBaseImage.color = _selected ? MoveSelectedBaseColor : DefaultBaseColor;
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

    private ProgressionCurve CloneCurve(ProgressionCurve _curve)
    {
        return new ProgressionCurve
        {
            type = _curve.type,
            baseValue = _curve.baseValue,
            manualValues = _curve.manualValues != null
                ? new List<float>(_curve.manualValues)
                : new List<float>()
        };
    }

    private List<AbilityToolSkillCommandEntry> CloneSkillCommands(List<AbilityToolSkillCommandEntry> _skillCommands)
    {
        List<AbilityToolSkillCommandEntry> result = new List<AbilityToolSkillCommandEntry>();
        if (_skillCommands == null)
            return result;

        for (int i = 0; i < _skillCommands.Count; i++)
        {
            AbilityToolSkillCommandEntry command = _skillCommands[i];
            if (command == null)
                continue;

            result.Add(new AbilityToolSkillCommandEntry
            {
                skillCommandType = command.skillCommandType,
                amountCurve = CloneCurve(command.amountCurve)
            });
        }

        return result;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowToolTip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideToolTip(this);
    }
}
