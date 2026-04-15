using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UI_TentAbilityComponent : MonoBehaviour
{
    private const float DefaultZoom = 1f;
    private const float MinZoom = 0.3f;
    private const float MaxZoom = 1f;
    private const float ZoomStep = 0.1f;
    private const float ZoomFollowSpeed = 18f;
    private const float ToolTipSpacing = 32f;
    private const float StraightLineOverlap = 1f;

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
    private readonly Dictionary<AbilityLineSegmentSpriteType, Sprite> lineSpriteMap = new Dictionary<AbilityLineSegmentSpriteType, Sprite>();
    private readonly List<AbilityNode> spawnedNodes = new List<AbilityNode>();
    private readonly Dictionary<SkillType, AbilityNode> spawnedNodeMap = new Dictionary<SkillType, AbilityNode>();
    private readonly List<AbilityLineConnection> lineConnections = new List<AbilityLineConnection>();
    private readonly List<AbilityLine> spawnedLines = new List<AbilityLine>();

    private bool hasBuiltNodes;
    private int activeLineCount;
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
        CachePictureBindings();
        CacheLineSpriteBindings();
        LoadNodeDefinitions();
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
        lineSpriteMap.Clear();

        for (int i = 0; i < lineSpriteBindings.Count; i++)
        {
            AbilityLineSegmentSpriteBinding binding = lineSpriteBindings[i];
            if (binding == null || binding.sprite == null)
                continue;

            lineSpriteMap[binding.lineType] = binding.sprite;
        }
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

        toolTipInstance.Hide();
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
        RefreshNodeVisibility();
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
        lineConnections.Clear();

        if (moveTarget == null)
            return;

        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode childNode = spawnedNodes[i];
            SkillType[] parents = childNode.ParentSkillTypes;

            for (int parentIndex = 0; parentIndex < parents.Length; parentIndex++)
            {
                if (spawnedNodeMap.TryGetValue(parents[parentIndex], out AbilityNode parentNode) == false)
                    continue;

                AbilityParentLineRouteJson route = FindParentLineRoute(childNode.SkillType, parents[parentIndex]);
                if (route != null && route.usePivot)
                {
                    lineConnections.Add(new AbilityLineConnection(
                        parentNode,
                        childNode,
                        true,
                        new Vector2Int(route.pivotX, route.pivotY)));
                }
                else
                {
                    lineConnections.Add(new AbilityLineConnection(parentNode, childNode));
                }
            }
        }

        RefreshLines();
    }

    // 자식 노드 정의에서 특정 부모와 연결될 라인 경로 오버라이드를 찾는다.
    private AbilityParentLineRouteJson FindParentLineRoute(SkillType _childSkillType, SkillType _parentSkillType)
    {
        if (nodeDefinitionMap.TryGetValue(_childSkillType, out AbilityNodeDefinitionJson childDefinition) == false)
            return null;

        AbilityParentLineRouteJson[] routes = childDefinition.parentLineRoutes;
        if (routes == null || routes.Length == 0)
            return null;

        for (int i = 0; i < routes.Length; i++)
        {
            AbilityParentLineRouteJson route = routes[i];
            if (route == null || string.IsNullOrWhiteSpace(route.parentSkillType))
                continue;

            if (Enum.TryParse(route.parentSkillType, true, out SkillType parsedParentSkillType) == false)
                continue;

            if (parsedParentSkillType == _parentSkillType)
                return route;
        }

        return null;
    }

    // 스킬 타입 하나를 기준으로 JSON 정의를 읽고 노드 프리팹을 만든다.
    private AbilityNode CreateNode(SkillType _skillType)
    {
        if (nodeDefinitionMap.TryGetValue(_skillType, out AbilityNodeDefinitionJson nodeDefinition) == false)
            return null;

        AbilityNode node = Instantiate(abilityNodePrefab, moveTarget);
        node.gameObject.name = $"AbilityNode_{_skillType}";
        node.BindOwner(this);
        node.ApplyDefinition(nodeDefinition, _skillType, ResolvePicture(_skillType), gridCellSize);
        spawnedNodes.Add(node);
        spawnedNodeMap[_skillType] = node;

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
    }

    // 능력 화면을 닫고 입력 상태와 툴팁을 정리한다.
    public void Close()
    {
        isDragging = false;
        hasZoomFocus = false;
        currentToolTipNode = null;

        if (toolTipInstance != null)
            toolTipInstance.Hide();

        if (abilityBackground != null)
            abilityBackground.gameObject.SetActive(false);
    }


#endregion


#region ToolTip

    // 노드 기준 좌우 규칙과 일정 거리 규칙에 맞춰 툴팁을 표시한다.
    public void ShowToolTip(AbilityNode _node)
    {
        if (_node == null || abilityBackground == null)
            return;

        currentToolTipNode = _node;
        EnsureToolTipInstance();
        if (toolTipInstance == null)
            return;

        RectTransform nodeRect = _node.RectTransform;
        if (nodeRect == null)
            return;

        toolTipInstance.SetContent(
            _node.GetToolTipTitleAndLevelText(),
            _node.GetToolTipDescriptionText(),
            _node.GetToolTipCostText());

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
        HandlePan();
        // 줌 기능
        HandleZoom();
        // 줌 애니메이션 기능
        UpdateZoomAnimation();
        // Line 스냅 및 재구성
        RefreshLines();
        // 툴팁 포지션 스냅
        UpdateToolTipPosition();
    }


    // 마우스 드래그로 능력 컨텐츠를 이동시킨다.
    private void HandlePan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool canDrag =
            mouse.leftButton.isPressed ||
            mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed;

        Vector2 currentMousePosition = mouse.position.ReadValue();

        if (canDrag == false)
        {
            isDragging = false;
            return;
        }

        if (isDragging == false)
        {
            isDragging = true;
            previousMousePosition = currentMousePosition;
            return;
        }

        Vector2 delta = currentMousePosition - previousMousePosition;
        previousMousePosition = currentMousePosition;

        float scaleFactor = 1f;
        if (rootCanvas != null)
            scaleFactor = Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.0001f);

        moveTarget.anchoredPosition += delta / scaleFactor;
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
    private void UpdateZoomAnimation()
    {
        float previousZoom = currentZoom;
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, 1f - Mathf.Exp(-ZoomFollowSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(currentZoom - targetZoom) < 0.001f)
            currentZoom = targetZoom;

        if (Mathf.Approximately(previousZoom, currentZoom) == false)
            ApplyZoomAroundFocus(previousZoom, currentZoom);

        moveTarget.localScale = Vector3.one * currentZoom;
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
        if (abilityLinePrefab == null || abilityBackground == null)
            return;

        activeLineCount = 0;

        RectTransform targetParent = lineParent != null ? lineParent : abilityBackground;
        int segmentSize = GetActiveSegmentSize();

        for (int i = 0; i < lineConnections.Count; i++)
        {
            if (lineConnections[i].ParentNode.gameObject.activeSelf == false || lineConnections[i].ChildNode.gameObject.activeSelf == false)
                continue;

            if (ShouldCullLineConnection(lineConnections[i], targetParent))
                continue;

            BuildLineSegments(lineConnections[i], targetParent, segmentSize);
        }

        HideUnusedLines();
    }

    // 한 부모-자식 연결에 대해 4px 또는 8px 세그먼트를 반복 배치한다.
    private void BuildLineSegments(AbilityLineConnection _connection, RectTransform _targetParent, int _segmentSize)
    {
        if (_connection.ParentNode == null || _connection.ChildNode == null)
            return;

        if (_connection.HasPivot)
        {
            Vector2Int pivotGrid = _connection.PivotGrid;
            Vector2 startCenter = GetNodeCenterInRectangle(_connection.ParentNode.RectTransform, _targetParent);
            Vector2 pivotCenter = GetGridPointCenterInRectangle(pivotGrid, _targetParent);
            Vector2 endCenter = GetNodeCenterInRectangle(_connection.ChildNode.RectTransform, _targetParent);

            BuildLineSegmentPath(_connection.ParentNode.SkillType, _connection.ChildNode.SkillType, "A", _connection.ParentNode.GridPosition, pivotGrid, startCenter, pivotCenter, _targetParent, _segmentSize, true, false);
            BuildLineSegmentPath(_connection.ParentNode.SkillType, _connection.ChildNode.SkillType, "B", pivotGrid, _connection.ChildNode.GridPosition, pivotCenter, endCenter, _targetParent, _segmentSize, true, true);
            return;
        }

        Vector2 directStartCenter = GetNodeCenterInRectangle(_connection.ParentNode.RectTransform, _targetParent);
        Vector2 directEndCenter = GetNodeCenterInRectangle(_connection.ChildNode.RectTransform, _targetParent);
        BuildLineSegmentPath(_connection.ParentNode.SkillType, _connection.ChildNode.SkillType, string.Empty, _connection.ParentNode.GridPosition, _connection.ChildNode.GridPosition, directStartCenter, directEndCenter, _targetParent, _segmentSize, false, false);
    }

    // 두 점 사이의 한 구간에 대해 4px 또는 8px 세그먼트를 반복 배치한다.
    private void BuildLineSegmentPath(
        SkillType _parentSkillType,
        SkillType _childSkillType,
        string _pathSuffix,
        Vector2Int _startGrid,
        Vector2Int _endGrid,
        Vector2 _startCenter,
        Vector2 _endCenter,
        RectTransform _targetParent,
        int _segmentSize,
        bool _hasCornerAnchor,
        bool _isStartCornerAnchor)
    {
        int dx = _endGrid.x - _startGrid.x;
        int dy = _endGrid.y - _startGrid.y;
        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        bool isHorizontal = absDx > 0 && dy == 0;
        bool isVertical = absDy > 0 && dx == 0;
        bool isDiagonal = absDx == absDy && absDx > 0;

        if (isHorizontal == false && isVertical == false && isDiagonal == false)
            return;

        AbilityLineSegmentSpriteType spriteType = GetSegmentSpriteType(stepX, stepY, _segmentSize);
        if (lineSpriteMap.TryGetValue(spriteType, out Sprite sprite) == false)
            return;

        Vector2 delta = _endCenter - _startCenter;

        if (delta.magnitude <= 0.001f)
            return;

        if (isHorizontal || isVertical)
        {
            BuildStretchedStraightLine(_parentSkillType, _childSkillType, _pathSuffix, _startCenter, _endCenter, _targetParent, isHorizontal, sprite, _hasCornerAnchor, _isStartCornerAnchor);
            return;
        }

        float primaryDistance = isHorizontal
            ? Mathf.Abs(_endCenter.x - _startCenter.x)
            : Mathf.Abs(_endCenter.y - _startCenter.y);

        if (isDiagonal)
            primaryDistance = Mathf.Abs(_endCenter.x - _startCenter.x);

        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(primaryDistance / _segmentSize));
        float coveredDistance = segmentCount * _segmentSize;
        float remainingDistance = Mathf.Max(0f, primaryDistance - coveredDistance);
        float leadingOffset = remainingDistance * 0.5f;

        Vector2 segmentAxisStep = new Vector2(stepX * _segmentSize, stepY * _segmentSize);
        Vector2 firstSegmentCenter = _startCenter + new Vector2(stepX, stepY) * (leadingOffset + (_segmentSize * 0.5f));
        firstSegmentCenter = SnapToPixel(firstSegmentCenter);

        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            Vector2 position = firstSegmentCenter + (segmentAxisStep * segmentIndex);

            AbilityLine line = GetOrCreatePooledLine(_targetParent);
            line.gameObject.name = string.IsNullOrEmpty(_pathSuffix)
                ? $"Line_{_parentSkillType}_{_childSkillType}_{segmentIndex}"
                : $"Line_{_parentSkillType}_{_childSkillType}_{_pathSuffix}_{segmentIndex}";
            line.Setup(sprite, position);
            line.MoveBehindSiblings();
        }
    }

    // 가로 또는 세로 선은 하나의 선분 오브젝트를 늘려서 표현한다.
    private void BuildStretchedStraightLine(
        SkillType _parentSkillType,
        SkillType _childSkillType,
        string _pathSuffix,
        Vector2 _startCenter,
        Vector2 _endCenter,
        RectTransform _targetParent,
        bool _isHorizontal,
        Sprite _sprite,
        bool _hasCornerAnchor,
        bool _isStartCornerAnchor)
    {
        Vector2 snappedStart = SnapToPixel(_startCenter);
        Vector2 snappedEnd = SnapToPixel(_endCenter);
        float baseLength;

        if (_hasCornerAnchor)
        {
            Vector2 pivotCenter = _isStartCornerAnchor ? snappedStart : snappedEnd;
            Vector2 farCenter = _isStartCornerAnchor ? snappedEnd : snappedStart;
            Vector2 axisDirection = (farCenter - pivotCenter).normalized;
            baseLength = _isHorizontal
                ? Mathf.Abs(farCenter.x - pivotCenter.x)
                : Mathf.Abs(farCenter.y - pivotCenter.y);

            if (baseLength <= 0.001f)
                return;

            float length = Mathf.Round(baseLength + StraightLineOverlap);
            bool anchorAtStart = _isHorizontal
                ? farCenter.x >= pivotCenter.x
                : farCenter.y >= pivotCenter.y;
            Vector2 anchoredCornerPosition = SnapToPixel(pivotCenter - (axisDirection * StraightLineOverlap));

            AbilityLine anchoredLine = GetOrCreatePooledLine(_targetParent);
            anchoredLine.gameObject.name = string.IsNullOrEmpty(_pathSuffix)
                ? $"Line_{_parentSkillType}_{_childSkillType}"
                : $"Line_{_parentSkillType}_{_childSkillType}_{_pathSuffix}";
            anchoredLine.SetupAnchoredSize(_sprite, anchoredCornerPosition, _isHorizontal, length, anchorAtStart);
            anchoredLine.MoveBehindSiblings();
            return;
        }

        baseLength = _isHorizontal
            ? Mathf.Abs(snappedEnd.x - snappedStart.x)
            : Mathf.Abs(snappedEnd.y - snappedStart.y);

        if (baseLength <= 0.001f)
            return;

        float centerLength = Mathf.Round(baseLength + (StraightLineOverlap * 2f));
        Vector2 center = SnapToPixel((snappedStart + snappedEnd) * 0.5f);

        AbilityLine line = GetOrCreatePooledLine(_targetParent);
        line.gameObject.name = string.IsNullOrEmpty(_pathSuffix)
            ? $"Line_{_parentSkillType}_{_childSkillType}"
            : $"Line_{_parentSkillType}_{_childSkillType}_{_pathSuffix}";
        line.SetupScaled(_sprite, center, _isHorizontal, centerLength);
        line.MoveBehindSiblings();
    }

    // 현재 줌 비율에 따라 사용할 라인 세그먼트 크기를 선택한다.
    private int GetActiveSegmentSize()
    {
        float projectedGridSize = gridCellSize * currentZoom;
        return projectedGridSize >= 16f ? 8 : 4;
    }

    // 방향과 세그먼트 크기에 맞는 라인 스프라이트 타입을 반환한다.
    private AbilityLineSegmentSpriteType GetSegmentSpriteType(int _stepX, int _stepY, int _segmentSize)
    {
        if (_segmentSize == 8)
        {
            if (_stepX != 0 && _stepY == 0)
                return AbilityLineSegmentSpriteType.Row8;

            if (_stepX == 0 && _stepY != 0)
                return AbilityLineSegmentSpriteType.Col8;

            return _stepX == _stepY
                ? AbilityLineSegmentSpriteType.DiagSWNE8
                : AbilityLineSegmentSpriteType.DiagSENW8;
        }

        if (_stepX != 0 && _stepY == 0)
            return AbilityLineSegmentSpriteType.Row4;

        if (_stepX == 0 && _stepY != 0)
            return AbilityLineSegmentSpriteType.Col4;

        return _stepX == _stepY
            ? AbilityLineSegmentSpriteType.DiagSWNE4
            : AbilityLineSegmentSpriteType.DiagSENW4;
    }

    // 노드 중심점을 대상 RectTransform의 로컬 좌표로 변환한다.
    private Vector2 GetNodeCenterInRectangle(RectTransform _nodeRect, RectTransform _targetRectangle)
    {
        Vector3[] corners = new Vector3[4];
        _nodeRect.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;

        Camera eventCamera = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = rootCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _targetRectangle,
            RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter),
            eventCamera,
            out Vector2 localPoint);

        return SnapToPixel(localPoint);
    }

    // 그리드 좌표 하나를 대상 RectTransform 기준 로컬 중심 좌표로 변환한다.
    private Vector2 GetGridPointCenterInRectangle(Vector2Int _gridPoint, RectTransform _targetRectangle)
    {
        if (moveTarget == null)
            return Vector2.zero;

        Vector3 worldPoint = moveTarget.TransformPoint(new Vector3(_gridPoint.x * gridCellSize, _gridPoint.y * gridCellSize, 0f));

        Camera eventCamera = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = rootCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _targetRectangle,
            RectTransformUtility.WorldToScreenPoint(eventCamera, worldPoint),
            eventCamera,
            out Vector2 localPoint);

        return SnapToPixel(localPoint);
    }


    // 정수 픽셀 좌표에 맞춰 위치를 스냅한다.
    private Vector2 SnapToPixel(Vector2 _position)
    {
        return new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }

    // 풀에서 사용 가능한 라인을 가져오거나 새로 만든다.
    private AbilityLine GetOrCreatePooledLine(RectTransform _targetParent)
    {
        AbilityLine line;

        if (activeLineCount < spawnedLines.Count)
        {
            line = spawnedLines[activeLineCount];
            RectTransform lineRect = line.transform as RectTransform;
            if (lineRect != null && lineRect.parent != _targetParent)
                lineRect.SetParent(_targetParent, false);
        }
        else
        {
            line = Instantiate(abilityLinePrefab, _targetParent, false);
            spawnedLines.Add(line);
        }

        activeLineCount++;
        return line;
    }

    // 이번 프레임에 사용하지 않은 라인은 숨긴다.
    private void HideUnusedLines()
    {
        for (int i = activeLineCount; i < spawnedLines.Count; i++)
        {
            if (spawnedLines[i] != null)
                spawnedLines[i].Hide();
        }
    }

#endregion


#region For System

    public enum AbilityLevelUpRejectReason
    {
        None,
        NotEnoughMoney,
        MaxLevel,
        PrerequisiteNotMet,
    }

    // 노드 클릭 시 상위 시스템에 전달할 요청 함수다.
    public void RequestNodeLevelUp(AbilityNode _node)
    {
        if (_node == null)
            return;

        if (CanRequestLevelUp(_node, out AbilityLevelUpRejectReason rejectReason) == false)
        {
            OnAbilityLevelUpRejected(_node.SkillType, rejectReason);
            return;
        }

        SkillType requestedSkillType = _node.SkillType;

        OnAbilityLevelUpRequested(requestedSkillType);

        // 이건 임시.
        OnAbilityLevelUpApproved(requestedSkillType);
    }

    // 부모 조건과 최대 레벨 여부를 기준으로 레벨업 요청 가능 여부를 판단한다.
    private bool CanRequestLevelUp(AbilityNode _node, out AbilityLevelUpRejectReason _rejectReason)
    {
        _rejectReason = AbilityLevelUpRejectReason.None;

        if (_node.CurrentLevel >= _node.MaxLevel)
        {
            _rejectReason = AbilityLevelUpRejectReason.MaxLevel;
            return false;
        }

        SkillType[] parents = _node.ParentSkillTypes;
        for (int i = 0; i < parents.Length; i++)
        {
            if (spawnedNodeMap.TryGetValue(parents[i], out AbilityNode parentNode) == false)
            {
                _rejectReason = AbilityLevelUpRejectReason.PrerequisiteNotMet;
                return false;
            }

            if (parentNode.IsUnlockedByLevel() == false)
            {
                _rejectReason = AbilityLevelUpRejectReason.PrerequisiteNotMet;
                return false;
            }
        }

        return true;
    }


    // 상위 로직에 어떤 스킬을 찍으려는지 전달하는 자리다.
    private void OnAbilityLevelUpRequested(SkillType _skillType)
    {
        // _skillType 이놈 찍은거다.
        Debug.Log($"Request Ability Unlock: {_skillType}");
    }

    // 해당 특성 찍기 승인
    public void OnAbilityLevelUpApproved(SkillType _skillType)
    {
        if (spawnedNodeMap.TryGetValue(_skillType, out AbilityNode node) == false)
            return;

        bool wasLockedByLevel = node.IsUnlockedByLevel() == false;
        node.ApplyApprovedLevelUp();

        // 해금되는 순간임
        if (wasLockedByLevel && node.IsUnlockedByLevel())
            RefreshNodeVisibility();
        else
            RefreshLines();

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

    // 부모가 모두 1레벨 이상이면 자식 노드를 표시한다. 부모가 없으면 시작 노드로 본다.
    private bool ShouldShowNode(AbilityNode _node)
    {
        SkillType[] parents = _node.ParentSkillTypes;
        if (parents == null || parents.Length == 0)
            return true;

        for (int i = 0; i < parents.Length; i++)
        {
            if (spawnedNodeMap.TryGetValue(parents[i], out AbilityNode parentNode) == false)
                return false;

            if (parentNode.IsUnlockedByLevel() == false)
                return false;
        }

        return true;
    }

#endregion


#region Optimization


    // 라인 연결의 바운드가 화면 영역 밖에 충분히 벗어나 있으면 이번 프레임 렌더링을 생략한다.
    private bool ShouldCullLineConnection(AbilityLineConnection _connection, RectTransform _targetParent)
    {
        Vector2 startCenter = GetNodeCenterInRectangle(_connection.ParentNode.RectTransform, _targetParent);
        Vector2 endCenter = GetNodeCenterInRectangle(_connection.ChildNode.RectTransform, _targetParent);

        float minX = Mathf.Min(startCenter.x, endCenter.x);
        float maxX = Mathf.Max(startCenter.x, endCenter.x);
        float minY = Mathf.Min(startCenter.y, endCenter.y);
        float maxY = Mathf.Max(startCenter.y, endCenter.y);

        if (_connection.HasPivot)
        {
            Vector2 pivotCenter = GetGridPointCenterInRectangle(_connection.PivotGrid, _targetParent);
            minX = Mathf.Min(minX, pivotCenter.x);
            maxX = Mathf.Max(maxX, pivotCenter.x);
            minY = Mathf.Min(minY, pivotCenter.y);
            maxY = Mathf.Max(maxY, pivotCenter.y);
        }

        Rect viewRect = abilityBackground.rect;
        float margin = GetActiveSegmentSize() * 2f;

        if (maxX < viewRect.xMin - margin)
            return true;

        if (minX > viewRect.xMax + margin)
            return true;

        if (maxY < viewRect.yMin - margin)
            return true;

        if (minY > viewRect.yMax + margin)
            return true;

        return false;
    }

#endregion

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
