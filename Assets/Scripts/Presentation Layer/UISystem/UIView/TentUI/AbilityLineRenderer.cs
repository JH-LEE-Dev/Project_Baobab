using System;
using System.Collections.Generic;
using UnityEngine;

public class AbilityLineRenderer
{
    private const float StraightLineOverlap = 1f;

    private readonly Dictionary<AbilityLineSegmentSpriteType, Sprite> lineSpriteMap = new Dictionary<AbilityLineSegmentSpriteType, Sprite>();
    private readonly List<AbilityLineConnection> lineConnections = new List<AbilityLineConnection>();
    private readonly List<AbilityLine> spawnedLines = new List<AbilityLine>();

    private RectTransform abilityBackground;
    private RectTransform moveTarget;
    private RectTransform lineParent;
    private AbilityLine abilityLinePrefab;
    private Canvas rootCanvas;
    private float gridCellSize;
    private int activeLineCount;
    private Func<SkillType, Color> lineColorResolver;

    // 라인 렌더러가 참조해야 하는 UI 루트와 콜백을 등록한다.
    public void Initialize(
        RectTransform _abilityBackground,
        RectTransform _moveTarget,
        RectTransform _lineParent,
        AbilityLine _abilityLinePrefab,
        Canvas _rootCanvas,
        float _gridCellSize,
        Func<SkillType, Color> _lineColorResolver)
    {
        abilityBackground = _abilityBackground;
        moveTarget = _moveTarget;
        lineParent = _lineParent;
        abilityLinePrefab = _abilityLinePrefab;
        rootCanvas = _rootCanvas;
        gridCellSize = _gridCellSize;
        lineColorResolver = _lineColorResolver;
    }

    // 인스펙터에서 연결된 라인 스프라이트를 타입별 조회 맵으로 캐시한다.
    public void CacheLineSpriteBindings(List<AbilityLineSegmentSpriteBinding> _lineSpriteBindings)
    {
        lineSpriteMap.Clear();

        if (_lineSpriteBindings == null)
            return;

        for (int i = 0; i < _lineSpriteBindings.Count; i++)
        {
            AbilityLineSegmentSpriteBinding binding = _lineSpriteBindings[i];
            if (binding == null || binding.sprite == null)
                continue;

            lineSpriteMap[binding.lineType] = binding.sprite;
        }
    }

    // 현재 노드 구조를 바탕으로 부모-자식 연결 리스트를 다시 만든다.
    public void RebuildConnections(
        List<AbilityNode> _spawnedNodes,
        Dictionary<SkillType, AbilityNode> _spawnedNodeMap,
        Dictionary<SkillType, AbilityNodeDefinitionJson> _nodeDefinitionMap)
    {
        lineConnections.Clear();

        if (_spawnedNodes == null)
            return;

        for (int i = 0; i < _spawnedNodes.Count; i++)
        {
            AbilityNode childNode = _spawnedNodes[i];
            if (childNode == null)
                continue;

            SkillType[] parents = childNode.ParentSkillTypes;
            for (int parentIndex = 0; parentIndex < parents.Length; parentIndex++)
            {
                if (_spawnedNodeMap.TryGetValue(parents[parentIndex], out AbilityNode parentNode) == false)
                    continue;

                AbilityParentJson route = FindParentLineRoute(_nodeDefinitionMap, childNode.SkillType, parents[parentIndex]);
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
    }

    // 현재 줌 비율을 기준으로 모든 라인 풀 오브젝트를 재배치한다.
    public void RefreshLines(float _currentZoom)
    {
        if (abilityLinePrefab == null || abilityBackground == null)
            return;

        activeLineCount = 0;

        RectTransform targetParent = lineParent != null ? lineParent : abilityBackground;
        int segmentSize = GetActiveSegmentSize(_currentZoom);

        for (int i = 0; i < lineConnections.Count; i++)
        {
            if (lineConnections[i].ParentNode.gameObject.activeSelf == false || lineConnections[i].ChildNode.gameObject.activeSelf == false)
                continue;

            if (ShouldCullLineConnection(lineConnections[i], targetParent, segmentSize))
                continue;

            BuildLineSegments(lineConnections[i], targetParent, segmentSize);
        }

        HideUnusedLines();
    }

    // 자식 정의에서 특정 부모 연결에 대한 꺾임 경로 설정을 찾는다.
    private AbilityParentJson FindParentLineRoute(
        Dictionary<SkillType, AbilityNodeDefinitionJson> _nodeDefinitionMap,
        SkillType _childSkillType,
        SkillType _parentSkillType)
    {
        if (_nodeDefinitionMap.TryGetValue(_childSkillType, out AbilityNodeDefinitionJson childDefinition) == false)
            return null;

        return childDefinition.FindParentLineRoute(_parentSkillType);
    }

    // 한 부모-자식 연결을 실제 라인 세그먼트/직선으로 분해한다.
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

    // 두 점 사이 한 구간을 가로/세로/대각선 규칙에 맞춰 라인으로 그린다.
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

        float primaryDistance = Mathf.Abs(_endCenter.x - _startCenter.x);
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
            line.Setup(sprite, position, GetLineColor(_childSkillType));
            line.MoveBehindSiblings();
        }
    }

    // 가로/세로 라인은 width, height 기반 선분 하나로 표현한다.
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
        Color lineColor = GetLineColor(_childSkillType);
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
            anchoredLine.SetupAnchoredSize(_sprite, anchoredCornerPosition, _isHorizontal, length, anchorAtStart, lineColor);
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
        line.SetupScaled(_sprite, center, _isHorizontal, centerLength, lineColor);
        line.MoveBehindSiblings();
    }

    // 현재 줌 비율에 따라 4px 또는 8px 대각선 세그먼트를 고른다.
    private int GetActiveSegmentSize(float _currentZoom)
    {
        float projectedGridSize = gridCellSize * _currentZoom;
        return projectedGridSize >= 16f ? 8 : 4;
    }

    // 방향과 세그먼트 크기에 맞는 라인 스프라이트 타입을 찾는다.
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

    // 노드 중심점을 대상 RectTransform 기준 로컬 좌표로 변환한다.
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

    // 그리드 좌표를 대상 RectTransform 기준 로컬 중심점으로 변환한다.
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

    // 정수 픽셀 좌표 기준으로 위치를 스냅한다.
    private Vector2 SnapToPixel(Vector2 _position)
    {
        return new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }

    // 화면 밖으로 충분히 벗어난 라인 연결은 이번 프레임 렌더링을 생략한다.
    private bool ShouldCullLineConnection(AbilityLineConnection _connection, RectTransform _targetParent, int _segmentSize)
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
        float margin = _segmentSize * 2f;

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

    // 풀에서 재사용 가능한 라인 오브젝트를 가져오거나 새로 생성한다.
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
            line = UnityEngine.Object.Instantiate(abilityLinePrefab, _targetParent, false);
            spawnedLines.Add(line);
        }

        activeLineCount++;
        return line;
    }

    // 이번 프레임에 사용하지 않은 라인 오브젝트는 숨긴다.
    private void HideUnusedLines()
    {
        for (int i = activeLineCount; i < spawnedLines.Count; i++)
        {
            if (spawnedLines[i] != null)
                spawnedLines[i].Hide();
        }
    }

    // 자식 노드의 현재 상태를 기준으로 라인 색상을 결정한다.
    private Color GetLineColor(SkillType _childSkillType)
    {
        if (lineColorResolver == null)
            return Color.white;

        return lineColorResolver(_childSkillType);
    }
}
