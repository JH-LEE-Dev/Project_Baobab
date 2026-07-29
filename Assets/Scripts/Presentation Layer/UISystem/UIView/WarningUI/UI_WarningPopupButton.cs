using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 경고/확인 팝업 전용 커스텀 버튼 클래스입니다.
/// 유니티 기본 Button 컴포넌트를 사용하지 않으며, Raycast 타겟과 시각적 타겟을 분리하여 관리합니다.
/// TreeProp의 LockObj와 동일한 펀치/흔들림 연출을 사용합니다.
/// </summary>
public class UI_WarningPopupButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Component")]
    [SerializeField, Tooltip("크기와 회전이 변형될 대상 (Raycast 본체와 다를 경우 지정)")] 
    private Graphic targetGraphic;

    [Header("Hover Animation")]
    [SerializeField] private float hoverPunchRotation = 20f;
    [SerializeField] private float hoverPunchDuration = 0.6f;
    [SerializeField] private int hoverPunchVibrato = 6;
    [SerializeField] private float hoverPunchElasticity = 1f;

    [Header("Click Animation")]
    [SerializeField] private float clickPunchStrength = 0.2f;
    [SerializeField] private float clickPunchRotation = 15f;
    [SerializeField] private float clickPunchDuration = 0.15f;
    [SerializeField] private int clickPunchVibrato = 4;
    [SerializeField] private float clickPunchElasticity = 0.8f;
    
    private Action onClickAction;
    private bool isInteractable = true;
    
    private bool isHovered = false;
    private bool isPointerDown = false;

    // 초기 상태 캐싱
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Transform scaleTarget;

    private Tween punchTween;

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
        
        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;
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
            
            scaleTarget.localScale = originalScale;
            scaleTarget.localRotation = originalRotation;
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
        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;
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
            scaleTarget.localScale = originalScale;
            scaleTarget.localRotation = originalRotation;
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable) return;
        if (null != onClickAction) onClickAction();
    }

    private void PlayHoverAnimation()
    {
        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;
        
        float _randomSign = 0.5f < UnityEngine.Random.value ? 1f : -1f;
        Vector3 _rotPunch = new Vector3(0f, 0f, hoverPunchRotation * _randomSign);
        
        punchTween = scaleTarget.DOPunchRotation(_rotPunch, hoverPunchDuration, hoverPunchVibrato, hoverPunchElasticity).SetUpdate(true);
    }

    private void PlayClickAnimation()
    {
        scaleTarget.localScale = originalScale;
        scaleTarget.localRotation = originalRotation;
        
        float _randomSign = 0.5f < UnityEngine.Random.value ? 1f : -1f;
        Vector3 _rotPunch = new Vector3(0f, 0f, clickPunchRotation * _randomSign);
        Vector3 _scalePunch = new Vector3(clickPunchStrength, clickPunchStrength, 0f);

        Sequence _seq = DOTween.Sequence().SetUpdate(true);
        _seq.Join(scaleTarget.DOPunchScale(_scalePunch, clickPunchDuration, clickPunchVibrato, clickPunchElasticity));
        _seq.Join(scaleTarget.DOPunchRotation(_rotPunch, clickPunchDuration, clickPunchVibrato, clickPunchElasticity));
        
        punchTween = _seq;
    }

    private void KillTween()
    {
        if (null != punchTween && true == punchTween.IsActive())
        {
            punchTween.Kill();
            punchTween = null;
        }
    }
}
