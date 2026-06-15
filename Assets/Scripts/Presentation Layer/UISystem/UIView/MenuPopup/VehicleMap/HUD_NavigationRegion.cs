using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // 외부 의존성
    [Header("UI References")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject lockObject;
    [SerializeField] private TextMeshProUGUI regionNameText;
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private GameObject newIndicatorObj;
    [Header("New Indicator Animation Settings")]
    [SerializeField] private float newIndicatorAnimDuration = 0.3f;
    [SerializeField] private Ease newIndicatorAnimEase = Ease.OutBack;

    private HUD_VehicleNavigation navigation;
    private Action unlockCompleteCallback;
    private UnityEngine.Events.UnityAction onOmpUnlockCompleteCallback;
    private string regionKey = string.Empty;
    private string regionNewKey = string.Empty;
    private MotionEntry unlockEntry;

    public bool IsInputBlocked => navigation != null && navigation.IsInputBlocked;

    [Header("Color Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectColor = Color.green;
    [SerializeField] private Color lockColor = Color.gray;

    [Header("Hover Color Settings")]
    [SerializeField] private Color normalHoverColor = Color.white;
    [SerializeField] private Color lockHoverColor = Color.red;
    [SerializeField] private float hoverColorDuration = 0.2f;

    [Header("Motion Tags")]
    [SerializeField] private string hoverTag = "Hover";
    [SerializeField] private string clickTag = "Click";
    [SerializeField] private string unHoverTag = "unHover";
    [SerializeField] private string lockClickTag = "LockClick";
    [SerializeField] private string appearTag = "Appear";
    [SerializeField] private string unlockTag = "UnLock";

    [Header("Disappear Config")]
    [SerializeField] private float disappearDuration = 0.25f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    // 내부 의존성
    private MapType mapType = MapType.None;
    private Action<MapType> onSelectEvent;
    private LocalizationManager localizationManager;

    private UnityEngine.Events.UnityAction onClickAnimationCompleteCallback;
    private MotionEntry hoverEntry;
    private MotionEntry clickEntry;
    private MotionEntry unHoverEntry;
    private MotionEntry appearEntry;
    private bool isHovered = false;
    private bool isClicked = false;
    private bool isLocked = false;
    private bool isSelected = false;
    private Tweener colorTween;
    private Tween appearDelayTween;
    private Tweener scaleTween;
    private TweenCallback onAppearDelayCompleteCallback;

    // 캐싱된 상수 및 리터럴 값
    private const bool forceReset = true;
    private const string townString = "Town";
    private const string plainsString = "Vegetatedplains";
    private const string forestString = "Deepmossforest";
    private const string noneString = "None";


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(MapType _mapType, Action<MapType> _onSelect, LocalizationManager _localizeManager, HUD_VehicleNavigation _navigation)
    {
        mapType = _mapType;
        onSelectEvent = _onSelect;
        localizationManager = _localizeManager;
        navigation = _navigation;
        isClicked = false;
        isLocked = false;
        isSelected = false;
        isHovered = false;
        onClickAnimationCompleteCallback = OnClickAnimationComplete;
        onAppearDelayCompleteCallback = OnAppearDelayComplete;
        onOmpUnlockCompleteCallback = OnOmpUnlockComplete;

        regionKey = string.Format("UnLock_Region_{0}", _mapType);
        regionNewKey = string.Format("New_Region_{0}", _mapType);

        if (null != omp)
            omp.Initialize();

        SetSelect(false);
        SetLock(false);

        UpdateRegionName();
    }

    public void SetLock(bool _isLock)
    {
        isLocked = _isLock;

        if (null != lockObject)
            lockObject.SetActive(isLocked);

        if (null != regionNameText)
            regionNameText.gameObject.SetActive(!isLocked);

        UpdateColor();
    }

    public void ClearEntry()
    {
        if (null != omp)
        {
            omp.SettingEntryMotion(hoverEntry, forceReset, forceReset);
            omp.SettingEntryMotion(clickEntry, forceReset, forceReset);
            omp.SettingEntryMotion(unHoverEntry, forceReset, forceReset);
            omp.SettingEntryMotion(appearEntry, forceReset, forceReset);
        }

        hoverEntry = null;
        clickEntry = null;
        unHoverEntry = null;
        appearEntry = null;
    }

    public void SetSelect(bool _isSelect)
    {
        isSelected = _isSelect;

        if (null == buttonImage)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        colorTween = buttonImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
    }

    public void SetNewIndicator(bool _active)
    {
        if (null == newIndicatorObj)
            Debug.LogError(string.Format("[HUD_NavigationRegion] newIndicatorObj is NULL for Region {0}! Please bind it in Inspector.", mapType));

        if (null != newIndicatorObj)
        {
            if (_active)
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
        Debug.Log(string.Format("[HUD_NavigationRegion] PlayUnlockProduction started for MapType: {0}", mapType));

        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
            appearDelayTween = null;
        }

        transform.localScale = Vector3.one;

        if (null != omp)
        {
            if (null != hoverEntry)
            {
                omp.SettingEntryMotion(hoverEntry, true, true);
                hoverEntry = null;
            }

            if (null != clickEntry)
            {
                omp.SettingEntryMotion(clickEntry, true, true);
                clickEntry = null;
            }

            if (null != unHoverEntry)
            {
                omp.SettingEntryMotion(unHoverEntry, true, true);
                unHoverEntry = null;
            }

            if (null != appearEntry)
            {
                omp.SettingEntryMotion(appearEntry, true, true);
                appearEntry = null;
            }

            unlockEntry = omp.Play(unlockTag, _onComplete: onOmpUnlockCompleteCallback);
            if (null == unlockEntry)
            {
                Debug.LogWarning(string.Format("[HUD_NavigationRegion] OMP '{0}' motion entry is missing! Skipping to complete.", unlockTag));
                OnOmpUnlockComplete();
            }
            else
            {
                Debug.Log(string.Format("[HUD_NavigationRegion] OMP '{0}' motion started playing.", unlockTag));
            }
        }
        else
        {
            Debug.LogWarning("[HUD_NavigationRegion] OMP is null! Skipping to complete.");
            OnOmpUnlockComplete();
        }

        if (null != vfxComponent)
        {
            ParticleSystem pfx = vfxComponent.Play(unlockTag, transform.position, Quaternion.identity, transform);
            if (pfx != null)
                Debug.Log(string.Format("[HUD_NavigationRegion] VFX '{0}' started playing.", unlockTag));
            else
                Debug.LogWarning(string.Format("[HUD_NavigationRegion] VFX '{0}' tag not found in VFXComponent!", unlockTag));
        }
    }

    private void OnOmpUnlockComplete()
    {
        Debug.Log(string.Format("[HUD_NavigationRegion] OnOmpUnlockComplete for MapType: {0}", mapType));

        if (null != omp && null != unlockEntry)
        {
            omp.SettingEntryMotion(unlockEntry, true, true);
            unlockEntry = null;
        }

        SetLock(false);
        SetNewIndicator(true);
        unlockCompleteCallback?.Invoke();
        unlockCompleteCallback = null;
    }

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != omp)
        {
            omp.ResetAllMotions();
            transform.localScale = Vector3.zero;

            appearDelayTween = DOVirtual.DelayedCall(_delay, onAppearDelayCompleteCallback).SetEase(Ease.Linear);
        }
    }

    private void OnAppearDelayComplete()
    {
        transform.localScale = Vector3.one;
        appearEntry = omp.Play(appearTag, bReset: forceReset);
    }

    public void ResetAnimation()
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != scaleTween && scaleTween.IsActive())
            scaleTween.Kill();

        transform.localScale = Vector3.zero;

        if (null != omp)
            omp.ResetAllMotions();
    }

    public void PlayDisappearAnimation(float _delay, TweenCallback _onComplete)
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != omp)
        {
            if (null != appearEntry)
            {
                omp.SettingEntryMotion(appearEntry, forceReset, forceReset);
                appearEntry = null;
            }

            if (omp.IsPlaying(appearTag))
                omp.Stop(appearTag);

            if (null != scaleTween && scaleTween.IsActive())
                scaleTween.Kill();

            scaleTween = transform.DOScale(Vector3.zero, disappearDuration)
                     .SetDelay(_delay)
                     .SetEase(disappearEase)
                     .OnComplete(_onComplete);
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    public MapType GetMapType()
    {
        return mapType;
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public bool IsSelected()
    {
        return isSelected;
    }


    // 내부 로직

    private void UpdateColor()
    {
        if (null == buttonImage)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (true == isLocked)
            buttonImage.color = lockColor;
        else if (true == isSelected)
            buttonImage.color = selectColor;
        else
            buttonImage.color = normalColor;
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

    private void UpdateRegionName()
    {
        if (null == regionNameText)
            return;

        if (null != localizationManager)
        {
            string _localizedName = localizationManager.GetText(mapType);
            if (false == string.IsNullOrEmpty(_localizedName))
            {
                regionNameText.text = _localizedName;
            }
            else
            {
                string _fallback = GetMapTypeString(mapType);
                regionNameText.text = _fallback;
            }
        }
        else
        {
            string _fallback = GetMapTypeString(mapType);

            regionNameText.text = _fallback;
        }
    }

    private string GetMapTypeString(MapType _type) => _type switch
    {
        MapType.Town => townString,
        MapType.WideGreenForest => plainsString,
        MapType.FluffySporeForest => forestString,
        _ => noneString
    };

    private void OnClickAnimationComplete()
    {
        isClicked = false;

        if (false == isLocked)
            onSelectEvent?.Invoke(mapType);

        if (true == isSelected)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != buttonImage)
        {
            Color _targetColor = true == isHovered ? GetHoverColor() : GetOriginalColor();
            colorTween = buttonImage.DOColor(_targetColor, hoverColorDuration).SetEase(Ease.Linear);
        }
    }


    // Event System 구현부

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (IsInputBlocked)
            return;

        if (true == IsTransitioning())
            return;

        if (false == isLocked)
        {
            if (PlayerPrefs.GetInt(regionNewKey, 0) == 1)
            {
                PlayerPrefs.SetInt(regionNewKey, 0);
                PlayerPrefs.Save();
                SetNewIndicator(false);
            }
        }

        if (true == isSelected || true == isClicked)
            return;

        isClicked = true;

        if (null != omp)
        {
            omp.SettingEntryMotion(appearEntry, forceReset, forceReset);
            omp.SettingEntryMotion(hoverEntry, forceReset, forceReset);
            omp.SettingEntryMotion(unHoverEntry, forceReset, forceReset);
            omp.SettingEntryMotion(clickEntry, forceReset, forceReset);
            omp.SettingEntryMotion(unlockEntry, forceReset, forceReset);

            string _targetTag = true == isLocked ? lockClickTag : clickTag;
            clickEntry = omp.Play(_targetTag, bReset: forceReset, _onComplete: onClickAnimationCompleteCallback);
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (IsInputBlocked)
            return;

        isHovered = true;

        if (true == isClicked)
            return;

        if (false == IsTransitioning())
        {
            if (null != omp)
            {
                omp.SettingEntryMotion(appearEntry, forceReset, forceReset);
                omp.SettingEntryMotion(hoverEntry, forceReset, forceReset);
                omp.SettingEntryMotion(unHoverEntry, forceReset, forceReset);
                omp.SettingEntryMotion(clickEntry, forceReset, forceReset);
                omp.SettingEntryMotion(unlockEntry, forceReset, forceReset);

                hoverEntry = omp.Play(hoverTag, bReset: forceReset);
            }
        }

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (false == isSelected)
        {
            if (null != buttonImage)
                colorTween = buttonImage.DOColor(GetHoverColor(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (IsInputBlocked)
            return;

        isHovered = false;

        if (true == isClicked)
            return;

        if (false == IsTransitioning())
        {
            if (null != omp)
            {
                omp.SettingEntryMotion(appearEntry, forceReset, forceReset);
                omp.SettingEntryMotion(hoverEntry, forceReset, forceReset);
                omp.SettingEntryMotion(clickEntry, forceReset, forceReset);
                omp.SettingEntryMotion(unHoverEntry, forceReset, forceReset);
                omp.SettingEntryMotion(unlockEntry, forceReset, forceReset);

                if (false == isSelected)
                {
                    if (null != buttonImage)
                        buttonImage.color = GetHoverColor();
                }

                unHoverEntry = omp.Play(unHoverTag, bReset: forceReset);
            }
        }

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (false == isSelected)
        {
            if (null != buttonImage)
                colorTween = buttonImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    private bool IsTransitioning()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            return true;

        if (null != scaleTween && true == scaleTween.IsActive())
            return true;

        if (null != omp && true == omp.IsPlaying(appearTag))
            return true;

        return false;
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != scaleTween && true == scaleTween.IsActive())
            scaleTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        isClicked = false;
        isHovered = false;
    }

    private void OnDestroy()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != scaleTween && true == scaleTween.IsActive())
            scaleTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();
    }
}
