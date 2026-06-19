using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using Coffee.UIEffects;

public class HUD_NavigationRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // 외부 의존성
    [Header("UI References")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject lockObject;
    [SerializeField] private TextMeshProUGUI regionNameText;
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private GameObject newIndicatorObj;

    [Header("Background Settings")]
    [SerializeField] private MapBackgroundData[] mapBackgrounds;

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

    [Header("Outline Color Settings")]
    [SerializeField] private Color outlineNormalColor = Color.white;
    [SerializeField] private Color outlineSelectColor = Color.green;
    [SerializeField] private Color outlineLockColor = Color.gray;

    [Header("Outline Hover Color Settings")]
    [SerializeField] private Color outlineNormalHoverColor = Color.white;
    [SerializeField] private Color outlineLockHoverColor = Color.red;

    [Header("UI Effect Outline Intensity Settings")]
    [SerializeField] private UIEffect uiEffect;
    [Range(0f, 1f)] [SerializeField] private float outlineNormalIntensity = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float outlineHoverIntensity = 1f;

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

    private HUD_VehicleNavigation navigation;
    private LocalizationManager localizationManager;

    // 내부 의존성
    private Action unlockCompleteCallback;
    private UnityEngine.Events.UnityAction onOmpUnlockCompleteCallback;
    private MotionEntry unlockEntry;
    private ParticleSystem unlockVfx;
    private MapType mapType = MapType.None;
    private Action<MapType> onSelectEvent;

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
    private Tweener outlineColorTween;
    private Tweener shadowAlphaTween;
    private Tween appearDelayTween;
    private Tweener scaleTween;
    private Sprite defaultBackgroundSprite;
    private Sprite cachedUnlockedSprite;
    private bool isBackgroundSwapped;
    private TweenCallback onAppearDelayCompleteCallback;
    [SerializeField] private CanvasGroup canvasGroup;

    // 캐싱된 상수 및 리터럴 값
    private const bool forceReset = true;
    private const string townString = "Town";
    private const string plainsString = "Vegetatedplains";
    private const string forestString = "Deepmossforest";
    private const string noneString = "None";

    public bool IsInputBlocked
    {
        get
        {
            if (null != navigation && true == navigation.IsInputBlocked)
            {
                return true;
            }
            return false;
        }
    }


    // 퍼블릭 초기화 및 제어 메서드

    private CanvasGroup GetCanvasGroup()
    {
        if (null == canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        return canvasGroup;
    }

    public void SetVisibility(bool _visible)
    {
        CanvasGroup _cg = GetCanvasGroup();
        _cg.alpha = true == _visible ? 1f : 0f;
        _cg.blocksRaycasts = _visible;
        _cg.interactable = _visible;

        if (false == _visible)
            CleanupOnHide();
    }

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

        if (null != buttonImage && null == defaultBackgroundSprite)
        {
            defaultBackgroundSprite = buttonImage.sprite;
        }

        cachedUnlockedSprite = null;
        if (null != mapBackgrounds)
        {
            int _len = mapBackgrounds.Length;
            for (int i = 0; i < _len; i++)
            {
                if (mapBackgrounds[i].mapType == _mapType)
                {
                    cachedUnlockedSprite = mapBackgrounds[i].backgroundSprite;
                    break;
                }
            }
        }
        isBackgroundSwapped = false;

        if (null != omp)
        {
            omp.Initialize();
        }

        SetSelect(false);
        SetLock(false);

        UpdateRegionName();
    }

    public void SetLock(bool _isLock)
    {
        isLocked = _isLock;

        if (null != lockObject)
        {
            lockObject.SetActive(isLocked);
        }

        if (null != regionNameText)
        {
            regionNameText.gameObject.SetActive(!isLocked);
        }

        UpdateColor();
        UpdateBackgroundSprite();
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

        if (null != buttonImage)
        {
            if (null != colorTween && true == colorTween.IsActive())
                colorTween.Kill();

            colorTween = buttonImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
        }

        if (null != outlineImage)
        {
            if (null != outlineColorTween && true == outlineColorTween.IsActive())
                outlineColorTween.Kill();

            outlineColorTween = outlineImage.DOColor(GetOriginalOutlineColor(), hoverColorDuration).SetEase(Ease.Linear);
        }

        if (null != uiEffect)
        {
            if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
                shadowAlphaTween.Kill();

            shadowAlphaTween = DOTween.To(GetShadowColorAlpha, SetShadowColorAlpha, GetTargetIntensity(), hoverColorDuration).SetEase(Ease.Linear);
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
                OnOmpUnlockComplete();
            }
        }
        else
        {
            OnOmpUnlockComplete();
        }
    }


    private void OnOmpUnlockComplete()
    {
        if (null != omp && null != unlockEntry)
        {
            omp.SettingEntryMotion(unlockEntry, true, true);
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

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

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
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != scaleTween && true == scaleTween.IsActive())
            scaleTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        if (null != buttonImage)
            buttonImage.color = GetOriginalColor();

        if (null != outlineImage)
            outlineImage.color = GetOriginalOutlineColor();

        if (null != uiEffect)
            uiEffect.shadowColorAlpha = GetTargetIntensity();

        transform.localScale = Vector3.zero;

        if (null != omp)
            omp.ResetAllMotions();
    }

    public void PlayDisappearAnimation(float _delay, TweenCallback _onComplete)
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            appearDelayTween.Kill();
        }

        if (null != omp)
        {
            if (null != appearEntry)
            {
                omp.SettingEntryMotion(appearEntry, forceReset, forceReset);
                appearEntry = null;
            }

            if (true == omp.IsPlaying(appearTag))
            {
                omp.Stop(appearTag);
            }

            if (null != scaleTween && true == scaleTween.IsActive())
            {
                scaleTween.Kill();
            }

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
        if (null != buttonImage)
        {
            if (null != colorTween && true == colorTween.IsActive())
                colorTween.Kill();

            if (true == isLocked)
                buttonImage.color = lockColor;
            else if (true == isSelected)
                buttonImage.color = selectColor;
            else
                buttonImage.color = normalColor;
        }

        if (null != outlineImage)
        {
            if (null != outlineColorTween && true == outlineColorTween.IsActive())
                outlineColorTween.Kill();

            if (true == isLocked)
                outlineImage.color = outlineLockColor;
            else if (true == isSelected)
                outlineImage.color = outlineSelectColor;
            else
                outlineImage.color = outlineNormalColor;
        }

        if (null != uiEffect)
        {
            if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
                shadowAlphaTween.Kill();

            uiEffect.shadowColorAlpha = GetTargetIntensity();
        }
    }

    private void UpdateBackgroundSprite()
    {
        if (null == buttonImage)
        {
            return;
        }

        if (false == isLocked)
        {
            if (null != cachedUnlockedSprite)
            {
                if (false == isBackgroundSwapped || buttonImage.sprite != cachedUnlockedSprite)
                {
                    buttonImage.sprite = cachedUnlockedSprite;
                    isBackgroundSwapped = true;
                }
            }
        }
        else
        {
            if (true == isBackgroundSwapped || buttonImage.sprite != defaultBackgroundSprite)
            {
                buttonImage.sprite = defaultBackgroundSprite;
                isBackgroundSwapped = false;
            }
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

    private Color GetOriginalOutlineColor()
    {
        if (true == isLocked)
            return outlineLockColor;
        if (true == isSelected)
            return outlineSelectColor;
        return outlineNormalColor;
    }

    private Color GetHoverOutlineColor()
    {
        if (true == isLocked)
            return outlineLockHoverColor;
        return outlineNormalHoverColor;
    }

    private float GetShadowColorAlpha()
    {
        if (null != uiEffect)
            return uiEffect.shadowColorAlpha;
        return 0f;
    }

    private void SetShadowColorAlpha(float _value)
    {
        if (null != uiEffect)
            uiEffect.shadowColorAlpha = _value;
    }

    private float GetTargetIntensity()
    {
        if (true == isSelected)
            return outlineHoverIntensity;
        if (true == isHovered)
            return outlineHoverIntensity;
        return outlineNormalIntensity;
    }

    private void UpdateRegionName()
    {
        if (null == regionNameText)
        {
            return;
        }

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

        if (null != buttonImage)
        {
            if (null != colorTween && true == colorTween.IsActive())
                colorTween.Kill();

            Color _targetColor = true == isHovered ? GetHoverColor() : GetOriginalColor();
            colorTween = buttonImage.DOColor(_targetColor, hoverColorDuration).SetEase(Ease.Linear);
        }

        if (null != outlineImage)
        {
            if (null != outlineColorTween && true == outlineColorTween.IsActive())
                outlineColorTween.Kill();

            Color _targetOutlineColor = true == isHovered ? GetHoverOutlineColor() : GetOriginalOutlineColor();
            outlineColorTween = outlineImage.DOColor(_targetOutlineColor, hoverColorDuration).SetEase(Ease.Linear);
        }

        if (null != uiEffect)
        {
            if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
                shadowAlphaTween.Kill();

            shadowAlphaTween = DOTween.To(GetShadowColorAlpha, SetShadowColorAlpha, GetTargetIntensity(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }


    // Event System 구현부

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
        if (true == IsInputBlocked)
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

        if (false == isSelected)
        {
            if (null != buttonImage)
            {
                if (null != colorTween && true == colorTween.IsActive())
                    colorTween.Kill();

                colorTween = buttonImage.DOColor(GetHoverColor(), hoverColorDuration).SetEase(Ease.Linear);
            }

            if (null != outlineImage)
            {
                if (null != outlineColorTween && true == outlineColorTween.IsActive())
                    outlineColorTween.Kill();

                outlineColorTween = outlineImage.DOColor(GetHoverOutlineColor(), hoverColorDuration).SetEase(Ease.Linear);
            }
        }

        if (null != uiEffect)
        {
            if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
                shadowAlphaTween.Kill();

            shadowAlphaTween = DOTween.To(GetShadowColorAlpha, SetShadowColorAlpha, outlineHoverIntensity, hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
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

                    if (null != outlineImage)
                        outlineImage.color = GetHoverOutlineColor();
                }

                unHoverEntry = omp.Play(unHoverTag, bReset: forceReset);
            }
        }

        if (false == isSelected)
        {
            if (null != buttonImage)
            {
                if (null != colorTween && true == colorTween.IsActive())
                    colorTween.Kill();

                colorTween = buttonImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
            }

            if (null != outlineImage)
            {
                if (null != outlineColorTween && true == outlineColorTween.IsActive())
                    outlineColorTween.Kill();

                outlineColorTween = outlineImage.DOColor(GetOriginalOutlineColor(), hoverColorDuration).SetEase(Ease.Linear);
            }
        }

        if (null != uiEffect)
        {
            if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
                shadowAlphaTween.Kill();

            float _targetIntensity = true == isSelected ? outlineHoverIntensity : outlineNormalIntensity;
            shadowAlphaTween = DOTween.To(GetShadowColorAlpha, SetShadowColorAlpha, _targetIntensity, hoverColorDuration).SetEase(Ease.Linear);
        }
    }

    private bool IsTransitioning()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
        {
            return true;
        }

        if (null != scaleTween && true == scaleTween.IsActive())
        {
            return true;
        }

        if (null != omp && true == omp.IsPlaying(appearTag))
        {
            return true;
        }

        return false;
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDisable()
    {
        CleanupOnHide();
    }

    private void CleanupOnHide()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != scaleTween && true == scaleTween.IsActive())
            scaleTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        if (null != vfxComponent && null != unlockVfx)
        {
            vfxComponent.Stop(unlockVfx, true);
            unlockVfx = null;
        }

        isClicked = false;
        isHovered = false;

        if (null != buttonImage)
            buttonImage.color = GetOriginalColor();

        if (null != outlineImage)
            outlineImage.color = GetOriginalOutlineColor();

        if (null != uiEffect)
            uiEffect.shadowColorAlpha = GetTargetIntensity();
    }

    private void OnDestroy()
    {
        if (null != appearDelayTween && true == appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != scaleTween && true == scaleTween.IsActive())
            scaleTween.Kill();

        if (null != colorTween && true == colorTween.IsActive())
            colorTween.Kill();

        if (null != outlineColorTween && true == outlineColorTween.IsActive())
            outlineColorTween.Kill();

        if (null != shadowAlphaTween && true == shadowAlphaTween.IsActive())
            shadowAlphaTween.Kill();

        if (null != vfxComponent && null != unlockVfx)
        {
            vfxComponent.Stop(unlockVfx, true);
            unlockVfx = null;
        }
    }
}

[System.Serializable]
public struct MapBackgroundData
{
    public MapType mapType;
    public Sprite backgroundSprite;
}
