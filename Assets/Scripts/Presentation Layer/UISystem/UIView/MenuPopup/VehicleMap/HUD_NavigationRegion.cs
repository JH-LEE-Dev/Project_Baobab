using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_NavigationRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
    [Header("UI References")]
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject lockObject;
    [SerializeField] private TextMeshProUGUI regionNameText;

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

    // //내부 의존성
    private MapType mapType = MapType.None;
    private Action<MapType> onSelectEvent;
    private LocalizationManager localizationManager;

    private MotionEntry hoverEntry;
    private MotionEntry clickEntry;
    private MotionEntry unHoverEntry;
    private bool isHovered = false;
    private bool isClicked = false;
    private bool isLocked = false;
    private bool isSelected = false;
    private bool isInitialized = false;
    private Tweener colorTween;
    private Tween appearDelayTween;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(MapType _mapType, Action<MapType> _onSelect, LocalizationManager _localizeManager)
    {
        mapType = _mapType;
        onSelectEvent = _onSelect;
        localizationManager = _localizeManager;
        isClicked = false;
        isLocked = false;
        isSelected = false;
        isHovered = false;

        if (null != omp)
            omp.Initialize();

        SetSelect(false);
        SetLock(false);

        UpdateRegionName();

        isInitialized = true;
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
            omp.SettingEntryMotion(hoverEntry, true, true);
            omp.SettingEntryMotion(clickEntry, true, true);
            omp.SettingEntryMotion(unHoverEntry, true, true);
        }

        hoverEntry = null;
        clickEntry = null;
        unHoverEntry = null;
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

    public void PlayAppearAnimation(float _delay)
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        if (null != omp)
        {
            omp.ResetAllMotions();
            transform.localScale = Vector3.zero;

            appearDelayTween = DOVirtual.DelayedCall(_delay, () =>
            {
                transform.localScale = Vector3.one;
                omp.Play(appearTag, bReset: true);
            }).SetEase(Ease.Linear);
        }
    }

    public void ResetAnimation()
    {
        if (null != appearDelayTween && appearDelayTween.IsActive())
            appearDelayTween.Kill();

        transform.localScale = Vector3.one;

        if (null != omp)
            omp.ResetAllMotions();
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


    // //내부 로직

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
                regionNameText.text = _localizedName;
            else
                regionNameText.text = mapType.ToString();
        }
        else
        {
            regionNameText.text = mapType.ToString();
        }
    }

    private void OnClickAnimationComplete()
    {
        isClicked = false;

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


    // //Event System 구현부

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (true == isSelected)
            return;

        if (false == isLocked)
            onSelectEvent?.Invoke(mapType);

        isClicked = true;

        if (null != omp)
        {
            if (null != hoverEntry)
                omp.SettingEntryMotion(hoverEntry, true, true);
            if (null != unHoverEntry)
                omp.SettingEntryMotion(unHoverEntry, true, true);

            string _targetTag = true == isLocked ? lockClickTag : clickTag;
            clickEntry = omp.Play(_targetTag, bReset: true, _onComplete: OnClickAnimationComplete);
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;

        if (true == isClicked)
            return;

        if (null != omp)
        {
            if (null != unHoverEntry)
                omp.SettingEntryMotion(unHoverEntry, true, true);
            if (null != clickEntry)
                omp.SettingEntryMotion(clickEntry, true, true);
            hoverEntry = omp.Play(hoverTag, bReset: true);
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
        isHovered = false;

        if (true == isClicked)
            return;

        if (null != colorTween && colorTween.IsActive())
            colorTween.Kill();

        if (null != omp)
        {
            if (null != hoverEntry)
                omp.SettingEntryMotion(hoverEntry, true, true);
            if (null != clickEntry)
                omp.SettingEntryMotion(clickEntry, true, true);

            if (false == isSelected)
            {
                if (null != buttonImage)
                    buttonImage.color = GetHoverColor();
            }

            unHoverEntry = omp.Play(unHoverTag, bReset: true);
        }

        if (false == isSelected)
        {
            if (null != buttonImage)
                colorTween = buttonImage.DOColor(GetOriginalColor(), hoverColorDuration).SetEase(Ease.Linear);
        }
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)
}
