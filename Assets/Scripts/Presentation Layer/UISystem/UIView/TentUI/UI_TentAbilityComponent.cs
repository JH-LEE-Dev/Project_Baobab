using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    private const float KeyboardMoveGridUnitsPerSecond = 9f;
    private const float DefaultPadCursorSpeedPixelsPerSecond = 280f;
    private const float PadCursorSensitivitySpeedMultiplier = 5.6f;

    /// <summary>
    /// 슬라이더가 0이어도 이 값 이하로는 내려가지 않습니다. (0 * 배수 = 정지 방지)
    /// 15 * 5.6 = 84px/s 정도로, 느리지만 화면을 가로지를 수는 있는 속도입니다.
    /// </summary>
    private const float MinPadCursorSensitivity = 15f;
    private const float PadZoomRepeatInterval = 0.1f;
    private const float ToolTipSpacing = 32f;
    private const float ToolTipVerticalScreenPadding = 16f;
    private const float UnlockRevealDuration = 0.1f;
    private const float UnlockRevealStaggerDelay = 0.025f;
#if UNITY_EDITOR
    private const float AutoLevelUpInterval = 0.1f;
#endif
    private const float AbilityUpgradeMaxSemitones = 6f;
    private const string SharedNodeVfxPoolName = "SharedAbilityNodeVFXPool";
    private const string ToolTipCostAvailableColor = "54D86A";
    private const string ToolTipCostUnavailableColor = "B94A42";
    private const string ToolTipCostMaxLevelColor = "58D7F2";
    private const string ToolTipValueColor = "54D86A";
    private static readonly Color CanApplyNodeColor = new Color32(84, 216, 106, 255);
    private static readonly Color CompletedColor = new Color32(88, 215, 242, 255);
    private static readonly Color CannotApplyNodeColor = new Color32(185, 74, 66, 255);
    private static readonly Color DefaultLineColor = new Color32(255, 255, 255, 255);

    private ISkillSystemProvider skillSystemProvider;
    private InputManager inputManager;
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
    // 패드 Hover는 전체 노드를 순회하지 않고 커서 주변의 논리 그리드 좌표만 조회한다.
    // 같은 좌표에 노드가 중복 배치되는 데이터도 안전하게 처리할 수 있도록 List를 값으로 둔다.
    private readonly Dictionary<Vector2Int, List<AbilityNode>> padHoverNodeGridIndex = new Dictionary<Vector2Int, List<AbilityNode>>();
    private readonly Dictionary<SkillType, Sprite> pictureSpriteMap = new Dictionary<SkillType, Sprite>();
    private readonly Dictionary<AbilityLevelBadgeType, Sprite> levelBadgeSpriteMap = new Dictionary<AbilityLevelBadgeType, Sprite>();
    private readonly List<AbilityNode> spawnedNodes = new List<AbilityNode>();
    private readonly Dictionary<SkillType, AbilityNode> spawnedNodeMap = new Dictionary<SkillType, AbilityNode>();
    private readonly Queue<AbilityNode> nodePool = new Queue<AbilityNode>();
    private readonly List<AbilityNodeUnlockReveal> activeUnlockReveals = new List<AbilityNodeUnlockReveal>(4);
#if UNITY_EDITOR
    private readonly List<AutoLevelUpRequest> activeAutoLevelUps = new List<AutoLevelUpRequest>(4);
#endif
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
    private bool nodeViewportLayoutDirty = true;
    private bool toolTipLayoutDirty;
    private HoverCaptureMode hoverCaptureMode;
    private AbilityNode capturedHoverNode;
    private AbilityNode currentToolTipNode;
    private AbilityNode currentCursorNode;
    private AbilityNode currentPadCursorNode;
    private AbilityToolTip toolTipInstance;
    private UISelectionCursor selectionCursorInstance;
    private RectTransform padCursorRect;
    private Image padCursorImage;
    private float currentPadCursorAlpha = 1f;
    // moveTarget 로컬 좌표. 화면이 움직여도 변하지 않는 특성 그리드상의 절대 위치다.
    private Vector2 padCursorGridPosition;
    private Vector2 padCursorScreenPosition;
    private Vector2 padViewFollowVelocity;
    private bool wasPadCameraLookAheadActive;
    private bool isPadCameraRecentering;
    private AbilityNode padSelectionCursorMagnetTargetNode;
    private Vector2 padSelectionCursorMagnetStartPosition;
    private float padSelectionCursorMagnetElapsed;
    private bool isPadSelectionCursorMagnetMoving;
    private int padInputSuppressedFrame = -1;
    private int padZoomHoldDirection;
    private float padZoomRepeatElapsed;
    private bool wasInputAllowedLastTick;
    private VFXComponent sharedNodeVfxPool;
    private Material circleRevealDimMaterialInstance;
    private ButtonControl moveUpControl;
    private ButtonControl moveDownControl;
    private ButtonControl moveLeftControl;
    private ButtonControl moveRightControl;
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

    private enum TentAbilityControlMode
    {
        MouseKeyboard,
        Pad
    }

    private TentAbilityControlMode currentControlMode = TentAbilityControlMode.MouseKeyboard;

    public bool IsMouseKeyboardControlMode => TentAbilityControlMode.MouseKeyboard == currentControlMode;

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

    [Header("Key Guide Localization")]
    [SerializeField] private TMP_Text keyGuideUpgradeText;
    [SerializeField] private TMP_Text keyGuideMoveText;
    [SerializeField] private TMP_Text keyGuideMagnificationText;
    [SerializeField] private TMP_Text keyGuideExitText;

    [Header("Ability Node Setup")]
    [SerializeField] private AbilityNode abilityNodePrefab;
    [SerializeField] private TextAsset abilityNodeJson;
    [SerializeField] private float gridCellSize = 32f;
    [SerializeField] private int prewarmNodePoolCount = 64;
    [SerializeField] private List<AbilityPictureBinding> pictureBindings = new List<AbilityPictureBinding>();
    [SerializeField] private List<AbilityLevelBadgeBinding> levelBadgeBindings = new List<AbilityLevelBadgeBinding>();
    [SerializeField] private List<AbilityLineSegmentSpriteBinding> lineSpriteBindings = new List<AbilityLineSegmentSpriteBinding>();
    [SerializeField] private Material lineMaterial;
    [SerializeField] private RectTransform lineParent;

    [Header("Node Viewport Culling")]
    [SerializeField, Min(0f)] private float nodeViewportCullPadding = 64f;

    [Header("ToolTip Setup")]
    [SerializeField] private AbilityToolTip toolTipPrefab;
    [SerializeField] private RectTransform toolTipParent;
    [SerializeField] private float toolTipPlacementHysteresis = 32f;

    [Header("Selection Cursor Setup")]
    [SerializeField] private UISelectionCursor selectionCursorPrefab;
    [SerializeField] private RectTransform selectionCursorParent;
    [SerializeField] private Vector2 selectionCursorSize = new Vector2(40f, 40f);

    [Header("Pad Cursor")]
    [SerializeField] private Sprite padCursorSprite;
    [SerializeField] private Vector2 padCursorSize = new Vector2(32f, 32f);
    [Tooltip("옵션의 가상 커서 감도(0~100) x 5.6으로 런타임에 갱신됩니다.")]
    [SerializeField, Min(0f)] private float padCursorSpeedPixelsPerSecond = DefaultPadCursorSpeedPixelsPerSecond;
    [Tooltip("TentUI의 왼쪽/오른쪽 스틱 입력에만 적용하는 로컬 데드존입니다.")]
    [SerializeField, Range(0f, 1f)] private float padCursorStickDeadzone = 0.2f;
    [Tooltip("SelectionCursor가 노드를 가리킬 때 PadCursor가 내려갈 알파입니다.")]
    [SerializeField, Range(0f, 1f)] private float padCursorNodeHoverAlpha = 0.2f;
    [Tooltip("PadCursor 알파가 1과 노드 Hover 알파 사이를 이동하는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float padCursorAlphaTransitionDuration = 0.2f;

    [Header("Pad Cursor Hover Correction")]
    [SerializeField] private Vector2 padCursorHoverCorrectionSize = new Vector2(64f, 64f);
    [SerializeField, Min(0f)] private float padCursorHoverReleasePadding = 8f;

    [Header("Pad Selection Cursor Magnet")]
    [SerializeField, Min(0.01f)] private float padSelectionCursorMagnetDuration = 0.35f;

    [Header("Pad Cursor Safe Area")]
    [Tooltip("오른쪽 스틱 최대 입력과 일반 커서 추적이 공유하는 화면상 타원 영역의 전체 크기입니다.")]
    [SerializeField] private Vector2 padCursorSafeAreaSize = new Vector2(450f, 250f);
    [SerializeField, Min(0.1f)] private float padViewFollowMaxGridUnitsPerSecond = 9f;
    [SerializeField, Min(0.01f)] private float padViewFollowSmoothTime = 0.16f;

    [Header("Pad Right Stick Look Ahead")]
    [Tooltip("오른쪽 스틱 Look Ahead와 중앙 복귀의 최대 카메라 이동속도입니다.")]
    [SerializeField, Min(0.1f)] private float padLookAheadMaxGridUnitsPerSecond = 24f;
    [Tooltip("오른쪽 스틱 Look Ahead와 중앙 복귀의 반응 시간입니다. 낮을수록 빠릿합니다.")]
    [SerializeField, Min(0.01f)] private float padLookAheadSmoothTime = 0.04f;

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

    public void Initialize(
        ISkillSystemProvider _skillSystemProvider,
        InputManager _inputManager,
        LocalizationManager _localizationManager = null)
    {
        skillSystemProvider = _skillSystemProvider;
        SetInputManager(_inputManager);
        SetLocalizationManager(_localizationManager);
        rootCanvas = GetComponentInParent<Canvas>();
        EnsureAbilityCanvasGroup();
        EnsureCircleRevealMask();
        EnsureCircleRevealDim();
        BindAbilityHUDIfNeeded();
        lineRenderer.CacheLineSpriteBindings(lineSpriteBindings);
        lineRenderer.Initialize(
            abilityBackground,
            moveTarget,
            lineParent,
            rootCanvas,
            gridCellSize,
            lineMaterial,
            GetLineColor,
            GetLineShineColorIndex);
        CachePictureBindings();
        CacheLevelBadgeBindings();
        LoadNodeDefinitions();
        EnsureSharedNodeVfxPool();
        PrewarmNodePool();
        EnsureToolTipInstance();
        EnsureSelectionCursorInstance();
        EnsurePadCursorInstance();
        RefreshLocalizedNodeTexts();
        RefreshKeyGuideTexts();
        RefreshAbilityHUDImmediately();
        BindPadCursorSensitivity();
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

    private void SetInputManager(InputManager _inputManager)
    {
        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= CacheKeyboardMoveControls;
            inputManager.inputReader.InputDeviceChangedEvent -= OnInputDeviceChanged;
        }

        inputManager = _inputManager;

        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.KeyBindingsChangedEvent += CacheKeyboardMoveControls;
            inputManager.inputReader.InputDeviceChangedEvent += OnInputDeviceChanged;
        }

        CacheKeyboardMoveControls();
    }

    private void BindPadCursorSensitivity()
    {
        SettingsManager _settings = SettingsManager.Instance;
        _settings.OnInputSettingsAppliedEvent -= ApplyPadCursorSensitivity;
        _settings.OnInputSettingsAppliedEvent += ApplyPadCursorSensitivity;
        ApplyPadCursorSensitivity(_settings.Current);
    }

    private void ApplyPadCursorSensitivity(SettingsData _data)
    {
        float _rawSensitivity = _data.virtualCursorSensitivity;
        if (float.IsNaN(_rawSensitivity))
            _rawSensitivity = SettingsData.SLIDER_CENTER_DEFAULT;

        // 하한이 반드시 있어야 한다. 슬라이더 0을 그대로 곱하면 속도가 0이 되어 커서가 아예
        // 움직이지 않고, 그러면 유저는 패드만으로 옵션 화면에 돌아가 값을 되돌릴 수단까지 잃는다.
        // 감도 0은 "느림"이어야지 "끔"이면 안 된다.
        float _sensitivity = Mathf.Clamp(
            _rawSensitivity,
            MinPadCursorSensitivity,
            SettingsData.SLIDER_MAX);
        padCursorSpeedPixelsPerSecond = _sensitivity * PadCursorSensitivitySpeedMultiplier;
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        if (false == hasOpenedView || null == abilityBackground || false == abilityBackground.gameObject.activeSelf)
            return;

        SetControlMode(EInputDeviceType.Gamepad == _device
            ? TentAbilityControlMode.Pad
            : TentAbilityControlMode.MouseKeyboard);
    }

    private void CacheKeyboardMoveControls()
    {
        moveUpControl = FindMoveButtonControl(ERebindableAction.MoveUp);
        moveDownControl = FindMoveButtonControl(ERebindableAction.MoveDown);
        moveLeftControl = FindMoveButtonControl(ERebindableAction.MoveLeft);
        moveRightControl = FindMoveButtonControl(ERebindableAction.MoveRight);
    }

    private ButtonControl FindMoveButtonControl(ERebindableAction _action)
    {
        if (inputManager == null || inputManager.inputReader == null)
            return null;

        string bindingPath = inputManager.GetBindingPath(_action);
        if (string.IsNullOrEmpty(bindingPath))
            return null;

        return InputSystem.FindControl(bindingPath) as ButtonControl;
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
        RefreshKeyGuideTexts();

        if (currentToolTipNode != null)
            ShowToolTip(currentToolTipNode);
    }

    private void RefreshKeyGuideTexts()
    {
        SetLocalizedText(keyGuideUpgradeText, LocKeys.AbilityUI.keyGuideAbilityUpgrade, "특성 업그레이드");
        SetLocalizedText(
            keyGuideMoveText,
            IsMouseKeyboardControlMode ? LocKeys.AbilityUI.keyGuideDragToMove : LocKeys.AbilityUI.keyGuideMove,
            IsMouseKeyboardControlMode ? "드래그로 이동" : "이동");
        SetLocalizedText(keyGuideMagnificationText, LocKeys.AbilityUI.keyGuideZoom, "확대 / 축소");
        SetLocalizedText(keyGuideExitText, LocKeys.AbilityUI.keyGuideExit, "나가기");
    }

    private void SetLocalizedText(TMP_Text _target, int _compositeKey, string _fallback)
    {
        if (_target == null)
            return;

        string localizedText = ResolveLocalizedText(_compositeKey);
        _target.text = string.IsNullOrEmpty(localizedText) ? _fallback : localizedText;
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

    private void EnsurePadCursorInstance()
    {
        if (padCursorRect != null || padCursorSprite == null)
            return;

        RectTransform _parent = transform as RectTransform;
        if (_parent == null)
            return;

        GameObject _cursorObject = new GameObject("PadCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _cursorObject.layer = gameObject.layer;

        padCursorRect = _cursorObject.GetComponent<RectTransform>();
        padCursorRect.SetParent(_parent, false);
        padCursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        padCursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        padCursorRect.pivot = new Vector2(0.5f, 0.5f);

        padCursorImage = _cursorObject.GetComponent<Image>();
        padCursorImage.sprite = padCursorSprite;
        padCursorImage.preserveAspect = true;
        padCursorImage.raycastTarget = false;

        // 원본이 32x32 픽셀 아트이므로 축소하지 않고 32x32 UI 단위로 고정한다.
        // SetNativeSize는 Canvas 연결 전 호출될 경우 기본 Reference PPU(100)를 사용해
        // 100x100으로 계산될 수 있으므로 초기화 순서에 의존하지 않는 명시 크기를 쓴다.
        padCursorRect.sizeDelta = padCursorSize;
        SetPadCursorAlphaImmediate(1f);

        _cursorObject.SetActive(false);
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
        wasInputAllowedLastTick = false;
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
        // 저장된 뷰를 복원한 뒤 화면 중앙을 그리드 좌표로 환산해야 실제 중앙에서 시작한다.
        SetControlMode(null != inputManager && true == inputManager.IsGamepadMode
            ? TentAbilityControlMode.Pad
            : TentAbilityControlMode.MouseKeyboard,
            true);
        RefreshNodeViewportCullingIfNeeded();
        BeginCircleReveal();
        RefreshOpenTransitionInput();
        RefreshLinesIfNeeded();
    }

    private void BuildNodesIfNeeded()
    {
        if (hasBuiltNodes || moveTarget == null || abilityNodePrefab == null)
            return;

        padHoverNodeGridIndex.Clear();
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
        RegisterPadHoverNode(node);

        return node;
    }

    private void RegisterPadHoverNode(AbilityNode _node)
    {
        if (_node == null)
            return;

        Vector2Int _gridPosition = _node.GridPosition;
        if (false == padHoverNodeGridIndex.TryGetValue(_gridPosition, out List<AbilityNode> _nodesAtPosition))
        {
            _nodesAtPosition = new List<AbilityNode>(1);
            padHoverNodeGridIndex.Add(_gridPosition, _nodesAtPosition);
        }

        _nodesAtPosition.Add(_node);
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

        int targetCount = Mathf.Min(Mathf.Max(prewarmNodePoolCount, 0), nodeBuildOrder.Count);
        for (int i = nodePool.Count; i < targetCount; i++)
        {
            AbilityNode pooledNode = Instantiate(abilityNodePrefab, moveTarget);
            pooledNode.BindOwner(this);
            pooledNode.gameObject.SetActive(false);
            nodePool.Enqueue(pooledNode);
        }

        hasPrewarmedNodePool = true;
    }

    private void EnsureSharedNodeVfxPool()
    {
        if (sharedNodeVfxPool != null || abilityNodePrefab == null || abilityNodePrefab.VfxTemplate == null)
            return;

        Transform existingPoolTransform = transform.Find(SharedNodeVfxPoolName);
        GameObject poolObject;
        if (existingPoolTransform != null)
        {
            poolObject = existingPoolTransform.gameObject;
        }
        else
        {
            poolObject = new GameObject(SharedNodeVfxPoolName);
            poolObject.transform.SetParent(transform, false);
        }

        sharedNodeVfxPool = poolObject.GetComponent<VFXComponent>();
        if (sharedNodeVfxPool == null)
            sharedNodeVfxPool = poolObject.AddComponent<VFXComponent>();

        sharedNodeVfxPool.InitializeFrom(abilityNodePrefab.VfxTemplate);
    }

    public void PlaySharedNodeEffect(
        string _effectTag,
        Transform _target,
        Color _color,
        string _sortingLayer,
        int _sortingOrder)
    {
        if (_target == null || string.IsNullOrEmpty(_effectTag))
            return;

        EnsureSharedNodeVfxPool();
        if (sharedNodeVfxPool == null)
            return;

        ParticleSystem effect = sharedNodeVfxPool.Get(_effectTag);
        if (effect == null)
            return;

        sharedNodeVfxPool.SetStartColor(effect, _color);
        sharedNodeVfxPool.SetSortingSettings(effect, _sortingLayer, _sortingOrder);
        Transform effectParent = moveTarget != null ? moveTarget : _target;
        sharedNodeVfxPool.Play(effect, _target.position, Quaternion.identity, effectParent);
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
        ApplyPadSelectionCursorZoom();
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

        hasOpenedView = false;

        CancelViewDrag();
        hasZoomFocus = false;
        isOpeningZoomReveal = false;
        openingZoomFocusPoint = Vector2.zero;
#if UNITY_EDITOR
        StopAllAutoLevelUps();
#endif
        StopAllNodeEffects();
        EndCircleRevealImmediately();
        StopViewShake();
        currentToolTipNode = null;
        currentCursorNode = null;
        ClearPadCursorHover();
        ResetPadViewFollowState();
        ResetPadZoomInput();
        padInputSuppressedFrame = -1;
        wasInputAllowedLastTick = false;
        ReleasePadInputFocus();

        if (padCursorRect != null)
        {
            SetPadCursorAlphaImmediate(1f);
            padCursorRect.gameObject.SetActive(false);
        }

        if (toolTipInstance != null)
            toolTipInstance.HideImmediately();

        if (selectionCursorInstance != null)
        {
            if (IsMouseKeyboardControlMode)
                selectionCursorInstance.Hide();
            else
                selectionCursorInstance.HideImmediately();
        }

        ResetPadSelectionCursorMagnet();

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
        // 패드 모드에서는 SelectionCursor를 Show 상태로 전환하지 않고 Idle을 계속 유지한다.
        if (false == IsMouseKeyboardControlMode)
            return;

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

        // 패드 모드에서는 Hide 모션을 사용하지 않는다. 자석 타깃은 Pad Hover 시스템이 별도로 갱신한다.
        if (selectionCursorInstance != null && IsMouseKeyboardControlMode)
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
            case SkillCommandType.FascinatingLogChance:
            case SkillCommandType.AdvancedLogChance:
            case SkillCommandType.PerfectLogChance:

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

        // 패드 모드에서는 이 TentUI가 EventSystem 선택권을 가진 동안에만 직접 입력을 처리한다.
        // Warning 같은 상위 팝업이 자신의 버튼을 선택하면 커서/A/트리거 입력이 뒤로 새지 않는다.
        bool _inputAllowed = IsMouseKeyboardControlMode || TryAcquirePadInputFocus(false);

        // 위에 떠 있던 모달이 A/×로 닫힌 프레임에 같은 입력이 뒤의 특성 선택으로 새지 않게 한다.
        if (_inputAllowed && false == wasInputAllowedLastTick)
            padInputSuppressedFrame = Time.frameCount;
        wasInputAllowedLastTick = _inputAllowed;

        bool padViewChanged = UpdatePadCursor(_inputAllowed);

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
            RefreshNodeViewportCullingIfNeeded();
            RefreshLinesIfNeeded();
            UpdateToolTipPositionIfNeeded();
            return;
        }

        // 드래그 이동
        bool viewChanged = boundsAdjusted || padViewChanged;
        if (_inputAllowed && IsMouseKeyboardControlMode)
            viewChanged |= UpdateKeyboardViewMovement();

#if UNITY_EDITOR
        UpdateAutoLevelUps();
#endif
        // 줌 기능
        bool zoomChanged = false;
        if (_inputAllowed)
        {
            zoomChanged = IsMouseKeyboardControlMode
                ? HandleZoom()
                : HandlePadZoom();

            if (false == IsMouseKeyboardControlMode)
                HandlePadNodeSelection();
        }
        else
        {
            ResetPadZoomInput();
        }

        if (zoomChanged)
            StopViewShake();
        // 줌 애니메이션 기능
        bool zoomAnimationChanged = UpdateZoomAnimation();
        viewChanged |= zoomAnimationChanged;

        if (zoomAnimationChanged && false == IsMouseKeyboardControlMode)
        {
            UpdatePadCursorScreenPositionFromGrid();
            RefreshPadCursorHover();
            UpdatePadSelectionCursorMagnet();
        }
        // Line 스냅 및 재구성
        if (viewChanged)
            MarkViewLayoutDirty();

        UpdateUnlockReveals();
        UpdateViewShake();
        RefreshNodeViewportCullingIfNeeded();
        RefreshLinesIfNeeded();
        // 툴팁 포지션 스냅
        UpdateToolTipPositionIfNeeded();
    }

    private void SetControlMode(TentAbilityControlMode _mode, bool _forceRefresh = false)
    {
        if (false == _forceRefresh && currentControlMode == _mode)
            return;

        if (TentAbilityControlMode.Pad == _mode)
            currentCursorNode?.SuspendPointerHoverForPadMode();

        currentControlMode = _mode;
        ResetPadViewFollowState();
        EnsurePadCursorInstance();
        ResetPadZoomInput();
        RefreshKeyGuideTexts();

        bool _showPadCursor = TentAbilityControlMode.Pad == currentControlMode && padCursorRect != null;
        if (padCursorRect != null)
        {
            padCursorRect.gameObject.SetActive(_showPadCursor);
            if (_showPadCursor)
            {
                SetPadCursorAlphaImmediate(1f);
                padCursorRect.SetAsLastSibling();
                CenterPadCursor();
                padInputSuppressedFrame = Time.frameCount;
                TryAcquirePadInputFocus(_forceRefresh);
            }
            else
            {
                SetPadCursorAlphaImmediate(1f);
            }
        }

        if (_showPadCursor)
            ActivatePadSelectionCursorIdle();
        else
        {
            ReleasePadInputFocus();
            RestoreMouseKeyboardSelectionCursor();
            ClearPadCursorHover();
            RefreshMouseKeyboardNodeHover();
        }
    }

    private void CenterPadCursor()
    {
        if (moveTarget == null)
            return;

        Vector2 _screenCenter = GetPadCursorScreenBounds().center;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moveTarget,
            _screenCenter,
            GetCanvasEventCamera(),
            out Vector2 _gridPosition))
        {
            padCursorGridPosition = ClampPadCursorGridPosition(_gridPosition);
            UpdatePadCursorScreenPositionFromGrid();
        }
    }

    private bool UpdatePadCursor(bool _inputAllowed)
    {
        if (TentAbilityControlMode.Pad != currentControlMode || padCursorRect == null || false == padCursorRect.gameObject.activeSelf)
        {
            ResetPadViewFollowState();
            return false;
        }

        Vector2 _direction = _inputAllowed && Time.frameCount != padInputSuppressedFrame
            ? ReadPadCursorDirection()
            : Vector2.zero;

        if (_direction.sqrMagnitude > 0.0001f)
        {
            // ScreenPointToLocalPointInRectangle는 실제 화면 픽셀을 Canvas 논리좌표로 변환한다.
            // 기준 해상도의 동일한 이동감을 유지하도록 Canvas 배율만큼 실제 픽셀 이동량을 보정한다.
            float _canvasScaleFactor = GetCanvasScaleFactor();
            Vector2 _screenDelta = _direction *
                                   (padCursorSpeedPixelsPerSecond * _canvasScaleFactor * Time.unscaledDeltaTime);
            MovePadCursorOnGrid(_screenDelta);
        }

        // 커서는 TentUI 최상단에 그리지만 실제 위치는 moveTarget의 그리드 좌표에서 매 프레임 투영한다.
        // 따라서 화면이 따라오면 노드와 마찬가지로 커서의 화면 위치도 함께 중앙 쪽으로 이동한다.
        UpdatePadCursorScreenPositionFromGrid();

        bool _cursorInputActive = _direction.sqrMagnitude > 0.0001f;
        bool _viewChanged = _inputAllowed && UpdatePadViewFollow(_cursorInputActive);
        if (false == _inputAllowed)
            ResetPadViewFollowState();

        if (_viewChanged)
            UpdatePadCursorScreenPositionFromGrid();

        RefreshPadCursorHover();
        UpdatePadSelectionCursorMagnet();
        UpdatePadCursorAlpha();

        // 툴팁이나 노드 이펙트가 런타임에 생성되어도 커서는 항상 TentUI 최상단에 둔다.
        if (selectionCursorInstance != null)
            selectionCursorInstance.transform.SetAsLastSibling();
        padCursorRect.SetAsLastSibling();

        return _viewChanged;
    }

    private void MovePadCursorOnGrid(Vector2 _screenDelta)
    {
        if (moveTarget == null || _screenDelta.sqrMagnitude <= 0.0001f)
            return;

        Camera _eventCamera = GetCanvasEventCamera();
        Vector2 _screenOrigin = GetPadCursorScreenBounds().center;
        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moveTarget,
            _screenOrigin,
            _eventCamera,
            out Vector2 _localOrigin))
            return;

        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moveTarget,
            _screenOrigin + _screenDelta,
            _eventCamera,
            out Vector2 _localDestination))
            return;

        padCursorGridPosition += _localDestination - _localOrigin;
        padCursorGridPosition = ClampPadCursorGridPosition(padCursorGridPosition);
    }

    private Vector2 ClampPadCursorGridPosition(Vector2 _gridPosition)
    {
        float _halfWidth = Mathf.Abs(viewGridHalfExtents.x) * gridCellSize;
        float _halfHeight = Mathf.Abs(viewGridHalfExtents.y) * gridCellSize;
        _gridPosition.x = Mathf.Clamp(_gridPosition.x, -_halfWidth, _halfWidth);
        _gridPosition.y = Mathf.Clamp(_gridPosition.y, -_halfHeight, _halfHeight);
        return _gridPosition;
    }

    private void UpdatePadCursorScreenPositionFromGrid()
    {
        if (moveTarget == null || padCursorRect == null)
            return;

        Vector3 _worldPosition = moveTarget.TransformPoint(padCursorGridPosition);
        padCursorScreenPosition = RectTransformUtility.WorldToScreenPoint(GetCanvasEventCamera(), _worldPosition);
        ApplyPadCursorPosition();
    }

    private Vector2 ReadPadCursorDirection()
    {
        Gamepad _gamepad = Gamepad.current;
        if (_gamepad == null)
            return Vector2.zero;

        Vector2 _leftStick = _gamepad.leftStick.ReadValue();
        float _deadzone = Mathf.Clamp01(padCursorStickDeadzone);
        float _magnitude = _leftStick.magnitude;
        if (_magnitude <= _deadzone)
            return Vector2.zero;

        // 왼쪽 스틱은 커서 이동 전용이다. 기울기 세기는 버려 대각선도 같은 초당 속도를 유지한다.
        return _leftStick / _magnitude;
    }

    private Vector2 ReadPadCameraLookAheadInput()
    {
        Gamepad _gamepad = Gamepad.current;
        if (_gamepad == null)
            return Vector2.zero;

        Vector2 _rightStick = _gamepad.rightStick.ReadValue();
        float _deadzone = Mathf.Clamp(padCursorStickDeadzone, 0f, 0.99f);
        float _magnitude = _rightStick.magnitude;
        if (_magnitude <= _deadzone)
            return Vector2.zero;

        // 오른쪽 스틱은 카메라에 가하는 힘이다. 데드존 바깥의 입력 세기를 0~1로 다시 매핑한다.
        float _strength = Mathf.InverseLerp(_deadzone, 1f, Mathf.Min(_magnitude, 1f));
        return (_rightStick / _magnitude) * _strength;
    }

    private bool TryAcquirePadInputFocus(bool _force)
    {
        EventSystem _eventSystem = EventSystem.current;
        if (null == _eventSystem)
            return true;

        GameObject _selectedObject = _eventSystem.currentSelectedGameObject;
        if (_selectedObject == gameObject)
            return true;

        bool _isFocusAvailable = null == _selectedObject || false == _selectedObject.activeInHierarchy;
        if (false == _force && false == _isFocusAvailable)
            return false;

        _eventSystem.SetSelectedGameObject(gameObject);
        return _eventSystem.currentSelectedGameObject == gameObject;
    }

    private void ReleasePadInputFocus()
    {
        EventSystem _eventSystem = EventSystem.current;
        if (null != _eventSystem && _eventSystem.currentSelectedGameObject == gameObject)
            _eventSystem.SetSelectedGameObject(null);
    }

    private void RefreshMouseKeyboardNodeHover()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
            spawnedNodes[i]?.RefreshHoverAfterCapture();
    }

    private Rect GetPadCursorScreenBounds()
    {
        Camera _camera = CameraFinder.Instance != null ? CameraFinder.Instance.PPMainCamera : null;
        if (_camera != null && _camera.pixelRect.width > 0f && _camera.pixelRect.height > 0f)
            return _camera.pixelRect;

        return new Rect(0f, 0f, Screen.width, Screen.height);
    }

    private void ApplyPadCursorPosition()
    {
        if (padCursorRect == null)
            return;

        RectTransform _parent = padCursorRect.parent as RectTransform;
        if (_parent == null)
            return;

        // 이동 누적값은 부드럽게 유지하되 실제 렌더링 좌표는 물리 픽셀에 맞춘다.
        // 픽셀 아트가 프레임마다 서로 다른 비율로 샘플링되는 현상을 막는다.
        Vector2 _snappedScreenPosition = new Vector2(
            Mathf.Round(padCursorScreenPosition.x),
            Mathf.Round(padCursorScreenPosition.y));

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            _snappedScreenPosition,
            GetCanvasEventCamera(),
            out Vector2 _localPosition))
        {
            float _scaleFactor = GetCanvasScaleFactor();
            _localPosition.x = Mathf.Round(_localPosition.x * _scaleFactor) / _scaleFactor;
            _localPosition.y = Mathf.Round(_localPosition.y * _scaleFactor) / _scaleFactor;
            padCursorRect.anchoredPosition = _localPosition;
        }
    }

    private float GetCanvasScaleFactor()
    {
        return rootCanvas != null ? Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.0001f) : 1f;
    }

    private void UpdatePadCursorAlpha()
    {
        if (padCursorImage == null)
            return;

        float _dimAlpha = Mathf.Clamp01(padCursorNodeHoverAlpha);
        float _targetAlpha = padSelectionCursorMagnetTargetNode != null ? _dimAlpha : 1f;
        float _fullRange = 1f - _dimAlpha;

        if (_fullRange <= 0.0001f)
        {
            SetPadCursorAlphaImmediate(_targetAlpha);
            return;
        }

        float _duration = Mathf.Max(0.01f, padCursorAlphaTransitionDuration);
        float _alphaSpeed = _fullRange / _duration;
        currentPadCursorAlpha = Mathf.MoveTowards(
            currentPadCursorAlpha,
            _targetAlpha,
            _alphaSpeed * Time.unscaledDeltaTime);
        ApplyPadCursorAlpha();
    }

    private void SetPadCursorAlphaImmediate(float _alpha)
    {
        currentPadCursorAlpha = Mathf.Clamp01(_alpha);
        ApplyPadCursorAlpha();
    }

    private void ApplyPadCursorAlpha()
    {
        if (padCursorImage == null)
            return;

        Color _color = padCursorImage.color;
        _color.a = currentPadCursorAlpha;
        padCursorImage.color = _color;
    }

    private void ActivatePadSelectionCursorIdle()
    {
        EnsureSelectionCursorInstance();
        if (selectionCursorInstance == null || padCursorRect == null)
            return;

        // 패드 커서와 노드의 화면 투영 위치를 같은 좌표계에서 보간하기 위해 TentUI 루트로 옮긴다.
        RectTransform _overlayParent = transform as RectTransform;
        RectTransform _selectionRect = selectionCursorInstance.transform as RectTransform;
        if (_overlayParent == null || _selectionRect == null)
            return;

        currentPadCursorNode?.SetPadCursorHover(false);
        currentPadCursorNode = null;
        currentCursorNode = null;
        ResetPadSelectionCursorMagnet();

        if (_selectionRect.parent != _overlayParent)
            _selectionRect.SetParent(_overlayParent, false);

        _selectionRect.anchorMin = new Vector2(0.5f, 0.5f);
        _selectionRect.anchorMax = new Vector2(0.5f, 0.5f);
        _selectionRect.pivot = new Vector2(0.5f, 0.5f);

        if (TryGetPadSelectionCursorTargetPosition(null, out Vector2 _padPosition))
        {
            selectionCursorInstance.ActivateIdleAtAnchoredPosition(_padPosition, selectionCursorSize);
            ApplyPadSelectionCursorZoom();
        }
    }

    private void RestoreMouseKeyboardSelectionCursor()
    {
        if (selectionCursorInstance == null)
            return;

        selectionCursorInstance.HideImmediately();
        ResetPadSelectionCursorMagnet();

        RectTransform _selectionRect = selectionCursorInstance.transform as RectTransform;
        RectTransform _mouseParent = selectionCursorParent != null ? selectionCursorParent : moveTarget;
        if (_selectionRect != null)
        {
            if (_mouseParent != null && _selectionRect.parent != _mouseParent)
                _selectionRect.SetParent(_mouseParent, false);

            // 키마 모드에서는 moveTarget의 줌을 부모로부터 다시 상속하므로 중복 배율을 제거한다.
            _selectionRect.localScale = Vector3.one;
        }
    }

    private void SetPadSelectionCursorMagnetTarget(AbilityNode _targetNode)
    {
        if (padSelectionCursorMagnetTargetNode == _targetNode)
            return;

        padSelectionCursorMagnetTargetNode = _targetNode;
        padSelectionCursorMagnetElapsed = 0f;
        isPadSelectionCursorMagnetMoving = true;

        RectTransform _selectionRect = selectionCursorInstance != null
            ? selectionCursorInstance.transform as RectTransform
            : null;
        if (_selectionRect != null)
            padSelectionCursorMagnetStartPosition = _selectionRect.anchoredPosition;
    }

    private void UpdatePadSelectionCursorMagnet()
    {
        if (currentControlMode != TentAbilityControlMode.Pad ||
            selectionCursorInstance == null ||
            false == selectionCursorInstance.gameObject.activeSelf)
            return;

        ApplyPadSelectionCursorZoom();

        if (false == TryGetPadSelectionCursorTargetPosition(
                padSelectionCursorMagnetTargetNode,
                out Vector2 _targetPosition))
            return;

        if (isPadSelectionCursorMagnetMoving)
        {
            float _duration = Mathf.Max(0.01f, padSelectionCursorMagnetDuration);
            padSelectionCursorMagnetElapsed += Time.unscaledDeltaTime;
            float _progress = Mathf.Clamp01(padSelectionCursorMagnetElapsed / _duration);
            float _easedProgress = EaseOutCubic(_progress);
            Vector2 _position = Vector2.LerpUnclamped(
                padSelectionCursorMagnetStartPosition,
                _targetPosition,
                _easedProgress);
            selectionCursorInstance.SetAnchoredPosition(_position);

            if (_progress < 1f)
                return;

            isPadSelectionCursorMagnetMoving = false;
        }

        // PadCursor로 돌아온 뒤에는 물론, 노드에 붙은 뒤에도 대상의 화면 이동을 정확히 추적한다.
        selectionCursorInstance.SetAnchoredPosition(_targetPosition);
    }

    private void ApplyPadSelectionCursorZoom()
    {
        if (currentControlMode != TentAbilityControlMode.Pad || selectionCursorInstance == null)
            return;

        RectTransform _selectionRect = selectionCursorInstance.transform as RectTransform;
        if (_selectionRect == null)
            return;

        // 키마 모드에서는 SelectionCursor가 moveTarget 아래에서 이 배율을 부모로부터 상속한다.
        // 패드 모드는 화면 오버레이로 옮겨졌으므로 같은 배율을 직접 적용해 노드 크기와 맞춘다.
        float _zoom = Mathf.Max(0.0001f, currentZoom);
        _selectionRect.localScale = new Vector3(_zoom, _zoom, 1f);
    }

    private bool TryGetPadSelectionCursorTargetPosition(AbilityNode _targetNode, out Vector2 _targetPosition)
    {
        _targetPosition = Vector2.zero;
        if (selectionCursorInstance == null)
            return false;

        RectTransform _selectionRect = selectionCursorInstance.transform as RectTransform;
        RectTransform _selectionParent = _selectionRect != null ? _selectionRect.parent as RectTransform : null;
        RectTransform _targetRect = _targetNode != null ? _targetNode.RectTransform : padCursorRect;
        if (_selectionParent == null || _targetRect == null)
            return false;

        Vector3 _targetWorldCenter = _targetRect.TransformPoint(_targetRect.rect.center);
        _targetPosition = _selectionParent.InverseTransformPoint(_targetWorldCenter);
        return true;
    }

    private void ResetPadSelectionCursorMagnet()
    {
        padSelectionCursorMagnetTargetNode = null;
        padSelectionCursorMagnetStartPosition = Vector2.zero;
        padSelectionCursorMagnetElapsed = 0f;
        isPadSelectionCursorMagnetMoving = false;
    }

    private bool UpdatePadViewFollow(bool _cursorInputActive)
    {
        if (false == IsViewInputEnabled() || moveTarget == null || padCursorRect == null)
        {
            ResetPadViewFollowState();
            return false;
        }

        RectTransform _cursorParent = padCursorRect.parent as RectTransform;
        if (_cursorParent == null)
            return false;

        Vector2 _cursorLocal = padCursorRect.anchoredPosition;
        Vector2 _safeHalfSize = new Vector2(
            Mathf.Max(1f, padCursorSafeAreaSize.x * 0.5f),
            Mathf.Max(1f, padCursorSafeAreaSize.y * 0.5f));

        Vector2 _lookAheadInput = Time.frameCount != padInputSuppressedFrame
            ? ReadPadCameraLookAheadInput()
            : Vector2.zero;
        bool _lookAheadActive = _lookAheadInput.sqrMagnitude > 0.0001f;

        if (_lookAheadActive)
        {
            wasPadCameraLookAheadActive = true;
            isPadCameraRecentering = false;

            // 오른쪽으로 미리 보려면 커서는 화면 왼쪽에 위치해야 한다.
            // 최대 입력은 Safe Area 가장자리까지 커서의 화면 목표 위치를 이동시킨다.
            Vector2 _targetCursorLocal = -GetPadEllipseOffset(_lookAheadInput, _safeHalfSize);
            return SmoothPadViewToCursorTarget(_cursorLocal, _targetCursorLocal, out _);
        }

        // 오른쪽 스틱을 놓은 직후에는 한 번 중앙으로 돌아간다.
        if (wasPadCameraLookAheadActive)
        {
            wasPadCameraLookAheadActive = false;
            isPadCameraRecentering = true;
        }

        // 중앙 복귀 중이라도 왼쪽 스틱으로 커서를 움직이면 기존 Safe Area 추적을 즉시 우선한다.
        if (_cursorInputActive && isPadCameraRecentering)
        {
            isPadCameraRecentering = false;
            padViewFollowVelocity = Vector2.zero;
        }

        if (isPadCameraRecentering)
        {
            bool _moved = SmoothPadViewToCursorTarget(_cursorLocal, Vector2.zero, out bool _reachedCenter);
            if (_reachedCenter)
                isPadCameraRecentering = false;
            return _moved;
        }

        return UpdatePadSafeAreaFollow(_cursorLocal, _safeHalfSize, _cursorParent);
    }

    private static Vector2 GetPadEllipseOffset(Vector2 _input, Vector2 _ellipseHalfSize)
    {
        float _strength = Mathf.Clamp01(_input.magnitude);
        if (_strength <= 0.0001f)
            return Vector2.zero;

        Vector2 _direction = _input / _input.magnitude;
        Vector2 _normalizedDirection = new Vector2(
            _direction.x / _ellipseHalfSize.x,
            _direction.y / _ellipseHalfSize.y);
        float _ellipseRadius = 1f / Mathf.Max(0.0001f, _normalizedDirection.magnitude);
        return _direction * (_ellipseRadius * _strength);
    }

    private bool SmoothPadViewToCursorTarget(
        Vector2 _cursorLocal,
        Vector2 _targetCursorLocal,
        out bool _reachedTarget)
    {
        float _maxSpeed = gridCellSize * padLookAheadMaxGridUnitsPerSecond * currentZoom;
        Vector2 _nextCursorLocal = Vector2.SmoothDamp(
            _cursorLocal,
            _targetCursorLocal,
            ref padViewFollowVelocity,
            Mathf.Max(0.01f, padLookAheadSmoothTime),
            _maxSpeed,
            Time.unscaledDeltaTime);
        Vector2 _viewDelta = _nextCursorLocal - _cursorLocal;
        _reachedTarget = (_cursorLocal - _targetCursorLocal).sqrMagnitude <= 0.01f &&
                         padViewFollowVelocity.sqrMagnitude <= 0.01f;

        if (_viewDelta.sqrMagnitude <= 0.0001f)
        {
            if (_reachedTarget)
                padViewFollowVelocity = Vector2.zero;
            return false;
        }

        bool _moved = ApplyViewLogicalDelta(_viewDelta);
        if (_moved)
            StopViewShake();
        else
            // 화면 경계에서 쌓인 속도가 반대 방향 전환을 늦추지 않게 한다.
            padViewFollowVelocity = Vector2.zero;

        return _moved;
    }

    private bool UpdatePadSafeAreaFollow(
        Vector2 _cursorLocal,
        Vector2 _safeHalfSize,
        RectTransform _cursorParent)
    {
        // 오른쪽 스틱 Look Ahead가 만드는 타원과 완전히 같은 경계를 사용한다.
        // 정규화 공간에서 단위원으로 Clamp한 뒤 다시 화면 좌표로 복원한다.
        Vector2 _normalizedCursor = new Vector2(
            _cursorLocal.x / _safeHalfSize.x,
            _cursorLocal.y / _safeHalfSize.y);
        Vector2 _clampedNormalizedCursor = Vector2.ClampMagnitude(_normalizedCursor, 1f);
        Vector2 _clampedToSafeArea = Vector2.Scale(_clampedNormalizedCursor, _safeHalfSize);
        Vector2 _overflow = _cursorLocal - _clampedToSafeArea;

        Vector2 _availableOutsideSafeArea = new Vector2(
            Mathf.Max(1f, _cursorParent.rect.width * 0.5f - _safeHalfSize.x),
            Mathf.Max(1f, _cursorParent.rect.height * 0.5f - _safeHalfSize.y));
        Vector2 _normalizedOverflow = new Vector2(
            Mathf.Clamp(_overflow.x / _availableOutsideSafeArea.x, -1f, 1f),
            Mathf.Clamp(_overflow.y / _availableOutsideSafeArea.y, -1f, 1f));

        float _maxSpeed = gridCellSize * padViewFollowMaxGridUnitsPerSecond * currentZoom;
        Vector2 _targetVelocity = -_normalizedOverflow * _maxSpeed;
        float _followRate = 1f / Mathf.Max(0.01f, padViewFollowSmoothTime);
        float _blend = 1f - Mathf.Exp(-_followRate * Time.unscaledDeltaTime);
        padViewFollowVelocity = Vector2.Lerp(padViewFollowVelocity, _targetVelocity, _blend);

        if (padViewFollowVelocity.sqrMagnitude <= 0.0001f)
        {
            padViewFollowVelocity = Vector2.zero;
            return false;
        }

        bool _moved = ApplyViewLogicalDelta(padViewFollowVelocity * Time.unscaledDeltaTime);
        if (_moved)
            StopViewShake();

        return _moved;
    }

    private void ResetPadViewFollowState()
    {
        padViewFollowVelocity = Vector2.zero;
        wasPadCameraLookAheadActive = false;
        isPadCameraRecentering = false;
    }

    private void RefreshPadCursorHover()
    {
        if (false == IsViewInputEnabled() || padHoverNodeGridIndex.Count == 0)
        {
            ClearPadCursorHover();
            return;
        }

        AbilityNode _hoveredNode = FindPadCursorHoverNode();

        if (currentPadCursorNode == _hoveredNode)
            return;

        currentPadCursorNode?.SetPadCursorHover(false);
        currentPadCursorNode = _hoveredNode;
        currentPadCursorNode?.SetPadCursorHover(true);
        SetPadSelectionCursorMagnetTarget(currentPadCursorNode);
    }

    private AbilityNode FindPadCursorHoverNode()
    {
        Vector2 _halfSize = new Vector2(
            Mathf.Max(1f, Mathf.Abs(padCursorHoverCorrectionSize.x) * 0.5f),
            Mathf.Max(1f, Mathf.Abs(padCursorHoverCorrectionSize.y) * 0.5f));

        float _releasePadding = Mathf.Max(0f, padCursorHoverReleasePadding);
        Vector2 _releaseHalfSize = _halfSize + Vector2.one * _releasePadding;
        AbilityNode _nearestNode = FindNearestPadHoverNode(_halfSize, out float _nearestSqrDistance);

        if (false == IsPadHoverNodeAvailable(currentPadCursorNode) ||
            false == IsPadCursorInsideNodeArea(currentPadCursorNode, _releaseHalfSize))
            return _nearestNode;

        if (_nearestNode == null || _nearestNode == currentPadCursorNode)
            return currentPadCursorNode;

        // 영역이 겹칠 때 새 후보가 padding만큼 확실히 가까워진 뒤 전환해 경계 떨림을 방지한다.
        Vector2 _currentDelta = padCursorGridPosition - GetNodeGridLocalCenter(currentPadCursorNode);
        float _currentDistance = _currentDelta.magnitude;
        float _nearestDistance = Mathf.Sqrt(_nearestSqrDistance);
        return _nearestDistance + _releasePadding < _currentDistance
            ? _nearestNode
            : currentPadCursorNode;
    }

    private AbilityNode FindNearestPadHoverNode(Vector2 _halfSize, out float _nearestSqrDistance)
    {
        float _cellSize = Mathf.Max(Mathf.Abs(gridCellSize), 0.0001f);
        int _minGridX = Mathf.CeilToInt((padCursorGridPosition.x - _halfSize.x) / _cellSize);
        int _maxGridX = Mathf.FloorToInt((padCursorGridPosition.x + _halfSize.x) / _cellSize);
        int _minGridY = Mathf.CeilToInt((padCursorGridPosition.y - _halfSize.y) / _cellSize);
        int _maxGridY = Mathf.FloorToInt((padCursorGridPosition.y + _halfSize.y) / _cellSize);

        AbilityNode _nearestNode = null;
        _nearestSqrDistance = float.PositiveInfinity;

        // 32 논리단위 그리드와 64x64 보정 영역 기준 최대 3x3 좌표만 조회한다.
        for (int _gridY = _minGridY; _gridY <= _maxGridY; _gridY++)
        {
            for (int _gridX = _minGridX; _gridX <= _maxGridX; _gridX++)
            {
                Vector2Int _gridPosition = new Vector2Int(_gridX, _gridY);
                if (false == padHoverNodeGridIndex.TryGetValue(_gridPosition, out List<AbilityNode> _nodesAtPosition))
                    continue;

                for (int i = 0; i < _nodesAtPosition.Count; i++)
                {
                    AbilityNode _candidate = _nodesAtPosition[i];
                    if (false == IsPadHoverNodeAvailable(_candidate))
                        continue;

                    Vector2 _nodeCenter = GetNodeGridLocalCenter(_candidate);
                    Vector2 _delta = padCursorGridPosition - _nodeCenter;
                    if (Mathf.Abs(_delta.x) > _halfSize.x || Mathf.Abs(_delta.y) > _halfSize.y)
                        continue;

                    float _sqrDistance = _delta.sqrMagnitude;
                    if (_sqrDistance >= _nearestSqrDistance)
                        continue;

                    _nearestSqrDistance = _sqrDistance;
                    _nearestNode = _candidate;
                }
            }
        }

        return _nearestNode;
    }

    private bool IsPadCursorInsideNodeArea(AbilityNode _node, Vector2 _halfSize)
    {
        Vector2 _delta = padCursorGridPosition - GetNodeGridLocalCenter(_node);
        return Mathf.Abs(_delta.x) <= _halfSize.x && Mathf.Abs(_delta.y) <= _halfSize.y;
    }

    private Vector2 GetNodeGridLocalCenter(AbilityNode _node)
    {
        Vector2Int _gridPosition = _node.GridPosition;
        return new Vector2(
            Mathf.Round(_gridPosition.x * gridCellSize),
            Mathf.Round(_gridPosition.y * gridCellSize));
    }

    private static bool IsPadHoverNodeAvailable(AbilityNode _node)
    {
        return _node != null && _node.IsProgressionVisible && _node.gameObject.activeInHierarchy;
    }

    private void ClearPadCursorHover()
    {
        currentPadCursorNode?.SetPadCursorHover(false);
        currentPadCursorNode = null;

        if (currentControlMode == TentAbilityControlMode.Pad)
            SetPadSelectionCursorMagnetTarget(null);
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
        if (false == IsMouseKeyboardControlMode)
            return;

        // 포커스 손실 등으로 PointerUp 이벤트가 누락되어도 캡처가 남지 않게 한다.
        if (hoverCaptureMode != HoverCaptureMode.None && IsAnyMouseButtonPressed(Mouse.current) == false)
            ReleaseCapturedNodeHover(true);
    }

    private void UpdateImmediateViewDrag()
    {
        if (false == IsMouseKeyboardControlMode)
        {
            ResetViewDragTracking();
            return;
        }

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

        if (ApplyViewLogicalDelta(_screenDelta / scaleFactor) == false)
            return;

        MarkViewLayoutDirty();
        RefreshLinesIfNeeded();
        UpdateToolTipPositionIfNeeded();
    }

    private bool UpdateKeyboardViewMovement()
    {
        if (IsViewInputEnabled() == false)
            return false;

        Vector2 input = new Vector2(
            ReadButton(moveRightControl) - ReadButton(moveLeftControl),
            ReadButton(moveUpControl) - ReadButton(moveDownControl));
        if (input.sqrMagnitude <= 0.0001f)
            return false;

        input.Normalize();
        float speed = gridCellSize * KeyboardMoveGridUnitsPerSecond * currentZoom;
        Vector2 logicalDelta = -input * speed * Time.unscaledDeltaTime;
        if (ApplyViewLogicalDelta(logicalDelta) == false)
            return false;

        StopViewShake();
        return true;
    }

    private static float ReadButton(ButtonControl _control)
    {
        return _control != null && _control.isPressed ? 1f : 0f;
    }

    private bool ApplyViewLogicalDelta(Vector2 _logicalDelta)
    {
        if (moveTarget == null || _logicalDelta.sqrMagnitude <= 0.0001f)
            return false;

        Vector2 previousPosition = moveTarget.anchoredPosition;
        Vector2 logicalPosition = previousPosition - currentViewShakeOffset;
        logicalPosition += _logicalDelta;
        moveTarget.anchoredPosition = ClampViewPosition(logicalPosition, currentZoom) + currentViewShakeOffset;

        if ((moveTarget.anchoredPosition - previousPosition).sqrMagnitude <= 0.0001f)
            return false;

        return true;
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

        return ApplyZoomStep(Mathf.Sign(scrollY), mouse.position.ReadValue());
    }

    private bool HandlePadZoom()
    {
        if (Time.frameCount == padInputSuppressedFrame || false == IsViewInputEnabled())
        {
            ResetPadZoomInput();
            return false;
        }

        Gamepad _gamepad = Gamepad.current;
        if (_gamepad == null)
        {
            ResetPadZoomInput();
            return false;
        }

        bool _zoomOutHeld = _gamepad.leftTrigger.isPressed;
        bool _zoomInHeld = _gamepad.rightTrigger.isPressed;
        int _direction = (_zoomInHeld ? 1 : 0) - (_zoomOutHeld ? 1 : 0);

        // 두 트리거를 동시에 누른 상태는 서로 상쇄한다. 한쪽을 놓으면 그 방향으로 새 입력을 시작한다.
        if (0 == _direction)
        {
            ResetPadZoomInput();
            return false;
        }

        bool _pressedThisFrame = 0 < _direction
            ? _gamepad.rightTrigger.wasPressedThisFrame
            : _gamepad.leftTrigger.wasPressedThisFrame;

        if (_pressedThisFrame || padZoomHoldDirection != _direction)
        {
            padZoomHoldDirection = _direction;
            padZoomRepeatElapsed = 0f;
            return ApplyZoomStep(_direction, padCursorScreenPosition);
        }

        padZoomRepeatElapsed += Time.unscaledDeltaTime;
        if (padZoomRepeatElapsed < PadZoomRepeatInterval)
            return false;

        padZoomRepeatElapsed -= PadZoomRepeatInterval;
        return ApplyZoomStep(_direction, padCursorScreenPosition);
    }

    private bool ApplyZoomStep(float _direction, Vector2 _focusScreenPosition)
    {
        float _previousTargetZoom = targetZoom;
        zoomFocusScreenPosition = _focusScreenPosition;
        hasZoomFocus = true;
        targetZoom = Mathf.Clamp(
            targetZoom + Mathf.Sign(_direction) * ZoomStep,
            GetEffectiveMinZoom(),
            MaxZoom);
        return false == Mathf.Approximately(_previousTargetZoom, targetZoom);
    }

    private void ResetPadZoomInput()
    {
        padZoomHoldDirection = 0;
        padZoomRepeatElapsed = 0f;
    }

    private void HandlePadNodeSelection()
    {
        if (Time.frameCount == padInputSuppressedFrame || false == IsViewInputEnabled())
            return;

        Gamepad _gamepad = Gamepad.current;
        if (_gamepad == null || false == _gamepad.buttonSouth.wasPressedThisFrame)
            return;

        AbilityNode _targetNode = currentPadCursorNode;
        if (false == IsPadHoverNodeAvailable(_targetNode))
            return;

        _targetNode.SubmitPadSelection();
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
        ApplyPadSelectionCursorZoom();
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
            ApplyPadSelectionCursorZoom();
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
        nodeViewportLayoutDirty = true;
        toolTipLayoutDirty = true;

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
        nodeViewportLayoutDirty = true;
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

    private int GetLineShineColorIndex(SkillType _childSkillType)
    {
        if (spawnedNodeMap.TryGetValue(_childSkillType, out AbilityNode childNode) == false)
            return -1;

        if (childNode.CompletedVisual)
            return 2;

        return childNode.CanApplyVisual ? 1 : 0;
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

#if UNITY_EDITOR
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
#endif

    private void StopAllNodeEffects()
    {
        if (sharedNodeVfxPool != null)
            sharedNodeVfxPool.StopAll();
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
#if UNITY_EDITOR
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
#endif

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

        int previousNodeLevel = node.CurrentLevel;
        bool wasLockedByLevel = node.IsUnlockedByLevel() == false;
        SyncNodeLevelsFromProvider();
        SkillInfo upgradedSkillInfo = GetSkillInfo(_skillType);
        bool reachedMaxLevel = upgradedSkillInfo.maxLevel > 0 &&
            previousNodeLevel < upgradedSkillInfo.maxLevel &&
            upgradedSkillInfo.currentLevel >= upgradedSkillInfo.maxLevel;
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

        if (reachedMaxLevel)
            node.PlayMaxLevelUpEffect();

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
        ReleasePadInputFocus();

        if (SettingsManager.HasInstance)
            SettingsManager.Instance.OnInputSettingsAppliedEvent -= ApplyPadCursorSensitivity;

        SetInputManager(null);
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
            if (node == null)
                continue;

            bool wasVisible = node.IsProgressionVisible;
            bool isVisible = ShouldShowNode(node);
            node.SetProgressionVisible(isVisible);

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

        nodeViewportLayoutDirty = true;
        RefreshNodeViewportCullingIfNeeded();
        lineRenderer.InvalidateVisualData();
        RefreshLines();
    }

    private void RefreshNodeViewportCullingIfNeeded()
    {
        if (nodeViewportLayoutDirty == false)
            return;

        nodeViewportLayoutDirty = false;
        if (abilityBackground == null || moveTarget == null)
            return;

        Rect viewportRect = abilityBackground.rect;
        float padding = Mathf.Max(0f, nodeViewportCullPadding);
        viewportRect.xMin -= padding;
        viewportRect.xMax += padding;
        viewportRect.yMin -= padding;
        viewportRect.yMax += padding;

        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            AbilityNode node = spawnedNodes[i];
            if (node == null)
                continue;

            if (node.IsProgressionVisible == false)
            {
                node.SetViewportVisible(false);
                continue;
            }

            RectTransform nodeRect = node.RectTransform;
            if (nodeRect == null)
            {
                node.SetViewportVisible(false);
                continue;
            }

            Bounds nodeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                abilityBackground,
                nodeRect);

            bool isInsideViewport =
                nodeBounds.max.x >= viewportRect.xMin &&
                nodeBounds.min.x <= viewportRect.xMax &&
                nodeBounds.max.y >= viewportRect.yMin &&
                nodeBounds.min.y <= viewportRect.yMax;

            node.SetViewportVisible(isInsideViewport);
        }
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
            if (reveal == null || reveal.Node == null || reveal.Node.IsProgressionVisible == false)
            {
                if (reveal != null && reveal.Node != null)
                    lineRenderer.ClearLineRevealProgress(reveal.Node.SkillType);

                lineLayoutDirty = true;
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

            if (reveal.PlayAppearSound && reveal.Node.gameObject.activeInHierarchy)
                Sound.PlayUI(SoundID.AbilityAppear);

            if (reveal.Node.gameObject.activeInHierarchy)
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
            if (node == null || node.IsProgressionVisible == false)
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

        lineRenderer.InvalidateVisualData();
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
