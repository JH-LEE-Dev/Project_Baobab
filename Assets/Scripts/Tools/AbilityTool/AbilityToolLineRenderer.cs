using System;
using System.Collections.Generic;
using UnityEngine;

public class AbilityToolLineRenderer
{
    private readonly List<AbilityLine> spawnedLines = new List<AbilityLine>();
    private readonly List<AbilityToolLineConnection> lineConnections = new List<AbilityToolLineConnection>();

    private RectTransform abilityBackground;
    private RectTransform moveTarget;
    private RectTransform lineParent;
    private AbilityLine abilityLinePrefab;
    private Canvas rootCanvas;
    private float gridCellSize;
    private int activeLineCount;
    private Func<float, int, bool, Sprite> straightSpriteResolver;
    private Func<float, int, int, Sprite> diagonalSpriteResolver;

    public void Initialize(
        RectTransform _abilityBackground,
        RectTransform _moveTarget,
        RectTransform _lineParent,
        AbilityLine _abilityLinePrefab,
        Canvas _rootCanvas,
        float _gridCellSize,
        Func<float, int, bool, Sprite> _straightSpriteResolver,
        Func<float, int, int, Sprite> _diagonalSpriteResolver)
    {
        abilityBackground = _abilityBackground;
        moveTarget = _moveTarget;
        lineParent = _lineParent;
        abilityLinePrefab = _abilityLinePrefab;
        rootCanvas = _rootCanvas;
        gridCellSize = _gridCellSize;
        straightSpriteResolver = _straightSpriteResolver;
        diagonalSpriteResolver = _diagonalSpriteResolver;
    }

    // 현재 툴 노드들의 부모 연결 정보를 읽어 라인 연결 목록을 다시 만든다.
    public void RebuildConnections(IReadOnlyList<AbilityToolNode> _nodes)
    {
        lineConnections.Clear();

        if (_nodes == null)
            return;

        for (int i = 0; i < _nodes.Count; i++)
        {
            AbilityToolNode childNode = _nodes[i];
            if (childNode == null)
                continue;

            List<AbilityToolParentLink> parentLinks = childNode.ParentLinks;
            if (parentLinks == null)
                continue;

            for (int linkIndex = 0; linkIndex < parentLinks.Count; linkIndex++)
            {
                AbilityToolParentLink link = parentLinks[linkIndex];
                if (link == null || link.parentNode == null)
                    continue;

                lineConnections.Add(new AbilityToolLineConnection(
                    link.parentNode,
                    childNode,
                    link.usePivot,
                    link.pivotGrid));
            }
        }
    }

    // 현재 줌 상태 기준으로 툴 라인을 다시 배치한다.
    public void RefreshLines(float _currentZoom)
    {
        if (abilityBackground == null || abilityLinePrefab == null)
            return;

        activeLineCount = 0;
        RectTransform targetParent = lineParent != null ? lineParent : abilityBackground;
        int segmentSize = GetActiveSegmentSize(_currentZoom);

        for (int i = 0; i < lineConnections.Count; i++)
        {
            AbilityToolLineConnection connection = lineConnections[i];
            if (connection.ParentNode == null || connection.ChildNode == null)
                continue;

            if (ShouldCullLineConnection(connection, targetParent, segmentSize))
                continue;

            BuildLineSegments(connection, targetParent, _currentZoom, segmentSize);
        }

        HideUnusedLines();
    }

    private void BuildLineSegments(AbilityToolLineConnection _connection, RectTransform _targetParent, float _currentZoom, int _segmentSize)
    {
        if (_connection.HasPivot)
        {
            Vector2 startCenter = GetNodeCenterInRectangle(_connection.ParentNode.RectTransform, _targetParent);
            Vector2 pivotCenter = GetGridPointCenterInRectangle(_connection.PivotGrid, _targetParent);
            Vector2 endCenter = GetNodeCenterInRectangle(_connection.ChildNode.RectTransform, _targetParent);

            BuildLineSegmentPath(_connection.ParentNode.GridPosition, _connection.PivotGrid, startCenter, pivotCenter, _targetParent, _currentZoom, _segmentSize, true, false);
            BuildLineSegmentPath(_connection.PivotGrid, _connection.ChildNode.GridPosition, pivotCenter, endCenter, _targetParent, _currentZoom, _segmentSize, true, true);
            return;
        }

        Vector2 directStartCenter = GetNodeCenterInRectangle(_connection.ParentNode.RectTransform, _targetParent);
        Vector2 directEndCenter = GetNodeCenterInRectangle(_connection.ChildNode.RectTransform, _targetParent);
        BuildLineSegmentPath(_connection.ParentNode.GridPosition, _connection.ChildNode.GridPosition, directStartCenter, directEndCenter, _targetParent, _currentZoom, _segmentSize, false, false);
    }

    private void BuildLineSegmentPath(
        Vector2Int _startGrid,
        Vector2Int _endGrid,
        Vector2 _startCenter,
        Vector2 _endCenter,
        RectTransform _targetParent,
        float _currentZoom,
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

        if (isHorizontal || isVertical)
        {
            Sprite straightSprite = straightSpriteResolver?.Invoke(_currentZoom, _segmentSize, isHorizontal);
            if (straightSprite == null)
                return;

            BuildStretchedStraightLine(_startCenter, _endCenter, _targetParent, isHorizontal, straightSprite, _hasCornerAnchor, _isStartCornerAnchor);
            return;
        }

        Sprite diagonalSprite = diagonalSpriteResolver?.Invoke(_currentZoom, stepX, stepY);
        if (diagonalSprite == null)
            return;

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
            line.Setup(diagonalSprite, position);
            line.MoveBehindSiblings();
        }
    }

    private void BuildStretchedStraightLine(
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

            float length = Mathf.Round(baseLength + 1f);
            bool anchorAtStart = _isHorizontal
                ? farCenter.x >= pivotCenter.x
                : farCenter.y >= pivotCenter.y;
            Vector2 anchoredCornerPosition = SnapToPixel(pivotCenter - (axisDirection * 1f));

            AbilityLine anchoredLine = GetOrCreatePooledLine(_targetParent);
            anchoredLine.SetupAnchoredSize(_sprite, anchoredCornerPosition, _isHorizontal, length, anchorAtStart);
            anchoredLine.MoveBehindSiblings();
            return;
        }

        baseLength = _isHorizontal
            ? Mathf.Abs(snappedEnd.x - snappedStart.x)
            : Mathf.Abs(snappedEnd.y - snappedStart.y);

        if (baseLength <= 0.001f)
            return;

        float centerLength = Mathf.Round(baseLength + 2f);
        Vector2 center = SnapToPixel((snappedStart + snappedEnd) * 0.5f);

        AbilityLine line = GetOrCreatePooledLine(_targetParent);
        line.SetupScaled(_sprite, center, _isHorizontal, centerLength);
        line.MoveBehindSiblings();
    }

    private int GetActiveSegmentSize(float _currentZoom)
    {
        float projectedGridSize = gridCellSize * _currentZoom;
        return projectedGridSize >= 16f ? 8 : 4;
    }

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

    private Vector2 SnapToPixel(Vector2 _position)
    {
        return new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }

    private bool ShouldCullLineConnection(AbilityToolLineConnection _connection, RectTransform _targetParent, int _segmentSize)
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

        if (maxX < viewRect.xMin - margin) return true;
        if (minX > viewRect.xMax + margin) return true;
        if (maxY < viewRect.yMin - margin) return true;
        if (minY > viewRect.yMax + margin) return true;

        return false;
    }

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

    private void HideUnusedLines()
    {
        for (int i = activeLineCount; i < spawnedLines.Count; i++)
        {
            if (spawnedLines[i] != null)
                spawnedLines[i].Hide();
        }
    }
}

public class AbilityToolLineConnection
{
    public AbilityToolNode ParentNode { get; }
    public AbilityToolNode ChildNode { get; }
    public bool HasPivot { get; }
    public Vector2Int PivotGrid { get; }

    public AbilityToolLineConnection(AbilityToolNode _parentNode, AbilityToolNode _childNode, bool _hasPivot, Vector2Int _pivotGrid)
    {
        ParentNode = _parentNode;
        ChildNode = _childNode;
        HasPivot = _hasPivot;
        PivotGrid = _pivotGrid;
    }
}
