using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 옵션 창 전용 버튼 클래스입니다. (닫기 버튼, 좌우 화살표 등)
/// 람다를 배제하고 GC 할당이 없는 커스텀 클릭 및 마우스 호버 모션을 지원합니다.
/// </summary>
public class UI_OptionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Component")]
    [SerializeField, Tooltip("크기와 색상이 변형될 대상 이미지 (Raycast 본체와 다를 경우 지정)")] 
    private Graphic targetGraphic;

    [Header("Motion Settings")]
    [SerializeField] private bool enableMotion = true;
    [SerializeField] private Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);
    [SerializeField] private Vector3 clickScale = new Vector3(0.9f, 0.9f, 1f);
    [SerializeField] private float tweenDuration = 0.1f;
    
    [Header("Color Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color clickColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    
    private Action onClickAction;
    private bool isInteractable = true;
    
    private bool isHovered = false;
    private bool isPointerDown = false;

    // 초기 상태 캐싱
    private Vector3 originalScale;

    private void Awake()
    {
        Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        originalScale = _scaleTarget.localScale;
    }

    public void Initialize(Action _onClick)
    {
        onClickAction = _onClick;
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;
        if (null != targetGraphic)
        {
            Color _c = targetGraphic.color;
            _c.a = true == isInteractable ? 1f : 0.5f;
            targetGraphic.color = _c;
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        if (false == isInteractable || false == enableMotion) return;
        
        if (false == isPointerDown)
        {
            KillTween();
            Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
            _scaleTarget.DOScale(hoverScale, tweenDuration).SetUpdate(true);
            if (null != targetGraphic) targetGraphic.DOColor(hoverColor, tweenDuration).SetUpdate(true);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (false == isInteractable || false == enableMotion) return;
        
        if (false == isPointerDown)
        {
            KillTween();
            Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
            _scaleTarget.DOScale(originalScale, tweenDuration).SetUpdate(true);
            if (null != targetGraphic) targetGraphic.DOColor(normalColor, tweenDuration).SetUpdate(true);
        }
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        isPointerDown = true;
        if (false == isInteractable || false == enableMotion) return;
        
        KillTween();
        Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        _scaleTarget.DOScale(clickScale, tweenDuration).SetUpdate(true);
        if (null != targetGraphic) targetGraphic.DOColor(clickColor, tweenDuration).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        isPointerDown = false;
        if (false == isInteractable || false == enableMotion) return;
        
        KillTween();
        Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        
        if (true == isHovered)
        {
            _scaleTarget.DOScale(hoverScale, tweenDuration).SetUpdate(true);
            if (null != targetGraphic) targetGraphic.DOColor(hoverColor, tweenDuration).SetUpdate(true);
        }
        else
        {
            _scaleTarget.DOScale(originalScale, tweenDuration).SetUpdate(true);
            if (null != targetGraphic) targetGraphic.DOColor(normalColor, tweenDuration).SetUpdate(true);
        }
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable) return;

        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }
    }

    private void KillTween()
    {
        Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        _scaleTarget.DOKill();
        if (null != targetGraphic) targetGraphic.DOKill();
    }

    private void OnDisable()
    {
        isHovered = false;
        isPointerDown = false;

        KillTween();
        
        Transform _scaleTarget = null != targetGraphic ? targetGraphic.transform : transform;
        _scaleTarget.localScale = originalScale;
        
        if (null != targetGraphic)
        {
            Color _c = normalColor;
            _c.a = true == isInteractable ? 1f : 0.5f;
            targetGraphic.color = _c;
        }
    }

    private void OnDestroy()
    {
        KillTween();
        onClickAction = null;
    }
}
