using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PresentationLayer.DOTweenAnimationSystem;

/// <summary>
/// 차량 네비게이션 UI에서 최종 결정을 내리는 확인 및 취소 버튼 클래스입니다.
/// </summary>
public class HUD_VehicleMapSelectorButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
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
    [SerializeField] private string inactiveMotionKey = "Inactive";

    // //내부 의존성
    private Action onConfirmEvent;
    private Action<RectTransform, Vector2> onHoverEnterEvent;
    private Action onHoverExitEvent;
    private RectTransform rect;
    private MotionEntry enterAnim;
    private MotionEntry exitAnim;
    private MotionEntry clickedAnim;

    private float currentAlpha = 1.0f;
    private bool isClicked = false;
    private bool isInitialized = false;
    private bool isButtonActive = true;


    // //퍼블릭 초기화 및 제어 메서드

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

        isButtonActive = _active;
        gameObject.SetActive(_active);

        if (null != buttonImage)
            buttonImage.raycastTarget = _active;

        if (true == _active && null != motionPlayer)
        {
            if (true == _withAnimation)
                motionPlayer.Play(activeMotionKey, bReset: true);
            else
                motionPlayer.Play(activeMotionKey, bReset: true);
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


    // //Event System 구현부

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (null == motionPlayer || isClicked || (isOkButton && false == isButtonActive))
            return;

        onHoverEnterEvent?.Invoke(rect, rect.rect.size);

        motionPlayer.SettingEntryMotion(clickedAnim, true, true);
        motionPlayer.SettingEntryMotion(exitAnim, true, true);

        enterAnim = motionPlayer.Play(hoverMotionKey, bReset: true);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null == motionPlayer || isClicked || (isOkButton && false == isButtonActive))
            return;

        onHoverExitEvent?.Invoke();

        motionPlayer.SettingEntryMotion(enterAnim, true, true);
        motionPlayer.SettingEntryMotion(clickedAnim, true, true);

        exitAnim = motionPlayer.Play(hoverOffMotionKey, bReset: true);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == motionPlayer || (isOkButton && false == isButtonActive))
            return;

        motionPlayer.SettingEntryMotion(enterAnim, true, true);
        motionPlayer.SettingEntryMotion(exitAnim, true, true);
        isClicked = true;

        clickedAnim = motionPlayer.Play(clickMotionKey, bReset: true, _onComplete: UnClicked);

        onConfirmEvent?.Invoke();
    }

    private void UnClicked() => isClicked = false;
}
