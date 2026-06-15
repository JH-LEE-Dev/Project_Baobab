using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_VehicleNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    // 이벤트
    public event Action<MapType> regionSelectedEvent;

    // 외부 의존성
    [Header("UI References")]
    [SerializeField] private GameObject regionPrefab;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private RectTransform dragAreaRect;
    [SerializeField] private RectTransform containerRect;
    [SerializeField] private TMP_Text mapNameText;
    [SerializeField] private ObjectMotionPlayer mapNameOmp;
    [SerializeField] private string mapNameChangeMotionTag = "Change";

    [Header("Scroll & Drag Settings")]
    [SerializeField] private float scrollSensitivity = 1f;
    [SerializeField] private float wheelSensitivity = 35f;
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private bool inertiaEnabled = true;
    [Range(0f, 1f)] [SerializeField] private float decelerationRate = 0.95f;
    [SerializeField] private float extraBottomPadding = 20f;
    [SerializeField] private float appearDelayGap = 0.1f;
    [SerializeField] private float maxDragVelocity = 2500f;

    [Header("Scroll Buttons")]
    [SerializeField] private HUD_NavigationScrollButton upButton;
    [SerializeField] private HUD_NavigationScrollButton downButton;

    [Header("Scroll Button Long Press Settings")]
    [SerializeField] private float repeatDelay = 0.4f;
    [SerializeField] private float repeatInterval = 0.1f;

    [Header("Localization")]
    [SerializeField] private LocalizationMapping localizationMapping;

    private IMapDataProvider mapDataProvider;
    private LocalizationManager localizationManager;
    private HUD_Vehicle vehicle;

    // 내부 의존성
    private readonly List<HUD_NavigationRegion> spawnedRegions = new List<HUD_NavigationRegion>(8);
    private TweenCallback onRegionDisappearCallback;
    private TweenCallback onAllDisappearComplete;
    private Action<bool> onUpButtonPressStateChangedCallback;
    private Action<bool> onDownButtonPressStateChangedCallback;
    private Action<MapType> onRegionSelectedCallback;
    private Vector2 startMousePos;
    private Vector2 startAnchoredPos;
    private string defaultMapNameText = string.Empty;
    private bool isTransitioning = false;
    private float initialY = 0f;
    private float targetY = 0f;
    private float currentYVelocity = 0f;
    private float dragVelocityY = 0f;
    private int disappearCompletedCount = 0;
    private int disappearActiveCount = 0;
    private MapType currentSelectedMapType = MapType.None;
    private bool isDragging;
    private bool isYPositionCached;
    private bool isUpButtonPressed = false;
    private bool isDownButtonPressed = false;
    private float nextScrollTime = 0f;
    private float buttonPressDuration = 0f;
    private TweenCallback playMapNameChangeMotionCallback;
    private Tween mapNameChangeTween;

    // 캐싱된 상수 및 리터럴 값
    private const float defaultScrollStepSize = 100f;
    private const float defaultElementHeight = 100f;
    private const float inertiaStopThreshold = 0.1f;
    private const float velocityStopThreshold = 1f;
    private const float positionTolerance = 0.05f;
    private const float targetFrameRate = 60f;
    private const string townString = "Town";
    private const string plainsString = "Vegetatedplains";
    private const string forestString = "Deepmossforest";
    private const string noneString = "None";

    public bool IsInputBlocked
    {
        get
        {
            if (null != vehicle && true == vehicle.IsUnlockingProductionActive)
            {
                return true;
            }
            return false;
        }
    }


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider, LocalizationManager _localizeManager, HUD_Vehicle _vehicle)
    {
        isDragging = false;
        mapDataProvider = _mapDataProvider;
        localizationManager = _localizeManager;
        vehicle = _vehicle;

        if (null != localizationManager)
        {
            if (null != localizationMapping)
            {
                localizationManager.LoadMappingData(localizationMapping);
            }
        }

        if (null != mapNameOmp)
        {
            mapNameOmp.Initialize();
        }

        isYPositionCached = false;
        onRegionDisappearCallback = OnRegionDisappearComplete;
        onUpButtonPressStateChangedCallback = OnUpButtonPressStateChanged;
        onDownButtonPressStateChangedCallback = OnDownButtonPressStateChanged;
        onRegionSelectedCallback = HandleRegionSelected;
        playMapNameChangeMotionCallback = PlayMapNameChangeMotion;

        if (null != mapNameText)
        {
            defaultMapNameText = mapNameText.text;
        }

        UpdateMapNameText(MapType.None);

        SetupRegionsFromData();

        if (null != upButton)
        {
            upButton.Initialize(onUpButtonPressStateChangedCallback);
        }

        if (null != downButton)
        {
            downButton.Initialize(onDownButtonPressStateChangedCallback);
        }
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        isDragging = true;
        dragVelocityY = 0f;

        if (null != containerRect && null != viewportRect)
        {
            if (true == RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, _eventData.position, _eventData.pressEventCamera, out Vector2 localPoint))
            {
                startMousePos = localPoint;
                startAnchoredPos = containerRect.anchoredPosition;
                targetY = startAnchoredPos.y;
            }
        }
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (false == isDragging)
        {
            return;
        }

        if (null == containerRect || null == viewportRect)
        {
            return;
        }

        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, _eventData.position, _eventData.pressEventCamera, out Vector2 currentLocalPoint))
        {
            return;
        }

        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, _eventData.position - _eventData.delta, _eventData.pressEventCamera, out Vector2 prevLocalPoint))
        {
            return;
        }

        float deltaY = (currentLocalPoint.y - startMousePos.y) * scrollSensitivity;

        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        
        float scrollRange = 0f;
        if (viewportHeight < contentHeight)
        {
            scrollRange = contentHeight - viewportHeight + extraBottomPadding;
        }

        float minScrollY = initialY;
        float maxScrollY = initialY + scrollRange;

        float nextTargetY = startAnchoredPos.y + deltaY;
        targetY = Mathf.Clamp(nextTargetY, minScrollY, maxScrollY);

        if (0f < Time.deltaTime)
        {
            float localDeltaY = currentLocalPoint.y - prevLocalPoint.y;
            float rawVelocity = (localDeltaY * scrollSensitivity) / Time.deltaTime;
            
            dragVelocityY = Mathf.Clamp(rawVelocity, -maxDragVelocity, maxDragVelocity);
        }
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        isDragging = false;
    }

    public void OnScroll(PointerEventData _eventData)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (null == containerRect || null == viewportRect)
        {
            return;
        }

        dragVelocityY = 0f;

        float scrollAmount = -_eventData.scrollDelta.y * wheelSensitivity;

        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        
        float scrollRange = 0f;
        if (viewportHeight < contentHeight)
        {
            scrollRange = contentHeight - viewportHeight + extraBottomPadding;
        }

        float minScrollY = initialY;
        float maxScrollY = initialY + scrollRange;

        targetY = Mathf.Clamp(targetY + scrollAmount, minScrollY, maxScrollY);
    }

    public void SetSelectedMapTypeWithoutAnimation(MapType _mapType)
    {
        currentSelectedMapType = _mapType;
        UpdateMapNameText(_mapType);

        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                spawnedRegions[i].SetSelect(_mapType == spawnedRegions[i].GetMapType());
            }
        }
    }

    public void ResetSelection(bool _updateText = true)
    {
        isTransitioning = false;
        currentSelectedMapType = MapType.None;

        if (true == _updateText)
        {
            UpdateMapNameText(MapType.None);
        }

        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                spawnedRegions[i].gameObject.SetActive(true);
                spawnedRegions[i].SetSelect(false);
                spawnedRegions[i].ClearEntry();
                spawnedRegions[i].ResetAnimation();
            }
        }

        if (null != upButton)
        {
            upButton.ResetAnimation();
        }

        if (null != downButton)
        {
            downButton.ResetAnimation();
        }

        if (true == isYPositionCached)
        {
            targetY = initialY;
            if (null != containerRect)
            {
                containerRect.anchoredPosition = new Vector2(containerRect.anchoredPosition.x, initialY);
            }
        }
    }

    public void PlayAppearAnimations()
    {
        if (null != upButton)
        {
            upButton.PlayAppearAnimation();
        }

        if (null != downButton)
        {
            downButton.PlayAppearAnimation();
        }

        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                spawnedRegions[i].gameObject.SetActive(true);
                spawnedRegions[i].PlayAppearAnimation(i * appearDelayGap);
            }
        }
    }

    public void PlayDisappearAnimations(TweenCallback _onComplete)
    {
        if (true == isTransitioning)
        {
            return;
        }

        isTransitioning = true;

        int activeCount = 0;
        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                activeCount++;
            }
        }

        if (0 == activeCount)
        {
            if (null != upButton)
            {
                upButton.gameObject.SetActive(false);
            }
            if (null != downButton)
            {
                downButton.gameObject.SetActive(false);
            }
            _onComplete?.Invoke();
            return;
        }

        onAllDisappearComplete = _onComplete;
        disappearCompletedCount = 0;
        disappearActiveCount = activeCount;

        if (null != upButton)
        {
            upButton.PlayDisappearAnimation(null);
        }
        if (null != downButton)
        {
            downButton.PlayDisappearAnimation(null);
        }

        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                float delay = (spawnedRegions.Count - 1 - i) * appearDelayGap;
                spawnedRegions[i].PlayDisappearAnimation(delay, onRegionDisappearCallback);
            }
        }
    }
 
    public void SetMapNameTextToInformation()
    {
        if (null == mapNameText)
        {
            return;
        }
 
        string _newText = string.Empty;
 
        if (null != localizationManager)
        {
            _newText = localizationManager.GetText(1, 1);
        }
 
        if (true == string.IsNullOrEmpty(_newText))
        {
            _newText = "등장하는 나무 정보";
        }
 
        if (_newText != mapNameText.text)
        {
            mapNameText.text = _newText;
 
            if (null != mapNameOmp)
            {
                mapNameOmp.ResetAllMotions();
                if (null != mapNameChangeTween && true == mapNameChangeTween.IsActive())
                {
                    mapNameChangeTween.Kill();
                }
                mapNameChangeTween = DOVirtual.DelayedCall(0.01f, playMapNameChangeMotionCallback).SetEase(Ease.Linear);
            }
        }
    }

    public HUD_NavigationRegion GetRegionInstance(MapType _mapType)
    {
        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i] && spawnedRegions[i].GetMapType() == _mapType)
            {
                return spawnedRegions[i];
            }
        }
        return null;
    }

    public void RefreshRegionLocks()
    {
        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        int activeIndex = 0;
        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo info = db.mapDatas[i];
            if (MapType.Town == info.mapType)
            {
                continue;
            }

            if (activeIndex < spawnedRegions.Count)
            {
                HUD_NavigationRegion region = spawnedRegions[activeIndex];
                if (null != region)
                {
                    bool isMapLocked = !info.bCanAccess;
                    string regionKey = string.Format("UnLock_Region_{0}", info.mapType);
                    if (false == isMapLocked && PlayerPrefs.GetInt(regionKey, 0) == 0)
                    {
                        isMapLocked = true;
                    }

                    region.SetLock(isMapLocked);

                    string regionNewKey = string.Format("New_Region_{0}", info.mapType);
                    region.SetNewIndicator(PlayerPrefs.GetInt(regionNewKey, 0) == 1);
                }
                activeIndex++;
            }
        }
    }

    public void ClearAllNewIndicators()
    {
        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            HUD_NavigationRegion region = spawnedRegions[i];
            if (null != region && true == region.gameObject.activeSelf)
            {
                MapType type = region.GetMapType();
                string key = string.Format("New_Region_{0}", type);
                if (1 == PlayerPrefs.GetInt(key, 0))
                {
                    PlayerPrefs.SetInt(key, 0);
                    region.SetNewIndicator(false);
                }
            }
        }
        PlayerPrefs.Save();
    }

    public MapType GetSelectedMapType()
    {
        return currentSelectedMapType;
    }


    // 내부 로직

    private void PlayMapNameChangeMotion()
    {
        if (null != mapNameOmp)
        {
            mapNameOmp.Play(mapNameChangeMotionTag, bReset: true);
        }
    }
 
    private void OnRegionDisappearComplete()
    {
        disappearCompletedCount++;
        if (disappearActiveCount == disappearCompletedCount)
        {
            for (int j = 0; j < spawnedRegions.Count; j++)
            {
                if (null != spawnedRegions[j])
                {
                    spawnedRegions[j].gameObject.SetActive(false);
                }
            }

            isTransitioning = false;
            onAllDisappearComplete?.Invoke();
            onAllDisappearComplete = null;
        }
    }

    private void ScrollByStep(bool _isUp)
    {
        if (null == containerRect || null == viewportRect)
        {
            return;
        }

        dragVelocityY = 0f;

        float stepSize = GetScrollStepSize();
        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;

        float scrollRange = 0f;
        if (viewportHeight < contentHeight)
        {
            scrollRange = contentHeight - viewportHeight + extraBottomPadding;
        }

        float minScrollY = initialY;
        float maxScrollY = initialY + scrollRange;

        float direction = true == _isUp ? -1f : 1f;
        targetY = Mathf.Clamp(targetY + direction * stepSize, minScrollY, maxScrollY);
    }

    private float GetScrollStepSize()
    {
        if (null == spawnedRegions || 0 == spawnedRegions.Count)
        {
            return defaultScrollStepSize;
        }

        float elementHeight = defaultElementHeight;
        HUD_NavigationRegion firstRegion = spawnedRegions[0];
        if (null != firstRegion)
        {
            RectTransform rectTrans = firstRegion.GetComponent<RectTransform>();
            if (null != rectTrans)
            {
                elementHeight = rectTrans.rect.height;
            }
        }

        float spacing = 0f;
        if (null != containerRect)
        {
            VerticalLayoutGroup layoutGroup = containerRect.GetComponent<VerticalLayoutGroup>();
            if (null != layoutGroup)
            {
                spacing = layoutGroup.spacing;
            }
        }

        return elementHeight + spacing;
    }

    private void HandleButtonPressStateChanged(bool _isUp, bool _isPressed)
    {
        if (true == _isUp)
        {
            isUpButtonPressed = _isPressed;
        }
        else
        {
            isDownButtonPressed = _isPressed;
        }

        if (true == _isPressed)
        {
            ScrollByStep(_isUp);
            buttonPressDuration = 0f;
            nextScrollTime = Time.unscaledTime + repeatDelay;
        }
        else
        {
            buttonPressDuration = 0f;
        }
    }

    private void OnUpButtonPressStateChanged(bool _isPressed)
    {
        HandleButtonPressStateChanged(true, _isPressed);
    }

    private void OnDownButtonPressStateChanged(bool _isPressed)
    {
        HandleButtonPressStateChanged(false, _isPressed);
    }

    private void SetupRegionsFromData()
    {
        if (null == mapDataProvider || null == containerRect || null == regionPrefab)
        {
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                Destroy(spawnedRegions[i].gameObject);
            }
        }
        spawnedRegions.Clear();

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo info = db.mapDatas[i];

            if (MapType.Town == info.mapType)
            {
                continue;
            }

            GameObject obj = Instantiate(regionPrefab, containerRect);
            if (null == obj)
            {
                continue;
            }

            HUD_NavigationRegion region = obj.GetComponent<HUD_NavigationRegion>();
            if (null != region)
            {
                region.Initialize(info.mapType, onRegionSelectedCallback, localizationManager, this);

                bool isMapLocked = !info.bCanAccess;

                string regionKey = string.Format("UnLock_Region_{0}", info.mapType);
                if (false == isMapLocked && PlayerPrefs.GetInt(regionKey, 0) == 0)
                {
                    isMapLocked = true;
                }

                region.SetLock(isMapLocked);

                string regionNewKey = string.Format("New_Region_{0}", info.mapType);
                region.SetNewIndicator(PlayerPrefs.GetInt(regionNewKey, 0) == 1);

                spawnedRegions.Add(region);
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        currentSelectedMapType = _mapType;
        regionSelectedEvent?.Invoke(_mapType);
        UpdateMapNameText(_mapType);

        for (int i = 0; i < spawnedRegions.Count; i++)
        {
            if (null != spawnedRegions[i])
            {
                spawnedRegions[i].SetSelect(_mapType == spawnedRegions[i].GetMapType());
            }
        }
    }

    private void UpdateMapNameText(MapType _mapType)
    {
        if (null == mapNameText)
        {
            return;
        }

        string _newText = string.Empty;

        if (null != localizationManager)
        {
            string _localizedName = localizationManager.GetText(_mapType);
            if (false == string.IsNullOrEmpty(_localizedName))
            {
                _newText = _localizedName;
            }
        }

        if (true == string.IsNullOrEmpty(_newText))
        {
            if (MapType.None == _mapType)
            {
                _newText = defaultMapNameText;
            }
            else
            {
                _newText = GetMapTypeString(_mapType);
            }
        }

        if (_newText != mapNameText.text)
        {
            mapNameText.text = _newText;

            if (null != mapNameOmp)
            {
                mapNameOmp.ResetAllMotions();
                if (null != mapNameChangeTween && true == mapNameChangeTween.IsActive())
                {
                    mapNameChangeTween.Kill();
                }
                mapNameChangeTween = DOVirtual.DelayedCall(0.01f, playMapNameChangeMotionCallback).SetEase(Ease.Linear);
            }
        }
    }

    private string GetMapTypeString(MapType _type) => _type switch
    {
        MapType.Town => townString,
        MapType.WideGreenForest => plainsString,
        MapType.FluffySporeForest => forestString,
        _ => noneString
    };


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Update()
    {
        if (null == containerRect)
        {
            return;
        }

        if (false == isYPositionCached)
        {
            initialY = containerRect.anchoredPosition.y;
            targetY = initialY;
            isYPositionCached = true;
        }

        if (true == isUpButtonPressed || true == isDownButtonPressed)
        {
            buttonPressDuration += Time.deltaTime;
            if (Time.unscaledTime >= nextScrollTime)
            {
                ScrollByStep(isUpButtonPressed);

                float currentInterval = buttonPressDuration < repeatDelay ? repeatDelay : repeatInterval;
                nextScrollTime = Time.unscaledTime + currentInterval;
            }
        }

        if (false == isDragging && true == inertiaEnabled && inertiaStopThreshold < Mathf.Abs(dragVelocityY))
        {
            dragVelocityY *= Mathf.Pow(decelerationRate, Time.deltaTime * targetFrameRate);

            float contentHeight = containerRect.rect.height;
            float viewportHeight = viewportRect.rect.height;
            
            float scrollRange = 0f;
            if (viewportHeight < contentHeight)
            {
                scrollRange = contentHeight - viewportHeight + extraBottomPadding;
            }

            float minScrollY = initialY;
            float maxScrollY = initialY + scrollRange;

            targetY += dragVelocityY * Time.deltaTime;
            targetY = Mathf.Clamp(targetY, minScrollY, maxScrollY);

            if (velocityStopThreshold > Mathf.Abs(dragVelocityY))
            {
                dragVelocityY = 0f;
            }
        }

        float diffY = Mathf.Abs(containerRect.anchoredPosition.y - targetY);
        if (true == isDragging || inertiaStopThreshold < Mathf.Abs(dragVelocityY) || positionTolerance < diffY)
        {
            float newY = Mathf.SmoothDamp(containerRect.anchoredPosition.y, targetY, ref currentYVelocity, smoothTime);
            containerRect.anchoredPosition = new Vector2(containerRect.anchoredPosition.x, newY);
        }
        else
        {
            currentYVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        if (null != mapNameChangeTween && true == mapNameChangeTween.IsActive())
        {
            mapNameChangeTween.Kill();
        }
    }
}
