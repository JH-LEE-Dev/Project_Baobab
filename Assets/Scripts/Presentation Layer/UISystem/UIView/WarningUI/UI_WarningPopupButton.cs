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
public class UI_WarningPopupButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
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

    [System.Serializable]
    public class ClickSettings
    {
        [Tooltip("전체 Click 연출 시간")]
        public float duration = 0.45f;

        [Header("Scale Settings")]
        public Vector2 squashScale = new Vector2(1.4f, 0.7f);
        public Vector2 recoilScale = new Vector2(0.8f, 1.3f);
        [Range(1, 5)] public int bounceCount = 2;
        [Range(0f, 1f)] public float bounceDamping = 0.25f;
        [Range(0f, 1f)] public float squashTimeRatio = 0.15f;
        [Range(0f, 1f)] public float recoilTimeRatio = 0.2f;
        [Range(0f, 1f)] public float restoreTimeRatio = 0.4f;
        public Ease squashEase = Ease.OutQuad;
        public Ease restoreEase = Ease.OutBack;
    }

    [Header("UI Component")]
    [SerializeField, Tooltip("크기와 회전이 변형될 대상 (Raycast 본체와 다를 경우 지정)")] 
    private Graphic targetGraphic;

    [Header("Motion Settings")]
    [SerializeField] private HoverSettings hoverSettings = new HoverSettings();
    [SerializeField] private UnhoverSettings unhoverSettings = new UnhoverSettings();
    [SerializeField] private ClickSettings clickSettings = new ClickSettings();
    
    private Action onClickAction;
    private bool isInteractable = true;
    
    private bool isHovered = false;
    private bool isPointerDown = false;

    // 초기 상태 캐싱
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Transform scaleTarget;

    private Tween activeTween;

    private void Awake()
    {
        scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        originalScale = scaleTarget.localScale;
        originalRotation = scaleTarget.localRotation;
    }

    private void OnDisable()
    {
        isHovered = false;
        isPointerDown = false;
        
        KillTween();
        
        if (null != scaleTarget)
        {
            scaleTarget.localScale = originalScale;
            scaleTarget.localRotation = originalRotation;
        }
    }

    private void OnDestroy()
    {
        KillTween();
        onClickAction = null;
    }

    public void Initialize(Action _onClick)
    {
        onClickAction = _onClick;
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;
        
        if (false == isInteractable)
        {
            KillTween();
            isHovered = false;
            isPointerDown = false;
            
            if (null != scaleTarget)
            {
                scaleTarget.localScale = originalScale;
                scaleTarget.localRotation = originalRotation;
            }
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        if (false == isInteractable) return;
        
        KillTween();
        if (true == isPointerDown)
        {
            PlayClickAnimation();
        }
        else
        {
            PlayHoverAnimation();
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (false == isInteractable) return;
        
        KillTween();
        if (false == isPointerDown)
        {
            PlayUnhoverAnimation();
        }
        else
        {
            if (null != scaleTarget)
            {
                scaleTarget.localScale = originalScale;
                scaleTarget.localRotation = originalRotation;
            }
        }
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        isPointerDown = true;
        if (false == isInteractable) return;
        
        KillTween();
        PlayClickAnimation();
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        isPointerDown = false;
        if (false == isInteractable) return;
        
        KillTween();
        if (true == isHovered)
        {
            PlayHoverAnimation();
        }
        else
        {
            PlayUnhoverAnimation();
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable) return;
        if (null != onClickAction) onClickAction();
    }

    private void PlayHoverAnimation()
    {
        if (null == scaleTarget) return;

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
        Sequence _rotSeq = DOTween.Sequence();
        float _angle = Mathf.Abs(hoverSettings.startAngle);
        int _swingCount = Mathf.Max(hoverSettings.swingCount, 1);
        float _rotDuration = hoverSettings.duration * Mathf.Clamp01(hoverSettings.rotationTimeRatio);
        float _swingDuration = _rotDuration / (_swingCount + 1);

        for (int i = 0; i < _swingCount; i++)
        {
            float _direction = (0 == i % 2) ? -1f : 1f;
            Vector3 _targetRot = originalRotation.eulerAngles;
            _targetRot.z += _angle * _direction;

            _rotSeq.Append(scaleTarget.DOLocalRotate(_targetRot, _swingDuration, RotateMode.Fast).SetEase(hoverSettings.rotationEase));
            _angle *= Mathf.Clamp01(hoverSettings.angleDamping);
        }

        _rotSeq.Append(scaleTarget.DOLocalRotate(originalRotation.eulerAngles, _swingDuration, RotateMode.Fast).SetEase(hoverSettings.rotationEase));
        _seq.Join(_rotSeq);

        activeTween = _seq;
    }

    private void PlayUnhoverAnimation()
    {
        if (null == scaleTarget) return;

        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;

        Sequence _seq = DOTween.Sequence().SetUpdate(true);
        float _angle = Mathf.Abs(unhoverSettings.startAngle);
        int _swingCount = Mathf.Max(unhoverSettings.swingCount, 1);
        float _rotDuration = unhoverSettings.duration * Mathf.Clamp01(unhoverSettings.rotationTimeRatio);
        float _swingDuration = _rotDuration / (_swingCount + 1);

        for (int i = 0; i < _swingCount; i++)
        {
            float _direction = (0 == i % 2) ? 1f : -1f;
            Vector3 _targetRot = originalRotation.eulerAngles;
            _targetRot.z += _angle * _direction;

            _seq.Append(scaleTarget.DOLocalRotate(_targetRot, _swingDuration, RotateMode.Fast).SetEase(unhoverSettings.rotationEase));
            _angle *= Mathf.Clamp01(unhoverSettings.angleDamping);
        }

        _seq.Append(scaleTarget.DOLocalRotate(originalRotation.eulerAngles, _swingDuration, RotateMode.Fast).SetEase(unhoverSettings.rotationEase));

        activeTween = _seq;
    }

    private void PlayClickAnimation()
    {
        if (null == scaleTarget) return;

        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;

        Vector3 _squashScale = new Vector3(
            originalScale.x * clickSettings.squashScale.x,
            originalScale.y * clickSettings.squashScale.y,
            originalScale.z);

        Vector3 _recoilScale = new Vector3(
            originalScale.x * clickSettings.recoilScale.x,
            originalScale.y * clickSettings.recoilScale.y,
            originalScale.z);

        int _bounceCount = Mathf.Max(clickSettings.bounceCount, 1);
        float _cycleRatio = clickSettings.squashTimeRatio + clickSettings.recoilTimeRatio;
        float _totalRatio = Mathf.Max((_cycleRatio * _bounceCount) + clickSettings.restoreTimeRatio, 0.0001f);
        float _squashDuration = clickSettings.duration * Mathf.Clamp01(clickSettings.squashTimeRatio / _totalRatio);
        float _recoilDuration = clickSettings.duration * Mathf.Clamp01(clickSettings.recoilTimeRatio / _totalRatio);
        float _restoreDuration = clickSettings.duration * Mathf.Clamp01(clickSettings.restoreTimeRatio / _totalRatio);

        Sequence _seq = DOTween.Sequence().SetUpdate(true);
        float _intensity = 1f;

        for (int i = 0; i < _bounceCount; i++)
        {
            Vector3 _dampedSquash = Vector3.Lerp(originalScale, _squashScale, _intensity);
            Vector3 _dampedRecoil = Vector3.Lerp(originalScale, _recoilScale, _intensity);

            _seq.Append(scaleTarget.DOScale(_dampedSquash, _squashDuration).SetEase(clickSettings.squashEase));
            _seq.Append(scaleTarget.DOScale(_dampedRecoil, _recoilDuration).SetEase(Ease.OutQuad));

            _intensity *= Mathf.Clamp01(clickSettings.bounceDamping);
        }

        _seq.Append(scaleTarget.DOScale(originalScale, _restoreDuration).SetEase(clickSettings.restoreEase));

        activeTween = _seq;
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
