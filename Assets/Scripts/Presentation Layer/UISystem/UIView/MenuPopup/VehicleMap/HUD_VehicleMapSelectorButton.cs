using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

/// <summary>
/// 차량 네비게이션 UI에서 최종 결정을 내리는 확인 및 취소 버튼 클래스입니다.
/// </summary>
public class HUD_VehicleMapSelectorButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    // 외부 의존성
    [Header("Animation")]
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private Image buttonImage;

    [Header("Button Config")]
    [SerializeField] private bool isOkButton;

    [Header("Motion Keys")]
    [SerializeField] private string hoverMotionKey = "Hover";
    [SerializeField] private string hoverOffMotionKey = "HoverOff";
    [SerializeField] private string clickMotionKey = "Click";
    [SerializeField] private string activeMotionKey = "Active";
    [SerializeField] private string appearMotionKey = "Appear";

    [Header("Color Config")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.green;
    [SerializeField] private Color clickColor = Color.yellow;
    [SerializeField] private float colorDuration = 0.2f;

    // 내부 의존성
    private Action onConfirmEvent;
    private Action<RectTransform, Vector2> onHoverEnterEvent;
    private Action onHoverExitEvent;
    private RectTransform rect;
    private MotionEntry enterAnim;
    private MotionEntry exitAnim;
    private MotionEntry clickedAnim;
    private MotionEntry appearAnim;
    private Tween appearDelayTween;
    private Tweener colorTween;
    private TweenCallback onAppearDelayCompleteCallback;

    private float currentAlpha = defaultAlpha;
    private bool isClicked = false;
    private bool isInitialized = false;
    private bool isButtonActive = true;
    private bool isHovered = false;

    // 캐싱된 상수 및 리터럴 값
    private const float defaultAlpha = 1.0f;
    private const bool forceReset = true;


    // 퍼블릭 초기화 및 제어 메서드

    /// <summary>
    /// 버튼을 초기화하고 콜백을 등록합니다.
    /// </summary>
    public void Initialize(Action _onConfirm, Action<RectTransform, Vector2> _onHoverEnter = null, Action _onHoverExit = null)
    {
        if (true == isInitialized)
            return;

        onConfirmEvent = _onConfirm;
        onHoverEnterEvent = _onHoverEnter;
        onHoverExitEvent = _onHoverExit;
        rect = GetComponent<RectTransform>();
        
        isInitialized = true;
    }

    /// <summary>
    /// OK 버튼의 활성화 상태를 제어하며, 필요 시 애니메이션을 재생합니다.
    /// </summary>
    public void SetButtonActive(bool _active, bool _withAnimation = true)
    {
        if (false == isOkButton)
            return;

        if (_active == isButtonActive)
            return;

        isButtonActive = _active;
        gameObject.SetActive(_active);

        if (null != buttonImage)
            buttonImage.raycastTarget = _active;

        if (true == _active && null != motionPlayer)
        {
            if (true == _withAnimation)
                motionPlayer.Play(activeMotionKey, bReset: forceReset);
            else
                motionPlayer.PlayBackward(activeMotionKey, bReset: forceReset);
        }
    }

    public void SetAlpha(float _alpha)
    {
        currentAlpha = _alpha;

        if (null == buttonImage)
            return;

        Color _color = buttonImage.color;
        _color.a = currentAlpha;
        buttonImage.color = _color;
    }

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
            transform.localScale = Vector3.zero;

            if (null == onAppearDelayCompleteCallback)
                onAppearDelayCompleteCallback = OnAppearDelayComplete;

            appearDelayTween = DOVirtual.DelayedCall(_delay, onAppearDelayCompleteCallback).SetEase(Ease.Linear);
        }
    }

    public void ResetAnimation()
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;

        transform.localScale = Vector3.zero;

        isHovered = false;
        isClicked = false;

        if (null != motionPlayer)
        {
            if (null != appearAnim)
            {
                motionPlayer.SettingEntryMotion(appearAnim, forceReset, forceReset);
                appearAnim = null;
            }
            motionPlayer.ResetAllMotions();
        }
    }


    // Event System 구현부

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;

        if (true == isOkButton && false == isButtonActive)
            return;

        onHoverEnterEvent?.Invoke(rect, rect.rect.size);
        PlayColorTween(hoverColor);

        if (null == motionPlayer || true == isClicked)
            return;

        if (null != appearAnim)
        {
            motionPlayer.SettingEntryMotion(appearAnim, forceReset, forceReset);
            appearAnim = null;
        }

        motionPlayer.SettingEntryMotion(clickedAnim, forceReset, forceReset);
        motionPlayer.SettingEntryMotion(exitAnim, forceReset, forceReset);

        enterAnim = motionPlayer.Play(hoverMotionKey, bReset: forceReset);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;

        if (true == isOkButton && false == isButtonActive)
            return;

        onHoverExitEvent?.Invoke();
        PlayColorTween(normalColor);

        if (null == motionPlayer || true == isClicked)
            return;

        if (null != appearAnim)
        {
            motionPlayer.SettingEntryMotion(appearAnim, forceReset, forceReset);
            appearAnim = null;
        }

        motionPlayer.SettingEntryMotion(enterAnim, forceReset, forceReset);
        motionPlayer.SettingEntryMotion(clickedAnim, forceReset, forceReset);

        exitAnim = motionPlayer.Play(hoverOffMotionKey, bReset: forceReset);
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (null == motionPlayer || (true == isOkButton && false == isButtonActive))
            return;

        if (null != appearAnim)
        {
            motionPlayer.SettingEntryMotion(appearAnim, forceReset, forceReset);
            appearAnim = null;
        }

        motionPlayer.SettingEntryMotion(enterAnim, forceReset, forceReset);
        motionPlayer.SettingEntryMotion(exitAnim, forceReset, forceReset);
        isClicked = true;

        clickedAnim = motionPlayer.Play(clickMotionKey, bReset: forceReset);
        PlayColorTween(clickColor);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        if (null == motionPlayer || (true == isOkButton && false == isButtonActive))
            return;

        isClicked = false;
        Color _targetColor = true == isHovered ? hoverColor : normalColor;
        PlayColorTween(_targetColor);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == motionPlayer || (true == isOkButton && false == isButtonActive))
            return;

        onConfirmEvent?.Invoke();
    }

    private void PlayColorTween(Color _targetColor)
    {
        if (null == buttonImage)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        colorTween = buttonImage.DOColor(_targetColor, colorDuration).SetEase(Ease.Linear);
    }

    private void OnAppearDelayComplete()
    {
        transform.localScale = Vector3.one;

        if (null != motionPlayer)
        {
            appearAnim = motionPlayer.Play(appearMotionKey, bReset: forceReset);
        }
    }

    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        isClicked = false;
        isHovered = false;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
            buttonImage.color = normalColor;

        if (null != motionPlayer)
            motionPlayer.ResetAllMotions();
    }
}
