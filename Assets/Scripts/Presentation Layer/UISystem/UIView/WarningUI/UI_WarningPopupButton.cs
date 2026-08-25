using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 경고/확인 팝업 전용 커스텀 버튼 클래스입니다.
/// 유니티 기본 Button 컴포넌트를 사용하지 않으며, Raycast 타겟과 시각적 타겟을 분리하여 관리합니다.
/// OMB_UIHoverWiggle, OMB_UIHoverOffWiggle, OMB_UIClickTwist 모션을 기반으로 한 연출을 지원합니다.
/// </summary>
public class UI_WarningPopupButton : Selectable,
    IPointerClickHandler,
    ISubmitHandler
{
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
        public float startAngle = 20f;
        public float angleDamping = 0.62f;
        public int swingCount = 5;
        [Range(0f, 1f)] public float rotationTimeRatio = 0.8f;
        public Ease rotationEase = Ease.OutSine;
    }

    [System.Serializable]
    public class UnhoverSettings
    {
        [Tooltip("전체 Unhover 연출 시간")]
        public float duration = 0.7f;

        [Header("Rotation Settings")]
        public float startAngle = 12f;
        public float angleDamping = 0.62f;
        public int swingCount = 5;
        [Range(0f, 1f)] public float rotationTimeRatio = 1f;
        public Ease rotationEase = Ease.OutSine;
    }

    [Header("UI Component")]
    [SerializeField, Tooltip("크기와 회전이 변형될 대상 (Raycast 본체와 다를 경우 지정)")] 
    private new Graphic targetGraphic;

    [Header("Cursor Settings")]
    [SerializeField, Tooltip("커서가 감쌀 실제 비주얼 RectTransform (미지정 시 targetGraphic 사용)")]
    private RectTransform cursorTargetTransform;
    [SerializeField] private Vector2 cursorPadding = new Vector2(8f, 8f);
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;
    [SerializeField] private bool useCustomCursorSize = false;
    [SerializeField] private Vector2 customCursorSize = new Vector2(100f, 40f);

    [Header("Motion Settings")]
    [SerializeField] private HoverSettings hoverSettings = new HoverSettings();
    [SerializeField] private UnhoverSettings unhoverSettings = new UnhoverSettings();
    
    private UI_WarningPopup parentPopup;
    private Action onClickAction;
    private Action onHoverAction;
    [SerializeField] private SoundID clickSoundId = SoundID.OptionClick;
    private bool isInteractable = true;
    private bool isHovered = false;

    public bool IsHovered => isHovered;

    // 초기 상태 캐싱
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Transform scaleTarget;

    private Tween activeTween;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        originalScale = scaleTarget.localScale;
        originalRotation = scaleTarget.localRotation;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        
        KillTween();
        
        if (null != scaleTarget)
        {
            scaleTarget.localScale = originalScale;
            scaleTarget.localRotation = originalRotation;
        }

        if (null != parentPopup)
        {
            parentPopup.OnButtonUnhovered(this);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        KillTween();
        onClickAction = null;
        onHoverAction = null;
        parentPopup = null;
    }

    public void Initialize(Action _onClick, Action _onHover = null, UI_WarningPopup _parentPopup = null)
    {
        onClickAction = _onClick;
        onHoverAction = _onHover;
        parentPopup = _parentPopup;
    }

    public void Initialize(Action _onClick, UI_WarningPopup _parentPopup)
    {
        onClickAction = _onClick;
        onHoverAction = null;
        parentPopup = _parentPopup;
    }

    public RectTransform GetCursorTargetRect()
    {
        if (null != cursorTargetTransform)
            return cursorTargetTransform;

        if (null != targetGraphic)
            return targetGraphic.rectTransform;

        return transform as RectTransform;
    }

    public Vector2 GetCursorSize()
    {
        if (true == useCustomCursorSize)
            return customCursorSize;

        RectTransform target = GetCursorTargetRect();
        if (null != target)
        {
            return target.rect.size + cursorPadding;
        }

        return customCursorSize;
    }

    public Vector2 GetCursorOffset()
    {
        return cursorOffset;
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;
        
        if (false == isInteractable)
        {
            KillTween();
            isHovered = false;
            
            if (null != scaleTarget)
            {
                scaleTarget.localScale = originalScale;
                scaleTarget.localRotation = originalRotation;
            }

            if (null != parentPopup)
            {
                parentPopup.OnButtonUnhovered(this);
            }
        }
    }

    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        isHovered = true;
        if (false == isInteractable)
            return;

        if (null != onHoverAction)
            onHoverAction();
        
        KillTween();
        PlayHoverAnimation();

        if (null != parentPopup)
        {
            parentPopup.OnButtonHovered(this);
        }
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        isHovered = false;
        if (false == isInteractable)
            return;
        
        KillTween();
        PlayUnhoverAnimation();

        if (null != parentPopup)
        {
            parentPopup.OnButtonUnhovered(this);
        }
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        isHovered = true;
        if (false == isInteractable)
            return;

        if (null != onHoverAction)
            onHoverAction();
        
        KillTween();
        PlayHoverAnimation();

        if (null != parentPopup)
        {
            parentPopup.OnButtonHovered(this);
        }
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);
        isHovered = false;
        if (false == isInteractable)
            return;
        
        KillTween();
        PlayUnhoverAnimation();

        if (null != parentPopup)
        {
            parentPopup.OnButtonUnhovered(this);
        }
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        OnPointerClick(null);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable)
            return;

        Sound.PlayUI(clickSoundId);

        if (null != parentPopup)
        {
            parentPopup.OnButtonClicked(this);
        }

        if (null != onClickAction)
            onClickAction();
    }

    private void PlayHoverAnimation()
    {
        if (null == scaleTarget)
            return;

        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;

        Sequence _seq = DOTween.Sequence().SetUpdate(true);

        // Scale Tween
        Vector3 _shrinkScale = originalScale * hoverSettings.shrinkScale;
        float _shrinkDuration = hoverSettings.duration * Mathf.Clamp01(hoverSettings.shrinkTimeRatio);
        float _restoreDuration = hoverSettings.duration * Mathf.Clamp01(hoverSettings.restoreTimeRatio);

        Sequence _scaleSeq = DOTween.Sequence();
        _scaleSeq.Append(scaleTarget.DOScale(_shrinkScale, _shrinkDuration).SetEase(Ease.OutQuad));
        _scaleSeq.Append(scaleTarget.DOScale(originalScale, _restoreDuration).SetEase(hoverSettings.scaleEase));
        _seq.Join(_scaleSeq);

        // Rotation Tween
        float _rotDuration = hoverSettings.duration * Mathf.Clamp01(hoverSettings.rotationTimeRatio);
        Sequence _rotSeq = CreateSwingSequence(hoverSettings.startAngle, hoverSettings.angleDamping, hoverSettings.swingCount, _rotDuration, hoverSettings.rotationEase, false);
        _seq.Join(_rotSeq);

        activeTween = _seq;
    }

    private void PlayUnhoverAnimation()
    {
        if (null == scaleTarget)
            return;

        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;

        Sequence _seq = DOTween.Sequence().SetUpdate(true);
        float _rotDuration = unhoverSettings.duration * Mathf.Clamp01(unhoverSettings.rotationTimeRatio);
        Sequence _rotSeq = CreateSwingSequence(unhoverSettings.startAngle, unhoverSettings.angleDamping, unhoverSettings.swingCount, _rotDuration, unhoverSettings.rotationEase, true);

        _seq.Join(_rotSeq);
        
        activeTween = _seq;
    }

    private Sequence CreateSwingSequence(float _startAngle, float _angleDamping, int _swingCount, float _rotDuration, Ease _rotationEase, bool _invertDirection)
    {
        Sequence _rotSeq = DOTween.Sequence();
        float _angle = Mathf.Abs(_startAngle);
        int _validSwingCount = Mathf.Max(_swingCount, 1);
        float _swingDuration = _rotDuration / (_validSwingCount + 1);

        for (int i = 0; i < _validSwingCount; i++)
        {
            float _direction = (0 == i % 2) ? -1f : 1f;
            if (true == _invertDirection) _direction *= -1f;

            Vector3 _targetRot = originalRotation.eulerAngles;
            _targetRot.z += _angle * _direction;

            _rotSeq.Append(scaleTarget.DOLocalRotate(_targetRot, _swingDuration, RotateMode.Fast).SetEase(_rotationEase));
            _angle *= Mathf.Clamp01(_angleDamping);
        }

        _rotSeq.Append(scaleTarget.DOLocalRotate(originalRotation.eulerAngles, _swingDuration, RotateMode.Fast).SetEase(_rotationEase));
        return _rotSeq;
    }

    private void KillTween()
    {
        if (null != activeTween && true == activeTween.IsActive())
        {
            activeTween.Kill();
            activeTween = null;
        }
    }
}
