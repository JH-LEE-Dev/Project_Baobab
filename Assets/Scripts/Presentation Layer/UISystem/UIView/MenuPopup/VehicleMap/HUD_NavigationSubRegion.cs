using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationSubRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // 외부 의존성
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private GameObject newIndicatorObj;

    [Header("New Indicator Animation Settings")]
    [SerializeField] private float newIndicatorAnimDuration = 0.3f;
    [SerializeField] private Ease newIndicatorAnimEase = Ease.OutBack;

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
    [SerializeField] private float hoverExitDelay = 0.15f;

    [Header("Motion Tags")]
    [SerializeField] private string hoverTag = "Hover";
    [SerializeField] private string hoverOffTag = "HoverOff";
    [SerializeField] private string clickTag = "Click";
    [SerializeField] private string lockClickTag = "LockClick";
    [SerializeField] private string appearTag = "Appear";
    [SerializeField] private string unlockTag = "UnLock";

    private HUD_NavigationSubField subField;

    // 내부 의존성
    private Action unlockCompleteCallback;
    private UnityEngine.Events.UnityAction onOmpUnlockCompleteCallback;
    private MotionEntry unlockEntry;
    private ParticleSystem unlockVfx;
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
    private Tween exitDelayTween;
    private PointerEventData pendingExitData;
    private UnityEngine.Events.UnityAction onClickAnimationCompleteCallback;
    private Tweener colorTween;
    private Tween appearDelayTween;
    private Tweener pingPongTween;
    private TweenCallback onAppearDelayCompleteCallback;

    // 캐싱된 상수 및 리터럴 값
    private const bool forceReset = true;

    public bool IsInputBlocked
    {
        get
        {
            if (null != subField && true == subField.IsInputBlocked)
            {
                return true;
            }
            return false;
        }
    }


    // 퍼블릭 초기화 및 제어 메서드

    public void Setup(ForestEnvironmentInfo _info, int _number, Action<RectTransform, Vector2> _onHoverEnter, Action _onHoverExit, Action<int> _onSelect, HUD_NavigationSubField _subField)
    {
        forestInfo = _info;
        subField = _subField;
        Initialize(_number);

        SetSelect(false);
        SetNumber(_number);

        string subKey = string.Format("UnLock_SubRegion_{0}", _info.forestType);
        string subNewKey = string.Format("New_SubRegion_{0}", _info.forestType);

        bool isSubLocked = !_info.bCanAccess || (PlayerPrefs.GetInt(subKey, 0) == 0);
        SetLock(isSubLocked);

        SetNewIndicator(PlayerPrefs.GetInt(subNewKey, 0) == 1);

        onHoverEnterEvent = _onHoverEnter;
        onHoverExitEvent = _onHoverExit;
        onSelectEvent = _onSelect;
    }

    public void Initialize(int _number)
    {
        if (true == isInitialized)
        {
            return;
        }

        rect = GetComponent<RectTransform>();
        onClickAnimationCompleteCallback = OnClickAnimationComplete;
        onAppearDelayCompleteCallback = OnAppearDelayComplete;
        onOmpUnlockCompleteCallback = OnOmpUnlockComplete;

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
        {
            return;
        }

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (null != pingPongTween && true == pingPongTween.IsActive())
        {
            pingPongTween.Kill();
        }

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

    public void SetNewIndicator(bool _active)
    {
        if (null != newIndicatorObj)
        {
            if (true == _active)
            {
                if (false == newIndicatorObj.activeSelf)
                {
                    newIndicatorObj.SetActive(true);
                    newIndicatorObj.transform.localScale = Vector3.zero;
                    newIndicatorObj.transform.DOScale(Vector3.one, newIndicatorAnimDuration).SetEase(newIndicatorAnimEase).SetUpdate(true);
                }
            }
            else
            {
                newIndicatorObj.SetActive(false);
            }
        }
    }

    public void PlayUnlockProduction(Action _onComplete)
    {
        unlockCompleteCallback = _onComplete;

        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
            appearDelayTween = null;
        }

        transform.localScale = Vector3.one;

        if (null != motionPlayer)
        {
            if (null != enterMotion)
            {
                motionPlayer.SettingEntryMotion(enterMotion, true, true);
                enterMotion = null;
            }

            if (null != exitMotion)
            {
                motionPlayer.SettingEntryMotion(exitMotion, true, true);
                exitMotion = null;
            }

            if (null != clickMotion)
            {
                motionPlayer.SettingEntryMotion(clickMotion, true, true);
                clickMotion = null;
            }

            unlockEntry = motionPlayer.Play(unlockTag, _onComplete: onOmpUnlockCompleteCallback);
            if (null == unlockEntry)
            {
                OnOmpUnlockComplete();
            }
        }
        else
        {
            OnOmpUnlockComplete();
        }
    }


    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
            transform.localScale = Vector3.zero;

            appearDelayTween = DOVirtual.DelayedCall(_delay, onAppearDelayCompleteCallback).SetEase(Ease.Linear);
        }
    }

    public void ResetAnimation()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

        if (null != pingPongTween && true == pingPongTween.IsActive())
        {
            pingPongTween.Kill();
        }

        transform.localScale = Vector3.one;

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
        }

        UpdateColor();
    }

    public void PlayDisappearAnimation(float _delay, TweenCallback _onComplete)
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

        if (null != pingPongTween && true == pingPongTween.IsActive())
        {
            pingPongTween.Kill();
        }

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
        }

        appearDelayTween = transform.DOScale(Vector3.zero, 0.2f)
                                     .SetDelay(_delay)
                                     .SetEase(Ease.InBack)
                                     .OnComplete(_onComplete);
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
        {
            rect = GetComponent<RectTransform>();
        }

        return rect;
    }


    // 내부 로직

    private void OnOmpUnlockComplete()
    {
        if (null != motionPlayer && null != unlockEntry)
        {
            motionPlayer.SettingEntryMotion(unlockEntry, true, true);
            unlockEntry = null;
        }

        if (null != vfxComponent && null != unlockVfx)
        {
            vfxComponent.Stop(unlockVfx);
            unlockVfx = null;
        }

        if (null != vfxComponent)
        {
            unlockVfx = vfxComponent.Play(unlockTag, transform.position, Quaternion.identity, transform);
        }

        SetLock(false);
        SetNewIndicator(true);
        unlockCompleteCallback?.Invoke();
        unlockCompleteCallback = null;
    }

    private void OnAppearDelayComplete()
    {
        transform.localScale = Vector3.one;

        motionPlayer.SettingEntryMotion(enterMotion, true, true);
        motionPlayer.SettingEntryMotion(exitMotion, true, true);

        motionPlayer.Play(appearTag, bReset: forceReset);
    }

    private void UpdateColor()
    {
        if (null == iconImage)
        {
            return;
        }

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (true == isLocked)
        {
            iconImage.color = lockColor;
        }
        else if (true == isSelected)
        {
            iconImage.color = selectColor;
        }
        else
        {
            iconImage.color = normalColor;
        }
    }

    private Color GetOriginalColor()
    {
        if (true == isLocked)
        {
            return lockColor;
        }
        if (true == isSelected)
        {
            return selectColor;
        }

        return normalColor;
    }

    private Color GetHoverColor()
    {
        if (true == isLocked)
        {
            return lockHoverColor;
        }
        return normalHoverColor;
    }

    private void OnClickAnimationComplete()
    {
        isClicked = false;

        if (true == isSelected)
        {
            return;
        }

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (null != iconImage)
        {
            Color _targetColor = true == isHovered ? GetHoverColor() : GetOriginalColor();
            colorTween = iconImage.DOColor(_targetColor, hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    private void ExecuteExit()
    {
        if (true == isClicked)
        {
            return;
        }

        if (false == IsTransitioning())
        {
            if (false == isLocked)
            {
                onHoverExitEvent?.Invoke();
            }

            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(enterMotion, forceReset, forceReset);
                motionPlayer.SettingEntryMotion(exitMotion, forceReset, forceReset);
                motionPlayer.SettingEntryMotion(clickMotion, forceReset, forceReset);
                motionPlayer.SettingEntryMotion(unlockEntry, forceReset, forceReset);

                if (false == isSelected)
                {
                    if (null != iconImage)
                    {
                        iconImage.color = GetHoverColor();
                    }
                }

                exitMotion = motionPlayer.Play(hoverOffTag, bReset: forceReset);
            }
        }

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (false == isSelected)
        {
            if (null != iconImage)
            {
                colorTween = iconImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
            }
        }
    }


    // Event System 구현부

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        isHovered = true;
        isPendingExit = false;
        hoverEnterTime = Time.unscaledTime;

        if (null != exitDelayTween) exitDelayTween.Kill();

        if (false == IsTransitioning())
        {
            if (false == isLocked)
            {
                RectTransform _targetRect = GetRectTransform();
                onHoverEnterEvent?.Invoke(_targetRect, _targetRect.rect.size);
            }

            if (null != motionPlayer && false == isClicked)
            {
                motionPlayer.SettingEntryMotion(enterMotion, forceReset, forceReset);
                motionPlayer.SettingEntryMotion(exitMotion, forceReset, forceReset);
                motionPlayer.SettingEntryMotion(clickMotion, forceReset, forceReset);
                motionPlayer.SettingEntryMotion(unlockEntry, forceReset, forceReset);

                UpdateColor();

                enterMotion = motionPlayer.Play(hoverTag, bReset: forceReset);
            }
        }

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (false == isSelected)
        {
            if (null != iconImage)
            {
                colorTween = iconImage.DOColor(GetHoverColor(), hoverColorDuration).SetEase(Ease.Linear);
            }
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (false == isHovered)
        {
            return;
        }

        isPendingExit = true;
        pendingExitData = _eventData;

        if (null != exitDelayTween) exitDelayTween.Kill();
        exitDelayTween = DOVirtual.DelayedCall(hoverExitDelay, () => {
            isPendingExit = false;
            if (null != pendingExitData)
            {
                if (false == RectTransformUtility.RectangleContainsScreenPoint(GetRectTransform(), pendingExitData.position, pendingExitData.enterEventCamera))
                {
                    isHovered = false;
                    ExecuteExit();
                }
            }
        }, true);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (true == IsTransitioning())
        {
            return;
        }

        if (true == isSelected || true == isClicked)
        {
            return;
        }

        if (false == isLocked)
        {
            onSelectEvent?.Invoke(fieldNumber);
        }

        isClicked = true;
        isPendingExit = false;

        if (null != motionPlayer)
        {
            motionPlayer.SettingEntryMotion(enterMotion, forceReset, forceReset);
            motionPlayer.SettingEntryMotion(exitMotion, forceReset, forceReset);
            motionPlayer.SettingEntryMotion(clickMotion, forceReset, forceReset);
            motionPlayer.SettingEntryMotion(unlockEntry, forceReset, forceReset);

            string _targetTag = true == isLocked ? lockClickTag : clickTag;
            clickMotion = motionPlayer.Play(_targetTag, bReset: forceReset, _onComplete: onClickAnimationCompleteCallback);
        }
    }

    private bool IsTransitioning()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            return true;
        }

        if (null != motionPlayer && true == motionPlayer.IsPlaying(appearTag))
        {
            return true;
        }

        return false;
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        isHovered = false;
        isClicked = false;
        isPendingExit = false;

        if (null != exitDelayTween) exitDelayTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

        if (null != pingPongTween && true == pingPongTween.IsActive())
        {
            pingPongTween.Kill();
        }

        if (null != vfxComponent && null != unlockVfx)
        {
            vfxComponent.Stop(unlockVfx, true);
            unlockVfx = null;
        }

        if (null != iconImage)
        {
            iconImage.color = normalColor;
        }

        if (null != motionPlayer)
        {
            motionPlayer.ResetAllMotions();
        }
    }

    private void OnDestroy()
    {
        if (null != exitDelayTween) exitDelayTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
        {
            colorTween.Kill();
        }

        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

        if (null != pingPongTween && true == pingPongTween.IsActive())
        {
            pingPongTween.Kill();
        }

        if (null != vfxComponent && null != unlockVfx)
        {
            vfxComponent.Stop(unlockVfx, true);
            unlockVfx = null;
        }
    }
}
