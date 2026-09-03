using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.InputSystem;

/// <summary>
/// 팝업/모달 대화상자 전용 공용 액션 버튼 컴포넌트입니다. (확인, 취소, 선택 등)
/// 부드러운 Wiggle/Twist 모션 및 커서박스 연동을 완벽히 지원하며 특정 팝업에 종속되지 않습니다.
/// </summary>
public class UI_PopupButton : Selectable,
    IPointerClickHandler,
    ISubmitHandler
{
    public event Action OnClickedEvent;

    [System.Serializable]
    public class HoverSettings
    {
        [Tooltip("전체 Hover 연출 시간")]
        public float duration = 0.5f;

        [Header("Scale Settings")]
        public float shrinkScale = 0.9f;
        [Range(0f, 1f)] public float shrinkTimeRatio = 0.1f;
        [Range(0f, 1f)] public float restoreTimeRatio = 0.15f;
        public Ease scaleEase = Ease.OutBack;

        [Header("Rotation Settings")]
        public float startAngle = 12f;
        public float angleDamping = 0.65f;
        public int swingCount = 4;
        [Range(0f, 1f)] public float rotationTimeRatio = 0.8f;
        public Ease rotationEase = Ease.OutSine;
    }

    [System.Serializable]
    public class UnhoverSettings
    {
        [Tooltip("전체 Unhover 연출 시간")]
        public float duration = 0.4f;

        [Header("Rotation Settings")]
        public float startAngle = 8f;
        public float angleDamping = 0.65f;
        public int swingCount = 3;
        [Range(0f, 1f)] public float rotationTimeRatio = 1f;
        public Ease rotationEase = Ease.OutSine;
    }

    [Header("UI Components")]
    [SerializeField] private Graphic targetGraphicOverride;

    [Header("Cursor Settings")]
    [SerializeField] private RectTransform cursorTargetTransform;
    [SerializeField] private Vector2 cursorPadding = Vector2.zero;
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Header("Motion Configs")]
    [SerializeField] private HoverSettings hoverSettings = new HoverSettings();
    [SerializeField] private UnhoverSettings unhoverSettings = new UnhoverSettings();
    [SerializeField] private float clickTwistAngle = 15f;
    [SerializeField] private float clickDuration = 0.3f;

    // 내부 상태
    private bool isHovered = false;
    private bool isPointerHovered = false;
    private Sequence hoverSequence;
    private Sequence clickSequence;
    private RectTransform cachedRectTransform;
    private Canvas cachedCanvas;

    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private Action onClickCallback;

    public RectTransform CachedRectTransform
    {
        get
        {
            if (null == cachedRectTransform) cachedRectTransform = GetComponent<RectTransform>();
            return cachedRectTransform;
        }
    }

    public bool IsHovered => isHovered;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;
        if (null == targetGraphicOverride)
        {
            targetGraphicOverride = targetGraphic;
        }
    }

    public void Initialize(InputManager _inputManager, ICursorBoxUI _cursorBoxUI, Action _onClickCallback = null)
    {
        inputManager = _inputManager;
        cursorBoxUI = _cursorBoxUI;
        onClickCallback = _onClickCallback;
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == IsInteractable() || false == gameObject.activeInHierarchy) return;

        ExecuteClick();
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        if (false == IsInteractable() || false == gameObject.activeInHierarchy) return;

        ExecuteClick();
    }

    public void ExecuteClick()
    {
        Sound.PlayUI(SoundID.OptionClick);
        PlayClickTwistAnimation();

        if (null != onClickCallback)
        {
            onClickCallback.Invoke();
        }

        OnClickedEvent?.Invoke();
    }

    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = true;
        isPointerHovered = true;
        Sound.PlayUI(SoundID.MainMenuDot01);
        PlayHoverWiggleAnimation();
        ShowCursor();
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = false;
        isPointerHovered = false;
        PlayUnhoverAnimation();
        HideCursor();
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        isHovered = true;
        Sound.PlayUI(SoundID.MainMenuDot01);
        PlayHoverWiggleAnimation();
        ShowCursor();
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);

        isHovered = false;
        PlayUnhoverAnimation();
        HideCursor();
    }

    public void ForceHover()
    {
        isHovered = true;
        PlayHoverWiggleAnimation();
        ShowCursor();
    }

    public void ForceUnhover()
    {
        isHovered = false;
        isPointerHovered = false;
        PlayUnhoverAnimation();
        HideCursor();
    }

    public bool IsMouseOver()
    {
        if (false == IsInteractable() || false == gameObject.activeInHierarchy) return false;
        if (true == isPointerHovered) return true;

        RectTransform _rect = CachedRectTransform;
        if (null == _rect) return false;

        Vector2 _mousePos = Vector2.zero;
        if (null != Mouse.current)
        {
            _mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            _mousePos = Input.mousePosition;
        }

        if (null == cachedCanvas)
        {
            cachedCanvas = GetComponentInParent<Canvas>();
        }

        Camera _cam = (null != cachedCanvas && RenderMode.ScreenSpaceOverlay != cachedCanvas.renderMode)
            ? cachedCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    private void PlayHoverWiggleAnimation()
    {
        KillActiveTweens();

        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        _targetT.localScale = Vector3.one;
        _targetT.localRotation = Quaternion.identity;

        Sequence _seq = DOTween.Sequence();
        float _dur = hoverSettings.duration;
        float _shrinkT = _dur * hoverSettings.shrinkTimeRatio;
        float _restoreT = _dur * hoverSettings.restoreTimeRatio;

        _seq.Append(_targetT.DOScale(hoverSettings.shrinkScale, _shrinkT).SetEase(Ease.OutQuad));
        _seq.Append(_targetT.DOScale(1f, _restoreT).SetEase(hoverSettings.scaleEase));

        float _rotTotalT = _dur * hoverSettings.rotationTimeRatio;
        float _rotStepT = _rotTotalT / Mathf.Max(1, hoverSettings.swingCount);
        float _angle = hoverSettings.startAngle;

        for (int i = 0; i < hoverSettings.swingCount; i++)
        {
            float _targetAngle = (0 == i % 2) ? -_angle : _angle;
            if (i == hoverSettings.swingCount - 1) _targetAngle = 0f;
            _seq.Insert(_shrinkT + (i * _rotStepT), _targetT.DOLocalRotate(new Vector3(0f, 0f, _targetAngle), _rotStepT).SetEase(hoverSettings.rotationEase));
            _angle *= hoverSettings.angleDamping;
        }

        _seq.SetTarget(this);
        hoverSequence = _seq;
    }

    private void PlayUnhoverAnimation()
    {
        KillActiveTweens();

        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        _targetT.localScale = Vector3.one;

        Sequence _seq = DOTween.Sequence();
        float _dur = unhoverSettings.duration;
        float _rotTotalT = _dur * unhoverSettings.rotationTimeRatio;
        float _rotStepT = _rotTotalT / Mathf.Max(1, unhoverSettings.swingCount);
        float _angle = unhoverSettings.startAngle;

        for (int i = 0; i < unhoverSettings.swingCount; i++)
        {
            float _targetAngle = (0 == i % 2) ? -_angle : _angle;
            if (i == unhoverSettings.swingCount - 1) _targetAngle = 0f;
            _seq.Append(_targetT.DOLocalRotate(new Vector3(0f, 0f, _targetAngle), _rotStepT).SetEase(unhoverSettings.rotationEase));
            _angle *= unhoverSettings.angleDamping;
        }

        _seq.SetTarget(this);
        hoverSequence = _seq;
    }

    private void PlayClickTwistAnimation()
    {
        KillActiveTweens();

        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        _targetT.localScale = Vector3.one;
        _targetT.localRotation = Quaternion.identity;

        Sequence _seq = DOTween.Sequence();
        float _half = clickDuration * 0.5f;

        _seq.Append(_targetT.DOScale(0.85f, _half).SetEase(Ease.OutQuad));
        _seq.Join(_targetT.DOLocalRotate(new Vector3(0f, 0f, clickTwistAngle), _half).SetEase(Ease.OutQuad));
        _seq.Append(_targetT.DOScale(1f, _half).SetEase(Ease.OutBack));
        _seq.Join(_targetT.DOLocalRotate(Vector3.zero, _half).SetEase(Ease.OutBack));

        _seq.SetTarget(this);
        clickSequence = _seq;
    }

    private void ShowCursor()
    {
        if (null == cursorBoxUI) return;

        RectTransform _target = (null != cursorTargetTransform) ? cursorTargetTransform : CachedRectTransform;
        if (null == _target) return;

        Vector2 _size = _target.rect.size + cursorPadding;
        cursorBoxUI.Show(_target, _size, cursorOffset);
    }

    private void HideCursor()
    {
        if (null == cursorBoxUI) return;

        RectTransform _target = (null != cursorTargetTransform) ? cursorTargetTransform : CachedRectTransform;
        if (null != _target)
        {
            cursorBoxUI.Hide(_target);
        }
    }

    private void KillActiveTweens()
    {
        if (null != hoverSequence && true == hoverSequence.IsActive())
        {
            hoverSequence.Kill();
            hoverSequence = null;
        }
        if (null != clickSequence && true == clickSequence.IsActive())
        {
            clickSequence.Kill();
            clickSequence = null;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        isPointerHovered = false;
        KillActiveTweens();
        HideCursor();

        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        if (null != _targetT)
        {
            _targetT.localScale = Vector3.one;
            _targetT.localRotation = Quaternion.identity;
        }
    }
}
