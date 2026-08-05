using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class UI_ExternalLinkButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField, Tooltip("색상 변화 연출을 적용할 시각적 이미지 (레이캐스트 전용 이미지와 분리)")]
    private Image targetVisualImage;
    [Header("Link Settings")]
    [SerializeField, Tooltip("클릭 시 이동할 외부 링크 URL (디스코드, 웹사이트 등)")]
    private string targetUrl = "https://discord.gg/your_invite_link";

    [Header("Color Settings")]
    [SerializeField, Tooltip("기본 상태 색상")]
    private Color normalColor = Color.white;
    [SerializeField, Tooltip("마우스 오버(Hover) 상태 색상")]
    private Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField, Tooltip("클릭(Click) 상태 색상")]
    private Color clickColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("Animation Settings")]
    [SerializeField, Tooltip("색상 전환 연출 시간")]
    private float transitionDuration = 0.15f;

    private Tween colorTween;
    private bool isHovered = false;

    private void Awake()
    {
        if (null == targetVisualImage)
        {
            targetVisualImage = GetComponent<Image>();
        }

        if (null != targetVisualImage)
        {
            targetVisualImage.color = normalColor;
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        PlayColorTween(hoverColor);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        PlayColorTween(normalColor);
    }

    public void SetUrl(string _url)
    {
        targetUrl = _url;
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == string.IsNullOrEmpty(targetUrl))
        {
            Application.OpenURL(targetUrl);
        }

        // 클릭 효과 연출: 순간적으로 ClickColor를 적용한 후 다시 원래 타겟 컬러로 DOTween 복귀
        if (null != targetVisualImage)
        {
            if (null != colorTween && true == colorTween.IsActive())
            {
                colorTween.Kill();
            }
            
            targetVisualImage.color = clickColor;
            
            Color _targetColor = true == isHovered ? hoverColor : normalColor;
            colorTween = targetVisualImage.DOColor(_targetColor, transitionDuration).SetEase(Ease.OutQuad);
        }
    }

    private void PlayColorTween(Color _targetColor)
    {
        if (null == targetVisualImage) return;

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        colorTween = targetVisualImage.DOColor(_targetColor, transitionDuration).SetEase(Ease.OutQuad);
    }

    private void OnDestroy()
    {
        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
            colorTween = null;
        }
    }
}
