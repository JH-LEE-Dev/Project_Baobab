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
        public float duration = 0.7f;

        [Header("Scale Settings")]
        public float shrinkScale = 0.8f;
        [Range(0f, 1f)] public float shrinkTimeRatio = 0.08f;
        [Range(0f, 1f)] public float restoreTimeRatio = 0.12f;
        public Ease scaleEase = Ease.OutBack;

        [Header("Rotation Settings")]
        public float startAngle = 8f;
        public float angleDamping = 1.5f;
        public int swingCount = 4;
        [Range(0f, 1f)] public float rotationTimeRatio = 0.8f;
        public Ease rotationEase = Ease.OutSine;
    }

    [System.Serializable]
    public class UnhoverSettings
    {
        [Tooltip("전체 Unhover 연출 시간")]
        public float duration = 0.7f;

        [Header("Rotation Settings")]
        public float startAngle = 5f;
        public float angleDamping = 0.62f;
        public int swingCount = 4;
        [Range(0f, 1f)] public float rotationTimeRatio = 1f;
        public Ease rotationEase = Ease.OutSine;
    }

    [Header("UI Components")]
    [SerializeField] private Graphic targetGraphicOverride;

    [Header("Cursor Settings")]
    [SerializeField] private RectTransform cursorTargetTransform;
    [SerializeField] private Vector2 cursorPadding = new Vector2(2f, 2f);
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Header("Motion Configs")]
    [SerializeField] private HoverSettings hoverSettings = new HoverSettings();
    [SerializeField] private UnhoverSettings unhoverSettings = new UnhoverSettings();
    [SerializeField] private float clickTwistAngle = 15f;
    [SerializeField] private float clickDuration = 0.3f;

    [Header("Visual Dim Settings")]
    [SerializeField] private float disabledAlpha = 0.35f;
    [SerializeField] private float dimFadeDuration = 0.15f;

    // 내부 상태
    private bool isHovered = false;
    private bool isPointerHovered = false;
    private Sequence hoverSequence;
    private Sequence clickSequence;
    private Tween dimTween;
    private RectTransform cachedRectTransform;
    private Canvas cachedCanvas;

    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private Action onClickCallback;
    private SoundID clickSoundId = SoundID.OptionClick;

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

    public void Initialize(InputManager _inputManager, ICursorBoxUI _cursorBoxUI, Action _onClickCallback = null, SoundID _clickSoundId = SoundID.OptionClick)
    {
        inputManager = _inputManager;
        cursorBoxUI = _cursorBoxUI;
        onClickCallback = _onClickCallback;
        clickSoundId = _clickSoundId;
    }

    public void SetInteractable(bool _isInteractable, bool _instant = false)
    {
        interactable = _isInteractable;
        if (null != targetGraphic)
        {
            targetGraphic.raycastTarget = _isInteractable;
        }
        if (false == _isInteractable)
        {
            ForceUnhover();
        }
        UpdateDimState(false == _isInteractable, _instant);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        bool _isDisabled = (SelectionState.Disabled == state);
        if (null != targetGraphic)
        {
            targetGraphic.raycastTarget = !_isDisabled;
        }
        UpdateDimState(_isDisabled, instant);
    }

    private void UpdateDimState(bool _isDim, bool _instant)
    {
        Graphic _targetGraphic = (null != targetGraphicOverride) ? targetGraphicOverride : targetGraphic;
        if (null == _targetGraphic) return;

        float _targetAlpha = (true == _isDim) ? disabledAlpha : 1f;

        if (null != dimTween && true == dimTween.IsActive())
        {
            dimTween.Kill();
            dimTween = null;
        }

        if (true == _instant || false == gameObject.activeInHierarchy)
        {
            Color _col = _targetGraphic.color;
            _col.a = _targetAlpha;
            _targetGraphic.color = _col;
        }
        else
        {
            dimTween = _targetGraphic.DOFade(_targetAlpha, dimFadeDuration).SetEase(Ease.OutQuad).SetTarget(this);
        }
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
        if (SoundID.None != clickSoundId)
        {
            Sound.PlayUI(clickSoundId);
        }
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
        if (false == IsInteractable()) return;
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        isHovered = true;
        isPointerHovered = true;
        Sound.PlayUI(SoundID.ResultUIHover);
        PlayHoverWiggleAnimation();
        ShowCursor();
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        if (null != inputManager && true == inputManager.IsGamepadMode) return;

        if (false == isHovered && false == isPointerHovered) return;

        isHovered = false;
        isPointerHovered = false;
        HideCursor();

        if (true == IsInteractable())
        {
            PlayUnhoverAnimation();
        }
        else
        {
            ResetMotionImmediate();
        }
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        if (false == IsInteractable()) return;
        if (null != inputManager && false == inputManager.IsGamepadMode) return;
        if (true == isHovered) return;

        isHovered = true;
        Sound.PlayUI(SoundID.ResultUIHover);
        PlayHoverWiggleAnimation();
        ShowCursor();
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);
        if (false == isHovered) return;
        if (true == isPointerHovered) return;

        isHovered = false;
        HideCursor();

        if (true == IsInteractable())
        {
            PlayUnhoverAnimation();
        }
        else
        {
            ResetMotionImmediate();
        }
    }

    public void ForceHover(bool _playAudio = false)
    {
        if (false == IsInteractable()) return;

        isHovered = true;
        if (true == _playAudio)
        {
            Sound.PlayUI(SoundID.ResultUIHover);
        }
        PlayHoverWiggleAnimation();
        ShowCursor();
    }

    public void ForceUnhover()
    {
        bool _wasHovered = isHovered || isPointerHovered;
        isHovered = false;
        isPointerHovered = false;
        HideCursor();

        if (true == _wasHovered && true == IsInteractable())
        {
            PlayUnhoverAnimation();
        }
        else
        {
            ResetMotionImmediate();
        }
    }

    public void ResetMotionImmediate()
    {
        KillActiveTweens();
        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        if (null != _targetT)
        {
            _targetT.localScale = Vector3.one;
            _targetT.localRotation = Quaternion.identity;
        }
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

    // hover/unhover 연출은 UI_WarningPopupButton(경고·저장 확인·ESC 메뉴 팝업 버튼)과 같은 방식으로 만든다.
    // 같은 수치를 넣어도 해석이 달라 이 버튼만 느낌이 달랐다. 특히 감쇠값(angleDamping, 기본 1.5)을
    // 그대로 곱해 흔들 때마다 각도가 커졌는데(8° → 12° → 18°), 경고 팝업 버튼은 0~1로 잘라 쓴다.
    // 수치를 바꿀 때는 두 버튼이 같은 느낌으로 남도록 함께 맞출 것.
    private void PlayHoverWiggleAnimation()
    {
        KillActiveTweens();

        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        _targetT.localScale = Vector3.one;
        _targetT.localRotation = Quaternion.identity;

        Sequence _seq = DOTween.Sequence().SetUpdate(true);

        float _shrinkT = hoverSettings.duration * Mathf.Clamp01(hoverSettings.shrinkTimeRatio);
        float _restoreT = hoverSettings.duration * Mathf.Clamp01(hoverSettings.restoreTimeRatio);

        Sequence _scaleSeq = DOTween.Sequence();
        _scaleSeq.Append(_targetT.DOScale(hoverSettings.shrinkScale, _shrinkT).SetEase(Ease.OutQuad));
        _scaleSeq.Append(_targetT.DOScale(1f, _restoreT).SetEase(hoverSettings.scaleEase));
        _seq.Join(_scaleSeq);

        float _rotT = hoverSettings.duration * Mathf.Clamp01(hoverSettings.rotationTimeRatio);
        _seq.Join(CreateSwingSequence(_targetT, hoverSettings.startAngle, hoverSettings.angleDamping, hoverSettings.swingCount, _rotT, hoverSettings.rotationEase, false));

        _seq.SetTarget(this);
        hoverSequence = _seq;
    }

    private void PlayUnhoverAnimation()
    {
        KillActiveTweens();

        Transform _targetT = (null != targetGraphicOverride) ? targetGraphicOverride.transform : transform;
        _targetT.localScale = Vector3.one;
        _targetT.localRotation = Quaternion.identity;

        Sequence _seq = DOTween.Sequence().SetUpdate(true);

        float _rotT = unhoverSettings.duration * Mathf.Clamp01(unhoverSettings.rotationTimeRatio);
        _seq.Join(CreateSwingSequence(_targetT, unhoverSettings.startAngle, unhoverSettings.angleDamping, unhoverSettings.swingCount, _rotT, unhoverSettings.rotationEase, true));

        _seq.SetTarget(this);
        hoverSequence = _seq;
    }

    /// <summary>
    /// 좌우로 _swingCount번 흔든 뒤 제자리로 돌아오는 회전 시퀀스입니다. (UI_WarningPopupButton.CreateSwingSequence와 동일)
    /// 감쇠는 0~1로 잘라 쓰므로 흔들림이 점점 커지는 일은 없습니다.
    /// </summary>
    private static Sequence CreateSwingSequence(Transform _target, float _startAngle, float _angleDamping, int _swingCount, float _rotDuration, Ease _rotationEase, bool _invertDirection)
    {
        Sequence _rotSeq = DOTween.Sequence();
        float _angle = Mathf.Abs(_startAngle);
        int _validSwingCount = Mathf.Max(_swingCount, 1);
        float _swingDuration = _rotDuration / (_validSwingCount + 1);

        for (int i = 0; i < _validSwingCount; i++)
        {
            float _direction = (0 == i % 2) ? -1f : 1f;
            if (true == _invertDirection) _direction *= -1f;

            _rotSeq.Append(_target.DOLocalRotate(new Vector3(0f, 0f, _angle * _direction), _swingDuration, RotateMode.Fast).SetEase(_rotationEase));
            _angle *= Mathf.Clamp01(_angleDamping);
        }

        _rotSeq.Append(_target.DOLocalRotate(Vector3.zero, _swingDuration, RotateMode.Fast).SetEase(_rotationEase));
        return _rotSeq;
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
        if (null != dimTween && true == dimTween.IsActive())
        {
            dimTween.Kill();
            dimTween = null;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        isPointerHovered = false;
        ResetMotionImmediate();
        HideCursor();
    }
}
