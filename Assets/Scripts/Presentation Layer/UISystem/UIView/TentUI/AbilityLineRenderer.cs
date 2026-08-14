using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AbilityLineRenderer
{
    private const float StraightLineOverlap = 1f;

    private readonly Dictionary<AbilityLineSegmentSpriteType, Sprite> lineSpriteMap = new Dictionary<AbilityLineSegmentSpriteType, Sprite>();
    private readonly Dictionary<AbilityLineSegmentSpriteType, AbilityLineGraphic> lineGraphicMap = new Dictionary<AbilityLineSegmentSpriteType, AbilityLineGraphic>();
    private readonly Dictionary<AbilityLineSegmentSpriteType, List<AbilityLineMeshQuad>> lineQuadMap = new Dictionary<AbilityLineSegmentSpriteType, List<AbilityLineMeshQuad>>();
    private readonly Dictionary<SkillType, float> lineRevealProgressMap = new Dictionary<SkillType, float>();
    private readonly List<AbilityLineConnection> lineConnections = new List<AbilityLineConnection>();

    private RectTransform abilityBackground;
    private RectTransform moveTarget;
    private RectTransform lineParent;
    private Canvas rootCanvas;
    private float gridCellSize;
    private Material lineMaterial;
    private bool hasConfiguredLineLayer;
    private Func<SkillType, Color> lineColorResolver;
    private Func<SkillType, int> lineShineColorIndexResolver;

    public void Initialize(
        RectTransform _abilityBackground,
        RectTransform _moveTarget,
        RectTransform _lineParent,
        Canvas _rootCanvas,
        float _gridCellSize,
        Material _lineMaterial,
        Func<SkillType, Color> _lineColorResolver,
        Func<SkillType, int> _lineShineColorIndexResolver)
    {
        abilityBackground = _abilityBackground;
        moveTarget = _moveTarget;
        lineParent = _lineParent;
        rootCanvas = _rootCanvas;
        gridCellSize = Mathf.Max(_gridCellSize, 0.0001f);
        lineMaterial = _lineMaterial;
        lineColorResolver = _lineColorResolver;
        lineShineColorIndexResolver = _lineShineColorIndexResolver;

        Canvas targetCanvas = rootCanvas != null && rootCanvas.rootCanvas != null
            ? rootCanvas.rootCanvas
            : rootCanvas;
        if (targetCanvas != null)
            targetCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;

        EnsureLineLayer();
        EnsureLineGraphics();
    }

    public void CacheLineSpriteBindings(List<AbilityLineSegmentSpriteBinding> _lineSpriteBindings)
    {
        lineSpriteMap.Clear();

        if (_lineSpriteBindings != null)
        {
            for (int i = 0; i < _lineSpriteBindings.Count; i++)
            {
                AbilityLineSegmentSpriteBinding binding = _lineSpriteBindings[i];
                if (binding == null || binding.sprite == null)
                    continue;

                lineSpriteMap[binding.lineType] = binding.sprite;
            }
        }

        EnsureLineGraphics();
    }

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

                AbilityParentJson route = FindParentLineRoute(
                    _nodeDefinitionMap,
                    childNode.SkillType,
                    parents[parentIndex]);

                lineConnections.Add(route != null && route.usePivot
                    ? new AbilityLineConnection(parentNode, childNode, true, new Vector2Int(route.pivotX, route.pivotY))
                    : new AbilityLineConnection(parentNode, childNode));
            }
        }
    }

    public void SetLineRevealProgress(SkillType _childSkillType, float _progress)
    {
        lineRevealProgressMap[_childSkillType] = Mathf.Clamp01(_progress);
    }

    public void ClearLineRevealProgress(SkillType _childSkillType)
    {
        lineRevealProgressMap.Remove(_childSkillType);
    }

    public void InvalidateVisualData()
    {
    }

    public void RefreshLines(float _currentZoom)
    {
        EnsureLineLayer();
        EnsureLineGraphics();

        if (abilityBackground == null || lineParent == null)
            return;

        ClearLineQuads();
        int segmentSize = GetActiveSegmentSize(_currentZoom);

        for (int i = 0; i < lineConnections.Count; i++)
        {
            AbilityLineConnection connection = lineConnections[i];
            if (connection.ParentNode == null || connection.ChildNode == null)
                continue;

            if (connection.ParentNode.IsProgressionVisible == false ||
                connection.ChildNode.IsProgressionVisible == false)
                continue;

            BuildLineSegments(connection, segmentSize);
        }

        ApplyLineQuads();
    }

    private void EnsureLineLayer()
    {
        if (hasConfiguredLineLayer || abilityBackground == null || lineParent == null)
            return;

        if (lineParent.parent != abilityBackground)
            lineParent.SetParent(abilityBackground, false);

        lineParent.SetAsFirstSibling();
        lineParent.anchorMin = Vector2.zero;
        lineParent.anchorMax = Vector2.one;
        lineParent.pivot = new Vector2(0.5f, 0.5f);
        lineParent.offsetMin = Vector2.zero;
        lineParent.offsetMax = Vector2.zero;
        lineParent.localScale = Vector3.one;
        lineParent.localRotation = Quaternion.identity;

        AbilityLineGraphic legacyGraphic = lineParent.GetComponent<AbilityLineGraphic>();
        if (legacyGraphic != null)
            legacyGraphic.enabled = false;

        hasConfiguredLineLayer = true;
    }

    private void EnsureLineGraphics()
    {
        if (lineParent == null || lineSpriteMap.Count == 0)
            return;

        foreach (KeyValuePair<AbilityLineSegmentSpriteType, Sprite> pair in lineSpriteMap)
        {
            if (pair.Value == null)
                continue;

            if (lineGraphicMap.TryGetValue(pair.Key, out AbilityLineGraphic graphic) == false || graphic == null)
            {
                string objectName = $"AbilityLineBatch_{pair.Key}";
                Transform existingChild = lineParent.Find(objectName);
                if (existingChild != null)
                    graphic = existingChild.GetComponent<AbilityLineGraphic>();

                if (graphic == null)
                {
                    GameObject graphicObject = new GameObject(
                        objectName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(AbilityLineGraphic));
                    graphicObject.layer = lineParent.gameObject.layer;
                    graphicObject.transform.SetParent(lineParent, false);
                    graphic = graphicObject.GetComponent<AbilityLineGraphic>();
                }

                PrepareFullScreenRect(graphic.rectTransform);
                lineGraphicMap[pair.Key] = graphic;
            }

            graphic.raycastTarget = false;
            graphic.color = Color.white;
            graphic.material = lineMaterial;
            graphic.SetLineSprite(pair.Value);

            if (lineQuadMap.ContainsKey(pair.Key) == false)
                lineQuadMap[pair.Key] = new List<AbilityLineMeshQuad>();
        }
    }

    private static void PrepareFullScreenRect(RectTransform _rectTransform)
    {
        if (_rectTransform == null)
            return;

        _rectTransform.anchorMin = Vector2.zero;
        _rectTransform.anchorMax = Vector2.one;
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
        _rectTransform.localScale = Vector3.one;
        _rectTransform.localRotation = Quaternion.identity;
    }

    private void ClearLineQuads()
    {
        foreach (List<AbilityLineMeshQuad> quads in lineQuadMap.Values)
            quads.Clear();
    }

    private void ApplyLineQuads()
    {
        foreach (KeyValuePair<AbilityLineSegmentSpriteType, AbilityLineGraphic> pair in lineGraphicMap)
        {
            if (pair.Value == null)
                continue;

            if (lineQuadMap.TryGetValue(pair.Key, out List<AbilityLineMeshQuad> quads) == false)
            {
                pair.Value.SetQuads(null);
                pair.Value.enabled = false;
                continue;
            }

            pair.Value.SetQuads(quads);
            pair.Value.enabled = quads.Count > 0;
        }
    }

    private AbilityParentJson FindParentLineRoute(
        Dictionary<SkillType, AbilityNodeDefinitionJson> _nodeDefinitionMap,
        SkillType _childSkillType,
        SkillType _parentSkillType)
    {
        if (_nodeDefinitionMap.TryGetValue(_childSkillType, out AbilityNodeDefinitionJson childDefinition) == false)
            return null;

        return childDefinition.FindParentLineRoute(_parentSkillType);
    }

    private void BuildLineSegments(AbilityLineConnection _connection, int _segmentSize)
    {
        Vector2 startCenter = GetNodeCenterInRectangle(_connection.ParentNode.RectTransform);
        Vector2 endCenter = GetNodeCenterInRectangle(_connection.ChildNode.RectTransform);
        bool hasPivot = _connection.HasPivot;
        Vector2 pivotCenter = hasPivot
            ? GetGridPointCenterInRectangle(_connection.PivotGrid)
            : Vector2.zero;

        if (ShouldCullLineConnection(startCenter, endCenter, pivotCenter, hasPivot, _segmentSize))
            return;

        float connectionProgress = GetLineRevealProgress(_connection.ChildNode.SkillType);
        Color color = GetLineColor(_connection.ChildNode.SkillType);
        int shineColorIndex = GetLineShineColorIndex(_connection.ChildNode.SkillType);

        if (hasPivot)
        {
            float firstLength = Vector2.Distance(startCenter, pivotCenter);
            float secondLength = Vector2.Distance(pivotCenter, endCenter);
            float totalLength = firstLength + secondLength;
            float pivotProgress = totalLength > 0.0001f ? firstLength / totalLength : 0.5f;

            BuildLineSegmentPath(
                _connection.ParentNode.GridPosition,
                _connection.PivotGrid,
                startCenter,
                pivotCenter,
                _segmentSize,
                true,
                false,
                Mathf.Clamp01(connectionProgress * 2f),
                color,
                shineColorIndex,
                0f,
                pivotProgress);
            BuildLineSegmentPath(
                _connection.PivotGrid,
                _connection.ChildNode.GridPosition,
                pivotCenter,
                endCenter,
                _segmentSize,
                true,
                true,
                Mathf.Clamp01((connectionProgress - 0.5f) * 2f),
                color,
                shineColorIndex,
                pivotProgress,
                1f);
            return;
        }

        BuildLineSegmentPath(
            _connection.ParentNode.GridPosition,
            _connection.ChildNode.GridPosition,
            startCenter,
            endCenter,
            _segmentSize,
            false,
            false,
            connectionProgress,
            color,
            shineColorIndex,
            0f,
            1f);
    }

    private void BuildLineSegmentPath(
        Vector2Int _startGrid,
        Vector2Int _endGrid,
        Vector2 _startCenter,
        Vector2 _endCenter,
        int _segmentSize,
        bool _hasCornerAnchor,
        bool _isStartCornerAnchor,
        float _progress,
        Color _color,
        int _shineColorIndex,
        float _shineProgressStart,
        float _shineProgressEnd)
    {
        if (_progress <= 0f)
            return;

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
        if (lineSpriteMap.TryGetValue(spriteType, out Sprite sprite) == false || sprite == null)
            return;

        Vector2 delta = _endCenter - _startCenter;
        if (delta.sqrMagnitude <= 0.0001f)
            return;

        if (isHorizontal || isVertical)
        {
            BuildStretchedStraightLine(
                spriteType,
                sprite,
                _startCenter,
                _endCenter,
                isHorizontal,
                _hasCornerAnchor,
                _isStartCornerAnchor,
                _progress,
                _color,
                _shineColorIndex,
                _shineProgressStart,
                _shineProgressEnd);
            return;
        }

        float primaryDistance = Mathf.Abs(_endCenter.x - _startCenter.x);
        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(primaryDistance / _segmentSize));
        float coveredDistance = segmentCount * _segmentSize;
        float remainingDistance = Mathf.Max(0f, primaryDistance - coveredDistance);
        float leadingOffset = remainingDistance * 0.5f;
        Vector2 segmentAxisStep = new Vector2(stepX * _segmentSize, stepY * _segmentSize);
        Vector2 firstSegmentCenter = SnapToPixel(
            _startCenter + new Vector2(stepX, stepY) * (leadingOffset + _segmentSize * 0.5f));
        Vector2 nativeSize = GetSpriteNativeSize(sprite);

        int revealSegmentCount = Mathf.CeilToInt(segmentCount * Mathf.Clamp01(_progress));
        for (int segmentIndex = 0; segmentIndex < revealSegmentCount; segmentIndex++)
        {
            Vector2 position = firstSegmentCenter + segmentAxisStep * segmentIndex;
            AddCenteredQuad(
                spriteType,
                position,
                nativeSize,
                _color,
                _shineColorIndex,
                _startCenter,
                _endCenter,
                _shineProgressStart,
                _shineProgressEnd);
        }
    }

    private void BuildStretchedStraightLine(
        AbilityLineSegmentSpriteType _spriteType,
        Sprite _sprite,
        Vector2 _startCenter,
        Vector2 _endCenter,
        bool _isHorizontal,
        bool _hasCornerAnchor,
        bool _isStartCornerAnchor,
        float _progress,
        Color _color,
        int _shineColorIndex,
        float _shineProgressStart,
        float _shineProgressEnd)
    {
        _progress = Mathf.Clamp01(_progress);
        Vector2 snappedStart = SnapToPixel(_startCenter);
        Vector2 snappedEnd = SnapToPixel(_endCenter);

        if (_hasCornerAnchor)
        {
            Vector2 pivotCenter = _isStartCornerAnchor ? snappedStart : snappedEnd;
            Vector2 farCenter = _isStartCornerAnchor ? snappedEnd : snappedStart;
            Vector2 cornerAxisDirection = (farCenter - pivotCenter).normalized;
            float baseLength = _isHorizontal
                ? Mathf.Abs(farCenter.x - pivotCenter.x)
                : Mathf.Abs(farCenter.y - pivotCenter.y);
            if (baseLength <= 0.001f)
                return;

            float length = Mathf.Round((baseLength + StraightLineOverlap) * _progress);
            if (length <= 0f)
                return;

            bool anchorAtStart = _isHorizontal
                ? farCenter.x >= pivotCenter.x
                : farCenter.y >= pivotCenter.y;
            Vector2 anchoredPosition = SnapToPixel(
                pivotCenter - cornerAxisDirection * StraightLineOverlap);
            AddAnchoredStraightQuad(
                _spriteType,
                _sprite,
                anchoredPosition,
                _isHorizontal,
                length,
                anchorAtStart,
                _color,
                _shineColorIndex,
                snappedStart,
                snappedEnd,
                _shineProgressStart,
                _shineProgressEnd);
            return;
        }

        float directLength = _isHorizontal
            ? Mathf.Abs(snappedEnd.x - snappedStart.x)
            : Mathf.Abs(snappedEnd.y - snappedStart.y);
        if (directLength <= 0.001f)
            return;

        Vector2 axisDirection = (snappedEnd - snappedStart).normalized;
        float lineLength = Mathf.Round((directLength + StraightLineOverlap) * _progress);
        if (lineLength <= 0f)
            return;

        bool directAnchorAtStart = _isHorizontal
            ? snappedEnd.x >= snappedStart.x
            : snappedEnd.y >= snappedStart.y;
        Vector2 anchoredStartPosition = SnapToPixel(
            snappedStart - axisDirection * StraightLineOverlap);
        AddAnchoredStraightQuad(
            _spriteType,
            _sprite,
            anchoredStartPosition,
            _isHorizontal,
            lineLength,
            directAnchorAtStart,
            _color,
            _shineColorIndex,
            snappedStart,
            snappedEnd,
            _shineProgressStart,
            _shineProgressEnd);
    }

    private void AddCenteredQuad(
        AbilityLineSegmentSpriteType _spriteType,
        Vector2 _center,
        Vector2 _size,
        Color _color,
        int _shineColorIndex,
        Vector2 _pathStart,
        Vector2 _pathEnd,
        float _shineProgressStart,
        float _shineProgressEnd)
    {
        Vector2 halfSize = _size * 0.5f;
        Rect rect = Rect.MinMaxRect(
            _center.x - halfSize.x,
            _center.y - halfSize.y,
            _center.x + halfSize.x,
            _center.y + halfSize.y);
        AddQuad(
            _spriteType,
            rect,
            _color,
            _shineColorIndex,
            _pathStart,
            _pathEnd,
            _shineProgressStart,
            _shineProgressEnd);
    }

    private void AddAnchoredStraightQuad(
        AbilityLineSegmentSpriteType _spriteType,
        Sprite _sprite,
        Vector2 _anchoredPosition,
        bool _isHorizontal,
        float _length,
        bool _anchorAtStart,
        Color _color,
        int _shineColorIndex,
        Vector2 _pathStart,
        Vector2 _pathEnd,
        float _shineProgressStart,
        float _shineProgressEnd)
    {
        Vector2 nativeSize = GetSpriteNativeSize(_sprite);
        float length = Mathf.Max(Mathf.Round(_length), 1f);
        Rect rect;

        if (_isHorizontal)
        {
            float xMin = _anchorAtStart ? _anchoredPosition.x : _anchoredPosition.x - length;
            rect = new Rect(
                xMin,
                _anchoredPosition.y - nativeSize.y * 0.5f,
                length,
                nativeSize.y);
        }
        else
        {
            float yMin = _anchorAtStart ? _anchoredPosition.y : _anchoredPosition.y - length;
            rect = new Rect(
                _anchoredPosition.x - nativeSize.x * 0.5f,
                yMin,
                nativeSize.x,
                length);
        }

        AddQuad(
            _spriteType,
            rect,
            _color,
            _shineColorIndex,
            _pathStart,
            _pathEnd,
            _shineProgressStart,
            _shineProgressEnd);
    }

    private void AddQuad(
        AbilityLineSegmentSpriteType _spriteType,
        Rect _rect,
        Color _color,
        int _shineColorIndex,
        Vector2 _pathStart,
        Vector2 _pathEnd,
        float _shineProgressStart,
        float _shineProgressEnd)
    {
        if (lineQuadMap.TryGetValue(_spriteType, out List<AbilityLineMeshQuad> quads) == false)
        {
            quads = new List<AbilityLineMeshQuad>();
            lineQuadMap[_spriteType] = quads;
        }

        Vector4 shineProgress = CalculateShineProgress(
            _rect,
            _pathStart,
            _pathEnd,
            _shineProgressStart,
            _shineProgressEnd);
        quads.Add(new AbilityLineMeshQuad(_rect, _color, shineProgress, _shineColorIndex));
    }

    private static Vector4 CalculateShineProgress(
        Rect _rect,
        Vector2 _pathStart,
        Vector2 _pathEnd,
        float _shineProgressStart,
        float _shineProgressEnd)
    {
        Vector2 path = _pathEnd - _pathStart;
        float pathLengthSquared = path.sqrMagnitude;
        if (pathLengthSquared <= 0.0001f)
            return new Vector4(_shineProgressStart, _shineProgressStart, _shineProgressStart, _shineProgressStart);

        float inversePathLengthSquared = 1f / pathLengthSquared;
        float centerProgress = Vector2.Dot(_rect.center - _pathStart, path) * inversePathLengthSquared;
        float horizontalContribution = path.x * (_rect.width * 0.5f) * inversePathLengthSquared;
        float verticalContribution = path.y * (_rect.height * 0.5f) * inversePathLengthSquared;

        return new Vector4(
            Mathf.Lerp(_shineProgressStart, _shineProgressEnd, Mathf.Clamp01(centerProgress - horizontalContribution - verticalContribution)),
            Mathf.Lerp(_shineProgressStart, _shineProgressEnd, Mathf.Clamp01(centerProgress - horizontalContribution + verticalContribution)),
            Mathf.Lerp(_shineProgressStart, _shineProgressEnd, Mathf.Clamp01(centerProgress + horizontalContribution + verticalContribution)),
            Mathf.Lerp(_shineProgressStart, _shineProgressEnd, Mathf.Clamp01(centerProgress + horizontalContribution - verticalContribution)));
    }

    private Vector2 GetSpriteNativeSize(Sprite _sprite)
    {
        if (_sprite == null)
            return Vector2.zero;

        Canvas targetCanvas = rootCanvas != null && rootCanvas.rootCanvas != null
            ? rootCanvas.rootCanvas
            : rootCanvas;
        float referencePixelsPerUnit = targetCanvas != null
            ? Mathf.Max(targetCanvas.referencePixelsPerUnit, 0.0001f)
            : 100f;
        float pixelsPerUnit = Mathf.Max(_sprite.pixelsPerUnit / referencePixelsPerUnit, 0.0001f);
        return _sprite.rect.size / pixelsPerUnit;
    }

    private int GetActiveSegmentSize(float _currentZoom)
    {
        float projectedGridSize = gridCellSize * Mathf.Max(_currentZoom, 0f);
        return projectedGridSize >= 16f ? 8 : 4;
    }

    private static AbilityLineSegmentSpriteType GetSegmentSpriteType(
        int _stepX,
        int _stepY,
        int _segmentSize)
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

    private Vector2 GetNodeCenterInRectangle(RectTransform _nodeRect)
    {
        if (_nodeRect == null || lineParent == null)
            return Vector2.zero;

        Vector3[] corners = new Vector3[4];
        _nodeRect.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Camera eventCamera = GetCanvasEventCamera();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            lineParent,
            RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter),
            eventCamera,
            out Vector2 localPoint);

        return SnapToPixel(localPoint);
    }

    private Vector2 GetGridPointCenterInRectangle(Vector2Int _gridPoint)
    {
        if (moveTarget == null || lineParent == null)
            return Vector2.zero;

        Vector3 worldPoint = moveTarget.TransformPoint(
            new Vector3(_gridPoint.x * gridCellSize, _gridPoint.y * gridCellSize, 0f));
        Camera eventCamera = GetCanvasEventCamera();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            lineParent,
            RectTransformUtility.WorldToScreenPoint(eventCamera, worldPoint),
            eventCamera,
            out Vector2 localPoint);

        return SnapToPixel(localPoint);
    }

    private static Vector2 SnapToPixel(Vector2 _position)
    {
        return new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }

    private Camera GetCanvasEventCamera()
    {
        Canvas targetCanvas = rootCanvas != null && rootCanvas.rootCanvas != null
            ? rootCanvas.rootCanvas
            : rootCanvas;
        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return targetCanvas.worldCamera;
    }

    private bool ShouldCullLineConnection(
        Vector2 _startCenter,
        Vector2 _endCenter,
        Vector2 _pivotCenter,
        bool _hasPivot,
        int _segmentSize)
    {
        float minX = Mathf.Min(_startCenter.x, _endCenter.x);
        float maxX = Mathf.Max(_startCenter.x, _endCenter.x);
        float minY = Mathf.Min(_startCenter.y, _endCenter.y);
        float maxY = Mathf.Max(_startCenter.y, _endCenter.y);

        if (_hasPivot)
        {
            minX = Mathf.Min(minX, _pivotCenter.x);
            maxX = Mathf.Max(maxX, _pivotCenter.x);
            minY = Mathf.Min(minY, _pivotCenter.y);
            maxY = Mathf.Max(maxY, _pivotCenter.y);
        }

        Rect viewRect = abilityBackground.rect;
        float margin = _segmentSize * 2f;
        return maxX < viewRect.xMin - margin ||
               minX > viewRect.xMax + margin ||
               maxY < viewRect.yMin - margin ||
               minY > viewRect.yMax + margin;
    }

    private Color GetLineColor(SkillType _childSkillType)
    {
        return lineColorResolver != null ? lineColorResolver(_childSkillType) : Color.white;
    }

    private int GetLineShineColorIndex(SkillType _childSkillType)
    {
        return lineShineColorIndexResolver != null ? lineShineColorIndexResolver(_childSkillType) : -1;
    }

    private float GetLineRevealProgress(SkillType _childSkillType)
    {
        return lineRevealProgressMap.TryGetValue(_childSkillType, out float progress) ? progress : 1f;
    }
}
