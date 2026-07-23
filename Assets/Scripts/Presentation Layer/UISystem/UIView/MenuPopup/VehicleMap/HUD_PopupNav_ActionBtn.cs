using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class HUD_PopupNav_ActionBtn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Bindings")]
    [Tooltip("마우스 클릭(Raycast)을 판정할 영역 (투명 이미지나 기본 버튼 배경)")]
    [SerializeField] private Graphic raycastTarget;
    
    [Tooltip("실제로 모션(스케일 뽀잉 등)이 재생될 트랜스폼. 비워두면 자기 자신(transform)을 사용합니다.")]
    [SerializeField] private RectTransform motionTargetRect;

    [Tooltip("호버 시 색상이 바뀔 대상 배경 이미지 (선택 사항)")]
    [SerializeField] private Image bgImage;

    [Header("Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    
    [SerializeField] private float hoverScaleMultiplier = 1.05f;
    [SerializeField] private float clickScaleMultiplier = 0.95f;
    [SerializeField] private float animDuration = 0.15f;
    [SerializeField] private float visibilityAnimDuration = 0.3f;

    private Action onClickAction;
    private Tween scaleTween;
    private Tween colorTween;
    
    private bool isHovered = false;
    private bool isPressed = false;

    private RectTransform TargetRect => motionTargetRect != null ? motionTargetRect : (RectTransform)transform;

    private void Awake()
    {
        if (null != raycastTarget)
        {
            raycastTarget.raycastTarget = true;
        }
        
        if (null != bgImage)
        {
            bgImage.color = normalColor;
        }
    }

    private void OnDisable()
    {
        isHovered = false;
        isPressed = false;
        
        if (null != scaleTween && scaleTween.IsActive()) scaleTween.Kill();
        if (null != colorTween && colorTween.IsActive()) colorTween.Kill();
        
        TargetRect.localScale = Vector3.one;
        if (null != bgImage) bgImage.color = normalColor;
    }

    public void Initialize(Action _onClick)
    {
        onClickAction = _onClick;
    }

    /// <summary>
    /// 메인 팝업에서 버튼을 띄우거나 숨길 때 호출
    /// </summary>
    public void SetVisibility(bool _isVisible, bool _playAnim)
    {
        gameObject.SetActive(true);

        if (null != scaleTween && scaleTween.IsActive()) scaleTween.Kill();

        if (false == _playAnim)
        {
            TargetRect.localScale = _isVisible ? Vector3.one : Vector3.zero;
            if (false == _isVisible)
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (true == _isVisible)
        {
            TargetRect.localScale = Vector3.zero;
            scaleTween = TargetRect.DOScale(1f, visibilityAnimDuration).SetEase(Ease.OutBack);
        }
        else
        {
            scaleTween = TargetRect.DOScale(0f, visibilityAnimDuration).SetEase(Ease.InBack).OnComplete(() => {
                gameObject.SetActive(false);
            });
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (true == isPressed) return;

        PlayHoverMotion();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (true == isPressed) return;

        PlayNormalMotion();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        PlayPressMotion();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (true == isHovered)
        {
            PlayHoverMotion();
        }
        else
        {
            PlayNormalMotion();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭음 재생 등 공통 효과가 있다면 여기에 추가 가능
        onClickAction?.Invoke();
    }

    private void PlayHoverMotion()
    {
        if (null != scaleTween && scaleTween.IsActive()) scaleTween.Kill();
        scaleTween = TargetRect.DOScale(hoverScaleMultiplier, animDuration).SetEase(Ease.OutQuad);

        if (null != bgImage)
        {
            if (null != colorTween && colorTween.IsActive()) colorTween.Kill();
            colorTween = bgImage.DOColor(hoverColor, animDuration);
        }
    }

    private void PlayPressMotion()
    {
        if (null != scaleTween && scaleTween.IsActive()) scaleTween.Kill();
        scaleTween = TargetRect.DOScale(clickScaleMultiplier, animDuration * 0.5f).SetEase(Ease.OutQuad);
    }

    private void PlayNormalMotion()
    {
        if (null != scaleTween && scaleTween.IsActive()) scaleTween.Kill();
        scaleTween = TargetRect.DOScale(1f, animDuration).SetEase(Ease.OutQuad);

        if (null != bgImage)
        {
            if (null != colorTween && colorTween.IsActive()) colorTween.Kill();
            colorTween = bgImage.DOColor(normalColor, animDuration);
        }
    }
}
