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
public class UI_WarningPopupButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
    private Graphic targetGraphic;

    [Header("Motion Settings")]
    [SerializeField] private HoverSettings hoverSettings = new HoverSettings();
    [SerializeField] private UnhoverSettings unhoverSettings = new UnhoverSettings();
    
    private Action onClickAction;
    private bool isInteractable = true;
    private bool isHovered = false;

    public bool IsHovered => isHovered;

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
        PlayHoverAnimation();
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (false == isInteractable) return;
        
        KillTween();
        PlayUnhoverAnimation();
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

    private void KillTween()
    {
        if (null != activeTween && true == activeTween.IsActive())
        {
            activeTween.Kill();
            activeTween = null;
        }
    }
}
