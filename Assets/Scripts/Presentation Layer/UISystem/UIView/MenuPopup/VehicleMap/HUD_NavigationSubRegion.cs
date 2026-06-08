using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationSubRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Color Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectColor = Color.green;
    [SerializeField] private Color lockColor = Color.gray;

    [Header("Hover Color Settings")]
    [SerializeField] private Color normalHoverColor = Color.white;
    [SerializeField] private Color lockHoverColor = Color.red;
    [SerializeField] private float hoverColorDuration = 0.2f;
    [SerializeField] private Color selectPingPongColor = Color.yellow;
    [SerializeField] private float selectPingPongDuration = 0.8f;

    [Header("Motion Tags")]
    [SerializeField] private string hoverTag = "Hover";
    [SerializeField] private string hoverOffTag = "HoverOff";
    [SerializeField] private string clickTag = "Click";
    [SerializeField] private string lockClickTag = "LockClick";
    [SerializeField] private string appearTag = "Appear";

    // //내부 의존성
    private RectTransform rect;
    private ForestEnvironmentInfo forestInfo;
    private Action<RectTransform, Vector2> onHoverEnterEvent;
    private Action onHoverExitEvent;
    private Action<int> onSelectEvent;

    private MotionEntry enterMotion;
    private MotionEntry exitMotion;
    private MotionEntry clickMotion;

    private int fieldNumber = 0;
    private bool isSelected = false;
    private bool isLocked = false;
    private bool isInitialized = false;
    private bool isClicked = false;
    private bool isHovered = false;
    private bool isPendingExit = false;
    private float hoverEnterTime = 0f;
    private float pendingExitTime = 0f;
    private PointerEventData pendingExitData;
    private Tweener colorTween;
    private Tween appearDelayTween;
    private Tweener pingPongTween;


    // //퍼블릭 초기화 및 제어 메서드

    public void Setup(ForestEnvironmentInfo _info, int _number, Action<RectTransform, Vector2> _onHoverEnter, Action _onHoverExit, Action<int> _onSelect)
    {
        forestInfo = _info;
        Initialize(_number);

        SetSelect(false);
        SetNumber(_number);
        SetLock(!_info.bCanAccess);

        onHoverEnterEvent = _onHoverEnter;
        onHoverExitEvent = _onHoverExit;
        onSelectEvent = _onSelect;
    }

    public void Initialize(int _number)
    {
        if (true == isInitialized)
            return;

        rect = GetComponent<RectTransform>();

        SetSelect(false);
        SetLock(false);
        isInitialized = true;
    }

    public void SetLock(bool _isLock)
    {
        isLocked = _isLock;
        UpdateColor();
    }

    public void SetSelect(bool _isSelect)
    {
        isSelected = _isSelect;

        if (null == iconImage)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != pingPongTween && pingPongTween.IsActive())
            pingPongTween.Kill();

        if (true == isSelected)
        {
            iconImage.color = selectColor;
            pingPongTween = iconImage.DOColor(selectPingPongColor, selectPingPongDuration)
                                     .SetLoops(-1, LoopType.Yoyo)
                                     .SetEase(Ease.InOutSine);
        }
        else
        {
            colorTween = iconImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
            transform.localScale = Vector3.zero;

            appearDelayTween = DOVirtual.DelayedCall(_delay, () =>
            {
                transform.localScale = Vector3.one;
                motionPlayer.Play(appearTag, bReset: true);
            }).SetEase(Ease.Linear);
        }
    }

    public void ResetAnimation()
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != pingPongTween && pingPongTween.IsActive())
            pingPongTween.Kill();

        transform.localScale = Vector3.one;

        if (null != motionPlayer)
            motionPlayer.ResetAllMotions();

        UpdateColor();
    }

    public void SetNumber(int _number)
    {
        fieldNumber = _number;
    }

    public void PlayOpenAnimation()
    {
        gameObject.SetActive(true);
    }

    public void PlayCloseAnimation()
    {
        gameObject.SetActive(false);
    }

    public ForestType GetForestType()
    {
        return forestInfo.forestType;
    }

    public ForestEnvironmentInfo GetForestInfo()
    {
        return forestInfo;
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public int GetNumber()
    {
        return fieldNumber;
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    public RectTransform GetRectTransform()
    {
        if (null == rect)
            rect = GetComponent<RectTransform>();

        return rect;
    }


    // //내부 로직

    private void UpdateColor()
    {
        if (null == iconImage)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (true == isLocked)
            iconImage.color = lockColor;
        else if (true == isSelected)
            iconImage.color = selectColor;
        else
            iconImage.color = normalColor;
    }

    private Color GetOriginalColor()
    {
        if (true == isLocked)
            return lockColor;
        if (true == isSelected)
            return selectColor;

        return normalColor;
    }

    private Color GetHoverColor()
    {
        return true == isLocked ? lockHoverColor : normalHoverColor;
    }

    private void OnClickAnimationComplete()
    {
        isClicked = false;

        if (true == isSelected)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != iconImage)
        {
            Color _targetColor = true == isHovered ? GetHoverColor() : GetOriginalColor();
            colorTween = iconImage.DOColor(_targetColor, hoverColorDuration).SetEase(Ease.Linear);
        }
    }


    // //Event System 구현부

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (false == isLocked)
        {
            RectTransform _targetRect = GetRectTransform();
            onHoverEnterEvent?.Invoke(_targetRect, _targetRect.rect.size);
        }

        isHovered = true;
        isPendingExit = false;
        hoverEnterTime = Time.unscaledTime;

        if (null != motionPlayer && false == isClicked)
        {
            if (null != exitMotion)
                motionPlayer.SettingEntryMotion(exitMotion, true, true);

            if (null != clickMotion)
                motionPlayer.SettingEntryMotion(clickMotion, true, true);

            UpdateColor();

            enterMotion = motionPlayer.Play(hoverTag, bReset: true);
        }

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (false == isSelected)
        {
            if (null != iconImage)
                colorTween = iconImage.DOColor(GetHoverColor(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (false == isHovered)
            return;

        isPendingExit = true;
        pendingExitTime = Time.unscaledTime + 0.15f;
        pendingExitData = _eventData;
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isLocked && false == isSelected)
            onSelectEvent?.Invoke(fieldNumber);

        isClicked = true;
        isPendingExit = false;

        if (null != motionPlayer)
        {
            if (null != enterMotion)
                motionPlayer.SettingEntryMotion(enterMotion, true, true);

            if (null != exitMotion)
                motionPlayer.SettingEntryMotion(exitMotion, true, true);

            string _targetTag = true == isLocked ? lockClickTag : clickTag;
            clickMotion = motionPlayer.Play(_targetTag, bReset: true, _onComplete: OnClickAnimationComplete);
        }
    }

    private void ExecuteExit()
    {
        if (true == isClicked)
            return;

        if (false == isLocked)
            onHoverExitEvent?.Invoke();

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != motionPlayer)
        {
            if (null != enterMotion)
                motionPlayer.SettingEntryMotion(enterMotion, true, true);

            if (null != clickMotion)
                motionPlayer.SettingEntryMotion(clickMotion, true, true);

            if (false == isSelected)
            {
                if (null != iconImage)
                    iconImage.color = GetHoverColor();
            }

            exitMotion = motionPlayer.Play(hoverOffTag, bReset: true);
        }

        if (false == isSelected)
        {
            if (null != iconImage)
                colorTween = iconImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Update()
    {
        if (true == isPendingExit && Time.unscaledTime >= pendingExitTime)
        {
            isPendingExit = false;

            if (null != pendingExitData)
            {
                if (false == RectTransformUtility.RectangleContainsScreenPoint(GetRectTransform(), pendingExitData.position, pendingExitData.enterEventCamera))
                {
                    isHovered = false;
                    ExecuteExit();
                }
            }
        }
    }
}
