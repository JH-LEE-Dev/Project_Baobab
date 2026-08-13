using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PresentationLayer.DOTweenAnimationSystem;
using DG.Tweening;
using Coffee.UIEffects;

public class AbilityNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private const float AbilityBarMaxHeight = 26f;
    private static readonly Color32 CanApplyShinyColor = new Color32(184, 255, 243, 255);
    private static readonly Color32 CannotApplyShinyColor = new Color32(255, 106, 98, 255);
    private static readonly Color32 CompletedShinyColor = new Color32(136, 145, 255, 255);

    [Header("Node Data")]
    [SerializeField] private SkillType skillType = SkillType.None;
    [SerializeField] private string displayName;
    [SerializeField] private AbilityLevelBadgeType levelBadge = AbilityLevelBadgeType.None;
    [SerializeField] private int requiredPrestigeLevel;
    [SerializeField] private int currentLevel;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private SkillType[] parentSkillTypes;

    [Header("UI References")]
    [SerializeField] private RectTransform abilityNodeTouchArea;
    [SerializeField] private RectTransform abilityVisualRoot;
    [SerializeField] private Image abilityBaseImage;
    [SerializeField] private Image abilityBackgroundImage;
    [SerializeField] private Image abilityPictureImage;
    [SerializeField] private Image abilityLevelImage;
    [SerializeField] private Image abilityBarImage;
    [SerializeField] private UIEffect abilityBaseEffect;

    [Header("Important Node Loop Effect")]
    [SerializeField] private Image importantNodeLoopEffectFirstImage;
    [SerializeField] private Image importantNodeLoopEffectSecondImage;
    [SerializeField] private Sprite[] importantNodeLoopEffectFrames;
    [SerializeField, Min(1f)] private float importantNodeLoopEffectFrameRate = 16f;

    [Header("Default Visual")]
    [SerializeField] private Sprite defaultPictureSprite;

    [Header("VFX Settings")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string effectLayerTag = "HUD";
    [SerializeField] private string levelUpImpactTag = "LevelUpEffect";

    [Header("Max Level Sprite Effect")]
    [SerializeField] private Image maxLevelUpEffectImage;
    [SerializeField] private Sprite[] maxLevelUpEffectFrames;
    [SerializeField, Min(1f)] private float maxLevelUpEffectFrameRate = 24f;

    [Header("Motion Settings")]
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private string hoverMotionTag = "UIHover";
    [SerializeField] private string unHoverMotionTag = "UIUnHover";
    [SerializeField] private string clickMotionTag = "UIClick";
    [SerializeField] private string nonPassClickMotionTag = "UIClick_Nonpass";
    [SerializeField] private bool resetCurrentMotionBeforePlay = false;
    [SerializeField] private float clickCancelDragThreshold = 8f;

    private UI_TentAbilityComponent owner;
    private bool canApplyVisual;
    private bool completedVisual;
    private bool visualHidden;
    private bool isPointerInside;
    private bool isPointerHovering;
    private bool consumedRapidClick;
    private Vector2 interactionShakeCompensation;
    private Color currentNodeFrameColor = Color.white;
    private MotionEntry hoverMotionEntry;
    private MotionEntry unHoverMotionEntry;
    private MotionEntry clickMotionEntry;
    private MotionEntry nonPassClickMotionEntry;
    private bool progressionVisible = true;
    private bool viewportVisible = true;
    private bool isImportantNode;
    private bool importantNodeLoopEffectInitialized;
    private bool importantNodeLoopEffectPlaying;
    private int importantNodeLoopEffectFirstFrameIndex;
    private int importantNodeLoopEffectSecondFrameIndex;
    private float importantNodeLoopEffectElapsed;
    private Tween maxLevelUpEffectTween;

    public SkillType SkillType => skillType;
    public string DisplayName => displayName;
    public AbilityLevelBadgeType LevelBadge => levelBadge;
    public int RequiredPrestigeLevel => Mathf.Max(requiredPrestigeLevel, 0);
    public int CurrentLevel => currentLevel;
    public Vector2Int GridPosition => gridPosition;
    public SkillType[] ParentSkillTypes => parentSkillTypes;
    public RectTransform RectTransform => transform as RectTransform;
    public bool CanApplyVisual => canApplyVisual;
    public bool CompletedVisual => completedVisual;
    public Color CurrentNodeFrameColor => currentNodeFrameColor;
    public bool IsPointerInside => isPointerInside;
    public bool IsProgressionVisible => progressionVisible;
    public VFXComponent VfxTemplate => vfxComponent;

    private void Awake()
    {
        CacheInteractionReferences();
        CacheAbilityBaseEffectReference();
        CacheImportantNodeLoopEffectReferences();
        SortImportantNodeLoopEffectFrames();
        RefreshImportantNodeClassification();

        if (null != motionPlayer)
            motionPlayer.Initialize();

        CacheMaxLevelUpEffectReference();
        SortMaxLevelUpEffectFrames();
        HideMaxLevelUpEffect();
    }

    private void OnEnable()
    {
        CacheInteractionReferences();
        CacheAbilityBaseEffectReference();
        CacheImportantNodeLoopEffectReferences();
        RefreshImportantNodeEffect();
        CacheMaxLevelUpEffectReference();
        HideMaxLevelUpEffect();
    }

    private void OnDisable()
    {
        StopImportantNodeLoopEffect();
        StopMaxLevelUpEffect();
        CancelHoverState();
        consumedRapidClick = false;
    }

    private void Update()
    {
        UpdateImportantNodeLoopEffect();
    }

    // 특성 노드의 내부 그림을 외부에서 교체한다.
    private void SetPicture(Sprite _sprite)
    {
        if (abilityPictureImage == null)
            return;

        abilityPictureImage.sprite = _sprite != null ? _sprite : defaultPictureSprite;
    }

    private void SetLevelBadgeSprite(Sprite _sprite)
    {
        CacheLevelImageIfNeeded();
        if (abilityLevelImage == null)
            return;

        bool shouldShow = levelBadge != AbilityLevelBadgeType.None && _sprite != null;
        abilityLevelImage.gameObject.SetActive(shouldShow);
        abilityLevelImage.sprite = shouldShow ? _sprite : null;
    }

    // 노드가 포인터 이벤트를 전달할 상위 능력 UI 컴포넌트를 연결한다.
    public void BindOwner(UI_TentAbilityComponent _owner)
    {
        owner = _owner;
    }

    public void SetProgressionVisible(bool _visible)
    {
        if (progressionVisible == _visible)
            return;

        progressionVisible = _visible;
        RefreshActiveState();
    }

    public void SetViewportVisible(bool _visible)
    {
        if (viewportVisible == _visible)
            return;

        viewportVisible = _visible;
        RefreshActiveState();
    }

    private void RefreshActiveState()
    {
        bool shouldBeActive = progressionVisible && viewportVisible;
        if (gameObject.activeSelf != shouldBeActive)
            gameObject.SetActive(shouldBeActive);
    }

    public void SetInteractionShakeCompensation(Vector2 _compensation)
    {
        CacheInteractionReferences();
        if (abilityNodeTouchArea == null)
            return;

        abilityNodeTouchArea.anchoredPosition += _compensation - interactionShakeCompensation;
        interactionShakeCompensation = _compensation;
    }

    // JSON에서 읽은 노드 정의를 현재 프리팹 인스턴스에 반영한다.
    public void ApplyDefinition(AbilityNodeDefinitionJson _definition, SkillType _skillType, string _displayName, Sprite _pictureSprite, Sprite _levelBadgeSprite, float _gridCellSize)
    {
        if (_definition == null)
            return;

        skillType = _skillType;
        displayName = _displayName;
        importantNodeLoopEffectInitialized = false;
        RefreshImportantNodeClassification();
        levelBadge = ParseLevelBadge(_definition.levelBadge);
        requiredPrestigeLevel = Mathf.Max(_definition.requiredPrestigeLevel, 0);
        currentLevel = 0;
        gridPosition = new Vector2Int(_definition.gridX, _definition.gridY);
        parentSkillTypes = ConvertParentSkillTypes(_definition.GetParentSkillTypeNames());

        SetPicture(_pictureSprite);
        SetLevelBadgeSprite(_levelBadgeSprite);
        ApplyLevelProgressBar(0, 0);
        ApplyAnchoredPosition(_gridCellSize);
    }

    public void ApplyLocalizedText(string _displayName)
    {
        displayName = _displayName;
        RefreshImportantNodeClassification();
    }

    // 노드의 JSON 기반 그리드 좌표를 실제 UI 좌표로 변환해 적용한다.
    private void ApplyAnchoredPosition(float _gridCellSize)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        Vector2 anchoredPosition = new Vector2(gridPosition.x * _gridCellSize, gridPosition.y * _gridCellSize);
        rectTransform.anchoredPosition = new Vector2(
            Mathf.Round(anchoredPosition.x),
            Mathf.Round(anchoredPosition.y));
    }

    // 부모 스킬 문자열 목록을 SkillType 배열로 변환한다.
    private SkillType[] ConvertParentSkillTypes(string[] _parentSkillTypeNames)
    {
        if (_parentSkillTypeNames == null || _parentSkillTypeNames.Length == 0)
            return Array.Empty<SkillType>();

        SkillType[] result = new SkillType[_parentSkillTypeNames.Length];

        for (int i = 0; i < _parentSkillTypeNames.Length; i++)
        {
            if (Enum.TryParse(_parentSkillTypeNames[i], true, out SkillType parsedSkillType))
                result[i] = parsedSkillType;
            else
                result[i] = SkillType.None;
        }

        return result;
    }

    private AbilityLevelBadgeType ParseLevelBadge(string _levelBadge)
    {
        if (string.IsNullOrWhiteSpace(_levelBadge))
            return AbilityLevelBadgeType.None;

        return Enum.TryParse(_levelBadge, true, out AbilityLevelBadgeType parsedLevelBadge)
            ? parsedLevelBadge
            : AbilityLevelBadgeType.None;
    }

    private void CacheLevelImageIfNeeded()
    {
        if (abilityLevelImage != null)
            return;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name == "AbilityLevel")
            {
                abilityLevelImage = images[i];
                return;
            }
        }
    }

    // 현재 레벨이 1 이상인지 반환한다.
    public bool IsUnlockedByLevel()
    {
        return currentLevel > 0;
    }

    // 현재 레벨을 외부에서 직접 반영한다.
    public void SetCurrentLevel(int _currentLevel)
    {
        currentLevel = Mathf.Max(_currentLevel, 0);
    }

    public void ApplyLevelProgressBar(int _currentLevel, int _maxLevel)
    {
        if (abilityBarImage == null)
            return;

        bool shouldShow = _currentLevel > 0 && _maxLevel > 0 && _currentLevel < _maxLevel;
        abilityBarImage.gameObject.SetActive(shouldShow);
        if (shouldShow == false)
            return;

        RectTransform barRectTransform = abilityBarImage.rectTransform;
        if (barRectTransform == null)
            return;

        float levelRatio = Mathf.Clamp01((float)_currentLevel / _maxLevel);
        Vector2 sizeDelta = barRectTransform.sizeDelta;
        sizeDelta.y = Mathf.Round(AbilityBarMaxHeight * levelRatio);
        barRectTransform.sizeDelta = sizeDelta;
    }

    // 현재 노드의 테두리/배경 표시 상태를 갱신한다.
    public void ApplyVisualState(Color _baseColor, bool _canApply, bool _completed)
    {
        canApplyVisual = _canApply;
        completedVisual = _completed;
        currentNodeFrameColor = _baseColor;

        if (abilityBaseImage != null)
        {
            _baseColor.a = visualHidden ? 0f : _baseColor.a;
            abilityBaseImage.color = _baseColor;
        }

        RefreshImportantNodeEffect();
    }

    public void SetVisualVisible(bool _visible)
    {
        visualHidden = _visible == false;
        ApplyImageAlpha(abilityBaseImage, _visible ? 1f : 0f);
        ApplyImageAlpha(abilityBackgroundImage, _visible ? 1f : 0f);
        ApplyImageAlpha(abilityPictureImage, _visible ? 1f : 0f);
        ApplyImageAlpha(abilityLevelImage, _visible ? 1f : 0f);
        RefreshImportantNodeEffect();
    }

    private void CacheAbilityBaseEffectReference()
    {
        if (abilityBaseEffect == null && abilityBaseImage != null)
            abilityBaseEffect = abilityBaseImage.GetComponent<UIEffect>();
    }

    private void RefreshImportantNodeClassification()
    {
        isImportantNode = HasMarkupTag(displayName);
        RefreshImportantNodeEffect();
    }

    private void RefreshImportantNodeEffect()
    {
        CacheAbilityBaseEffectReference();
        bool shouldEnable = isImportantNode && visualHidden == false;
        if (abilityBaseEffect != null)
        {
            if (shouldEnable == false)
            {
                abilityBaseEffect.enabled = false;
            }
            else
            {
                abilityBaseEffect.edgeMode = EdgeMode.Shiny;
                abilityBaseEffect.edgeWidth = 0.5f;
                abilityBaseEffect.edgeColorFilter = ColorFilter.Replace;
                abilityBaseEffect.edgeShinyRate = 0.5f;
                abilityBaseEffect.edgeShinyWidth = 0.4f;
                abilityBaseEffect.edgeShinyAutoPlaySpeed = 0.75f;
                abilityBaseEffect.edgeColor = GetImportantNodeShinyColor();
                abilityBaseEffect.enabled = true;
            }
        }

        RefreshImportantNodeLoopEffect();
    }

    private Color GetImportantNodeShinyColor()
    {
        return completedVisual
            ? CompletedShinyColor
            : canApplyVisual
                ? CanApplyShinyColor
                : CannotApplyShinyColor;
    }

    private void CacheImportantNodeLoopEffectReferences()
    {
        if (importantNodeLoopEffectFirstImage == null)
        {
            Transform firstTransform = FindChildRecursive(transform, "LoopEffect_First");
            if (firstTransform != null)
                importantNodeLoopEffectFirstImage = firstTransform.GetComponent<Image>();
        }

        if (importantNodeLoopEffectSecondImage == null)
        {
            Transform secondTransform = FindChildRecursive(transform, "LoopEffect_Second");
            if (secondTransform != null)
                importantNodeLoopEffectSecondImage = secondTransform.GetComponent<Image>();
        }

        if (importantNodeLoopEffectFirstImage != null)
            importantNodeLoopEffectFirstImage.raycastTarget = false;

        if (importantNodeLoopEffectSecondImage != null)
            importantNodeLoopEffectSecondImage.raycastTarget = false;
    }

    private void RefreshImportantNodeLoopEffect()
    {
        CacheImportantNodeLoopEffectReferences();
        bool shouldPlay = isImportantNode
            && visualHidden == false
            && isActiveAndEnabled
            && importantNodeLoopEffectFrames != null
            && importantNodeLoopEffectFrames.Length > 0;

        if (shouldPlay == false)
        {
            StopImportantNodeLoopEffect();
            return;
        }

        if (importantNodeLoopEffectInitialized == false)
        {
            importantNodeLoopEffectFirstFrameIndex = UnityEngine.Random.Range(0, importantNodeLoopEffectFrames.Length);
            importantNodeLoopEffectSecondFrameIndex = UnityEngine.Random.Range(0, importantNodeLoopEffectFrames.Length);
            importantNodeLoopEffectElapsed = 0f;
            importantNodeLoopEffectInitialized = true;
        }

        importantNodeLoopEffectPlaying = true;
        SetImportantNodeLoopEffectVisible(true);
        ApplyImportantNodeLoopEffectColors();
        ApplyImportantNodeLoopEffectFrames();
    }

    private void UpdateImportantNodeLoopEffect()
    {
        if (importantNodeLoopEffectPlaying == false
            || importantNodeLoopEffectFrames == null
            || importantNodeLoopEffectFrames.Length == 0)
        {
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, importantNodeLoopEffectFrameRate);
        importantNodeLoopEffectElapsed += Time.unscaledDeltaTime;
        int advancedFrameCount = Mathf.FloorToInt(importantNodeLoopEffectElapsed / frameDuration);
        if (advancedFrameCount <= 0)
            return;

        importantNodeLoopEffectElapsed -= advancedFrameCount * frameDuration;
        importantNodeLoopEffectFirstFrameIndex =
            (importantNodeLoopEffectFirstFrameIndex + advancedFrameCount) % importantNodeLoopEffectFrames.Length;
        importantNodeLoopEffectSecondFrameIndex =
            (importantNodeLoopEffectSecondFrameIndex + advancedFrameCount) % importantNodeLoopEffectFrames.Length;
        ApplyImportantNodeLoopEffectFrames();
    }

    private void StopImportantNodeLoopEffect()
    {
        importantNodeLoopEffectPlaying = false;
        importantNodeLoopEffectElapsed = 0f;
        SetImportantNodeLoopEffectVisible(false);
    }

    private void SetImportantNodeLoopEffectVisible(bool _visible)
    {
        if (importantNodeLoopEffectFirstImage != null)
            importantNodeLoopEffectFirstImage.gameObject.SetActive(_visible);

        if (importantNodeLoopEffectSecondImage != null)
            importantNodeLoopEffectSecondImage.gameObject.SetActive(_visible);
    }

    private void ApplyImportantNodeLoopEffectColors()
    {
        if (importantNodeLoopEffectFirstImage != null)
            importantNodeLoopEffectFirstImage.color = currentNodeFrameColor;

        if (importantNodeLoopEffectSecondImage != null)
            importantNodeLoopEffectSecondImage.color = GetImportantNodeShinyColor();
    }

    private void ApplyImportantNodeLoopEffectFrames()
    {
        if (importantNodeLoopEffectFrames == null || importantNodeLoopEffectFrames.Length == 0)
            return;

        if (importantNodeLoopEffectFirstImage != null)
            importantNodeLoopEffectFirstImage.sprite = importantNodeLoopEffectFrames[importantNodeLoopEffectFirstFrameIndex];

        if (importantNodeLoopEffectSecondImage != null)
            importantNodeLoopEffectSecondImage.sprite = importantNodeLoopEffectFrames[importantNodeLoopEffectSecondFrameIndex];
    }

    private void SortImportantNodeLoopEffectFrames()
    {
        if (importantNodeLoopEffectFrames != null && importantNodeLoopEffectFrames.Length > 1)
            Array.Sort(importantNodeLoopEffectFrames, CompareSpriteFrameNames);
    }

    private static bool HasMarkupTag(string _text)
    {
        if (string.IsNullOrEmpty(_text))
            return false;

        int tagStartIndex = _text.IndexOf('<');
        return tagStartIndex >= 0 && _text.IndexOf('>', tagStartIndex + 1) > tagStartIndex;
    }

    public void PlayUnlockAppearMotion()
    {
        PlayClickMotion();
    }

    public void PlayMaxLevelUpEffect()
    {
        StopMaxLevelUpEffect();
        CacheMaxLevelUpEffectReference();
        if (maxLevelUpEffectImage == null || maxLevelUpEffectFrames == null || maxLevelUpEffectFrames.Length == 0)
            return;

        maxLevelUpEffectImage.raycastTarget = false;
        maxLevelUpEffectImage.sprite = maxLevelUpEffectFrames[0];
        maxLevelUpEffectImage.gameObject.SetActive(true);

        float frameRate = Mathf.Max(1f, maxLevelUpEffectFrameRate);
        float duration = maxLevelUpEffectFrames.Length / frameRate;
        int currentFrameIndex = 0;
        maxLevelUpEffectTween = DOVirtual.Float(0f, duration, duration, _elapsedTime =>
        {
            int frameIndex = Mathf.Min(
                Mathf.FloorToInt(_elapsedTime * frameRate),
                maxLevelUpEffectFrames.Length - 1);
            if (frameIndex == currentFrameIndex)
                return;

            currentFrameIndex = frameIndex;
            maxLevelUpEffectImage.sprite = maxLevelUpEffectFrames[frameIndex];
        })
        .SetEase(Ease.Linear)
        .SetUpdate(true)
        .OnComplete(() =>
        {
            maxLevelUpEffectTween = null;
            HideMaxLevelUpEffect();
        });
    }

    private void StopMaxLevelUpEffect()
    {
        if (maxLevelUpEffectTween != null && maxLevelUpEffectTween.IsActive())
            maxLevelUpEffectTween.Kill(false);

        maxLevelUpEffectTween = null;
        HideMaxLevelUpEffect();
    }

    private void HideMaxLevelUpEffect()
    {
        if (maxLevelUpEffectImage != null)
            maxLevelUpEffectImage.gameObject.SetActive(false);
    }

    private void CacheMaxLevelUpEffectReference()
    {
        if (maxLevelUpEffectImage == null)
        {
            Transform effectTransform = FindChildRecursive(transform, "MaxLevelUpEffect");
            if (effectTransform != null)
                maxLevelUpEffectImage = effectTransform.GetComponent<Image>();
        }

        if (maxLevelUpEffectImage != null)
            maxLevelUpEffectImage.raycastTarget = false;
    }

    private void SortMaxLevelUpEffectFrames()
    {
        if (maxLevelUpEffectFrames != null && maxLevelUpEffectFrames.Length > 1)
            Array.Sort(maxLevelUpEffectFrames, CompareSpriteFrameNames);
    }

    private static int CompareSpriteFrameNames(Sprite _left, Sprite _right)
    {
        int leftIndex = GetSpriteFrameIndex(_left);
        int rightIndex = GetSpriteFrameIndex(_right);
        int indexComparison = leftIndex.CompareTo(rightIndex);
        if (indexComparison != 0)
            return indexComparison;

        string leftName = _left == null ? string.Empty : _left.name;
        string rightName = _right == null ? string.Empty : _right.name;
        return string.CompareOrdinal(leftName, rightName);
    }

    private static int GetSpriteFrameIndex(Sprite _sprite)
    {
        if (_sprite == null || string.IsNullOrEmpty(_sprite.name))
            return int.MaxValue;

        string spriteName = _sprite.name;
        int digitStart = spriteName.Length;
        while (digitStart > 0 && char.IsDigit(spriteName[digitStart - 1]))
            digitStart--;

        if (digitStart >= spriteName.Length)
            return int.MaxValue;

        return int.TryParse(spriteName.Substring(digitStart), out int frameIndex)
            ? frameIndex
            : int.MaxValue;
    }

    // 마우스가 노드 위에 올라오면 상위 컴포넌트에 툴팁 표시를 요청한다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        if (owner != null && owner.CanShowNodeHover(this) == false)
            return;

        BeginHover();
    }

    // 마우스가 노드 밖으로 나가면 상위 컴포넌트에 툴팁 숨김을 요청한다.
    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        if (owner != null && owner.ShouldKeepNodeHoverCaptured(this))
            return;

        EndHover();
    }

    // 어느 마우스 버튼이든 노드에서 눌리면 Hover를 캡처한다. 레벨업 입력은 좌클릭만 처리한다.
    public void OnPointerDown(PointerEventData eventData)
    {
        owner?.CaptureNodeHover(this);

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

#if UNITY_EDITOR
        if (IsShiftPressed())
        {
            consumedRapidClick = true;
            owner?.StartAutoNodeLevelUp(this, IsControlPressed());
            return;
        }
#endif
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        owner?.ReleaseNodeHoverCapture(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        if (consumedRapidClick)
        {
            consumedRapidClick = false;
            return;
        }

        if (IsDraggedClick(eventData))
            return;

        bool isApproved;
#if UNITY_EDITOR
        isApproved = owner != null && (IsControlPressed()
            ? owner.TryRequestNodeLevelUpWithoutCost(this)
            : owner.TryRequestNodeLevelUp(this));
#else
        isApproved = owner != null && owner.TryRequestNodeLevelUp(this);
#endif
        if (true == isApproved)
            PlayClickRequestMotion();
        else
            PlayRejectedRequestMotion();
    }

#if UNITY_EDITOR
    private bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private bool IsControlPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
    }
#endif

    private bool IsDraggedClick(PointerEventData _eventData)
    {
        if (_eventData == null)
            return false;

        float threshold = Mathf.Max(0f, clickCancelDragThreshold);
        if (threshold <= 0f)
            return false;

        Vector2 dragDelta = _eventData.position - _eventData.pressPosition;
        return dragDelta.sqrMagnitude > threshold * threshold;
    }

    public void PlayClickRequestMotion()
    {
        PlayClickMotion();
        PlayApprovedNodeEffect(currentNodeFrameColor);
    }

    // 특성 찍기에 성공했을 때 노드 이펙트를 재생하는 자리.
    private void PlayApprovedNodeEffect(Color _nodeFrameColor)
    {
        owner?.PlaySharedNodeEffect(
            levelUpImpactTag,
            transform,
            _nodeFrameColor,
            effectLayerTag,
            2);
    }

    public void PlayRejectedRequestMotion()
    {
        PlayNonPassClickMotion();
    }

    private void PlayHoverMotion()
    {
        if (null == motionPlayer || string.IsNullOrEmpty(hoverMotionTag))
            return;

        if (motionPlayer.IsPlaying(hoverMotionTag))
            return;

        ResetEntryMotion(clickMotionEntry);
        ResetEntryMotion(unHoverMotionEntry);
        ResetEntryMotion(nonPassClickMotionEntry);
        hoverMotionEntry = motionPlayer.Play(hoverMotionTag, bReset: resetCurrentMotionBeforePlay);
    }

    private void PlayUnHoverMotion()
    {
        if (null == motionPlayer || string.IsNullOrEmpty(unHoverMotionTag))
            return;

        if (IsClickMotionPlaying())
            return;

        ResetEntryMotion(hoverMotionEntry);
        unHoverMotionEntry = motionPlayer.Play(unHoverMotionTag, bReset: resetCurrentMotionBeforePlay);
    }

    private void PlayClickMotion()
    {
        if (null == motionPlayer || string.IsNullOrEmpty(clickMotionTag))
            return;

        ResetEntryMotion(hoverMotionEntry);
        ResetEntryMotion(unHoverMotionEntry);
        ResetEntryMotion(nonPassClickMotionEntry);
        clickMotionEntry = motionPlayer.Play(clickMotionTag, bReset: resetCurrentMotionBeforePlay);
    }

    private void PlayNonPassClickMotion()
    {
        if (null == motionPlayer || string.IsNullOrEmpty(nonPassClickMotionTag))
            return;

        ResetEntryMotion(hoverMotionEntry);
        ResetEntryMotion(unHoverMotionEntry);
        ResetEntryMotion(clickMotionEntry);
        nonPassClickMotionEntry = motionPlayer.Play(nonPassClickMotionTag, bReset: resetCurrentMotionBeforePlay);
    }

    private void ResetEntryMotion(MotionEntry _entry)
    {
        if (null == motionPlayer || null == _entry)
            return;

        motionPlayer.SettingEntryMotion(_entry, true, true);
    }

    private void ApplyImageAlpha(Image _image, float _alpha)
    {
        if (_image == null)
            return;

        Color color = _image.color;
        color.a = _alpha;
        _image.color = color;
    }

    private bool IsClickMotionPlaying()
    {
        if (motionPlayer == null)
            return false;

        bool isClickPlaying = string.IsNullOrEmpty(clickMotionTag) == false &&
                              motionPlayer.IsPlaying(clickMotionTag);
        bool isNonPassClickPlaying = string.IsNullOrEmpty(nonPassClickMotionTag) == false &&
                                     motionPlayer.IsPlaying(nonPassClickMotionTag);

        return isClickPlaying || isNonPassClickPlaying;
    }

    public void RefreshHoverAfterCapture()
    {
        if (isPointerInside && (owner == null || owner.CanShowNodeHover(this)))
            BeginHover();
        else
            EndHover();
    }

    private void BeginHover()
    {
        if (isPointerHovering)
            return;

        isPointerHovering = true;
        owner?.PlayNodeHoverSound();
        owner?.ShowSelectionCursor(this);
        owner?.ShowToolTip(this);
        PlayHoverMotion();
    }

    private void EndHover()
    {
        if (isPointerHovering == false)
            return;

        isPointerHovering = false;
        owner?.HideSelectionCursor(this);
        owner?.HideToolTip(this);
        PlayUnHoverMotion();
    }

    private void CacheInteractionReferences()
    {
        if (abilityNodeTouchArea == null)
            abilityNodeTouchArea = FindChildRecursive(transform, "AbilityNode_TouchArea") as RectTransform;

        if (abilityVisualRoot == null)
            abilityVisualRoot = FindChildRecursive(transform, "AbilityVisual") as RectTransform;

        if (motionPlayer == null)
            motionPlayer = GetComponentInChildren<ObjectMotionPlayer>(true);

        if (vfxComponent == null)
            vfxComponent = GetComponentInChildren<VFXComponent>(true);

        if (EnsureTouchAreaRaycastTarget())
            DisableVisualRaycasts();
    }

    private bool EnsureTouchAreaRaycastTarget()
    {
        if (abilityNodeTouchArea == null)
            return false;

        Image touchAreaImage = abilityNodeTouchArea.GetComponent<Image>();
        if (touchAreaImage != null)
            touchAreaImage.raycastTarget = true;

        return touchAreaImage != null;
    }

    private void DisableVisualRaycasts()
    {
        if (abilityVisualRoot == null)
            return;

        Graphic[] graphics = abilityVisualRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void CancelHoverState()
    {
        isPointerInside = false;
        owner?.NotifyNodeHoverUnavailable(this);

        if (isPointerHovering)
        {
            isPointerHovering = false;
            owner?.HideSelectionCursor(this);
            owner?.HideToolTip(this);
        }
    }
}
