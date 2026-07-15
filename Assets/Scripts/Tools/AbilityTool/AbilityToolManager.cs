using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AbilityToolManager : MonoBehaviour
{
    private const int AbilityLocalizationJsonId = 2;
    private const int LocalizationEntryIdMask = 0x1FFFFF;
    private const int AbilityNameLocalizationIdStart = 101;
    private const int AbilityCommandDescriptionLocalizationIdStart = 9100;
    private const string AbilityNameKeySuffix = "_Name";
    private const float DefaultZoom = 1f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 1f;
    private const float ZoomStep = 0.1f;
    private const float ZoomFollowSpeed = 18f;
    private const float DragThreshold = 4f;
    private const float ToolTipSpacing = 32f;

    private readonly Dictionary<Vector2Int, AbilityToolNode> nodeMap = new Dictionary<Vector2Int, AbilityToolNode>();
    private readonly Dictionary<SkillType, Sprite> pictureSpriteMap = new Dictionary<SkillType, Sprite>();
    private readonly Dictionary<AbilityLevelBadgeType, Sprite> levelBadgeSpriteMap = new Dictionary<AbilityLevelBadgeType, Sprite>();
    private readonly Dictionary<int, LocalizationEntry> localizationEntryMap = new Dictionary<int, LocalizationEntry>();
    private readonly Dictionary<SkillCommandType, LocalizationEntry> localizationEntryBySkillCommandMap = new Dictionary<SkillCommandType, LocalizationEntry>();
    private readonly List<AbilityToolNode> nodeList = new List<AbilityToolNode>();
    private readonly AbilityToolLineRenderer lineRenderer = new AbilityToolLineRenderer();

    private Canvas rootCanvas;
    private bool hasZoomFocus;
    private bool isPointerHeld;
    private bool dragPerformedThisPress;
    private bool dragPerformedOnReleasedFrame;
    private bool hasPendingPivot;
    private Vector2 previousMousePosition;
    private Vector2 pointerPressScreenPosition;
    private Vector2 zoomFocusScreenPosition;
    private Vector2Int currentHoverGrid;
    private Vector2Int pendingPivotGrid;
    private bool hasHoverGrid;
    private float currentZoom = DefaultZoom;
    private float targetZoom = DefaultZoom;
    private AbilityToolNode selectedChildNode;
    private AbilityToolNode selectedMoveNode;
    private AbilityToolNode currentToolTipNode;
    private AbilityToolTip toolTipInstance;
    private bool allowEmptyExport;

    [Header("UI References")]
    [SerializeField] private RectTransform abilityBackground;
    [SerializeField] private RectTransform moveTarget;
    [SerializeField] private RectTransform gridCursor;
    [SerializeField] private RectTransform pivotMarker;
    [SerializeField] private RectTransform lineParent;
    [SerializeField] private TMP_Text gridCoordinateText;

    [Header("ToolTip Setup")]
    [SerializeField] private AbilityToolTip toolTipPrefab;
    [SerializeField] private RectTransform toolTipParent;

    [Header("Node Setup")]
    [SerializeField] private AbilityToolNode abilityToolNodePrefab;
    [SerializeField] private AbilityLine abilityLinePrefab;
    [SerializeField] private float gridCellSize = 32f;
    [SerializeField] private List<AbilityPictureBinding> pictureBindings = new List<AbilityPictureBinding>();
    [SerializeField] private List<AbilityLevelBadgeBinding> levelBadgeBindings = new List<AbilityLevelBadgeBinding>();

    [Header("Line Sprites")]
    [SerializeField] private Sprite row4Sprite;
    [SerializeField] private Sprite col4Sprite;
    [SerializeField] private Sprite diagSENW4Sprite;
    [SerializeField] private Sprite diagSWNE4Sprite;
    [SerializeField] private Sprite row8Sprite;
    [SerializeField] private Sprite col8Sprite;
    [SerializeField] private Sprite diagSENW8Sprite;
    [SerializeField] private Sprite diagSWNE8Sprite;

    [Header("Json IO")]
    [SerializeField] private TextAsset importJson;
    [SerializeField] private TextAsset importLocalizationJson;
    [SerializeField] private SkillDataBase skillDataBaseAsset;
    [SerializeField] private string uiExportAssetPath = "Assets/Data/Ability/AbilityNodeDatabase.json";
    [SerializeField] private string localizationExportAssetPath = "Assets/Resources/Localization/AbilityUI.json";


#region Default

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        CachePictureBindings();
        CacheLevelBadgeBindings();
        EnsureGridCursorFollowsMoveTarget();
        EnsurePivotMarkerFollowsMoveTarget();
        lineRenderer.Initialize(
            abilityBackground,
            moveTarget,
            lineParent,
            abilityLinePrefab,
            rootCanvas,
            gridCellSize,
            ResolveStraightSprite,
            ResolveDiagonalSprite);
        ResetView();
        EnsureToolTipInstance();
        CacheExistingNodes();
        RebuildLines();
        UpdateGridCursor();
        UpdatePivotMarker();
        UpdateGridCoordinateText();
        ImportAbilityJson();
    }

    private void Update()
    {
        HandlePan();
        HandleZoom();
        UpdateZoomAnimation();
        UpdateGridCursor();
        UpdatePivotMarker();
        UpdateGridCoordinateText();
        RefreshLines();
        RefreshNodePictures();
        RefreshNodeLevelBadges();
        UpdateToolTipPosition();
        HandleToolInput();
    }

    private void CachePictureBindings()
    {
        pictureSpriteMap.Clear();

        if (pictureBindings == null)
            return;

        for (int i = 0; i < pictureBindings.Count; i++)
        {
            AbilityPictureBinding binding = pictureBindings[i];
            if (binding == null || binding.sprite == null)
                continue;

            pictureSpriteMap[binding.skillType] = binding.sprite;
        }
    }

    private void CacheLevelBadgeBindings()
    {
        levelBadgeSpriteMap.Clear();

        if (levelBadgeBindings == null)
            return;

        for (int i = 0; i < levelBadgeBindings.Count; i++)
        {
            AbilityLevelBadgeBinding binding = levelBadgeBindings[i];
            if (binding == null || binding.levelBadge == AbilityLevelBadgeType.None || binding.sprite == null)
                continue;

            levelBadgeSpriteMap[binding.levelBadge] = binding.sprite;
        }
    }

    // 툴 화면의 이동과 확대 축소 상태를 기본값으로 되돌린다.
    private void ResetView()
    {
        if (moveTarget == null)
            return;

        isPointerHeld = false;
        dragPerformedThisPress = false;
        dragPerformedOnReleasedFrame = false;
        hasZoomFocus = false;
        hasPendingPivot = false;
        selectedChildNode = null;
        selectedMoveNode = null;
        currentZoom = DefaultZoom;
        targetZoom = DefaultZoom;
        moveTarget.anchoredPosition = Vector2.zero;
        moveTarget.localScale = Vector3.one * currentZoom;
    }

    // 씬에 이미 배치된 툴 노드들을 읽어 좌표 맵으로 등록한다.
    private void CacheExistingNodes()
    {
        nodeMap.Clear();
        nodeList.Clear();

        if (moveTarget == null)
            return;

        AbilityToolNode[] existingNodes = moveTarget.GetComponentsInChildren<AbilityToolNode>(true);
        for (int i = 0; i < existingNodes.Length; i++)
        {
            AbilityToolNode node = existingNodes[i];
            if (node == null)
                continue;

            node.ApplyAnchoredPosition(gridCellSize);
            node.BindOwner(this);
            node.SetSelectedVisual(false);
            node.SetMoveSelectedVisual(false);
            node.SetPicture(ResolvePicture(node.SkillType));
            node.SetLevelBadgeSprite(ResolveLevelBadgeSprite(node.LevelBadge));
            nodeMap[node.GridPosition] = node;
            nodeList.Add(node);
        }
    }

    // 현재 노드 연결 정보 기준으로 툴 라인을 다시 계산한다.
    private void RebuildLines()
    {
        lineRenderer.RebuildConnections(nodeList);
        RefreshLines();
    }

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


#region ToolTip

    public void ShowToolTip(AbilityToolNode _node)
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

        string costText = BuildToolTipCostText(_node, out MoneyType costMoneyType);
        toolTipInstance.SetContent(
            BuildToolTipTitleText(_node),
            BuildToolTipLevelText(_node),
            BuildToolTipDescriptionText(_node),
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

    public void HideToolTip(AbilityToolNode _node)
    {
        if (currentToolTipNode != null && _node != currentToolTipNode)
            return;

        currentToolTipNode = null;

        if (toolTipInstance != null)
            toolTipInstance.Hide();
    }

    private void UpdateToolTipPosition()
    {
        if (currentToolTipNode == null || toolTipInstance == null || toolTipInstance.gameObject.activeSelf == false)
            return;

        ShowToolTip(currentToolTipNode);
    }

    private string BuildToolTipTitleText(AbilityToolNode _node)
    {
        return _node.DisplayName;
    }

    private string BuildToolTipLevelText(AbilityToolNode _node)
    {
        return $"{ResolveToolLocalizationText(LocKeys.AbilityUI.commonLevel)} : 0 / {Mathf.Max(_node.MaxLevel, 1)}";
    }

    private string BuildToolTipCostText(AbilityToolNode _node, out MoneyType _costMoneyType)
    {
        long moneyCost = AbilityNumberFormatter.RoundToLong(_node.MoneyCurve.Evaluate(1));
        _costMoneyType = MoneyType.None;

        if (moneyCost > 0)
        {
            _costMoneyType = MoneyType.Coin;
            return AbilityNumberFormatter.FormatCompact(moneyCost);
        }

        return ResolveToolLocalizationText(LocKeys.AbilityUI.commonFree);
    }

    private string BuildToolTipDescriptionText(AbilityToolNode _node)
    {
        SkillCommandType commandType = GetPrimarySkillCommandType(_node);
        if (commandType == SkillCommandType.None)
            return string.Empty;

        if (localizationEntryBySkillCommandMap.TryGetValue(commandType, out LocalizationEntry entry) == false ||
            string.IsNullOrEmpty(entry.kr))
        {
            return string.Empty;
        }

        return entry.kr;
    }

    private SkillCommandType GetPrimarySkillCommandType(AbilityToolNode _node)
    {
        if (_node == null || _node.SkillCommands == null)
            return SkillCommandType.None;

        for (int i = 0; i < _node.SkillCommands.Count; i++)
        {
            AbilityToolSkillCommandEntry command = _node.SkillCommands[i];
            if (command != null && command.skillCommandType != SkillCommandType.None)
                return command.skillCommandType;
        }

        return SkillCommandType.None;
    }

    private string ResolveToolLocalizationText(int _compositeKey)
    {
        int entryId = _compositeKey & LocalizationEntryIdMask;
        if (localizationEntryMap.TryGetValue(entryId, out LocalizationEntry entry) &&
            string.IsNullOrEmpty(entry.kr) == false)
        {
            return entry.kr;
        }

        return string.Empty;
    }

#endregion


#region Input

    // 인게임 특성창과 동일하게 좌클릭/우클릭/휠클릭 드래그로 이동한다.
    private void HandlePan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || moveTarget == null)
            return;

        bool canDrag =
            mouse.leftButton.isPressed ||
            mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed;

        Vector2 currentMousePosition = mouse.position.ReadValue();

        if (canDrag == false)
        {
            if (isPointerHeld)
                dragPerformedOnReleasedFrame = dragPerformedThisPress;

            isPointerHeld = false;
            dragPerformedThisPress = false;
            return;
        }

        dragPerformedOnReleasedFrame = false;

        if (isPointerHeld == false)
        {
            isPointerHeld = true;
            dragPerformedThisPress = false;
            previousMousePosition = currentMousePosition;
            pointerPressScreenPosition = currentMousePosition;
            return;
        }

        Vector2 delta = currentMousePosition - previousMousePosition;
        previousMousePosition = currentMousePosition;

        if (dragPerformedThisPress == false)
        {
            float dragDistance = (currentMousePosition - pointerPressScreenPosition).magnitude;
            if (dragDistance >= DragThreshold)
                dragPerformedThisPress = true;
        }

        if (dragPerformedThisPress == false)
            return;

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

    // 인게임 특성창과 동일하게 목표 줌값을 부드럽게 따라간다.
    private void UpdateZoomAnimation()
    {
        if (moveTarget == null)
            return;

        float previousZoom = currentZoom;
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, 1f - Mathf.Exp(-ZoomFollowSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(currentZoom - targetZoom) < 0.001f)
            currentZoom = targetZoom;

        if (Mathf.Approximately(previousZoom, currentZoom) == false)
            ApplyZoomAroundFocus(previousZoom, currentZoom);

        moveTarget.localScale = Vector3.one * currentZoom;
    }

    // 마우스가 가리키는 위치를 기준으로 확대 축소가 일어나도록 위치를 보정한다.
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

    // 일반 좌클릭 선택/생성, B+좌클 연결, 우클릭 삭제 입력을 처리한다.
    private void HandleToolInput()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || keyboard == null || hasHoverGrid == false)
            return;

        if (dragPerformedOnReleasedFrame)
            return;

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            DeleteNodeAtGrid(currentHoverGrid);
            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame == false)
            return;

        bool linkModeHeld = keyboard.bKey.isPressed;
        bool moveModeHeld = keyboard.tKey.isPressed;
        AbilityToolNode hoveredNode = GetNodeAtGrid(currentHoverGrid);

        if (moveModeHeld)
        {
            HandleMoveModeClick(hoveredNode, currentHoverGrid);
            return;
        }

        if (linkModeHeld)
        {
            ClearMoveSelectionMode();
            HandleLinkModeClick(hoveredNode, currentHoverGrid);
            return;
        }

        if (hoveredNode != null)
        {
            SelectNodeInEditor(hoveredNode);
            ClearLinkSelectionMode();
            ClearMoveSelectionMode();
            return;
        }

        ClearLinkSelectionMode();
        ClearMoveSelectionMode();
        CreateNodeAtGrid(currentHoverGrid);
    }

    // B+좌클 입력으로 자식 선택, 피벗 지정, 부모 연결을 처리한다.
    private void HandleLinkModeClick(AbilityToolNode _hoveredNode, Vector2Int _hoveredGrid)
    {
        if (selectedChildNode == null)
        {
            if (_hoveredNode == null)
                return;

            selectedChildNode = _hoveredNode;
            hasPendingPivot = false;
            ClearMoveSelectionMode();
            selectedChildNode.SetSelectedVisual(true);
            SelectNodeInEditor(selectedChildNode);
            return;
        }

        if (_hoveredNode == null)
        {
            hasPendingPivot = true;
            pendingPivotGrid = _hoveredGrid;
            return;
        }

        if (_hoveredNode == selectedChildNode)
        {
            ClearLinkSelectionMode();
            return;
        }

        bool linked = TryLinkSelectedChildToParent(_hoveredNode);
        ClearLinkSelectionMode();

        if (linked)
            RebuildLines();
    }

    // T+좌클릭으로 노드를 선택한 뒤, 빈 그리드를 다시 T+좌클릭하면 위치만 이동한다.
    private void HandleMoveModeClick(AbilityToolNode _hoveredNode, Vector2Int _hoveredGrid)
    {
        if (selectedMoveNode == null)
        {
            if (_hoveredNode == null)
                return;

            ClearLinkSelectionMode();
            selectedMoveNode = _hoveredNode;
            selectedMoveNode.SetMoveSelectedVisual(true);
            SelectNodeInEditor(selectedMoveNode);
            return;
        }

        if (_hoveredNode != null)
        {
            ClearMoveSelectionMode();
            return;
        }

        MoveSelectedNodeToGrid(_hoveredGrid);
    }

#endregion


#region Grid Cursor

    // 마우스가 어느 그리드 칸을 가리키는지 계산한다.
    private void UpdateGridCursor()
    {
        if (abilityBackground == null || moveTarget == null || gridCursor == null)
            return;

        if (TryGetHoveredGrid(out Vector2Int hoveredGrid, out Vector2 localCenter))
        {
            hasHoverGrid = true;
            currentHoverGrid = hoveredGrid;
            gridCursor.gameObject.SetActive(true);
            gridCursor.anchoredPosition = SnapToPixel(localCenter);
            gridCursor.sizeDelta = new Vector2(gridCellSize, gridCellSize);
        }
        else
        {
            hasHoverGrid = false;
            gridCursor.gameObject.SetActive(false);
        }
    }

    // 현재 선택된 피벗 위치를 표시할 마커를 갱신한다.
    private void UpdatePivotMarker()
    {
        if (pivotMarker == null)
            return;

        if (selectedChildNode == null || hasPendingPivot == false)
        {
            pivotMarker.gameObject.SetActive(false);
            return;
        }

        pivotMarker.gameObject.SetActive(true);
        pivotMarker.anchoredPosition = SnapToPixel(new Vector2(
            pendingPivotGrid.x * gridCellSize,
            pendingPivotGrid.y * gridCellSize));
    }

    // 현재 마우스가 가리키는 그리드 좌표를 텍스트로 표시한다.
    private void UpdateGridCoordinateText()
    {
        if (gridCoordinateText == null)
            return;

        if (hasHoverGrid == false)
        {
            gridCoordinateText.text = "X : -  Y : -";
            return;
        }

        gridCoordinateText.text = $"X : {currentHoverGrid.x}  Y : {currentHoverGrid.y}";
    }

    // 커서가 노드 컨텐츠와 같은 좌표계/스케일을 따르도록 MoveTarget 아래로 정렬한다.
    private void EnsureGridCursorFollowsMoveTarget()
    {
        if (gridCursor == null || moveTarget == null)
            return;

        if (gridCursor.parent != moveTarget)
            gridCursor.SetParent(moveTarget, false);

        gridCursor.anchorMin = new Vector2(0.5f, 0.5f);
        gridCursor.anchorMax = new Vector2(0.5f, 0.5f);
        gridCursor.pivot = new Vector2(0.5f, 0.5f);
        gridCursor.localScale = Vector3.one;
        gridCursor.sizeDelta = new Vector2(gridCellSize, gridCellSize);
    }

    // 피벗 마커가 노드와 같은 좌표계/스케일을 따르도록 MoveTarget 아래로 정렬한다.
    private void EnsurePivotMarkerFollowsMoveTarget()
    {
        if (pivotMarker == null || moveTarget == null)
            return;

        if (pivotMarker.parent != moveTarget)
            pivotMarker.SetParent(moveTarget, false);

        pivotMarker.anchorMin = new Vector2(0.5f, 0.5f);
        pivotMarker.anchorMax = new Vector2(0.5f, 0.5f);
        pivotMarker.pivot = new Vector2(0.5f, 0.5f);
        pivotMarker.localScale = Vector3.one;
    }

    // 현재 마우스 위치를 moveTarget 기준 그리드 좌표와 중심점으로 변환한다.
    private bool TryGetHoveredGrid(out Vector2Int _gridPosition, out Vector2 _gridCenterLocalPosition)
    {
        _gridPosition = Vector2Int.zero;
        _gridCenterLocalPosition = Vector2.zero;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        Camera eventCamera = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = rootCanvas.worldCamera;

        if (RectTransformUtility.RectangleContainsScreenPoint(abilityBackground, mouse.position.ReadValue(), eventCamera) == false)
            return false;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moveTarget,
            mouse.position.ReadValue(),
            eventCamera,
            out Vector2 localPoint) == false)
            return false;

        int gridX = Mathf.RoundToInt(localPoint.x / gridCellSize);
        int gridY = Mathf.RoundToInt(localPoint.y / gridCellSize);
        _gridPosition = new Vector2Int(gridX, gridY);
        _gridCenterLocalPosition = new Vector2(gridX * gridCellSize, gridY * gridCellSize);
        return true;
    }

    // 정수 픽셀 좌표 기준으로 위치를 스냅한다.
    private Vector2 SnapToPixel(Vector2 _position)
    {
        return new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }

#endregion


#region Node Placement

    // 지정한 그리드 칸이 비어 있으면 툴 노드를 생성한다.
    private void CreateNodeAtGrid(Vector2Int _gridPosition)
    {
        if (abilityToolNodePrefab == null || moveTarget == null)
            return;

        if (nodeMap.ContainsKey(_gridPosition))
            return;

        AbilityToolNode node = Instantiate(abilityToolNodePrefab, moveTarget);
        node.BindOwner(this);
        node.SetGridPosition(_gridPosition, gridCellSize);
        node.SetSelectedVisual(false);
        node.SetPicture(ResolvePicture(node.SkillType));
        node.SetLevelBadgeSprite(ResolveLevelBadgeSprite(node.LevelBadge));
        nodeMap[_gridPosition] = node;
        nodeList.Add(node);
        allowEmptyExport = false;
        RebuildLines();
        SelectNodeInEditor(node);
    }

    // 지정한 그리드 칸에 노드가 있으면 삭제하고 관련 라인 참조도 정리한다.
    private void DeleteNodeAtGrid(Vector2Int _gridPosition)
    {
        if (nodeMap.TryGetValue(_gridPosition, out AbilityToolNode node) == false)
            return;

        nodeMap.Remove(_gridPosition);
        nodeList.Remove(node);

        for (int i = 0; i < nodeList.Count; i++)
            nodeList[i].RemoveNullOrTargetParentLinks(node);

        if (selectedChildNode == node)
            ClearLinkSelectionMode();

        if (selectedMoveNode == node)
            ClearMoveSelectionMode();

        if (currentToolTipNode == node)
            HideToolTip(node);

        Destroy(node.gameObject);
        RebuildLines();
    }

    // 현재 가리키는 그리드에 노드가 있으면 반환한다.
    private AbilityToolNode GetNodeAtGrid(Vector2Int _gridPosition)
    {
        nodeMap.TryGetValue(_gridPosition, out AbilityToolNode node);
        return node;
    }

    // 선택된 노드의 데이터는 유지하고 그리드 위치만 바꾼 뒤, 관련 라인을 다시 계산한다.
    private void MoveSelectedNodeToGrid(Vector2Int _gridPosition)
    {
        if (selectedMoveNode == null)
            return;

        if (nodeMap.ContainsKey(_gridPosition))
        {
            ClearMoveSelectionMode();
            return;
        }

        Vector2Int previousGridPosition = selectedMoveNode.GridPosition;
        nodeMap.Remove(previousGridPosition);

        selectedMoveNode.SetGridPosition(_gridPosition, gridCellSize);
        nodeMap[_gridPosition] = selectedMoveNode;

        AbilityToolNode movedNode = selectedMoveNode;
        ClearMoveSelectionMode();
        SelectNodeInEditor(movedNode);
        RebuildLines();

        if (currentToolTipNode == movedNode)
            ShowToolTip(movedNode);
    }

    // 선택된 자식 노드와 나중에 찍은 부모 노드를 실제 연결한다.
    private bool TryLinkSelectedChildToParent(AbilityToolNode _parentNode)
    {
        if (selectedChildNode == null || _parentNode == null)
            return false;

        if (hasPendingPivot)
        {
            if (IsSupportedLineSegment(selectedChildNode.GridPosition, pendingPivotGrid) == false)
                return false;

            if (IsSupportedLineSegment(pendingPivotGrid, _parentNode.GridPosition) == false)
                return false;

            selectedChildNode.AddOrUpdateParentLink(_parentNode, true, pendingPivotGrid);
            return true;
        }

        if (IsSupportedLineSegment(selectedChildNode.GridPosition, _parentNode.GridPosition) == false)
            return false;

        selectedChildNode.AddOrUpdateParentLink(_parentNode, false, Vector2Int.zero);
        return true;
    }

    // 직선/대각선 규칙에 맞는 연결인지 검사한다.
    private bool IsSupportedLineSegment(Vector2Int _startGrid, Vector2Int _endGrid)
    {
        int dx = _endGrid.x - _startGrid.x;
        int dy = _endGrid.y - _startGrid.y;

        if (dx == 0 && dy == 0)
            return false;

        if (dx == 0 || dy == 0)
            return true;

        return Mathf.Abs(dx) == Mathf.Abs(dy);
    }

    // 연결 선택 모드를 해제하고 시각 상태를 초기화한다.
    private void ClearLinkSelectionMode()
    {
        if (selectedChildNode != null)
            selectedChildNode.SetSelectedVisual(false);

        selectedChildNode = null;
        hasPendingPivot = false;
    }

    // 이동 선택 모드를 해제하고 파란색 테두리를 원래 색으로 되돌린다.
    private void ClearMoveSelectionMode()
    {
        if (selectedMoveNode != null)
            selectedMoveNode.SetMoveSelectedVisual(false);

        selectedMoveNode = null;
    }

    // 에디터에서 해당 노드를 선택해 인스펙터 수정이 바로 가능하게 한다.
    private void SelectNodeInEditor(AbilityToolNode _node)
    {
#if UNITY_EDITOR
        if (_node != null)
            Selection.activeGameObject = _node.gameObject;
#endif
    }

#endregion


#region Node Picture

    private void RefreshNodePictures()
    {
        CachePictureBindings();

        for (int i = 0; i < nodeList.Count; i++)
        {
            AbilityToolNode node = nodeList[i];
            if (node == null)
                continue;

            Sprite sprite = ResolvePicture(node.SkillType);
            if (node.HasPictureRefreshRequest(sprite) == false)
                continue;

            node.SetPicture(sprite);
        }
    }

    private Sprite ResolvePicture(SkillType _skillType)
    {
        if (pictureSpriteMap.TryGetValue(_skillType, out Sprite sprite))
            return sprite;

        return null;
    }

    private void RefreshNodeLevelBadges()
    {
        CacheLevelBadgeBindings();

        for (int i = 0; i < nodeList.Count; i++)
        {
            AbilityToolNode node = nodeList[i];
            if (node == null)
                continue;

            node.SetLevelBadgeSprite(ResolveLevelBadgeSprite(node.LevelBadge));
        }
    }

    public void RefreshNodeLevelBadge(AbilityToolNode _node)
    {
        if (_node == null)
            return;

        CacheLevelBadgeBindings();
        _node.SetLevelBadgeSprite(ResolveLevelBadgeSprite(_node.LevelBadge));
    }

    private Sprite ResolveLevelBadgeSprite(AbilityLevelBadgeType _levelBadge)
    {
        if (_levelBadge == AbilityLevelBadgeType.None)
            return null;

        if (levelBadgeSpriteMap.TryGetValue(_levelBadge, out Sprite sprite))
            return sprite;

        return null;
    }

#endregion


#region Json IO

    [ContextMenu("Export Ability Data")]
    public void ExportAbilityJson()
    {
        if (nodeList.Count == 0 && allowEmptyExport == false)
        {
            Debug.LogWarning("Ability tool blocked an empty export. Use Clear Ability Nodes first if you intend to erase the saved data.");
            return;
        }

        AbilityLocalizationExportContext localizationContext = LoadLocalizationExportContext();
        AbilityToolExportDatabaseJson databaseJson = new AbilityToolExportDatabaseJson
        {
            nodes = BuildExportNodes(localizationContext)
        };

        string uiJson = JsonUtility.ToJson(databaseJson, true);
        string uiAbsolutePath = GetAbsolutePath(uiExportAssetPath);
        string localizationJson = JsonUtility.ToJson(BuildLocalizationDataJson(localizationContext), true);
        string localizationAbsolutePath = GetAbsolutePath(localizationExportAssetPath);

        WriteJsonFile(uiAbsolutePath, uiJson);
        WriteJsonFile(localizationAbsolutePath, localizationJson);
        RefreshLocalizationEntryMap();
        ExportSkillDataBaseAsset();
        allowEmptyExport = false;

        Debug.Log($"Ability tool exported UI json: {uiAbsolutePath}");
        Debug.Log($"Ability tool exported localization json: {localizationAbsolutePath}");
        ShowExportCompletedDialog();
    }

    [ContextMenu("Import Ability Json")]
    public void ImportAbilityJson()
    {
        CachePictureBindings();

        string json = ResolveImportJsonText();
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("Ability tool import failed. Import Json is empty.");
            return;
        }

        AbilityNodeDatabaseJson databaseJson = JsonUtility.FromJson<AbilityNodeDatabaseJson>(json);
        if (databaseJson == null || databaseJson.nodes == null)
        {
            Debug.LogWarning("Ability tool import failed. Json does not contain nodes.");
            return;
        }

        RefreshLocalizationEntryMap();
        RemoveAllNodes();

        Dictionary<SkillType, AbilityToolNode> skillNodeMap = new Dictionary<SkillType, AbilityToolNode>();
        for (int i = 0; i < databaseJson.nodes.Length; i++)
        {
            AbilityNodeDefinitionJson nodeDefinition = databaseJson.nodes[i];
            if (nodeDefinition == null || string.IsNullOrWhiteSpace(nodeDefinition.skillType))
                continue;

            if (Enum.TryParse(nodeDefinition.skillType, true, out SkillType parsedSkillType) == false)
            {
                Debug.LogWarning($"Ability tool import skipped unknown SkillType: {nodeDefinition.skillType}");
                continue;
            }

            if (skillNodeMap.ContainsKey(parsedSkillType))
            {
                Debug.LogWarning($"Ability tool import skipped duplicated SkillType: {parsedSkillType}");
                continue;
            }

            string displayName = ResolveImportedLocalizationText(
                nodeDefinition.nameLocId,
                localizationEntryMap,
                $"{parsedSkillType} name");
            AbilityToolNode node = CreateNodeFromDefinition(nodeDefinition, parsedSkillType, displayName);
            if (node == null)
                continue;

            skillNodeMap[parsedSkillType] = node;
        }

        ApplyImportedParentLinks(databaseJson.nodes, skillNodeMap);
        ApplyImportedSkillLogic(skillNodeMap);
        RefreshNodePictures();
        RebuildLines();
        allowEmptyExport = false;
    }

    [ContextMenu("Clear Ability Nodes")]
    public void ClearAbilityNodes()
    {
        HideToolTip(currentToolTipNode);
        RemoveAllNodes();
        allowEmptyExport = true;
        Debug.Log("Ability tool cleared nodes. Export Ability Data to save the empty state.");
    }

    private AbilityToolExportNodeJson[] BuildExportNodes(AbilityLocalizationExportContext _localizationContext)
    {
        List<AbilityToolExportNodeJson> exportNodes = new List<AbilityToolExportNodeJson>();

        for (int i = 0; i < nodeList.Count; i++)
        {
            AbilityToolNode node = nodeList[i];
            if (node == null || node.SkillType == SkillType.None)
                continue;

            int nameLocId = _localizationContext.GetOrCreateName(
                node.NameLocId,
                BuildAbilityLocalizationKey(node.SkillType, AbilityNameKeySuffix),
                node.DisplayName);
            node.ApplyLocalizationId(nameLocId);
            EnsureCommandDescriptionEntries(_localizationContext, node.SkillCommands);

            exportNodes.Add(new AbilityToolExportNodeJson
            {
                skillType = node.SkillType.ToString(),
                nameLocId = nameLocId,
                levelBadge = node.LevelBadge.ToString(),
                requiredPrestigeLevel = node.RequiredPrestigeLevel,
                gridX = node.GridPosition.x,
                gridY = node.GridPosition.y,
                parents = BuildExportParents(node.ParentLinks)
            });
        }

        return exportNodes.ToArray();
    }

    private void ExportSkillDataBaseAsset()
    {
        if (skillDataBaseAsset == null)
        {
            Debug.LogWarning("Ability tool skipped SkillDataBase export. Skill Data Base Asset is not assigned.");
            return;
        }

#if UNITY_EDITOR
        Undo.RecordObject(skillDataBaseAsset, "Export Ability Skill Data");
#endif

        skillDataBaseAsset.skills = BuildExportSkills();

#if UNITY_EDITOR
        EditorUtility.SetDirty(skillDataBaseAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif

        Debug.Log($"Ability tool exported SkillDataBase asset: {skillDataBaseAsset.name}");
    }

    private List<Skill> BuildExportSkills()
    {
        List<Skill> exportSkills = new List<Skill>();

        for (int i = 0; i < nodeList.Count; i++)
        {
            AbilityToolNode node = nodeList[i];
            if (node == null || node.SkillType == SkillType.None)
                continue;

            exportSkills.Add(new Skill
            {
                skillType = node.SkillType,
                maxLevel = Mathf.Max(node.MaxLevel, 1),
                cost = new SkillCost
                {
                    moneyCurve = CloneCurve(node.MoneyCurve),
                    carrotCurve = CreateZeroCurve()
                },
                skillTypes = BuildExportSkillCommands(node.SkillCommands),
                prerequisiteSkills = BuildExportPrerequisiteSkills(node.ParentLinks)
            });
        }

        return exportSkills;
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

    private ProgressionCurve CreateZeroCurve()
    {
        return new ProgressionCurve
        {
            type = ProgressionType.Constant,
            baseValue = 0f,
            manualValues = new List<float>()
        };
    }

    private List<SkillCommandInfo> BuildExportSkillCommands(List<AbilityToolSkillCommandEntry> _skillCommands)
    {
        if (_skillCommands == null || _skillCommands.Count == 0)
            return new List<SkillCommandInfo>();

        List<SkillCommandInfo> exportCommands = new List<SkillCommandInfo>();
        for (int i = 0; i < _skillCommands.Count; i++)
        {
            AbilityToolSkillCommandEntry command = _skillCommands[i];
            if (command == null || command.skillCommandType == SkillCommandType.None)
                continue;

            exportCommands.Add(new SkillCommandInfo
            {
                skillCommandType = command.skillCommandType,
                amountCurve = CloneCurve(command.amountCurve)
            });
        }

        return exportCommands;
    }

    private List<SkillType> BuildExportPrerequisiteSkills(List<AbilityToolParentLink> _parentLinks)
    {
        if (_parentLinks == null || _parentLinks.Count == 0)
            return new List<SkillType>();

        List<SkillType> prerequisites = new List<SkillType>();
        for (int i = 0; i < _parentLinks.Count; i++)
        {
            AbilityToolParentLink parentLink = _parentLinks[i];
            if (parentLink == null || parentLink.parentNode == null || parentLink.parentNode.SkillType == SkillType.None)
                continue;

            prerequisites.Add(parentLink.parentNode.SkillType);
        }

        return prerequisites;
    }

    private AbilityParentJson[] BuildExportParents(List<AbilityToolParentLink> _parentLinks)
    {
        if (_parentLinks == null || _parentLinks.Count == 0)
            return Array.Empty<AbilityParentJson>();

        List<AbilityParentJson> exportParents = new List<AbilityParentJson>();
        for (int i = 0; i < _parentLinks.Count; i++)
        {
            AbilityToolParentLink parentLink = _parentLinks[i];
            if (parentLink == null || parentLink.parentNode == null || parentLink.parentNode.SkillType == SkillType.None)
                continue;

            exportParents.Add(new AbilityParentJson
            {
                skillType = parentLink.parentNode.SkillType.ToString(),
                usePivot = parentLink.usePivot,
                pivotX = parentLink.pivotGrid.x,
                pivotY = parentLink.pivotGrid.y
            });
        }

        return exportParents.ToArray();
    }

    private AbilityLocalizationExportContext LoadLocalizationExportContext()
    {
        AbilityLocalizationExportContext context = new AbilityLocalizationExportContext();
        string localizationJson = ResolveImportLocalizationJsonText();

        if (string.IsNullOrWhiteSpace(localizationJson) == false)
        {
            LocalizationDataJson data = JsonUtility.FromJson<LocalizationDataJson>(localizationJson);
            if (data != null && data.entries != null)
            {
                for (int i = 0; i < data.entries.Length; i++)
                    context.AddOrUpdate(data.entries[i]);
            }
        }

        context.EnsureNextId();
        return context;
    }

    private Dictionary<int, LocalizationEntry> LoadLocalizationEntryMap()
    {
        Dictionary<int, LocalizationEntry> entryMap = new Dictionary<int, LocalizationEntry>();
        string localizationJson = ResolveImportLocalizationJsonText();
        if (string.IsNullOrWhiteSpace(localizationJson))
            return entryMap;

        LocalizationDataJson data = JsonUtility.FromJson<LocalizationDataJson>(localizationJson);
        if (data == null || data.entries == null)
            return entryMap;

        for (int i = 0; i < data.entries.Length; i++)
        {
            LocalizationEntry entry = data.entries[i];
            if (entry.id <= 0)
                continue;

            entryMap[entry.id] = entry;
        }

        return entryMap;
    }

    private void RefreshLocalizationEntryMap()
    {
        localizationEntryMap.Clear();
        localizationEntryBySkillCommandMap.Clear();
        Dictionary<int, LocalizationEntry> loadedEntryMap = LoadLocalizationEntryMap();
        foreach (KeyValuePair<int, LocalizationEntry> pair in loadedEntryMap)
        {
            localizationEntryMap[pair.Key] = pair.Value;

            LocalizationEntry entry = pair.Value;
            if (entry.enumType == nameof(SkillCommandType) &&
                string.IsNullOrWhiteSpace(entry.enumValue) == false &&
                Enum.TryParse(entry.enumValue, true, out SkillCommandType commandType))
            {
                localizationEntryBySkillCommandMap[commandType] = entry;
            }
        }
    }

    private string ResolveImportedLocalizationText(int _locId, Dictionary<int, LocalizationEntry> _localizationEntryMap, string _label)
    {
        if (_locId <= 0)
        {
            Debug.LogWarning($"Ability tool import missing localization id: {_label}");
            return string.Empty;
        }

        if (_localizationEntryMap != null &&
            _localizationEntryMap.TryGetValue(_locId, out LocalizationEntry entry) &&
            string.IsNullOrEmpty(entry.kr) == false)
        {
            return entry.kr;
        }

        Debug.LogWarning($"Ability tool import missing localization entry: {_label} ({_locId})");
        return string.Empty;
    }

    private LocalizationDataJson BuildLocalizationDataJson(AbilityLocalizationExportContext _context)
    {
        return new LocalizationDataJson
        {
            jsonId = AbilityLocalizationJsonId,
            entries = _context != null ? _context.GetEntries() : Array.Empty<LocalizationEntry>()
        };
    }

    private string BuildAbilityLocalizationKey(SkillType _skillType, string _suffix)
    {
        return _skillType + _suffix;
    }

    private string BuildAbilityDescriptionLocalizationKey(SkillCommandType _commandType)
    {
        return "AbilityDescription_" + _commandType;
    }

    private void EnsureCommandDescriptionEntries(AbilityLocalizationExportContext _localizationContext, List<AbilityToolSkillCommandEntry> _skillCommands)
    {
        if (_localizationContext == null || _skillCommands == null)
            return;

        for (int i = 0; i < _skillCommands.Count; i++)
        {
            AbilityToolSkillCommandEntry command = _skillCommands[i];
            if (command == null || command.skillCommandType == SkillCommandType.None)
                continue;

            _localizationContext.GetOrCreateEnum(
                BuildAbilityDescriptionLocalizationKey(command.skillCommandType),
                nameof(SkillCommandType),
                command.skillCommandType.ToString());
        }
    }

    private AbilityToolNode CreateNodeFromDefinition(AbilityNodeDefinitionJson _nodeDefinition, SkillType _skillType, string _displayName)
    {
        if (abilityToolNodePrefab == null || moveTarget == null)
            return null;

        Vector2Int gridPosition = new Vector2Int(_nodeDefinition.gridX, _nodeDefinition.gridY);
        if (nodeMap.ContainsKey(gridPosition))
        {
            Debug.LogWarning($"Ability tool import skipped occupied grid: {gridPosition}");
            return null;
        }

        AbilityToolNode node = Instantiate(abilityToolNodePrefab, moveTarget);
        node.BindOwner(this);
        node.ApplyDefinition(_nodeDefinition, _skillType, _displayName, gridCellSize);
        node.SetSelectedVisual(false);
        node.SetPicture(ResolvePicture(_skillType));
        node.SetLevelBadgeSprite(ResolveLevelBadgeSprite(node.LevelBadge));
        nodeMap[gridPosition] = node;
        nodeList.Add(node);
        return node;
    }

    private void ApplyImportedParentLinks(AbilityNodeDefinitionJson[] _nodeDefinitions, Dictionary<SkillType, AbilityToolNode> _skillNodeMap)
    {
        if (_nodeDefinitions == null)
            return;

        for (int i = 0; i < _nodeDefinitions.Length; i++)
        {
            AbilityNodeDefinitionJson nodeDefinition = _nodeDefinitions[i];
            if (nodeDefinition == null || Enum.TryParse(nodeDefinition.skillType, true, out SkillType childSkillType) == false)
                continue;

            if (_skillNodeMap.TryGetValue(childSkillType, out AbilityToolNode childNode) == false)
                continue;

            string[] parentNames = nodeDefinition.GetParentSkillTypeNames();
            for (int parentIndex = 0; parentIndex < parentNames.Length; parentIndex++)
            {
                string parentName = parentNames[parentIndex];
                if (string.IsNullOrWhiteSpace(parentName))
                    continue;

                if (Enum.TryParse(parentName, true, out SkillType parentSkillType) == false)
                    continue;

                if (_skillNodeMap.TryGetValue(parentSkillType, out AbilityToolNode parentNode) == false)
                    continue;

                AbilityParentJson route = nodeDefinition.FindParentLineRoute(parentSkillType);
                bool usePivot = route != null && route.usePivot;
                Vector2Int pivotGrid = usePivot
                    ? new Vector2Int(route.pivotX, route.pivotY)
                    : Vector2Int.zero;

                childNode.AddOrUpdateParentLink(parentNode, usePivot, pivotGrid);
            }
        }
    }

    private void ApplyImportedSkillLogic(Dictionary<SkillType, AbilityToolNode> _skillNodeMap)
    {
        if (skillDataBaseAsset == null || skillDataBaseAsset.skills == null)
            return;

        for (int i = 0; i < skillDataBaseAsset.skills.Count; i++)
        {
            Skill skillData = skillDataBaseAsset.skills[i];
            if (_skillNodeMap.TryGetValue(skillData.skillType, out AbilityToolNode node) == false)
                continue;

            node.ApplyLogicData(
                skillData.maxLevel,
                skillData.cost.moneyCurve,
                skillData.cost.carrotCurve,
                ConvertSkillCommands(skillData.skillTypes));
        }
    }

    private List<AbilityToolSkillCommandEntry> ConvertSkillCommands(List<SkillCommandInfo> _skillCommands)
    {
        List<AbilityToolSkillCommandEntry> result = new List<AbilityToolSkillCommandEntry>();
        if (_skillCommands == null)
            return result;

        for (int i = 0; i < _skillCommands.Count; i++)
        {
            SkillCommandInfo command = _skillCommands[i];

            result.Add(new AbilityToolSkillCommandEntry
            {
                skillCommandType = command.skillCommandType,
                amountCurve = CloneCurve(command.amountCurve)
            });
        }

        return result;
    }

    private void RemoveAllNodes()
    {
        ClearLinkSelectionMode();
        ClearMoveSelectionMode();

        for (int i = nodeList.Count - 1; i >= 0; i--)
        {
            if (nodeList[i] != null)
                DestroyToolObject(nodeList[i].gameObject);
        }

        nodeMap.Clear();
        nodeList.Clear();
        RebuildLines();
    }

    private void DestroyToolObject(GameObject _gameObject)
    {
        if (_gameObject == null)
            return;

        if (Application.isPlaying)
            Destroy(_gameObject);
        else
            DestroyImmediate(_gameObject);
    }

    private string ResolveImportJsonText()
    {
        if (importJson != null)
            return importJson.text;

        string absolutePath = GetAbsolutePath(uiExportAssetPath);
        if (File.Exists(absolutePath))
            return File.ReadAllText(absolutePath, Encoding.UTF8);

        return string.Empty;
    }

    private string ResolveImportLocalizationJsonText()
    {
        if (importLocalizationJson != null)
            return importLocalizationJson.text;

        string absolutePath = GetAbsolutePath(localizationExportAssetPath);
        if (File.Exists(absolutePath))
            return File.ReadAllText(absolutePath, Encoding.UTF8);

        return string.Empty;
    }

    private void WriteJsonFile(string _absolutePath, string _json)
    {
        string directoryPath = Path.GetDirectoryName(_absolutePath);

        if (string.IsNullOrWhiteSpace(directoryPath) == false)
            Directory.CreateDirectory(directoryPath);

        File.WriteAllText(_absolutePath, _json, Encoding.UTF8);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    private void ShowExportCompletedDialog()
    {
#if UNITY_EDITOR
        EditorUtility.DisplayDialog(
            "Ability Tool Export",
            "Ability data export completed.",
            "OK");
#endif
    }

    private string GetAbsolutePath(string _assetPath)
    {
        if (Path.IsPathRooted(_assetPath))
            return _assetPath;

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), _assetPath));
    }

    [Serializable]
    private class AbilityToolExportDatabaseJson
    {
        public AbilityToolExportNodeJson[] nodes;
    }

    [Serializable]
    private class AbilityToolExportNodeJson
    {
        public string skillType;
        public int nameLocId;
        public string levelBadge;
        public int requiredPrestigeLevel;
        public int gridX;
        public int gridY;
        public AbilityParentJson[] parents;
    }

    private class AbilityLocalizationExportContext
    {
        private readonly List<LocalizationEntry> entries = new List<LocalizationEntry>();
        private readonly Dictionary<int, int> indexById = new Dictionary<int, int>();
        private readonly Dictionary<string, int> idByKey = new Dictionary<string, int>();
        private readonly Dictionary<string, int> idByEnum = new Dictionary<string, int>();
        private int nextNameId = AbilityNameLocalizationIdStart;
        private int nextCommandDescriptionId = AbilityCommandDescriptionLocalizationIdStart;

        public void AddOrUpdate(LocalizationEntry _entry)
        {
            if (_entry.id <= 0)
                return;

            string enumLookupKey = BuildEnumLookupKey(_entry.enumType, _entry.enumValue);
            if (string.IsNullOrEmpty(enumLookupKey) == false &&
                idByEnum.TryGetValue(enumLookupKey, out int existingEnumId) &&
                existingEnumId != _entry.id)
            {
                return;
            }

            if (indexById.TryGetValue(_entry.id, out int index))
            {
                entries[index] = _entry;
            }
            else
            {
                indexById[_entry.id] = entries.Count;
                entries.Add(_entry);
            }

            if (string.IsNullOrEmpty(_entry.key) == false)
                idByKey[_entry.key] = _entry.id;

            if (string.IsNullOrEmpty(enumLookupKey) == false)
                idByEnum[enumLookupKey] = _entry.id;
        }

        public void EnsureNextId()
        {
            nextNameId = AbilityNameLocalizationIdStart;
            nextCommandDescriptionId = AbilityCommandDescriptionLocalizationIdStart;
            for (int i = 0; i < entries.Count; i++)
            {
                LocalizationEntry entry = entries[i];
                if (IsAbilityNameEntry(entry))
                    nextNameId = Mathf.Max(nextNameId, entry.id + 1);
                else if (IsCommandDescriptionEntry(entry))
                    nextCommandDescriptionId = Mathf.Max(nextCommandDescriptionId, entry.id + 1);
            }
        }

        public int GetOrCreateName(int _preferredId, string _key, string _krText)
        {
            int id = ResolveExistingNameId(_preferredId, _key);
            if (id <= 0)
                id = ResolveNewNameId();

            LocalizationEntry entry = GetEntryOrDefault(id);
            entry.id = id;
            entry.key = _key;
            entry.kr = _krText ?? string.Empty;
            entry.en = entry.en ?? string.Empty;
            entry.enumType = entry.enumType ?? string.Empty;
            entry.enumValue = entry.enumValue ?? string.Empty;
            AddOrUpdate(entry);
            return id;
        }

        public int GetOrCreateEnum(string _key, string _enumType, string _enumValue)
        {
            int id = ResolveExistingEnumId(_enumType, _enumValue);
            if (id <= 0)
                id = ResolveExistingId(0, _key);
            if (id <= 0)
                id = ResolveNewCommandDescriptionId();

            LocalizationEntry entry = GetEntryOrDefault(id);
            entry.id = id;
            if (string.IsNullOrEmpty(entry.key))
                entry.key = _key;
            entry.kr = entry.kr ?? string.Empty;
            entry.en = entry.en ?? string.Empty;
            entry.enumType = _enumType ?? string.Empty;
            entry.enumValue = _enumValue ?? string.Empty;
            AddOrUpdate(entry);
            return id;
        }

        public LocalizationEntry[] GetEntries()
        {
            entries.Sort((a, b) => a.id.CompareTo(b.id));
            return entries.ToArray();
        }

        private int ResolveExistingNameId(int _preferredId, string _key)
        {
            if (_preferredId > 0 && IsNameLocalizationId(_preferredId) && indexById.ContainsKey(_preferredId))
                return _preferredId;

            if (string.IsNullOrEmpty(_key) == false && idByKey.TryGetValue(_key, out int idByExistingKey) &&
                IsNameLocalizationId(idByExistingKey))
            {
                return idByExistingKey;
            }

            if (_preferredId > 0 && IsNameLocalizationId(_preferredId) && indexById.ContainsKey(_preferredId) == false)
                return _preferredId;

            return 0;
        }

        private int ResolveExistingId(int _preferredId, string _key)
        {
            if (_preferredId > 0 && indexById.ContainsKey(_preferredId))
                return _preferredId;

            if (string.IsNullOrEmpty(_key) == false && idByKey.TryGetValue(_key, out int idByExistingKey))
                return idByExistingKey;

            if (_preferredId > 0 && indexById.ContainsKey(_preferredId) == false)
                return _preferredId;

            return 0;
        }

        private int ResolveExistingEnumId(string _enumType, string _enumValue)
        {
            string enumLookupKey = BuildEnumLookupKey(_enumType, _enumValue);
            if (string.IsNullOrEmpty(enumLookupKey) == false &&
                idByEnum.TryGetValue(enumLookupKey, out int idByExistingEnum))
            {
                return idByExistingEnum;
            }

            return 0;
        }

        private int ResolveNewNameId()
        {
            while (indexById.ContainsKey(nextNameId))
                nextNameId++;

            return nextNameId++;
        }

        private int ResolveNewCommandDescriptionId()
        {
            while (indexById.ContainsKey(nextCommandDescriptionId))
                nextCommandDescriptionId++;

            return nextCommandDescriptionId++;
        }

        private bool IsAbilityNameEntry(LocalizationEntry _entry)
        {
            return IsNameLocalizationId(_entry.id) &&
                   string.IsNullOrEmpty(_entry.enumType) &&
                   string.IsNullOrEmpty(_entry.enumValue) &&
                   string.IsNullOrEmpty(_entry.key) == false &&
                   _entry.key.EndsWith(AbilityNameKeySuffix, StringComparison.Ordinal);
        }

        private bool IsCommandDescriptionEntry(LocalizationEntry _entry)
        {
            return _entry.id >= AbilityCommandDescriptionLocalizationIdStart &&
                   string.IsNullOrEmpty(BuildEnumLookupKey(_entry.enumType, _entry.enumValue)) == false;
        }

        private bool IsNameLocalizationId(int _id)
        {
            return _id >= AbilityNameLocalizationIdStart && _id < AbilityCommandDescriptionLocalizationIdStart;
        }

        private string BuildEnumLookupKey(string _enumType, string _enumValue)
        {
            if (string.IsNullOrWhiteSpace(_enumType) || string.IsNullOrWhiteSpace(_enumValue))
                return string.Empty;

            return _enumType.Trim() + ":" + _enumValue.Trim().ToLowerInvariant();
        }

        private LocalizationEntry GetEntryOrDefault(int _id)
        {
            if (indexById.TryGetValue(_id, out int index))
                return entries[index];

            return new LocalizationEntry();
        }
    }

#endregion


#region Line Sprites

    // 현재 줌 기준 가로/세로 선분에 사용할 스프라이트를 반환한다.
    private Sprite ResolveStraightSprite(float _currentZoom, int _segmentSize, bool _isHorizontal)
    {
        if (_segmentSize >= 8)
            return _isHorizontal ? row8Sprite : col8Sprite;

        return _isHorizontal ? row4Sprite : col4Sprite;
    }

    // 현재 줌과 방향 기준 대각선에 사용할 스프라이트를 반환한다.
    private Sprite ResolveDiagonalSprite(float _currentZoom, int _stepX, int _stepY)
    {
        bool sameDirection = _stepX == _stepY;
        bool useLargeSegment = gridCellSize * _currentZoom >= 16f;

        if (useLargeSegment)
            return sameDirection ? diagSWNE8Sprite : diagSENW8Sprite;

        return sameDirection ? diagSWNE4Sprite : diagSENW4Sprite;
    }

    // 현재 줌 상태 기준으로 라인 렌더링을 다시 갱신한다.
    private void RefreshLines()
    {
        lineRenderer.RefreshLines(currentZoom);
    }

#endregion
}
