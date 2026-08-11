using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class UI_TentAbilityComponent : MonoBehaviour
{
    private const int AbilityLocalizationJsonId = 2;
    private const float DefaultZoom = 1f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 1f;
    private const float ZoomStep = 0.1f;
    private const float ZoomFollowSpeed = 18f;
    private const float ToolTipSpacing = 32f;
    private const float ToolTipVerticalScreenPadding = 16f;
    private const float UnlockRevealDuration = 0.1f;
    private const float UnlockRevealStaggerDelay = 0.025f;
    private const float AutoLevelUpInterval = 0.1f;
    private const float AbilityUpgradeMaxSemitones = 6f;
    private const string ToolTipCostAvailableColor = "54D86A";
    private const string ToolTipCostUnavailableColor = "B94A42";
    private const string ToolTipCostMaxLevelColor = "58D7F2";
    private const string ToolTipValueColor = "54D86A";
    private static readonly Color CanApplyNodeColor = new Color32(84, 216, 106, 255);
    private static readonly Color CompletedColor = new Color32(88, 215, 242, 255);
    private static readonly Color CannotApplyNodeColor = new Color32(185, 74, 66, 255);
    private static readonly Color DefaultLineColor = new Color32(255, 255, 255, 255);

    private ISkillSystemProvider skillSystemProvider;
    private LocalizationManager localizationManager;
    private Canvas rootCanvas;
    private bool hasDraggedCurrentPress;
    private bool hasZoomFocus;
    private bool hasPreviousMousePosition;
    private Vector2 previousMousePosition;
    private Vector2 zoomFocusScreenPosition;
    private Vector2 currentViewShakeOffset;
    private float currentZoom = DefaultZoom;
    private float targetZoom = DefaultZoom;
    private float viewShakeElapsed;
    private float closeFadeElapsed;
    private float openingZoomElapsed;
    private float circleRevealElapsed;
    private float openingZoomStart;
    private float openingZoomTarget;
    private float nodeHoverSoundEnableUnscaledTime;
    private bool isViewShaking;
    private bool isCloseFading;
    private bool isOpeningZoomReveal;
    private bool isCircleRevealPlaying;
    private bool hasOpenedView;
    private bool hasSavedView;
    private float savedZoom = DefaultZoom;
    private Vector2 savedViewPosition = Vector2.zero;
    private Vector2 openingZoomFocusPoint = Vector2.zero;
    private CanvasGroup abilityCanvasGroup;
    private TentUICircleRevealStencil circleRevealStencilMask;

    private readonly Dictionary<SkillType, AbilityNodeDefinitionJson> nodeDefinitionMap = new Dictionary<SkillType, AbilityNodeDefinitionJson>();
    private readonly List<SkillType> nodeBuildOrder = new List<SkillType>();
    private readonly Dictionary<SkillType, Sprite> pictureSpriteMap = new Dictionary<SkillType, Sprite>();
    private readonly Dictionary<AbilityLevelBadgeType, Sprite> levelBadgeSpriteMap = new Dictionary<AbilityLevelBadgeType, Sprite>();
    private readonly List<AbilityNode> spawnedNodes = new List<AbilityNode>();
    private readonly Dictionary<SkillType, AbilityNode> spawnedNodeMap = new Dictionary<SkillType, AbilityNode>();
    private readonly Queue<AbilityNode> nodePool = new Queue<AbilityNode>();
    private readonly List<AbilityNodeUnlockReveal> activeUnlockReveals = new List<AbilityNodeUnlockReveal>(4);
    private readonly List<AutoLevelUpRequest> activeAutoLevelUps = new List<AutoLevelUpRequest>(4);
    private readonly AbilityLineRenderer lineRenderer = new AbilityLineRenderer();

    // 튜토리얼 "도끼를 강화하세요" 스텝 동안 이 화면이 열렸는지. 그 스텝의 퀘스트 안내 UI와 이 화면의
    // 오픈 리빌 연출(원형 리빌 등)이 동시에 보이면 가시성이 나빠져서(겹침), 퀘스트 UI가 화면에서
    // 완전히 사라진 뒤에 특성HUD를 노출하는 데 이 값과 아래 플래그를 함께 사용할 예정이다.
    private bool bIsTutorialState;
    // 위 튜토리얼 스텝의 퀘스트 안내 UI가 화면에서 완전히 사라진 시점에 true가 된다.
    private bool bTutorialUpgradeAxeQuestUIHidden;

    public bool IsTutorialState => bIsTutorialState;
    public bool IsTutorialUpgradeAxeQuestUIHidden => bTutorialUpgradeAxeQuestUIHidden;

    private bool hasBuiltNodes;
    private bool hasPrewarmedNodePool;
    private bool lineLayoutDirty;
    private bool toolTipLayoutDirty;
    private HoverCaptureMode hoverCaptureMode;
    private AbilityNode capturedHoverNode;
    private AbilityNode currentToolTipNode;
    private AbilityNode currentCursorNode;
    private AbilityToolTip toolTipInstance;
    private UISelectionCursor selectionCursorInstance;
    private Material circleRevealDimMaterialInstance;
    private ToolTipPlacementMode toolTipPlacementMode = ToolTipPlacementMode.Right;
    private readonly Dictionary<SkillType, SkillAccumulatedValueChangeData> toolTipPreviewDataMap = new Dictionary<SkillType, SkillAccumulatedValueChangeData>();

    private enum ToolTipPlacementMode
    {
        Right,
        Left
    }

    private enum HoverCaptureMode
    {
        None,
        Empty,
        Node
    }

    private struct PrestigeHUDState
    {
        public static PrestigeHUDState Invalid => new PrestigeHUDState(false, 0, 0, 1);

        public bool IsValid { get; }
        public int PrestigeLevel { get; }
        public int Experience { get; }
        public int ExperienceLimit { get; }

        public PrestigeHUDState(int _prestigeLevel, int _experience, int _experienceLimit)
            : this(true, _prestigeLevel, _experience, _experienceLimit)
        {
        }

        private PrestigeHUDState(bool _isValid, int _prestigeLevel, int _experience, int _experienceLimit)
        {
            IsValid = _isValid;
            PrestigeLevel = Mathf.Max(0, _prestigeLevel);
            ExperienceLimit = Mathf.Max(1, _experienceLimit);
            Experience = Mathf.Clamp(_experience, 0, ExperienceLimit);
        }
    }

    [Header("UI References")]
    [SerializeField] private RectTransform abilityBackground;
    [SerializeField] private RectTransform moveTarget;
    [SerializeField] private AbilityHUD abilityHUD;

    [Header("Ability Node Setup")]
    [SerializeField] private AbilityNode abilityNodePrefab;
    [SerializeField] private AbilityLine abilityLinePrefab;
    [SerializeField] private TextAsset abilityNodeJson;
    [SerializeField] private float gridCellSize = 32f;
    [SerializeField] private int prewarmNodePoolCount = 64;
    [SerializeField] private List<AbilityPictureBinding> pictureBindings = new List<AbilityPictureBinding>();
    [SerializeField] private List<AbilityLevelBadgeBinding> levelBadgeBindings = new List<AbilityLevelBadgeBinding>();
    [SerializeField] private List<AbilityLineSegmentSpriteBinding> lineSpriteBindings = new List<AbilityLineSegmentSpriteBinding>();
    [SerializeField] private RectTransform lineParent;

    [Header("ToolTip Setup")]
    [SerializeField] private AbilityToolTip toolTipPrefab;
    [SerializeField] private RectTransform toolTipParent;
    [SerializeField] private float toolTipPlacementHysteresis = 32f;

    [Header("Selection Cursor Setup")]
    [SerializeField] private UISelectionCursor selectionCursorPrefab;
    [SerializeField] private RectTransform selectionCursorParent;
    [SerializeField] private Vector2 selectionCursorSize = new Vector2(40f, 40f);

    [Header("Node Hover Sound")]
    [SerializeField, Min(0f)] private float nodeHoverSoundSuppressDurationAfterOpen = 0.3f;

    [Header("View Bounds")]
    [SerializeField] private Vector2 viewGridHalfExtents = new Vector2(60f, 30f);

    [Header("View Shake Settings")]
    [SerializeField] private float viewShakeDuration = 0.16f;
    [SerializeField] private float viewShakeStrength = 5.6f;
    [SerializeField] private float viewShakeFrequency = 72f;
    [SerializeField, Range(0f, 1f)] private float viewShakeVerticalRatio = 0.45f;

    [Header("Open/Close Animation")]
    [SerializeField] private Image circleMaskImage;
    [SerializeField] private Image circleRevealDimImage;
    [SerializeField] private Material circleRevealDimMaterial;
    [SerializeField] private float closeFadeDuration = 0.2f;
    [SerializeField] private float openZoomRevealDuration = 0.5f;
    [SerializeField] private float openZoomRevealMultiplier = 2f;
    [SerializeField] private float circleRevealDuration = 0.28f;
    [SerializeField] private float circleRevealStartRadius = 4f;
    [SerializeField, Range(0f, 1f)] private float circleRevealDimMaxAlpha = 0.5f;
    [SerializeField] private float circleRevealDimRadiusInsetPixel = 5f;



#region Initializing

    public void Initialize(ISkillSystemProvider _skillSystemProvider, LocalizationManager _localizationManager = null)
    {
        skillSystemProvider = _skillSystemProvider;
        SetLocalizationManager(_localizationManager);
        rootCanvas = GetComponentInParent<Canvas>();
        EnsureAbilityCanvasGroup();
        EnsureCircleRevealMask();
        EnsureCircleRevealDim();
        BindAbilityHUDIfNeeded();
        lineRenderer.Initialize(abilityBackground, moveTarget, lineParent, abilityLinePrefab, rootCanvas, gridCellSize, GetLineColor);
        CachePictureBindings();
        CacheLevelBadgeBindings();
        CacheLineSpriteBindings();
        LoadNodeDefinitions();
        PrewarmNodePool();
        EnsureToolTipInstance();
        EnsureSelectionCursorInstance();
        RefreshLocalizedNodeTexts();
        RefreshAbilityHUDImmediately();
        Close();
    }

    // 이 TentUI 오픈이 튜토리얼 "도끼를 강화하세요" 스텝 중인지를 전달받는다(GameplayUICoordinator가
    // TentUI를 열기 직전에 호출). 튜토리얼이 끝나 있으면 이전에 남아있던 퀘스트 UI 소멸 플래그도 함께 지운다.
    public void SetTutorialState(bool _bIsTutorial)
    {
        bIsTutorialState = _bIsTutorial;

        if (bIsTutorialState == false)
            bTutorialUpgradeAxeQuestUIHidden = false;
    }

    // "도끼를 강화하세요" 퀘스트 안내 UI가 화면에서 완전히 사라진 시점에 GameplayUICoordinator가 호출한다.
    public void NotifyTutorialUpgradeAxeQuestUIHidden()
    {
        bTutorialUpgradeAxeQuestUIHidden = true;
    }

    private void SetLocalizationManager(LocalizationManager _localizationManager)
    {
        if (localizationManager == _localizationManager)
            return;

        if (localizationManager != null)
            localizationManager.OnLanguageChanged -= HandleLanguageChanged;

        localizationManager = _localizationManager;

        if (localizationManager != null)
            localizationManager.OnLanguageChanged += HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        RefreshLocalizedNodeTexts();

        if (currentToolTipNode != null)
            ShowToolTip(currentToolTipNode);
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

    private void CacheLevelBadgeBindings()
    {
        levelBadgeSpriteMap.Clear();

        for (int i = 0; i < levelBadgeBindings.Count; i++)
        {
            AbilityLevelBadgeBinding binding = levelBadgeBindings[i];
            if (binding == null || binding.levelBadge == AbilityLevelBadgeType.None || binding.sprite == null)
                continue;

            levelBadgeSpriteMap[binding.levelBadge] = binding.sprite;
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

    private void EnsureSelectionCursorInstance()
    {
        if (selectionCursorInstance != null || selectionCursorPrefab == null || moveTarget == null)
            return;

        RectTransform parent = selectionCursorParent != null ? selectionCursorParent : moveTarget;
        selectionCursorInstance = Instantiate(selectionCursorPrefab, parent);
        selectionCursorInstance.Initialize(selectionCursorSize);
    }



#endregion


#region Default

    // 능력 화면을 열고 초기 노드 빌드와 가시성 갱신을 수행한다.
    public void Open()
    {
        if (abilityBackground == null)
            return;

        nodeHoverSoundEnableUnscaledTime = Time.unscaledTime + Mathf.Max(0f, nodeHoverSoundSuppressDurationAfterOpen);
        hasOpenedView = true;
        isCloseFading = false;
        isCircleRevealPlaying = false;
        toolTipPlacementMode = ToolTipPlacementMode.Right;
        SetCircleMaskActive(true);
        abilityBackground.gameObject.SetActive(true);
        SetAbilityAlpha(1f);
        SetAbilityInputEnabled(false);
        BuildNodesIfNeeded();
        EnsureCircleRevealStencilReaders();
        SyncNodeLevelsFromProvider();
        RefreshNodeVisibility(false);
        RefreshNodeAvailabilityVisuals();
        RefreshAbilityHUDImmediately();
        RestoreViewOnOpen();
        BeginCircleReveal();
        RefreshOpenTransitionInput();
        RefreshLinesIfNeeded();
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
        node.ApplyDefinition(
            nodeDefinition,
            _skillType,
            ResolveLocalizedEntryText(nodeDefinition.nameLocId),
            ResolvePicture(_skillType),
            ResolveLevelBadgeSprite(ParseLevelBadge(nodeDefinition.levelBadge)),
            gridCellSize);
        spawnedNodes.Add(node);
        spawnedNodeMap[_skillType] = node;

        return node;
    }

    private void RefreshLocalizedNodeTexts()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            if (node == null)
                continue;

            if (nodeDefinitionMap.TryGetValue(node.SkillType, out AbilityNodeDefinitionJson nodeDefinition) == false)
                continue;

            node.ApplyLocalizedText(
                ResolveLocalizedEntryText(nodeDefinition.nameLocId));
        }
    }

    private string ResolveLocalizedEntryText(int _entryId)
    {
        if (localizationManager != null && _entryId > 0)
        {
            string localizedText = localizationManager.GetText(AbilityLocalizationJsonId, _entryId);
            if (string.IsNullOrEmpty(localizedText) == false)
                return localizedText;
        }

        return string.Empty;
    }

    private string ResolveLocalizedText(int _compositeKey)
    {
        if (localizationManager != null)
        {
            string localizedText = localizationManager.GetText(_compositeKey);
            if (string.IsNullOrEmpty(localizedText) == false)
                return localizedText;
        }

        return string.Empty;
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

    private Sprite ResolveLevelBadgeSprite(AbilityLevelBadgeType _levelBadge)
    {
        if (_levelBadge == AbilityLevelBadgeType.None)
            return null;

        if (levelBadgeSpriteMap.TryGetValue(_levelBadge, out Sprite sprite))
            return sprite;

        return null;
    }

    private AbilityLevelBadgeType ParseLevelBadge(string _levelBadge)
    {
        if (string.IsNullOrWhiteSpace(_levelBadge))
            return AbilityLevelBadgeType.None;

        return Enum.TryParse(_levelBadge, true, out AbilityLevelBadgeType parsedLevelBadge)
            ? parsedLevelBadge
            : AbilityLevelBadgeType.None;
    }

    // 저장된 마지막 투자 노드와 줌 상태가 있으면 복원하고, 없으면 기본 위치로 연다.
    private void RestoreViewOnOpen()
    {
        if (moveTarget == null)
            return;

        CancelViewDrag();
        hasZoomFocus = false;
        StopViewShake();
        float finalZoom = hasSavedView ? savedZoom : DefaultZoom;
        Vector2 finalViewPosition = hasSavedView ? savedViewPosition : Vector2.zero;
        float effectiveMinZoom = GetEffectiveMinZoom();
        currentZoom = Mathf.Clamp(finalZoom, effectiveMinZoom, MaxZoom);
        targetZoom = currentZoom;
        moveTarget.localScale = Vector3.one * currentZoom;
        moveTarget.anchoredPosition = ClampViewPosition(finalViewPosition, currentZoom);

        BeginOpenZoomReveal(moveTarget.anchoredPosition, currentZoom);
        MarkViewLayoutDirty();
    }

    private void BeginOpenZoomReveal(Vector2 _finalViewPosition, float _finalZoom)
    {
        openingZoomElapsed = 0f;
        openingZoomTarget = Mathf.Clamp(_finalZoom, GetEffectiveMinZoom(), MaxZoom);
        openingZoomStart = Mathf.Max(openingZoomTarget * Mathf.Max(openZoomRevealMultiplier, 1f), openingZoomTarget);
        openingZoomFocusPoint = -_finalViewPosition / Mathf.Max(openingZoomTarget, 0.0001f);
        isOpeningZoomReveal = openZoomRevealDuration > 0f && Mathf.Approximately(openingZoomStart, openingZoomTarget) == false;

        if (isOpeningZoomReveal == false)
        {
            return;
        }

        currentZoom = openingZoomStart;
        targetZoom = openingZoomTarget;
        ApplyViewZoomForReveal(currentZoom);
    }

    private void ApplyViewZoomForReveal(float _zoom)
    {
        currentZoom = Mathf.Max(_zoom, MinZoom);
        moveTarget.localScale = Vector3.one * currentZoom;
        moveTarget.anchoredPosition = -openingZoomFocusPoint * currentZoom;
        ClampCurrentViewPosition(currentZoom);
    }

    private void EnsureAbilityCanvasGroup()
    {
        if (abilityCanvasGroup != null || abilityBackground == null)
            return;

        abilityCanvasGroup = abilityBackground.GetComponent<CanvasGroup>();
        if (abilityCanvasGroup == null)
            abilityCanvasGroup = abilityBackground.gameObject.AddComponent<CanvasGroup>();
    }

    private void EnsureCircleRevealMask()
    {
        if (circleMaskImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].name == "CircleMask")
                {
                    circleMaskImage = images[i];
                    break;
                }
            }
        }

        if (circleMaskImage == null)
            return;

        PrepareCircleMaskRect(circleRevealStartRadius);
        circleMaskImage.raycastTarget = false;
        circleMaskImage.rectTransform.SetAsLastSibling();
        Mask legacyMask = circleMaskImage.GetComponent<Mask>();
        if (legacyMask != null)
            legacyMask.enabled = false;

        circleRevealStencilMask = circleMaskImage.GetComponent<TentUICircleRevealStencil>();
        if (circleRevealStencilMask == null)
            circleRevealStencilMask = circleMaskImage.gameObject.AddComponent<TentUICircleRevealStencil>();

        circleRevealStencilMask.ConfigureAsMaskWriter();
        circleMaskImage.material = null;

        if (abilityBackground != null && abilityBackground.parent != circleMaskImage.rectTransform)
        {
            abilityBackground.SetParent(circleMaskImage.rectTransform, false);
            PrepareAbilityBackgroundForCircleMask();
            EnsureCircleRevealStencilReaders();
            abilityBackground.SetAsLastSibling();
        }
        else if (abilityBackground != null)
        {
            PrepareAbilityBackgroundForCircleMask();
            EnsureCircleRevealStencilReaders();
        }

        circleMaskImage.gameObject.SetActive(false);
    }

    private void EnsureCircleRevealDim()
    {
        if (circleRevealDimImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].name == "CircleRevealDim")
                {
                    circleRevealDimImage = images[i];
                    break;
                }
            }
        }

        if (circleRevealDimImage == null)
            return;

        PrepareFullScreenRect(circleRevealDimImage.rectTransform);
        circleRevealDimImage.raycastTarget = false;
        circleRevealDimImage.rectTransform.SetAsLastSibling();

        if (circleRevealDimMaterialInstance == null && circleRevealDimMaterial != null)
            circleRevealDimMaterialInstance = new Material(circleRevealDimMaterial);

        if (circleRevealDimMaterialInstance != null)
            circleRevealDimImage.material = circleRevealDimMaterialInstance;

        circleRevealDimImage.gameObject.SetActive(false);
    }

    private void EnsureCircleRevealStencilReaders()
    {
        if (abilityBackground == null)
            return;

        Graphic[] graphics = abilityBackground.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic == circleMaskImage)
                continue;

            TentUICircleRevealStencil stencilReader = graphic.GetComponent<TentUICircleRevealStencil>();
            if (stencilReader == null)
                stencilReader = graphic.gameObject.AddComponent<TentUICircleRevealStencil>();

            stencilReader.ConfigureAsMaskReader();
        }
    }

    private void PrepareCircleMaskRect(float _radius)
    {
        if (circleMaskImage == null)
            return;

        RectTransform rectTransform = circleMaskImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.one * Mathf.Max(_radius * 2f, 0f);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private void PrepareAbilityBackgroundForCircleMask()
    {
        if (abilityBackground == null)
            return;

        abilityBackground.anchorMin = new Vector2(0.5f, 0.5f);
        abilityBackground.anchorMax = new Vector2(0.5f, 0.5f);
        abilityBackground.pivot = new Vector2(0.5f, 0.5f);
        abilityBackground.anchoredPosition = Vector2.zero;
        abilityBackground.sizeDelta = GetCircleMaskFullSize();
        abilityBackground.localScale = Vector3.one;
        abilityBackground.localRotation = Quaternion.identity;
    }

    private void PrepareFullScreenRect(RectTransform _rectTransform)
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

    private void SetAbilityAlpha(float _alpha)
    {
        EnsureAbilityCanvasGroup();
        if (abilityCanvasGroup == null)
            return;

        abilityCanvasGroup.alpha = Mathf.Clamp01(_alpha);
    }

    private void SetAbilityInputEnabled(bool _enabled)
    {
        EnsureAbilityCanvasGroup();
        if (abilityCanvasGroup == null)
            return;

        abilityCanvasGroup.interactable = _enabled;
        abilityCanvasGroup.blocksRaycasts = abilityBackground != null && abilityBackground.gameObject.activeSelf;
    }

    private void SetCircleMaskActive(bool _active)
    {
        EnsureCircleRevealMask();
        if (circleMaskImage == null)
            return;

        circleMaskImage.gameObject.SetActive(_active);
    }

    private void SetCircleRevealDimActive(bool _active)
    {
        EnsureCircleRevealDim();
        if (circleRevealDimImage == null)
            return;

        circleRevealDimImage.gameObject.SetActive(_active);
    }

    private void BeginCircleReveal()
    {
        EnsureCircleRevealMask();
        if (circleMaskImage == null || circleRevealDuration <= 0f)
        {
            EndCircleRevealImmediately();
            return;
        }

        circleRevealElapsed = 0f;
        isCircleRevealPlaying = true;
        SetCircleMaskActive(true);
        ApplyCircleRevealRadius(circleRevealStartRadius);
        SetCircleRevealDimActive(circleRevealDimImage != null && circleRevealDimMaterialInstance != null);
    }

    private void EndCircleRevealImmediately()
    {
        isCircleRevealPlaying = false;

        if (circleMaskImage != null)
            ApplyCircleRevealRadius(GetCircleRevealMaxRadius());

        SetCircleRevealDimActive(false);
    }

    private bool UpdateCircleReveal()
    {
        if (circleMaskImage == null)
        {
            EndCircleRevealImmediately();
            return false;
        }

        circleRevealElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(circleRevealDuration, 0.0001f);
        float progress = Mathf.Clamp01(circleRevealElapsed / duration);
        float easedProgress = EaseInCubic(progress);
        float radius = Mathf.Lerp(circleRevealStartRadius, GetCircleRevealMaxRadius(), easedProgress);
        ApplyCircleRevealRadius(radius);

        if (progress < 1f)
            return true;

        EndCircleRevealImmediately();
        RefreshOpenTransitionInput();
        return true;
    }

    private void ApplyCircleRevealRadius(float _radius)
    {
        PrepareCircleMaskRect(_radius);
        PrepareAbilityBackgroundForCircleMask();
        ApplyCircleRevealDim(_radius);
    }

    private void ApplyCircleRevealDim(float _radius)
    {
        if (circleRevealDimImage == null || circleRevealDimMaterialInstance == null)
            return;

        float pixelScaleFactor = GetCanvasPixelScaleFactor();
        float dimRadius = Mathf.Max(0f, _radius * pixelScaleFactor - circleRevealDimRadiusInsetPixel);
        circleRevealDimMaterialInstance.SetVector("_RevealCenter", new Vector4(0.5f, 0.5f, 0f, 0f));
        circleRevealDimMaterialInstance.SetFloat("_RevealRadius", dimRadius);
        circleRevealDimMaterialInstance.SetFloat("_RevealSoftness", Mathf.Max(1f, pixelScaleFactor));
        circleRevealDimMaterialInstance.SetFloat("_OverlayAlpha", circleRevealDimMaxAlpha);
        circleRevealDimImage.SetMaterialDirty();
    }

    private float GetCanvasPixelScaleFactor()
    {
        if (rootCanvas == null)
            return 1f;

        Canvas targetCanvas = rootCanvas.rootCanvas != null ? rootCanvas.rootCanvas : rootCanvas;
        return Mathf.Max(targetCanvas.scaleFactor, 0.0001f);
    }

    private float GetCircleRevealMaxRadius()
    {
        Vector2 fullSize = GetCircleMaskFullSize();
        float width = fullSize.x;
        float height = fullSize.y;

        return Mathf.Sqrt(width * width + height * height) * 0.5f + 4f;
    }

    private Vector2 GetCircleMaskFullSize()
    {
        RectTransform parentRectTransform = circleMaskImage != null ? circleMaskImage.rectTransform.parent as RectTransform : null;
        if (parentRectTransform != null && parentRectTransform.rect.width > 0f && parentRectTransform.rect.height > 0f)
            return parentRectTransform.rect.size;

        RectTransform rootRectTransform = transform as RectTransform;
        if (rootRectTransform != null && rootRectTransform.rect.width > 0f && rootRectTransform.rect.height > 0f)
            return rootRectTransform.rect.size;

        if (rootCanvas != null && rootCanvas.pixelRect.width > 0f && rootCanvas.pixelRect.height > 0f)
        {
            float scaleFactor = Mathf.Max(rootCanvas.scaleFactor, 0.0001f);
            return rootCanvas.pixelRect.size / scaleFactor;
        }

        return new Vector2(Screen.width, Screen.height);
    }

    private void RefreshOpenTransitionInput()
    {
        SetAbilityInputEnabled(isOpeningZoomReveal == false && isCircleRevealPlaying == false && isCloseFading == false);
    }

    private void SaveCurrentView()
    {
        if (moveTarget == null)
            return;

        Vector2 currentViewPosition = moveTarget.anchoredPosition - currentViewShakeOffset;
        float sourceZoom = Mathf.Max(currentZoom, 0.0001f);
        Vector2 currentViewCenter = -currentViewPosition / sourceZoom;

        savedZoom = Mathf.Clamp(currentZoom, GetEffectiveMinZoom(), MaxZoom);
        savedViewPosition = ClampViewPosition(-currentViewCenter * savedZoom, savedZoom);
        hasSavedView = true;
    }

    // 능력 화면을 닫고 입력 상태와 툴팁을 정리한다.
    public void Close()
    {
        if (hasOpenedView && abilityBackground != null && abilityBackground.gameObject.activeSelf)
            SaveCurrentView();

        CancelViewDrag();
        hasZoomFocus = false;
        isOpeningZoomReveal = false;
        openingZoomFocusPoint = Vector2.zero;
        StopAllAutoLevelUps();
        StopAllNodeEffects();
        EndCircleRevealImmediately();
        StopViewShake();
        currentToolTipNode = null;
        currentCursorNode = null;

        if (toolTipInstance != null)
            toolTipInstance.HideImmediately();

        if (selectionCursorInstance != null)
            selectionCursorInstance.Hide();

        if (abilityBackground == null)
            return;

        if (abilityBackground.gameObject.activeSelf && hasBuiltNodes && closeFadeDuration > 0f)
        {
            closeFadeElapsed = 0f;
            isCloseFading = true;
            SetAbilityInputEnabled(false);
            SetAbilityAlpha(1f);
            return;
        }

        SetAbilityAlpha(0f);
        SetAbilityInputEnabled(false);
        if (abilityBackground != null)
            abilityBackground.gameObject.SetActive(false);

        SetCircleMaskActive(false);
        SetCircleRevealDimActive(false);
    }

    public void Refresh()
    {
        BuildNodesIfNeeded();
        EnsureCircleRevealStencilReaders();
        SyncNodeLevelsFromProvider();
        RefreshNodeVisibility(false);
        RefreshNodeAvailabilityVisuals();
        RefreshAbilityHUDImmediately();

        if (currentToolTipNode != null)
            ShowToolTip(currentToolTipNode);
    }


#endregion


#region Node Hover Capture

    // 드래그 중에는 눌렀던 노드 하나만 Hover 연출의 소유권을 유지한다.
    public bool CanShowNodeHover(AbilityNode _node)
    {
        if (_node == null)
            return false;

        return hoverCaptureMode == HoverCaptureMode.None ||
               (hoverCaptureMode == HoverCaptureMode.Node && capturedHoverNode == _node);
    }

    public bool ShouldKeepNodeHoverCaptured(AbilityNode _node)
    {
        return _node != null &&
               hoverCaptureMode == HoverCaptureMode.Node &&
               capturedHoverNode == _node;
    }

    public void CaptureNodeHover(AbilityNode _node)
    {
        if (_node == null || _node.IsPointerInside == false || IsViewInputEnabled() == false)
            return;

        if (hoverCaptureMode == HoverCaptureMode.Node && capturedHoverNode != _node)
            return;

        hoverCaptureMode = HoverCaptureMode.Node;
        capturedHoverNode = _node;
        _node.RefreshHoverAfterCapture();
    }

    public void ReleaseNodeHoverCapture(AbilityNode _node)
    {
        if (_node == null || capturedHoverNode != _node)
            return;

        ReleaseCapturedNodeHover(true);
    }

    public void NotifyNodeHoverUnavailable(AbilityNode _node)
    {
        if (capturedHoverNode == _node)
        {
            capturedHoverNode = null;
            hoverCaptureMode = HoverCaptureMode.Empty;
        }
    }

    private void BeginEmptyHoverCapture()
    {
        if (hoverCaptureMode != HoverCaptureMode.None)
            return;

        hoverCaptureMode = HoverCaptureMode.Empty;
        capturedHoverNode = null;
    }

    private void ReleaseCapturedNodeHover(bool _restoreCurrentPointerHover)
    {
        if (hoverCaptureMode == HoverCaptureMode.None)
            return;

        AbilityNode releasedNode = capturedHoverNode;
        hoverCaptureMode = HoverCaptureMode.None;
        capturedHoverNode = null;
        releasedNode?.RefreshHoverAfterCapture();

        if (_restoreCurrentPointerHover == false)
            return;

        for (int i = spawnedNodes.Count - 1; i >= 0; i--)
        {
            AbilityNode node = spawnedNodes[i];
            if (node == null || node == releasedNode || node.IsPointerInside == false)
                continue;

            node.RefreshHoverAfterCapture();
            break;
        }
    }

#endregion


#region Selection Cursor

    public void ShowSelectionCursor(AbilityNode _node)
    {
        if (_node == null)
            return;

        currentCursorNode = _node;
        EnsureSelectionCursorInstance();
        if (selectionCursorInstance == null)
            return;

        selectionCursorInstance.Show(_node.RectTransform);
    }

    public void PlayNodeHoverSound()
    {
        if (Time.unscaledTime < nodeHoverSoundEnableUnscaledTime)
            return;

        Sound.PlayUI(SoundID.AbilityHover);
    }

    public void HideSelectionCursor(AbilityNode _node)
    {
        if (currentCursorNode != null && _node != currentCursorNode)
            return;

        currentCursorNode = null;

        if (selectionCursorInstance != null)
            selectionCursorInstance.Hide();
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
        AbilityLevelUpRejectReason applyReason = GetToolTipApplyReason(_node.SkillType);
        RequestToolTipPreviewData(_node.SkillType);
        toolTipPreviewDataMap.TryGetValue(_node.SkillType, out SkillAccumulatedValueChangeData previewData);
        ApplyToolTipContent(_node, skillInfo, applyReason, previewData);

        toolTipInstance.Show();
        Vector2 toolTipSize = toolTipInstance.GetSize();

        Vector3[] worldCorners = new Vector3[4];
        nodeRect.GetWorldCorners(worldCorners);
        Camera eventCamera = GetCanvasEventCamera();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            abilityBackground,
            RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[0]),
            eventCamera,
            out Vector2 localBottomLeft);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            abilityBackground,
            RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[2]),
            eventCamera,
            out Vector2 localTopRight);

        Vector2 nodeCenter = (localBottomLeft + localTopRight) * 0.5f;
        float nodeWidth = Mathf.Abs(localTopRight.x - localBottomLeft.x);
        bool placeOnRight = ResolveToolTipPlaceOnRight(nodeCenter.x);
        float direction = placeOnRight ? 1f : -1f;

        float x = nodeCenter.x + direction * ((nodeWidth * 0.5f) + ToolTipSpacing + (toolTipSize.x * 0.5f));
        float y = ClampToolTipYToScreen(nodeCenter.y, toolTipSize.y);

        toolTipInstance.SetAnchoredPosition(new Vector2(x, y));

        if (shouldPlayShowMotion)
            toolTipInstance.PlayShowMotion();
    }

    // 툴팁의 실제 높이를 기준으로 화면 상하 경계 안에 위치를 제한한다.
    private float ClampToolTipYToScreen(float _requestedY, float _toolTipHeight)
    {
        if (abilityBackground == null)
            return _requestedY;

        Rect screenRect = abilityBackground.rect;
        float halfToolTipHeight = Mathf.Max(_toolTipHeight, 0f) * 0.5f;
        float padding = Mathf.Max(ToolTipVerticalScreenPadding, 0f);
        float minY = screenRect.yMin + halfToolTipHeight + padding;
        float maxY = screenRect.yMax - halfToolTipHeight - padding;

        if (minY > maxY)
            return screenRect.center.y;

        return Mathf.Clamp(_requestedY, minY, maxY);
    }

    private bool ResolveToolTipPlaceOnRight(float _nodeCenterX)
    {
        float centerX = abilityBackground != null ? abilityBackground.rect.center.x : 0f;
        float localCenterOffsetX = _nodeCenterX - centerX;
        float hysteresis = Mathf.Max(0f, toolTipPlacementHysteresis);

        if (toolTipPlacementMode == ToolTipPlacementMode.Right)
        {
            if (localCenterOffsetX > hysteresis)
                toolTipPlacementMode = ToolTipPlacementMode.Left;
        }
        else if (localCenterOffsetX < -hysteresis)
        {
            toolTipPlacementMode = ToolTipPlacementMode.Right;
        }

        return toolTipPlacementMode == ToolTipPlacementMode.Right;
    }

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
            nextCost = 0L
        };
    }

    // 툴팁 제목과 현재/최대 레벨 문자열을 만든다.
    private string BuildToolTipTitleText(AbilityNode _node)
    {
        return _node.DisplayName;
    }

    private string BuildToolTipLevelText(SkillInfo _skillInfo)
    {
        int maxLevel = Mathf.Max(_skillInfo.maxLevel, 0);
        return $"{ResolveLocalizedText(LocKeys.AbilityUI.commonLevel)} : {_skillInfo.currentLevel} / {maxLevel}";
    }

    private string BuildToolTipCostText(SkillInfo _skillInfo, AbilityLevelUpRejectReason _applyReason, out MoneyType _costMoneyType)
    {
        _costMoneyType = MoneyType.None;

        if (IsMaxLevel(_skillInfo, _applyReason))
            return BuildToolTipColorText($"<WAVE>{ResolveLocalizedText(LocKeys.AbilityUI.commonMaxLevel)}</WAVE>", ToolTipCostMaxLevelColor);

        if (_skillInfo.nextCost <= 0 || _skillInfo.moneyType == MoneyType.None || _skillInfo.moneyType == MoneyType.Max)
            return ResolveLocalizedText(LocKeys.AbilityUI.commonFree);

        _costMoneyType = _skillInfo.moneyType;
        string costText = AbilityNumberFormatter.FormatCompact(_skillInfo.nextCost);
        string color = GetToolTipCostColor(_applyReason);
        return string.IsNullOrEmpty(color) ? costText : BuildToolTipColorText(costText, color);
    }

    private bool IsMaxLevel(SkillInfo _skillInfo, AbilityLevelUpRejectReason _applyReason)
    {
        return (_skillInfo.maxLevel > 0 && _skillInfo.currentLevel >= _skillInfo.maxLevel) ||
               _applyReason == AbilityLevelUpRejectReason.MaxLevel;
    }

    private void RequestToolTipPreviewData(SkillType _skillType)
    {
        if (skillSystemProvider == null || _skillType == SkillType.None)
            return;

        toolTipPreviewDataMap.Remove(_skillType);
        skillSystemProvider.RequestSkillValuePreviewData(_skillType);
    }

    public void SkillAccumulatedValuePreviewProvided(SkillAccumulatedValueChangeData _data)
    {
        if (currentToolTipNode == null)
            return;

        SkillType skillType = currentToolTipNode.SkillType;
        toolTipPreviewDataMap[skillType] = _data;

        if (toolTipInstance == null || toolTipInstance.gameObject.activeSelf == false)
            return;

        SkillInfo skillInfo = GetSkillInfo(skillType);
        AbilityLevelUpRejectReason applyReason = GetToolTipApplyReason(skillType);
        ApplyToolTipContent(currentToolTipNode, skillInfo, applyReason, _data);

        toolTipLayoutDirty = true;
    }

    private void ApplyToolTipContent(AbilityNode _node, SkillInfo _skillInfo, AbilityLevelUpRejectReason _applyReason, SkillAccumulatedValueChangeData _previewData)
    {
        if (_node == null || toolTipInstance == null)
            return;

        _previewData = ConvertToToolTipDisplayValue(_previewData);

        string costText = BuildToolTipCostText(_skillInfo, _applyReason, out MoneyType costMoneyType);
        string descriptionFormat = GetToolTipDescriptionFormat(_previewData.type);
        toolTipInstance.SetBackgroundColor(_node.CurrentNodeFrameColor);
        toolTipInstance.SetContent(
            BuildToolTipTitleText(_node),
            BuildToolTipLevelText(_skillInfo),
            BuildToolTipDescriptionText(descriptionFormat, _previewData),
            BuildToolTipValueText(_previewData, IsMaxLevel(_skillInfo, _applyReason), ShouldAppendPercentUnit(descriptionFormat)),
            costText,
            costMoneyType);
    }

    private SkillAccumulatedValueChangeData ConvertToToolTipDisplayValue(SkillAccumulatedValueChangeData _data)
    {
        float baseValue = GetToolTipDisplayBaseValue(_data.type);
        _data.currentValueX += baseValue;
        _data.totalValueZ += baseValue;
        return _data;
    }

    private float GetToolTipDisplayBaseValue(SkillCommandType _type)
    {
        switch (_type)
        {
            case SkillCommandType.AxeDurability:
                return 40f;
            case SkillCommandType.InventoryExpansion:
                return 2f;
            case SkillCommandType.SawmillLogStorageExpansion:
                return 2f;
            case SkillCommandType.WoodenTransportBox:
                return 2f;
            case SkillCommandType.LogCapacityIncrease:
                return 5f;
            case SkillCommandType.ProcessLineExpand:
                return 1f;
            case SkillCommandType.StaminaMaxIncrease:
                return 100f;
            default:
                return 0f;
        }
    }

    private string GetToolTipDescriptionFormat(SkillCommandType _commandType)
    {
        if (_commandType == SkillCommandType.None || localizationManager == null)
            return string.Empty;

        return localizationManager.GetText(_commandType);
    }

    private string BuildToolTipDescriptionText(string _format, SkillAccumulatedValueChangeData _data)
    {
        if (string.IsNullOrEmpty(_format))
            return string.Empty;

        try
        {
            return string.Format(
                _format,
                FormatToolTipValue(_data.addedValueY),
                FormatToolTipValue(_data.currentValueX),
                FormatToolTipValue(_data.totalValueZ));
        }
        catch (FormatException)
        {
            return _format;
        }
    }

    private string BuildToolTipValueText(SkillAccumulatedValueChangeData _data, bool _isMaxLevel, bool _appendPercentUnit)
    {
        if (_data.type == SkillCommandType.None)
            return string.Empty;

        if (ShouldUseUnlockToolTipValueText(_data.type))
            return BuildUnlockToolTipValueText(_isMaxLevel);

        string currentValue = FormatToolTipValue(_data.currentValueX, _appendPercentUnit);
        if (_isMaxLevel)
            return BuildToolTipColorText(currentValue, ToolTipCostMaxLevelColor);

        string totalValue = BuildToolTipColorText(FormatToolTipValue(_data.totalValueZ, _appendPercentUnit), ToolTipValueColor);
        return $"{currentValue} -> {totalValue}";
    }

    private bool ShouldUseUnlockToolTipValueText(SkillCommandType _type)
    {
        switch (_type)
        {
            case SkillCommandType.ShockWaveMastery:
            case SkillCommandType.ShockWaveEnforcement:
            case SkillCommandType.ShockWaveCritical:
            case SkillCommandType.MultiAttack:
            case SkillCommandType.ShieldExplosionUnlock:
            case SkillCommandType.BoomerangCritical:
            case SkillCommandType.ShieldExplosionResearch:
            case SkillCommandType.ConstellationManifestUnlock:
                return true;
            default:
                return false;
        }
    }

    private string BuildUnlockToolTipValueText(bool _isUnlocked)
    {
        int textKey = _isUnlocked ? LocKeys.AbilityUI.commonUnlockComplete : LocKeys.AbilityUI.commonToUnlock;
        string color = _isUnlocked ? ToolTipCostMaxLevelColor : ToolTipValueColor;
        return BuildToolTipColorText(ResolveLocalizedText(textKey), color);
    }

    private string FormatToolTipValue(float _value, bool _appendPercentUnit = false)
    {
        string valueText;
        if (Mathf.Approximately(_value, Mathf.Round(_value)))
            valueText = Mathf.RoundToInt(_value).ToString();
        else
            valueText = _value.ToString("0.##");

        return _appendPercentUnit ? valueText + "%" : valueText;
    }

    private bool ShouldAppendPercentUnit(string _descriptionFormat)
    {
        if (string.IsNullOrEmpty(_descriptionFormat))
            return false;

        return _descriptionFormat.Contains("{0}%") ||
               _descriptionFormat.Contains("{1}%") ||
               _descriptionFormat.Contains("{2}%");
    }

    private AbilityLevelUpRejectReason GetToolTipApplyReason(SkillType _skillType)
    {
        if (skillSystemProvider == null)
            return AbilityLevelUpRejectReason.Pass;

        return NormalizeRejectReason(skillSystemProvider.CanApplySkill(_skillType));
    }

    private string GetToolTipCostColor(AbilityLevelUpRejectReason _applyReason)
    {
        switch (_applyReason)
        {
            case AbilityLevelUpRejectReason.Pass:
                return ToolTipCostAvailableColor;
            case AbilityLevelUpRejectReason.NotEnoughMoney:
                return ToolTipCostUnavailableColor;
            default:
                return string.Empty;
        }
    }

    private string BuildToolTipColorText(string _text, string _color)
    {
        return $"<COLOR={_color}>{_text}</COLOR>";
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

        if (isCloseFading)
        {
            UpdateCloseFade();
            return;
        }

        bool boundsAdjusted = EnsureViewWithinBounds();

        if (isOpeningZoomReveal || isCircleRevealPlaying)
        {
            bool revealViewChanged = boundsAdjusted;
            if (isOpeningZoomReveal)
                revealViewChanged |= UpdateOpenZoomReveal();

            if (isCircleRevealPlaying)
                revealViewChanged |= UpdateCircleReveal();

            if (revealViewChanged)
                MarkViewLayoutDirty();

            UpdateUnlockReveals();
            RefreshLinesIfNeeded();
            UpdateToolTipPositionIfNeeded();
            return;
        }

        // 드래그 이동
        bool viewChanged = boundsAdjusted;

        UpdateAutoLevelUps();
        // 줌 기능
        bool zoomChanged = HandleZoom();
        if (zoomChanged)
            StopViewShake();
        // 줌 애니메이션 기능
        viewChanged |= UpdateZoomAnimation();
        // Line 스냅 및 재구성
        if (viewChanged)
            MarkViewLayoutDirty();

        UpdateUnlockReveals();
        UpdateViewShake();
        RefreshLinesIfNeeded();
        // 툴팁 포지션 스냅
        UpdateToolTipPositionIfNeeded();
    }


    // 마우스 드래그로 능력 컨텐츠를 이동시킨다.
    private void UpdateCloseFade()
    {
        closeFadeElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(closeFadeDuration, 0.0001f);
        float progress = Mathf.Clamp01(closeFadeElapsed / duration);
        SetAbilityAlpha(1f - progress);

        if (progress < 1f)
            return;

        isCloseFading = false;
        SetAbilityAlpha(0f);
        SetAbilityInputEnabled(false);

        if (abilityBackground != null)
            abilityBackground.gameObject.SetActive(false);

        SetCircleMaskActive(false);
        SetCircleRevealDimActive(false);
    }

    private bool UpdateOpenZoomReveal()
    {
        openingZoomElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(openZoomRevealDuration, 0.0001f);
        float progress = Mathf.Clamp01(openingZoomElapsed / duration);
        float easedProgress = EaseOutCubic(progress);
        float previousZoom = currentZoom;
        float nextZoom = Mathf.Lerp(openingZoomStart, openingZoomTarget, easedProgress);

        ApplyViewZoomForReveal(nextZoom);

        if (progress >= 1f)
        {
            isOpeningZoomReveal = false;
            currentZoom = openingZoomTarget;
            targetZoom = openingZoomTarget;
            ApplyViewZoomForReveal(currentZoom);
            RefreshOpenTransitionInput();
            openingZoomFocusPoint = Vector2.zero;
        }

        return Mathf.Approximately(previousZoom, currentZoom) == false;
    }

    private float EaseOutCubic(float _value)
    {
        float inverse = 1f - Mathf.Clamp01(_value);
        return 1f - inverse * inverse * inverse;
    }

    private float EaseInCubic(float _value)
    {
        float value = Mathf.Clamp01(_value);
        return value * value * value;
    }

    private void Update()
    {
        UpdateImmediateViewDrag();
    }

    private void LateUpdate()
    {
        // 포커스 손실 등으로 PointerUp 이벤트가 누락되어도 캡처가 남지 않게 한다.
        if (hoverCaptureMode != HoverCaptureMode.None && IsAnyMouseButtonPressed(Mouse.current) == false)
            ReleaseCapturedNodeHover(true);
    }

    private void UpdateImmediateViewDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ResetViewDragTracking();
            return;
        }

        Vector2 currentMousePosition = mouse.position.ReadValue();
        EnsurePreviousMousePosition(currentMousePosition);

        if (IsViewInputEnabled() == false || IsAnyMouseButtonPressed(mouse) == false)
        {
            hasDraggedCurrentPress = false;
            previousMousePosition = currentMousePosition;
            return;
        }

        // 입력을 누른 순간에는 우선 "Hover 없음"을 캡처한다.
        // 같은 프레임에 노드 PointerDown이 오면 CaptureNodeHover가 노드 캡처로 승격한다.
        BeginEmptyHoverCapture();

        Vector2 delta = currentMousePosition - previousMousePosition;
        previousMousePosition = currentMousePosition;
        if (delta.sqrMagnitude <= 0.0001f)
            return;

        if (hasDraggedCurrentPress == false)
        {
            hasDraggedCurrentPress = true;
            StopViewShake();
        }

        ApplyViewDragScreenDelta(delta);
    }

    private void ApplyViewDragScreenDelta(Vector2 _screenDelta)
    {
        if (moveTarget == null || _screenDelta.sqrMagnitude <= 0.0001f)
            return;

        float scaleFactor = 1f;
        if (rootCanvas != null)
            scaleFactor = Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.0001f);

        Vector2 previousPosition = moveTarget.anchoredPosition;
        Vector2 logicalPosition = previousPosition - currentViewShakeOffset;
        logicalPosition += _screenDelta / scaleFactor;
        moveTarget.anchoredPosition = ClampViewPosition(logicalPosition, currentZoom) + currentViewShakeOffset;

        if ((moveTarget.anchoredPosition - previousPosition).sqrMagnitude <= 0.0001f)
            return;

        MarkViewLayoutDirty();
        RefreshLinesIfNeeded();
        UpdateToolTipPositionIfNeeded();
    }

    private bool IsViewInputEnabled()
    {
        return rootCanvas != null &&
               abilityBackground != null &&
               abilityBackground.gameObject.activeSelf &&
               moveTarget != null &&
               abilityCanvasGroup != null &&
               abilityCanvasGroup.interactable &&
               isOpeningZoomReveal == false &&
               isCircleRevealPlaying == false &&
               isCloseFading == false;
    }

    private static bool IsAnyMouseButtonPressed(Mouse _mouse)
    {
        return _mouse != null &&
               (_mouse.leftButton.isPressed ||
                _mouse.rightButton.isPressed ||
                _mouse.middleButton.isPressed);
    }

    private void EnsurePreviousMousePosition(Vector2 _currentMousePosition)
    {
        if (hasPreviousMousePosition)
            return;

        previousMousePosition = _currentMousePosition;
        hasPreviousMousePosition = true;
    }

    private void ResetViewDragTracking()
    {
        hasDraggedCurrentPress = false;
        hasPreviousMousePosition = false;
    }

    private void CancelViewDrag()
    {
        ReleaseCapturedNodeHover(false);

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ResetViewDragTracking();
            return;
        }

        hasDraggedCurrentPress = false;
        previousMousePosition = mouse.position.ReadValue();
        hasPreviousMousePosition = true;
    }

    // 마우스 휠 입력으로 목표 줌 값을 갱신한다.
    private bool HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
            return false;

        zoomFocusScreenPosition = mouse.position.ReadValue();
        hasZoomFocus = true;
        targetZoom += Mathf.Sign(scrollY) * ZoomStep;
        targetZoom = Mathf.Clamp(targetZoom, GetEffectiveMinZoom(), MaxZoom);
        return true;
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
        ClampCurrentViewPosition(currentZoom);
        return Mathf.Approximately(previousZoom, currentZoom) == false;
    }

    // 마우스가 가리키는 지점을 기준으로 확대/축소가 일어나도록 위치를 보정한다.
    private void ApplyZoomAroundFocus(float _previousZoom, float _currentZoom)
    {
        if (moveTarget == null || hasZoomFocus == false)
            return;

        Camera eventCamera = GetCanvasEventCamera();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moveTarget,
            zoomFocusScreenPosition,
            eventCamera,
            out Vector2 localPointBeforeScale) == false)
            return;

        moveTarget.anchoredPosition += localPointBeforeScale * (_previousZoom - _currentZoom);
    }

    private bool EnsureViewWithinBounds()
    {
        if (moveTarget == null || abilityBackground == null)
            return false;

        bool changed = false;
        float effectiveMinZoom = GetEffectiveMinZoom();
        targetZoom = Mathf.Clamp(targetZoom, effectiveMinZoom, MaxZoom);

        if (currentZoom < effectiveMinZoom)
        {
            Vector2 logicalPosition = moveTarget.anchoredPosition - currentViewShakeOffset;
            Vector2 viewCenter = -logicalPosition / Mathf.Max(currentZoom, 0.0001f);
            currentZoom = effectiveMinZoom;
            moveTarget.localScale = Vector3.one * currentZoom;
            moveTarget.anchoredPosition = -viewCenter * currentZoom + currentViewShakeOffset;
            changed = true;
        }

        return ClampCurrentViewPosition(currentZoom) || changed;
    }

    private float GetEffectiveMinZoom()
    {
        if (abilityBackground == null)
            return MinZoom;

        float horizontalGridLimit = Mathf.Max(Mathf.Abs(viewGridHalfExtents.x), 0.0001f);
        float verticalGridLimit = Mathf.Max(Mathf.Abs(viewGridHalfExtents.y), 0.0001f);
        float horizontalContentHalfSize = horizontalGridLimit * Mathf.Max(gridCellSize, 0.0001f);
        float verticalContentHalfSize = verticalGridLimit * Mathf.Max(gridCellSize, 0.0001f);
        Rect viewportRect = abilityBackground.rect;
        float horizontalRequiredZoom = viewportRect.width * 0.5f / horizontalContentHalfSize;
        float verticalRequiredZoom = viewportRect.height * 0.5f / verticalContentHalfSize;

        return Mathf.Clamp(
            Mathf.Max(MinZoom, horizontalRequiredZoom, verticalRequiredZoom),
            MinZoom,
            MaxZoom);
    }

    private bool ClampCurrentViewPosition(float _zoom)
    {
        if (moveTarget == null)
            return false;

        Vector2 logicalPosition = moveTarget.anchoredPosition - currentViewShakeOffset;
        Vector2 clampedPosition = ClampViewPosition(logicalPosition, _zoom);
        if ((clampedPosition - logicalPosition).sqrMagnitude <= 0.0001f)
            return false;

        moveTarget.anchoredPosition = clampedPosition + currentViewShakeOffset;
        return true;
    }

    private Vector2 ClampViewPosition(Vector2 _position, float _zoom)
    {
        if (abilityBackground == null || moveTarget == null)
            return _position;

        Rect viewportRect = abilityBackground.rect;
        float anchorReferenceX = Mathf.Lerp(viewportRect.xMin, viewportRect.xMax, moveTarget.anchorMin.x);
        float anchorReferenceY = Mathf.Lerp(viewportRect.yMin, viewportRect.yMax, moveTarget.anchorMin.y);
        float viewportLeft = viewportRect.xMin - anchorReferenceX;
        float viewportRight = viewportRect.xMax - anchorReferenceX;
        float viewportBottom = viewportRect.yMin - anchorReferenceY;
        float viewportTop = viewportRect.yMax - anchorReferenceY;
        float contentHalfWidth = Mathf.Abs(viewGridHalfExtents.x) * gridCellSize * Mathf.Max(_zoom, 0f);
        float contentHalfHeight = Mathf.Abs(viewGridHalfExtents.y) * gridCellSize * Mathf.Max(_zoom, 0f);
        float minX = viewportRight - contentHalfWidth;
        float maxX = viewportLeft + contentHalfWidth;
        float minY = viewportTop - contentHalfHeight;
        float maxY = viewportBottom + contentHalfHeight;

        _position.x = minX <= maxX ? Mathf.Clamp(_position.x, minX, maxX) : (minX + maxX) * 0.5f;
        _position.y = minY <= maxY ? Mathf.Clamp(_position.y, minY, maxY) : (minY + maxY) * 0.5f;
        return _position;
    }

    private Camera GetCanvasEventCamera()
    {
        Canvas targetCanvas = rootCanvas != null && rootCanvas.rootCanvas != null ? rootCanvas.rootCanvas : rootCanvas;
        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return targetCanvas.worldCamera;
    }

    private void PlayViewShake()
    {
        if (moveTarget == null)
            return;

        StopViewShake();
        isViewShaking = true;
        viewShakeElapsed = 0f;
    }

    private void UpdateViewShake()
    {
        if (isViewShaking == false || moveTarget == null)
            return;

        viewShakeElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(viewShakeDuration, 0.0001f);
        float progress = Mathf.Clamp01(viewShakeElapsed / duration);
        float strength = viewShakeStrength * (1f - progress);
        float time = viewShakeElapsed * viewShakeFrequency;
        float x = Mathf.Sin(time) * strength;
        float y = Mathf.Sin(time * 1.7f + 0.8f) * strength * viewShakeVerticalRatio;

        SetViewShakeOffset(new Vector2(Mathf.Round(x), Mathf.Round(y)));

        if (progress >= 1f)
            StopViewShake();
    }

    private void StopViewShake()
    {
        if (moveTarget != null)
            SetViewShakeOffset(Vector2.zero);

        isViewShaking = false;
        viewShakeElapsed = 0f;
    }

    private void SetViewShakeOffset(Vector2 _offset)
    {
        if (moveTarget == null)
            return;

        Vector2 logicalPosition = moveTarget.anchoredPosition - currentViewShakeOffset;
        Vector2 clampedPosition = ClampViewPosition(logicalPosition + _offset, currentZoom);
        Vector2 appliedOffset = clampedPosition - logicalPosition;
        Vector2 offsetDelta = appliedOffset - currentViewShakeOffset;
        moveTarget.anchoredPosition = clampedPosition;
        currentViewShakeOffset = appliedOffset;

        RectTransform lineShakeTarget = GetLineShakeTarget();
        if (lineShakeTarget != null)
            lineShakeTarget.anchoredPosition += offsetDelta;

        Vector2 interactionCompensation = -appliedOffset / Mathf.Max(currentZoom, 0.0001f);
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            if (node != null)
                node.SetInteractionShakeCompensation(interactionCompensation);
        }
    }

    private RectTransform GetLineShakeTarget()
    {
        if (lineParent == null)
            return null;

        if (moveTarget != null && lineParent.IsChildOf(moveTarget))
            return null;

        return lineParent;
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

        return GetNodeStateColor(childNode);
    }

    // MaxLevel까지 찍힌 노드로 들어오는 라인만 일반 라인보다 위에 그린다.
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
        if (isOpeningZoomReveal || isCloseFading)
            return false;

        if (_node == null)
            return false;

        SkillType requestedSkillType = _node.SkillType;

        return OnAbilityLevelUpRequested(requestedSkillType);
    }

    public bool TryRequestNodeLevelUpWithoutCost(AbilityNode _node)
    {
        if (isOpeningZoomReveal || isCloseFading)
            return false;

        if (_node == null)
            return false;

        SkillType requestedSkillType = _node.SkillType;

        return OnAbilityLevelUpWithoutCostRequested(requestedSkillType);
    }

    private class AutoLevelUpRequest
    {
        public AbilityNode Node { get; }
        public bool WithoutCost { get; set; }
        public float Elapsed { get; set; }

        public AutoLevelUpRequest(AbilityNode _node, bool _withoutCost)
        {
            Node = _node;
            WithoutCost = _withoutCost;
            Elapsed = AutoLevelUpInterval;
        }
    }

    public void StartAutoNodeLevelUp(AbilityNode _node, bool _withoutCost = false)
    {
        if (isOpeningZoomReveal || isCloseFading || _node == null || skillSystemProvider == null)
            return;

        if (CanRequestAutoNodeLevelUp(_node, _withoutCost) == false)
        {
            AbilityLevelUpRejectReason rejectReason = NormalizeRejectReason(skillSystemProvider.CanApplySkill(_node.SkillType));
            OnAbilityLevelUpRejected(_node.SkillType, rejectReason);
            _node.PlayRejectedRequestMotion();
            return;
        }

        for (int i = 0; i < activeAutoLevelUps.Count; i++)
        {
            AutoLevelUpRequest request = activeAutoLevelUps[i];
            if (request != null && request.Node == _node)
            {
                request.WithoutCost = _withoutCost;
                request.Elapsed = AutoLevelUpInterval;
                return;
            }
        }

        activeAutoLevelUps.Add(new AutoLevelUpRequest(_node, _withoutCost));
    }

    private void UpdateAutoLevelUps()
    {
        if (activeAutoLevelUps.Count == 0)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        for (int i = activeAutoLevelUps.Count - 1; i >= 0; i--)
        {
            AutoLevelUpRequest request = activeAutoLevelUps[i];
            AbilityNode node = request?.Node;
            if (node == null || node.gameObject.activeInHierarchy == false || CanRequestAutoNodeLevelUp(node, request.WithoutCost) == false)
            {
                activeAutoLevelUps.RemoveAt(i);
                continue;
            }

            request.Elapsed += deltaTime;
            if (request.Elapsed < AutoLevelUpInterval)
                continue;

            request.Elapsed = 0f;
            if (TryRequestAutoNodeLevelUp(request))
            {
                node.PlayClickRequestMotion();
                continue;
            }

            node.PlayRejectedRequestMotion();
            activeAutoLevelUps.RemoveAt(i);
        }
    }

    private bool TryRequestAutoNodeLevelUp(AutoLevelUpRequest _request)
    {
        if (_request == null)
            return false;

        return _request.WithoutCost
            ? TryRequestNodeLevelUpWithoutCost(_request.Node)
            : TryRequestNodeLevelUp(_request.Node);
    }

    private bool CanRequestAutoNodeLevelUp(AbilityNode _node, bool _withoutCost)
    {
        if (_withoutCost)
            return _node != null && skillSystemProvider != null;

        return CanRequestNodeLevelUp(_node);
    }

    private bool CanRequestNodeLevelUp(AbilityNode _node)
    {
        if (_node == null || skillSystemProvider == null)
            return false;

        return skillSystemProvider.CanApplySkill(_node.SkillType) == AbilityLevelUpRejectReason.Pass;
    }

    private void StopAllAutoLevelUps()
    {
        activeAutoLevelUps.Clear();
    }

    private void StopAllNodeEffects()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            if (node != null)
                node.StopAllEffectsImmediately();
        }
    }


    // 상위 로직에 어떤 스킬을 찍으려는지 전달하는 자리다.
    private bool OnAbilityLevelUpRequested(SkillType _skillType)
    {
        if (skillSystemProvider == null)
        {
            Debug.LogWarning($"SkillSystemProvider is null. Request skipped: {_skillType}");
            return false;
        }

        PrestigeHUDState _previousHUDState = GetPrestigeHUDState();
        AbilityLevelUpRejectReason reason = skillSystemProvider.TryApplySkill(_skillType);

        if (reason == AbilityLevelUpRejectReason.Pass)
        {
            OnAbilityLevelUpApproved(_skillType, _previousHUDState, GetPrestigeHUDState());
            return true;
        }
        else
        {
            OnAbilityLevelUpRejected(_skillType, NormalizeRejectReason(reason));
            return false;
        }
    }

    // 상위 시스템의 세부 실패 사유를 UI에서 사용할 공통 사유로 정리한다.
    private bool OnAbilityLevelUpWithoutCostRequested(SkillType _skillType)
    {
        if (skillSystemProvider == null)
        {
            Debug.LogWarning($"SkillSystemProvider is null. Request skipped: {_skillType}");
            return false;
        }

        PrestigeHUDState _previousHUDState = GetPrestigeHUDState();
        AbilityLevelUpRejectReason reason = skillSystemProvider.TryApplySkillWithoutCost(_skillType);

        if (reason == AbilityLevelUpRejectReason.Pass)
        {
            OnAbilityLevelUpApproved(_skillType, _previousHUDState, GetPrestigeHUDState());
            return true;
        }
        else
        {
            OnAbilityLevelUpRejected(_skillType, NormalizeRejectReason(reason));
            return false;
        }
    }

    private AbilityLevelUpRejectReason NormalizeRejectReason(AbilityLevelUpRejectReason _reason)
    {
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
        OnAbilityLevelUpApproved(_skillType, GetPrestigeHUDState(), GetPrestigeHUDState());
    }

    private void OnAbilityLevelUpApproved(SkillType _skillType, PrestigeHUDState _previousHUDState, PrestigeHUDState _currentHUDState)
    {
        if (spawnedNodeMap.TryGetValue(_skillType, out AbilityNode node) == false)
            return;

        bool wasLockedByLevel = node.IsUnlockedByLevel() == false;
        SyncNodeLevelsFromProvider();
        SkillInfo upgradedSkillInfo = GetSkillInfo(_skillType);
        PlayAbilityUpgradeSounds(upgradedSkillInfo);
        bool prestigeIncreased = _currentHUDState.IsValid &&
            _previousHUDState.IsValid &&
            _currentHUDState.PrestigeLevel > _previousHUDState.PrestigeLevel;

        // 해금되는 순간임
        if ((wasLockedByLevel && node.IsUnlockedByLevel()) || prestigeIncreased)
            RefreshNodeVisibility(true);
        else
            RefreshLines();

        RefreshNodeAvailabilityVisuals();

        if (currentToolTipNode == node)
        {
            ShowToolTip(node);
            if (toolTipInstance != null)
                toolTipInstance.PlayClickMotion();
        }

        PlayViewShake();
        PlayAbilityHUDEffect(_previousHUDState, _currentHUDState);
    }

    // 상위 로직에서 거절 및 이유 (연출을 위함임)
    public void OnAbilityLevelUpRejected(SkillType _skillType, AbilityLevelUpRejectReason _rejectReason)
    {
        if (spawnedNodeMap.TryGetValue(_skillType, out AbilityNode node) == false)
            return;

        if (_rejectReason == AbilityLevelUpRejectReason.MaxLevel ||
            _rejectReason == AbilityLevelUpRejectReason.NotEnoughMoney)
        {
            Sound.PlayUI(SoundID.AbilityUpgradeFailed);
        }

        if (currentToolTipNode == node)
            ShowToolTip(node);
    }

    private void PlayAbilityUpgradeSounds(SkillInfo _skillInfo)
    {
        int maxLevel = Mathf.Max(_skillInfo.maxLevel, 1);
        int reachedLevel = Mathf.Clamp(_skillInfo.currentLevel, 1, maxLevel);
        float reachedRatio = (float)reachedLevel / maxLevel;
        float semitones = AbilityUpgradeMaxSemitones * reachedRatio;
        float pitch = Mathf.Pow(2f, semitones / 12f);

        Sound.PlayUI(SoundID.AbilityUpgrade, 1f, pitch);

        if (_skillInfo.maxLevel > 0 && _skillInfo.currentLevel >= _skillInfo.maxLevel)
        {
            Sound.PlayUI(SoundID.AbilityFinalUpgrade);
            Sound.PlayUI(SoundID.AbilityFinalEX);
            Sound.PlayUI(SoundID.AbilityFinalEX2);
        }
    }



    // 부모 레벨 기준으로 자식 노드와 라인의 노출 상태를 갱신한다.
    // 상위 스킬 시스템이 가진 실제 레벨 상태를 UI 노드에 반영한다.
    private void BindAbilityHUDIfNeeded()
    {
        if (abilityHUD != null)
            return;

        abilityHUD = GetComponentInChildren<AbilityHUD>(true);
        if (abilityHUD != null)
            return;

        Transform parentTransform = transform.parent;
        while (parentTransform != null && abilityHUD == null)
        {
            abilityHUD = parentTransform.GetComponentInChildren<AbilityHUD>(true);
            parentTransform = parentTransform.parent;
        }
    }

    private void RefreshAbilityHUDImmediately()
    {
        BindAbilityHUDIfNeeded();

        if (abilityHUD == null || skillSystemProvider == null)
            return;

        PrestigeHUDState _state = GetPrestigeHUDState();
        abilityHUD.SetState(_state.Experience, _state.ExperienceLimit, _state.PrestigeLevel);
    }

    private void OnDestroy()
    {
        SetLocalizationManager(null);
    }

    private void PlayAbilityHUDEffect(PrestigeHUDState _previousState, PrestigeHUDState _currentState)
    {
        BindAbilityHUDIfNeeded();

        if (abilityHUD == null || false == _currentState.IsValid)
            return;

        if (false == _previousState.IsValid)
        {
            abilityHUD.SetState(_currentState.Experience, _currentState.ExperienceLimit, _currentState.PrestigeLevel);
            return;
        }

        if (_currentState.PrestigeLevel > _previousState.PrestigeLevel)
        {
            abilityHUD.SetState(_previousState.ExperienceLimit, _previousState.ExperienceLimit, _previousState.PrestigeLevel);
            abilityHUD.ResetExperience_Effect(_currentState.Experience, _currentState.ExperienceLimit, _currentState.PrestigeLevel);
            return;
        }

        if (_currentState.Experience != _previousState.Experience ||
            _currentState.ExperienceLimit != _previousState.ExperienceLimit)
        {
            abilityHUD.SetFlowerStack(_currentState.PrestigeLevel);
            abilityHUD.SetExperience_Effect(_currentState.Experience, _currentState.ExperienceLimit);
            return;
        }

        abilityHUD.SetState(_currentState.Experience, _currentState.ExperienceLimit, _currentState.PrestigeLevel);
    }

    private PrestigeHUDState GetPrestigeHUDState()
    {
        if (skillSystemProvider == null)
            return PrestigeHUDState.Invalid;

        return new PrestigeHUDState(
            skillSystemProvider.GetCurrentPrestigeLevel(),
            skillSystemProvider.GetCurrentPrestigeExp(),
            skillSystemProvider.GetPrestigeExpLimit());
    }

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

    private void RefreshNodeVisibility(bool _playUnlockReveal)
    {
        bool assignedAppearSound = false;

        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            bool wasVisible = node != null && node.gameObject.activeSelf;
            bool isVisible = ShouldShowNode(node);
            node.gameObject.SetActive(isVisible);

            if (_playUnlockReveal && wasVisible == false && isVisible)
            {
                StartUnlockReveal(node, assignedAppearSound == false);
                assignedAppearSound = true;
            }

            if (isVisible == false && currentToolTipNode == node)
                HideToolTip(node);

            if (isVisible == false && currentCursorNode == node)
                HideSelectionCursor(node);
        }

        RefreshLines();
    }

    private void StartUnlockReveal(AbilityNode _node, bool _playAppearSound)
    {
        if (_node == null)
            return;

        for (int i = 0; i < activeUnlockReveals.Count; i++)
        {
            if (activeUnlockReveals[i].Node != _node)
                continue;

            activeUnlockReveals[i].Elapsed = 0f;
            activeUnlockReveals[i].Delay = GetUnlockRevealDelay();
            activeUnlockReveals[i].PlayAppearSound = _playAppearSound;
            lineRenderer.SetLineRevealProgress(_node.SkillType, 0f);
            _node.SetVisualVisible(false);
            lineLayoutDirty = true;
            return;
        }

        lineRenderer.SetLineRevealProgress(_node.SkillType, 0f);
        _node.SetVisualVisible(false);
        activeUnlockReveals.Add(new AbilityNodeUnlockReveal(_node, GetUnlockRevealDelay(), _playAppearSound));
        lineLayoutDirty = true;
    }

    private void UpdateUnlockReveals()
    {
        if (activeUnlockReveals.Count == 0)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        for (int i = activeUnlockReveals.Count - 1; i >= 0; i--)
        {
            AbilityNodeUnlockReveal reveal = activeUnlockReveals[i];
            if (reveal == null || reveal.Node == null || reveal.Node.gameObject.activeSelf == false)
            {
                activeUnlockReveals.RemoveAt(i);
                continue;
            }

            reveal.Elapsed += deltaTime;
            float progress = Mathf.Clamp01((reveal.Elapsed - reveal.Delay) / UnlockRevealDuration);
            lineRenderer.SetLineRevealProgress(reveal.Node.SkillType, progress);
            lineLayoutDirty = true;

            if (progress < 1f)
                continue;

            lineRenderer.ClearLineRevealProgress(reveal.Node.SkillType);
            reveal.Node.SetVisualVisible(true);

            if (reveal.PlayAppearSound)
                Sound.PlayUI(SoundID.AbilityAppear);

            reveal.Node.PlayUnlockAppearMotion();
            activeUnlockReveals.RemoveAt(i);
        }
    }

    private float GetUnlockRevealDelay()
    {
        return activeUnlockReveals.Count * UnlockRevealStaggerDelay;
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

            SkillInfo skillInfo = GetSkillInfo(node.SkillType);
            node.ApplyLevelProgressBar(skillInfo.currentLevel, skillInfo.maxLevel);

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
    private Color GetNodeStateColor(AbilityNode _node)
    {
        if (_node == null)
            return DefaultLineColor;

        if (_node.CompletedVisual)
            return CompletedColor;

        if (_node.CanApplyVisual)
            return CanApplyNodeColor;

        return CannotApplyNodeColor;
    }

    private bool ShouldShowNode(AbilityNode _node)
    {
        if (_node == null)
            return false;

        if (SatisfiesPrestigeRequirement(_node) == false)
            return false;

        if (skillSystemProvider != null)
            return ShouldShowNodeByProvider(_node);

        return ShouldShowNodeByVisualConnection(_node);
    }

    private bool SatisfiesPrestigeRequirement(AbilityNode _node)
    {
        if (_node == null)
            return false;

        if (_node.RequiredPrestigeLevel <= 0)
            return true;

        if (skillSystemProvider == null)
            return false;

        return skillSystemProvider.GetCurrentPrestigeLevel() >= _node.RequiredPrestigeLevel;
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

public class AbilityNodeUnlockReveal
{
    public AbilityNode Node { get; }
    public float Elapsed { get; set; }
    public float Delay { get; set; }
    public bool PlayAppearSound { get; set; }

    public AbilityNodeUnlockReveal(AbilityNode _node, float _delay, bool _playAppearSound)
    {
        Node = _node;
        Elapsed = 0f;
        Delay = _delay;
        PlayAppearSound = _playAppearSound;
    }
}
