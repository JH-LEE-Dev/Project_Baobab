using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UI_TentAbilityComponent : MonoBehaviour
{
    private const float DefaultZoom = 1f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 1f;
    private const float ZoomStep = 0.1f;
    private const float ZoomFollowSpeed = 18f;
    private const float ToolTipSpacing = 32f;
    private static readonly Color CanApplyNodeColor = new Color32(88, 215, 242, 255);
    private static readonly Color CompletedColor = new Color32(84, 216, 106, 255);
    private static readonly Color CannotApplyNodeColor = new Color32(185, 74, 66, 255);
    private static readonly Color DefaultLineColor = new Color32(255, 255, 255, 255);

    private ISkillSystemProvider skillSystemProvider;
    private Canvas rootCanvas;
    private bool isDragging;
    private bool hasZoomFocus;
    private Vector2 previousMousePosition;
    private Vector2 zoomFocusScreenPosition;
    private float currentZoom = DefaultZoom;
    private float targetZoom = DefaultZoom;

    private readonly Dictionary<SkillType, AbilityNodeDefinitionJson> nodeDefinitionMap = new Dictionary<SkillType, AbilityNodeDefinitionJson>();
    private readonly List<SkillType> nodeBuildOrder = new List<SkillType>();
    private readonly Dictionary<SkillType, Sprite> pictureSpriteMap = new Dictionary<SkillType, Sprite>();
    private readonly List<AbilityNode> spawnedNodes = new List<AbilityNode>();
    private readonly Dictionary<SkillType, AbilityNode> spawnedNodeMap = new Dictionary<SkillType, AbilityNode>();
    private readonly Queue<AbilityNode> nodePool = new Queue<AbilityNode>();
    private readonly AbilityLineRenderer lineRenderer = new AbilityLineRenderer();

    private bool hasBuiltNodes;
    private bool hasPrewarmedNodePool;
    private bool lineLayoutDirty;
    private bool toolTipLayoutDirty;
    private AbilityNode currentToolTipNode;
    private AbilityToolTip toolTipInstance;

    [Header("UI References")]
    [SerializeField] private RectTransform abilityBackground;
    [SerializeField] private RectTransform moveTarget;

    [Header("Ability Node Setup")]
    [SerializeField] private AbilityNode abilityNodePrefab;
    [SerializeField] private AbilityLine abilityLinePrefab;
    [SerializeField] private TextAsset abilityNodeJson;
    [SerializeField] private float gridCellSize = 32f;
    [SerializeField] private int prewarmNodePoolCount = 64;
    [SerializeField] private List<AbilityPictureBinding> pictureBindings = new List<AbilityPictureBinding>();
    [SerializeField] private List<AbilityLineSegmentSpriteBinding> lineSpriteBindings = new List<AbilityLineSegmentSpriteBinding>();
    [SerializeField] private RectTransform lineParent;

    [Header("ToolTip Setup")]
    [SerializeField] private AbilityToolTip toolTipPrefab;
    [SerializeField] private RectTransform toolTipParent;



#region Initializing

    public void Initialize(ISkillSystemProvider _skillSystemProvider)
    {
        skillSystemProvider = _skillSystemProvider;
        rootCanvas = GetComponentInParent<Canvas>();
        lineRenderer.Initialize(abilityBackground, moveTarget, lineParent, abilityLinePrefab, rootCanvas, gridCellSize, GetLineColor, ShouldDrawLineAboveDefault);
        CachePictureBindings();
        CacheLineSpriteBindings();
        LoadNodeDefinitions();
        PrewarmNodePool();
        EnsureToolTipInstance();
        Close();
    }


        // 인스펙터에서 연결한 스킬별 아이콘 스프라이트를 캐시한다.
    private void CachePictureBindings()
    {
        pictureSpriteMap.Clear();

        for (int i = 0; i < pictureBindings.Count; i++)
        {
            AbilityPictureBinding binding = pictureBindings[i];
            if (binding == null || binding.skillType == SkillType.None || binding.sprite == null)
                continue;

            pictureSpriteMap[binding.skillType] = binding.sprite;
        }
    }

    // 인스펙터에서 연결한 라인 세그먼트 스프라이트를 타입별 조회 맵으로 캐시한다.
    private void CacheLineSpriteBindings()
    {
        lineRenderer.CacheLineSpriteBindings(lineSpriteBindings);
    }

    // JSON 노드 정의를 읽어 SkillType 기준 조회 맵으로 만든다.
    private void LoadNodeDefinitions()
    {
        nodeDefinitionMap.Clear();
        nodeBuildOrder.Clear();

        if (abilityNodeJson == null || string.IsNullOrWhiteSpace(abilityNodeJson.text))
            return;

        AbilityNodeDatabaseJson databaseJson = JsonUtility.FromJson<AbilityNodeDatabaseJson>(abilityNodeJson.text);
        if (databaseJson == null || databaseJson.nodes == null)
            return;

        for (int i = 0; i < databaseJson.nodes.Length; i++)
        {
            AbilityNodeDefinitionJson nodeDefinition = databaseJson.nodes[i];
            if (nodeDefinition == null)
                continue;

            if (Enum.TryParse(nodeDefinition.skillType, true, out SkillType parsedSkillType) == false)
                continue;

            nodeDefinitionMap[parsedSkillType] = nodeDefinition;
            nodeBuildOrder.Add(parsedSkillType);
        }
    }

    // 툴팁 프리팹 인스턴스를 한 번만 생성하고 계속 재사용한다.
    private void EnsureToolTipInstance()
    {
        if (toolTipInstance != null || toolTipPrefab == null || abilityBackground == null)
            return;

        RectTransform parent = toolTipParent != null ? toolTipParent : abilityBackground;
        toolTipInstance = Instantiate(toolTipPrefab, parent);

        RectTransform toolTipRoot = toolTipInstance.GetRoot();
        if (toolTipRoot != null)
        {
            toolTipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            toolTipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            toolTipRoot.pivot = new Vector2(0.5f, 0.5f);
        }

        toolTipInstance.HideImmediately();
    }



#endregion


#region Default

    // 능력 화면을 열고 초기 노드 빌드와 가시성 갱신을 수행한다.
    public void Open()
    {
        if (abilityBackground == null)
            return;

        abilityBackground.gameObject.SetActive(true);
        BuildNodesIfNeeded();
        SyncNodeLevelsFromProvider();
        RefreshNodeVisibility();
        RefreshNodeAvailabilityVisuals();
        ResetView();
    }

    private void BuildNodesIfNeeded()
    {
        if (hasBuiltNodes || moveTarget == null || abilityNodePrefab == null)
            return;

        for (int i = 0; i < nodeBuildOrder.Count; i++)
        {
            CreateNode(nodeBuildOrder[i]);
        }
        BuildLines();
        hasBuiltNodes = true;
    }

        // 부모자식 관계를 따라 라인 연결 정보를 만든다.
    private void BuildLines()
    {
        lineRenderer.RebuildConnections(spawnedNodes, spawnedNodeMap, nodeDefinitionMap);
        RefreshLines();
    }

    // 자식 노드 정의에서 특정 부모와 연결될 라인 경로 오버라이드를 찾는다.
    // 스킬 타입 하나를 기준으로 JSON 정의를 읽고 노드 프리팹을 만든다.
    private AbilityNode CreateNode(SkillType _skillType)
    {
        if (nodeDefinitionMap.TryGetValue(_skillType, out AbilityNodeDefinitionJson nodeDefinition) == false)
            return null;

        AbilityNode node = GetNodeFromPool();
        node.gameObject.name = $"AbilityNode_{_skillType}";
        node.BindOwner(this);
        node.ApplyDefinition(nodeDefinition, _skillType, ResolvePicture(_skillType), gridCellSize);
        spawnedNodes.Add(node);
        spawnedNodeMap[_skillType] = node;

        return node;
    }

    // 특성창 오픈 순간의 Instantiate 부하를 줄이기 위해 노드 프리팹을 미리 비활성 상태로 준비한다.
    private void PrewarmNodePool()
    {
        if (hasPrewarmedNodePool || moveTarget == null || abilityNodePrefab == null)
            return;

        int targetCount = Mathf.Max(prewarmNodePoolCount, 0);
        for (int i = nodePool.Count; i < targetCount; i++)
        {
            AbilityNode pooledNode = Instantiate(abilityNodePrefab, moveTarget);
            pooledNode.BindOwner(this);
            pooledNode.gameObject.SetActive(false);
            nodePool.Enqueue(pooledNode);
        }

        hasPrewarmedNodePool = true;
    }

    // 풀에 준비된 노드를 꺼내고, 부족하면 새로 만들어 반환한다.
    private AbilityNode GetNodeFromPool()
    {
        AbilityNode node = null;
        while (nodePool.Count > 0 && node == null)
            node = nodePool.Dequeue();

        if (node == null)
            node = Instantiate(abilityNodePrefab, moveTarget);

        RectTransform rectTransform = node.RectTransform;
        if (rectTransform != null && rectTransform.parent != moveTarget)
            rectTransform.SetParent(moveTarget, false);

        node.gameObject.SetActive(true);
        return node;
    }

    // 스킬 타입에 대응되는 아이콘 스프라이트를 반환한다.
    private Sprite ResolvePicture(SkillType _skillType)
    {
        if (pictureSpriteMap.TryGetValue(_skillType, out Sprite sprite))
            return sprite;

        return null;
    }

    // 능력 화면 기본 위치와 줌 상태를 초기화한다.
    private void ResetView()
    {
        if (moveTarget == null)
            return;

        isDragging = false;
        hasZoomFocus = false;
        currentZoom = DefaultZoom;
        targetZoom = DefaultZoom;
        moveTarget.anchoredPosition = Vector2.zero;
        moveTarget.localScale = Vector3.one * currentZoom;
        MarkViewLayoutDirty();
    }

    // 능력 화면을 닫고 입력 상태와 툴팁을 정리한다.
    public void Close()
    {
        isDragging = false;
        hasZoomFocus = false;
        currentToolTipNode = null;

        if (toolTipInstance != null)
            toolTipInstance.HideImmediately();

        if (abilityBackground != null)
            abilityBackground.gameObject.SetActive(false);
    }

    public void Refresh()
    {
        BuildNodesIfNeeded();
        SyncNodeLevelsFromProvider();
        RefreshNodeVisibility();
        RefreshNodeAvailabilityVisuals();

        if (currentToolTipNode != null)
            ShowToolTip(currentToolTipNode);
    }


#endregion


#region ToolTip

    // 노드 기준 좌우 규칙과 일정 거리 규칙에 맞춰 툴팁을 표시한다.
    public void ShowToolTip(AbilityNode _node)
    {
        if (_node == null || abilityBackground == null)
            return;

        bool shouldPlayShowMotion = currentToolTipNode != _node ||
                                    toolTipInstance == null ||
                                    toolTipInstance.gameObject.activeSelf == false;
        currentToolTipNode = _node;
        EnsureToolTipInstance();
        if (toolTipInstance == null)
            return;

        RectTransform nodeRect = _node.RectTransform;
        if (nodeRect == null)
            return;

        SkillInfo skillInfo = GetSkillInfo(_node.SkillType);
        string costText = BuildToolTipCostText(skillInfo, out MoneyType costMoneyType);
        toolTipInstance.SetContent(
            BuildToolTipTitleAndLevelText(_node, skillInfo),
            _node.GetToolTipDescriptionText(),
            costText,
            costMoneyType);

        toolTipInstance.Show();
        Vector2 toolTipSize = toolTipInstance.GetSize();

        Vector3[] worldCorners = new Vector3[4];
        nodeRect.GetWorldCorners(worldCorners);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            abilityBackground,
            RectTransformUtility.WorldToScreenPoint(null, worldCorners[0]),
            null,
            out Vector2 localBottomLeft);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            abilityBackground,
            RectTransformUtility.WorldToScreenPoint(null, worldCorners[2]),
            null,
            out Vector2 localTopRight);

        Vector2 nodeCenter = (localBottomLeft + localTopRight) * 0.5f;
        float nodeWidth = Mathf.Abs(localTopRight.x - localBottomLeft.x);
        bool placeOnRight = nodeCenter.x < 0f;
        float direction = placeOnRight ? 1f : -1f;

        float x = nodeCenter.x + direction * ((nodeWidth * 0.5f) + ToolTipSpacing + (toolTipSize.x * 0.5f));
        float y = nodeCenter.y;

        toolTipInstance.SetAnchoredPosition(new Vector2(x, y));

        if (shouldPlayShowMotion)
            toolTipInstance.PlayShowMotion();
    }

    // 상위 스킬 시스템에서 툴팁에 필요한 레벨/비용 정보를 가져온다.
    private SkillInfo GetSkillInfo(SkillType _skillType)
    {
        if (skillSystemProvider != null)
            return skillSystemProvider.GetSkillInfo(_skillType);

        return new SkillInfo
        {
            skillType = _skillType,
            currentLevel = 0,
            maxLevel = 0,
            moneyType = MoneyType.None,
            nextCost = 0
        };
    }

    // 툴팁 제목과 현재/최대 레벨 문자열을 만든다.
    private string BuildToolTipTitleAndLevelText(AbilityNode _node, SkillInfo _skillInfo)
    {
        int maxLevel = Mathf.Max(_skillInfo.maxLevel, 0);
        return $"{_node.DisplayName}\n레벨 : {_skillInfo.currentLevel} / {maxLevel}";
    }

    // 상위 스킬 정보 기준으로 다음 강화 비용 문자열을 만든다.
    private string BuildToolTipCostText(SkillInfo _skillInfo)
    {
        if (_skillInfo.maxLevel > 0 && _skillInfo.currentLevel >= _skillInfo.maxLevel)
            return "최대 레벨";

        if (_skillInfo.nextCost <= 0 || _skillInfo.moneyType == MoneyType.None || _skillInfo.moneyType == MoneyType.Max)
            return "무료";

        return $"{_skillInfo.nextCost} {_skillInfo.moneyType}";
    }

    private string BuildToolTipCostText(SkillInfo _skillInfo, out MoneyType _costMoneyType)
    {
        _costMoneyType = MoneyType.None;

        if (_skillInfo.maxLevel > 0 && _skillInfo.currentLevel >= _skillInfo.maxLevel)
            return BuildToolTipCostText(_skillInfo);

        if (_skillInfo.nextCost <= 0 || _skillInfo.moneyType == MoneyType.None || _skillInfo.moneyType == MoneyType.Max)
            return BuildToolTipCostText(_skillInfo);

        _costMoneyType = _skillInfo.moneyType;
        return _skillInfo.nextCost.ToString();
    }

    // 현재 노드에 대한 툴팁을 숨긴다.
    public void HideToolTip(AbilityNode _node)
    {
        if (currentToolTipNode != null && _node != currentToolTipNode)
            return;

        currentToolTipNode = null;

        if (toolTipInstance != null)
            toolTipInstance.Hide();
    }

        // 툴팁이 표시 중이면 현재 호버 노드 기준으로 위치를 계속 갱신한다.
    private void UpdateToolTipPosition()
    {
        if (currentToolTipNode == null || toolTipInstance == null || toolTipInstance.gameObject.activeSelf == false)
            return;

        ShowToolTip(currentToolTipNode);
    }


#endregion


#region Ticking

    // 능력 화면이 열려 있는 동안 팬, 줌, 라인 재배치, 툴팁 추적을 수행함
    public void Tick()
    {
        if (abilityBackground == null || abilityBackground.gameObject.activeSelf == false || moveTarget == null)
            return;

        // 드래그 이동
        bool viewChanged = false;

        viewChanged |= HandlePan();
        // 줌 기능
        HandleZoom();
        // 줌 애니메이션 기능
        viewChanged |= UpdateZoomAnimation();
        // Line 스냅 및 재구성
        if (viewChanged)
            MarkViewLayoutDirty();

        RefreshLinesIfNeeded();
        // 툴팁 포지션 스냅
        UpdateToolTipPositionIfNeeded();
    }


    // 마우스 드래그로 능력 컨텐츠를 이동시킨다.
    private bool HandlePan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        bool canDrag =
            mouse.leftButton.isPressed ||
            mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed;

        Vector2 currentMousePosition = mouse.position.ReadValue();

        if (canDrag == false)
        {
            isDragging = false;
            return false;
        }

        if (isDragging == false)
        {
            isDragging = true;
            previousMousePosition = currentMousePosition;
            return false;
        }

        Vector2 delta = currentMousePosition - previousMousePosition;
        previousMousePosition = currentMousePosition;

        if (delta.sqrMagnitude <= 0.0001f)
            return false;

        float scaleFactor = 1f;
        if (rootCanvas != null)
            scaleFactor = Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.0001f);

        moveTarget.anchoredPosition += delta / scaleFactor;
        return true;
    }

    // 마우스 휠 입력으로 목표 줌 값을 갱신한다.
    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
            return;

        zoomFocusScreenPosition = mouse.position.ReadValue();
        hasZoomFocus = true;
        targetZoom += Mathf.Sign(scrollY) * ZoomStep;
        targetZoom = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);
    }

    // 목표 줌 값을 따라가며 현재 줌을 부드럽게 갱신한다.
    private bool UpdateZoomAnimation()
    {
        if (Mathf.Approximately(currentZoom, targetZoom))
            return false;

        float previousZoom = currentZoom;
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, 1f - Mathf.Exp(-ZoomFollowSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(currentZoom - targetZoom) < 0.001f)
            currentZoom = targetZoom;

        if (Mathf.Approximately(previousZoom, currentZoom) == false)
            ApplyZoomAroundFocus(previousZoom, currentZoom);

        moveTarget.localScale = Vector3.one * currentZoom;
        return Mathf.Approximately(previousZoom, currentZoom) == false;
    }

    // 마우스가 가리키는 지점을 기준으로 확대/축소가 일어나도록 위치를 보정한다.
    private void ApplyZoomAroundFocus(float _previousZoom, float _currentZoom)
    {
        if (moveTarget == null || hasZoomFocus == false)
            return;

        Camera eventCamera = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = rootCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moveTarget,
            zoomFocusScreenPosition,
            eventCamera,
            out Vector2 localPointBeforeScale) == false)
            return;

        moveTarget.anchoredPosition += localPointBeforeScale * (_previousZoom - _currentZoom);
    }


    // 현재 보이는 노드 상태를 기준으로 라인 세그먼트를 다시 배치한다.
    private void RefreshLines()
    {
        lineRenderer.RefreshLines(currentZoom);
        lineLayoutDirty = false;
    }

    private void MarkViewLayoutDirty()
    {
        lineLayoutDirty = true;
        toolTipLayoutDirty = true;
    }

    private void RefreshLinesIfNeeded()
    {
        if (lineLayoutDirty == false)
            return;

        RefreshLines();
    }

    private void UpdateToolTipPositionIfNeeded()
    {
        if (toolTipLayoutDirty == false)
            return;

        UpdateToolTipPosition();
        toolTipLayoutDirty = false;
    }

    // 한 부모-자식 연결에 대해 4px 또는 8px 세그먼트를 반복 배치한다.
    // 두 점 사이의 한 구간에 대해 4px 또는 8px 세그먼트를 반복 배치한다.
    // 가로 또는 세로 선은 하나의 선분 오브젝트를 늘려서 표현한다.
    // 부모와 연결된 라인 색상은 도착 자식 노드의 현재 찍기 가능 여부를 따른다.
    private Color GetLineColor(SkillType _childSkillType)
    {
        if (spawnedNodeMap.TryGetValue(_childSkillType, out AbilityNode childNode) == false)
            return DefaultLineColor;

        return childNode.CompletedVisual ? CompletedColor : DefaultLineColor;
    }

    // MaxLevel까지 찍힌 노드로 들어오는 라인만 일반 라인보다 위에 그린다.
    private bool ShouldDrawLineAboveDefault(SkillType _childSkillType)
    {
        return spawnedNodeMap.TryGetValue(_childSkillType, out AbilityNode childNode) &&
               childNode.CompletedVisual;
    }

    // 현재 줌 비율에 따라 사용할 라인 세그먼트 크기를 선택한다.
    // 방향과 세그먼트 크기에 맞는 라인 스프라이트 타입을 반환한다.
    // 노드 중심점을 대상 RectTransform의 로컬 좌표로 변환한다.
    // 그리드 좌표 하나를 대상 RectTransform 기준 로컬 중심 좌표로 변환한다.

    // 정수 픽셀 좌표에 맞춰 위치를 스냅한다.
    // 풀에서 사용 가능한 라인을 가져오거나 새로 만든다.
    // 이번 프레임에 사용하지 않은 라인은 숨긴다.
#endregion


#region For System

    // 노드 클릭 시 상위 시스템에 전달할 요청 함수다.
    public void RequestNodeLevelUp(AbilityNode _node)
    {
        TryRequestNodeLevelUp(_node);
    }

    public bool TryRequestNodeLevelUp(AbilityNode _node)
    {
        if (_node == null)
            return false;

        SkillType requestedSkillType = _node.SkillType;

        return OnAbilityLevelUpRequested(requestedSkillType);
    }


    // 상위 로직에 어떤 스킬을 찍으려는지 전달하는 자리다.
    private bool OnAbilityLevelUpRequested(SkillType _skillType)
    {
        if (skillSystemProvider == null)
        {
            Debug.LogWarning($"SkillSystemProvider is null. Request skipped: {_skillType}");
            return false;
        }

        AbilityLevelUpRejectReason reason = skillSystemProvider.TryApplySkill(_skillType);

        if (reason == AbilityLevelUpRejectReason.Pass)
        {
            OnAbilityLevelUpApproved(_skillType);
            return true;
        }
        else
        {
            OnAbilityLevelUpRejected(_skillType, NormalizeRejectReason(reason));
            return false;
        }
    }

    // 상위 시스템의 세부 실패 사유를 UI에서 사용할 공통 사유로 정리한다.
    private AbilityLevelUpRejectReason NormalizeRejectReason(AbilityLevelUpRejectReason _reason)
    {
        Debug.Log("RejectReason : " + _reason);

        switch (_reason)
        {
            case AbilityLevelUpRejectReason.NotEnoughMoney:
            case AbilityLevelUpRejectReason.NotEnoughCarrot:
                return AbilityLevelUpRejectReason.NotEnoughMoney;
            default:
                return _reason;
        }
    }

    // 해당 특성 찍기 승인
    public void OnAbilityLevelUpApproved(SkillType _skillType)
    {
        if (spawnedNodeMap.TryGetValue(_skillType, out AbilityNode node) == false)
            return;

        bool wasLockedByLevel = node.IsUnlockedByLevel() == false;
        SyncNodeLevelsFromProvider();

        // 해금되는 순간임
        if (wasLockedByLevel && node.IsUnlockedByLevel())
            RefreshNodeVisibility();
        else
            RefreshLines();

        RefreshNodeAvailabilityVisuals();

        if (currentToolTipNode == node)
            ShowToolTip(node);
    }

    // 상위 로직에서 거절 및 이유 (연출을 위함임)
    public void OnAbilityLevelUpRejected(SkillType _skillType, AbilityLevelUpRejectReason _rejectReason)
    {
        if (spawnedNodeMap.TryGetValue(_skillType, out AbilityNode node) == false)
            return;

        Debug.Log($"Reject Ability Unlock: {_skillType}, Reason: {_rejectReason}");

        if (currentToolTipNode == node)
            ShowToolTip(node);
    }



    // 부모 레벨 기준으로 자식 노드와 라인의 노출 상태를 갱신한다.
    // 상위 스킬 시스템이 가진 실제 레벨 상태를 UI 노드에 반영한다.
    private void SyncNodeLevelsFromProvider()
    {
        if (skillSystemProvider == null)
            return;

        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            if (node == null)
                continue;

            if (skillSystemProvider.IsApplied(node.SkillType, out int currentLevel))
                node.SetCurrentLevel(currentLevel);
            else
                node.SetCurrentLevel(0);
        }
    }

    private void RefreshNodeVisibility()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            bool isVisible = ShouldShowNode(node);
            node.gameObject.SetActive(isVisible);

            if (isVisible == false && currentToolTipNode == node)
                HideToolTip(node);
        }

        RefreshLines();
    }

    // 현재 보이는 노드를 순회하며 찍기 가능 여부를 확인하고 테두리/배경 색을 갱신한다.
    private void RefreshNodeAvailabilityVisuals()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            if (node == null || node.gameObject.activeSelf == false)
                continue;

            bool canApply = false;
            bool isCompleted = false;
            if (skillSystemProvider != null)
            {
                AbilityLevelUpRejectReason reason = skillSystemProvider.CanApplySkill(node.SkillType);
                canApply = reason == AbilityLevelUpRejectReason.Pass;
                isCompleted = reason == AbilityLevelUpRejectReason.MaxLevel;
            }

            Color baseColor = CannotApplyNodeColor;
            if (isCompleted)
                baseColor = CompletedColor;
            else if (canApply)
                baseColor = CanApplyNodeColor;

            node.ApplyVisualState(
                baseColor,
                canApply,
                isCompleted);
        }

        RefreshLines();
    }

    // 부모가 모두 1레벨 이상이면 자식 노드를 표시한다. 부모가 없으면 시작 노드로 본다.
    private bool ShouldShowNode(AbilityNode _node)
    {
        if (_node == null)
            return false;

        if (skillSystemProvider != null)
            return ShouldShowNodeByProvider(_node);

        return ShouldShowNodeByVisualConnection(_node);
    }

    // 상위 스킬 시스템의 선행 조건 상태를 기준으로 노드 노출 여부를 판단한다.
    private bool ShouldShowNodeByProvider(AbilityNode _node)
    {
        List<SkillNode> prerequisites = skillSystemProvider.GetPrerequisites(_node.SkillType);
        if (prerequisites == null || prerequisites.Count == 0)
            return true;

        for (int i = 0; i < prerequisites.Count; i++)
        {
            SkillNode prerequisite = prerequisites[i];
            if (prerequisite == null || prerequisite.bApplied == false)
                return false;
        }

        return true;
    }

    // Provider가 아직 연결되지 않은 테스트 상황에서는 UI 연결 정보의 루트 노드만 노출한다.
    private bool ShouldShowNodeByVisualConnection(AbilityNode _node)
    {
        SkillType[] parents = _node.ParentSkillTypes;
        if (parents == null || parents.Length == 0)
            return true;

        return false;
    }

#endregion




    // 라인 연결의 바운드가 화면 영역 밖에 충분히 벗어나 있으면 이번 프레임 렌더링을 생략한다.

}

public class AbilityLineConnection
{
    public AbilityNode ParentNode { get; }
    public AbilityNode ChildNode { get; }
    public bool HasPivot { get; }
    public Vector2Int PivotGrid { get; }

    public AbilityLineConnection(AbilityNode _parentNode, AbilityNode _childNode)
    {
        ParentNode = _parentNode;
        ChildNode = _childNode;
        HasPivot = false;
        PivotGrid = Vector2Int.zero;
    }

    public AbilityLineConnection(AbilityNode _parentNode, AbilityNode _childNode, bool _hasPivot, Vector2Int _pivotGrid)
    {
        ParentNode = _parentNode;
        ChildNode = _childNode;
        HasPivot = _hasPivot;
        PivotGrid = _pivotGrid;
    }
}
