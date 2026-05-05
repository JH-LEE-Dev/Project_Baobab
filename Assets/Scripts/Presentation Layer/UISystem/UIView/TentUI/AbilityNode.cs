using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PresentationLayer.DOTweenAnimationSystem;

public class AbilityNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Node Data")]
    [SerializeField] private SkillType skillType = SkillType.None;
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private int currentLevel;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private SkillType[] parentSkillTypes;

    [Header("UI References")]
    [SerializeField] private Image abilityBaseImage;
    [SerializeField] private Image abilityBackgroundImage;
    [SerializeField] private Image abilityPictureImage;

    [Header("Default Visual")]
    [SerializeField] private Sprite defaultPictureSprite;

    [Header("Motion Settings")]
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private string hoverMotionTag = "UIHover";
    [SerializeField] private string unHoverMotionTag = "UIUnHover";
    [SerializeField] private string clickMotionTag = "UIClick";
    [SerializeField] private string nonPassClickMotionTag = "UIClick_Nonpass";
    [SerializeField] private bool resetCurrentMotionBeforePlay = false;
    [SerializeField] private float hoverStablePadding = 8f;

    private UI_TentAbilityComponent owner;
    private Canvas rootCanvas;
    private bool canApplyVisual;
    private bool completedVisual;
    private bool isPointerHovering;
    private MotionEntry hoverMotionEntry;
    private MotionEntry unHoverMotionEntry;
    private MotionEntry clickMotionEntry;
    private MotionEntry nonPassClickMotionEntry;

    public SkillType SkillType => skillType;
    public string DisplayName => displayName;
    public string Description => description;
    public int CurrentLevel => currentLevel;
    public Vector2Int GridPosition => gridPosition;
    public SkillType[] ParentSkillTypes => parentSkillTypes;
    public RectTransform RectTransform => transform as RectTransform;
    public bool CanApplyVisual => canApplyVisual;
    public bool CompletedVisual => completedVisual;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();

        if (null != motionPlayer)
            return;

        motionPlayer = GetComponentInChildren<ObjectMotionPlayer>(true);
    }

    private void Update()
    {
        if (isPointerHovering == false)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (IsScreenPointInsideStableHoverArea(mouse.position.ReadValue()))
            return;

        EndHover();
    }

    // 특성 노드의 내부 그림을 외부에서 교체한다.
    private void SetPicture(Sprite _sprite)
    {
        if (abilityPictureImage == null)
            return;

        abilityPictureImage.sprite = _sprite != null ? _sprite : defaultPictureSprite;
    }

    // 노드가 포인터 이벤트를 전달할 상위 능력 UI 컴포넌트를 연결한다.
    public void BindOwner(UI_TentAbilityComponent _owner)
    {
        owner = _owner;
    }

    // 현재 노드가 어떤 스킬 타입인지 반환한다.
    public SkillType GetSkillType()
    {
        return skillType;
    }

    // 이 노드가 다음에 요청할 레벨을 반환한다.
    public int GetNextLevel()
    {
        return currentLevel + 1;
    }

    // JSON에서 읽은 노드 정의를 현재 프리팹 인스턴스에 반영한다.
    public void ApplyDefinition(AbilityNodeDefinitionJson _definition, SkillType _skillType, Sprite _pictureSprite, float _gridCellSize)
    {
        if (_definition == null)
            return;

        skillType = _skillType;
        displayName = _definition.displayName;
        description = _definition.description;
        currentLevel = 0;
        gridPosition = new Vector2Int(_definition.gridX, _definition.gridY);
        parentSkillTypes = ConvertParentSkillTypes(_definition.GetParentSkillTypeNames());

        SetPicture(_pictureSprite);
        ApplyAnchoredPosition(_gridCellSize);
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

    // 현재 노드의 테두리/배경 표시 상태를 갱신한다.
    public void ApplyVisualState(Color _baseColor, bool _canApply, bool _completed)
    {
        canApplyVisual = _canApply;
        completedVisual = _completed;

        if (abilityBaseImage != null)
            abilityBaseImage.color = _baseColor;
    }

    // 툴팁 제목 줄에 표시할 이름과 현재 레벨 문자열을 만든다.
    public string GetToolTipTitleAndLevelText()
    {
        return $"{displayName}\n레벨 : {currentLevel}";
    }

    // 툴팁 설명 문자열을 반환한다.
    public string GetToolTipDescriptionText()
    {
        return description;
    }

    // 비용 정보는 상위 시스템이 소유하므로 UI 노드는 직접 표시하지 않는다.
    public string GetToolTipCostText()
    {
        return string.Empty;
    }

    // 마우스가 노드 위에 올라오면 상위 컴포넌트에 툴팁 표시를 요청한다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isPointerHovering)
            return;

        isPointerHovering = true;
        owner?.ShowToolTip(this);
        PlayHoverMotion();
    }

    // 마우스가 노드 밖으로 나가면 상위 컴포넌트에 툴팁 숨김을 요청한다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (IsPointerInsideStableHoverArea(eventData))
            return;

        EndHover();
    }

    // 노드 클릭 시 상위 컴포넌트에 레벨업 요청을 전달한다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        bool isApproved = owner != null && owner.TryRequestNodeLevelUp(this);
        if (true == isApproved)
            PlayClickMotion();
        else
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

    private bool IsPointerInsideStableHoverArea(PointerEventData _eventData)
    {
        if (_eventData == null)
            return false;

        return IsScreenPointInsideStableHoverArea(_eventData.position);
    }

    private bool IsScreenPointInsideStableHoverArea(Vector2 _screenPoint)
    {
        RectTransform nodeRectTransform = RectTransform;
        RectTransform visualRectTransform = GetStableHoverVisualRectTransform();
        if (nodeRectTransform == null || visualRectTransform == null)
            return false;

        Vector2 center = RectTransformUtility.WorldToScreenPoint(GetEventCamera(), nodeRectTransform.TransformPoint(Vector3.zero));
        Vector2 size = visualRectTransform.rect.size;
        Vector3 lossyScale = nodeRectTransform.lossyScale;
        size.x *= Mathf.Abs(lossyScale.x);
        size.y *= Mathf.Abs(lossyScale.y);
        size += Vector2.one * hoverStablePadding * 2f;

        Rect stableRect = new Rect(center - size * 0.5f, size);
        return stableRect.Contains(_screenPoint);
    }

    private void EndHover()
    {
        if (isPointerHovering == false)
            return;

        isPointerHovering = false;
        owner?.HideToolTip(this);
        PlayUnHoverMotion();
    }

    private RectTransform GetStableHoverVisualRectTransform()
    {
        if (abilityBaseImage != null)
            return abilityBaseImage.rectTransform;

        if (abilityBackgroundImage != null)
            return abilityBackgroundImage.rectTransform;

        if (abilityPictureImage != null)
            return abilityPictureImage.rectTransform;

        return RectTransform;
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return rootCanvas.worldCamera;
    }
}
