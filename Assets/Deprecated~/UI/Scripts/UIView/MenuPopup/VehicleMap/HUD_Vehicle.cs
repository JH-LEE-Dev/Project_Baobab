using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_Vehicle : MonoBehaviour
{
    private enum NavigationState
    {
        Region,
        SubRegion,
        TreeField
    }

    private struct UnlockQueueItem
    {
        public bool isRegion;
        public MapType mapType;
        public ForestType forestType;
    }

    // 이벤트
    public event Action<MapType, ForestType> mapSelectedEvent;

    // 외부 의존성
    [SerializeField] private Image lightImage;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private HUD_VehicleNavigation navigation;
    [SerializeField] private HUD_NavigationSubField subField;
    [SerializeField] private HUD_NavigationTreeField treeField;
    [SerializeField] private HUD_VehicleMapSelectorButton prevButton;
    [SerializeField] private HUD_VehicleMapSelectorButton homeButton;
    [SerializeField] private HUD_VehicleMapSelectorButton cancelButton;
    [SerializeField] private HUD_VehicleMapSelectorButton selectButton;

    [SerializeField] private string backgroundMotionTag = "Background";
    [SerializeField] private string controlBoardMotionTag = "ControlBoard";
    [SerializeField] private string navTopMotionTag = "OnNavTop";
    [SerializeField] private float selectorButtonAppearDelay = 0.2f;

    [Header("Appear Delays")]
    [SerializeField] private float prevButtonAppearDelay = 0.1f;
    [SerializeField] private float homeButtonAppearDelay = 0.2f;

    private IMapDataProvider mapDataProvider;
    public IMapDataProvider MapDataProvider => mapDataProvider;

    // 내부 의존성
    private readonly Queue<UnlockQueueItem> unlockQueue = new Queue<UnlockQueueItem>();
    private bool isUnlockingProductionActive = false;

    private TweenCallback onDisappearCompleteCallback;
    private UnityEngine.Events.UnityAction onNavTopCompleteCallback;
    private UnityEngine.Events.UnityAction handleCloseCallback;
    private List<ForestEnvironmentInfo> pendingForestDatas;
    private MapType pendingMapType = MapType.None;
    private bool isUnlockStateSyncPending = true;
    private bool regionUnlockOccurred = false;

    private int activeUnlockCount = 0;
    private TweenCallback triggerAllUnlocksDirectlyCallback;
    private Action onUnlockProductionCompleteCallback;


    private Action onPrevCallback;
    private Action onHomeCallback;
    private Action handlePrevClickedCallback;
    private Action handleHomeClickedCallback;
    private Action handleSelectButtonClickedCallback;
    private Action onSubRegionDisappearCompleteCallback;
    private Action onTreeFieldPrevDisappearCompleteCallback;
    private Action onTreeFieldHomeDisappearCompleteCallback;

    private MapType lastSelectedMapType = MapType.None;
    private NavigationState currentState = NavigationState.Region;

    // 캐싱된 상수 및 리터럴 값
    private const float transparentAlpha = 0f;
    private const bool forceReset = true;
    private const float delayedCallDuration = 0.1f;

    public bool IsUnlockingProductionActive
    {
        get
        {
            return isUnlockingProductionActive;
        }
    }


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider, Action _onPrev, Action _onHome, Action _onClose, LocalizationManager _localizeManager)
    {
        onDisappearCompleteCallback = OnDisappearComplete;
        onNavTopCompleteCallback = OnNavTopComplete;
        handleCloseCallback = HandleClose;
        onPrevCallback = _onPrev;
        onHomeCallback = _onHome;
        handlePrevClickedCallback = HandlePrevClicked;
        handleHomeClickedCallback = HandleHomeClicked;
        handleSelectButtonClickedCallback = HandleSelectButtonClicked;
        onSubRegionDisappearCompleteCallback = OnSubRegionDisappearComplete;
        onTreeFieldPrevDisappearCompleteCallback = OnTreeFieldPrevDisappear;
        onTreeFieldHomeDisappearCompleteCallback = OnTreeFieldHomeDisappear;

        triggerAllUnlocksDirectlyCallback = TriggerAllUnlocksDirectly;
        onUnlockProductionCompleteCallback = OnUnlockProductionComplete;

        mapDataProvider = _mapDataProvider;
        isUnlockStateSyncPending = true;

        if (null != lightImage)
        {
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);
        }

        if (null != navigation)
        {
            navigation.Initialize(mapDataProvider, _localizeManager, this);
            navigation.regionSelectedEvent -= HandleRegionSelected;
            navigation.regionSelectedEvent += HandleRegionSelected;
        }

        if (null != subField)
        {
            subField.Initialize(this);
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
            subField.subRegionSelectedEvent += HandleSubRegionSelected;
        }

        if (null != treeField)
        {
            treeField.Initialize(_localizeManager);
            treeField.treeSelectedEvent -= HandleTreeSelected;
            treeField.treeSelectedEvent += HandleTreeSelected;
        }

        if (null != prevButton)
        {
            prevButton.Initialize(handlePrevClickedCallback);
        }

        if (null != homeButton)
        {
            homeButton.Initialize(handleHomeClickedCallback);
        }

        if (null != cancelButton)
        {
            cancelButton.Initialize(_onClose);
        }

        if (null != selectButton)
        {
            selectButton.Initialize(handleSelectButtonClickedCallback);
        }

        if (null != omp)
        {
            omp.Initialize();
        }
    }

    public void SyncUnlockStates()
    {
        isUnlockStateSyncPending = true;
        InitUnlockStates();
    }

    public void Open()
    {
        gameObject.SetActive(true);

        InitUnlockStates();

        if (null != navigation)
        {
            navigation.RefreshRegionLocks();
        }

        omp.Play(navTopMotionTag, bReset: forceReset, _onComplete: onNavTopCompleteCallback);
        omp.Play(backgroundMotionTag, bReset: forceReset);
        omp.Play(controlBoardMotionTag, bReset: forceReset, _onComplete: null);
    }

    public void Close(bool _isSkip = false)
    {
        if (null != lightImage)
        {
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);
        }

        if (null != navigation)
        {
            navigation.ClearAllNewIndicators();
        }

        if (null != subField)
        {
            subField.ClearAllNewIndicators();
        }

        if (null != navigation)
        {
            navigation.ResetSelection();
        }

        if (null != subField)
        {
            subField.ResetSelection();
        }

        if (null != treeField)
        {
            treeField.ResetSelection();
        }

        if (null != prevButton)
        {
            prevButton.ResetAnimation();
        }

        if (null != homeButton)
        {
            homeButton.ResetAnimation();
        }

        if (null != cancelButton)
        {
            cancelButton.ResetAnimation();
        }

        if (null != selectButton)
        {
            selectButton.ResetAnimation();
        }

        omp.PlayBackward(backgroundMotionTag, bReset: forceReset, _skip: _isSkip);
        omp.PlayBackward(controlBoardMotionTag, bReset: forceReset, _skip: _isSkip, _isSkipCallback: true, _onComplete: handleCloseCallback);
    }

    public MapType GetSelectedMapType()
    {
        if (null != navigation)
        {
            return navigation.GetSelectedMapType();
        }
        return MapType.None;
    }


    // 내부 로직

    private void OnSubRegionDisappearComplete()
    {
        currentState = NavigationState.TreeField;

        if (null != subField && null != treeField)
        {
            ForestEnvironmentInfo forestInfo = subField.GetSelectedForestInfo();
            treeField.SetTreeField(forestInfo);
        }

        if (null != navigation)
        {
            navigation.SetMapNameTextToInformation();
        }

        if (null != selectButton)
        {
            selectButton.PlayAppearAnimation(selectorButtonAppearDelay);
        }
    }

    private void OnTreeFieldPrevDisappear()
    {
        currentState = NavigationState.SubRegion;
        if (null != subField)
        {
            subField.SetSubRegions(pendingMapType, pendingForestDatas);
        }

        if (null != navigation)
        {
            navigation.SetSelectedMapTypeWithoutAnimation(pendingMapType);
        }
    }

    private void OnTreeFieldHomeDisappear()
    {
        RestoreToHome();
        onHomeCallback?.Invoke();
    }

    private void OnNavTopComplete()
    {
        bool hasPendingRegionUnlock = CheckPendingRegionUnlocks();

        if (true == hasPendingRegionUnlock)
        {
            currentState = NavigationState.Region;
            lastSelectedMapType = MapType.None;

            if (null != navigation)
            {
                navigation.PlayAppearAnimations();
            }

            if (null != cancelButton)
            {
                cancelButton.PlayAppearAnimation(selectorButtonAppearDelay);
            }

            BuildRegionUnlockQueue();
            if (unlockQueue.Count > 0)
            {
                ProcessNextUnlock();
            }
        }
        else
        {
            MapType pendingSubRegionMapType = FindPendingSubRegionUnlockMapType();

            if (MapType.None != pendingSubRegionMapType)
            {
                lastSelectedMapType = pendingSubRegionMapType;
                RestoreToSelectedRegion(pendingSubRegionMapType);

                if (null != cancelButton)
                {
                    cancelButton.PlayAppearAnimation(homeButtonAppearDelay + delayedCallDuration);
                }
            }
            else if (MapType.None != lastSelectedMapType)
            {
                RestoreToSelectedRegion(lastSelectedMapType);

                if (null != cancelButton)
                {
                    cancelButton.PlayAppearAnimation(homeButtonAppearDelay + delayedCallDuration);
                }
            }
            else
            {
                currentState = NavigationState.Region;

                if (null != navigation)
                {
                    navigation.PlayAppearAnimations();
                }

                if (null != cancelButton)
                {
                    cancelButton.PlayAppearAnimation(selectorButtonAppearDelay);
                }
            }
        }
    }

    private void InitUnlockStates()
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

        bool isFirstPlayableRegionFound = false;

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (MapType.Town == regionInfo.mapType)
            {
                continue;
            }

            if (!isFirstPlayableRegionFound)
            {
                isFirstPlayableRegionFound = true;
                
                if (!regionInfo.isUnlocked)
                {
                    // 첫 대지역의 첫 서브지역 무조건 해금 (최초 부여 시 NEW 뱃지 숨김)
                    mapDataProvider.MarkMapUnlocked(regionInfo.mapType);
                    mapDataProvider.MarkMapUnlockAnimationPlayed(regionInfo.mapType);
                    mapDataProvider.MarkMapLevelAsRead(regionInfo.mapType);
                    
                    if (null != regionInfo.forestDatas && 0 < regionInfo.forestDatas.Count)
                    {
                        ForestType firstSubRegion = regionInfo.forestDatas[0].forestType;
                        mapDataProvider.MarkUnlocked(regionInfo.mapType, firstSubRegion);
                        mapDataProvider.MarkUnlockAnimationPlayed(regionInfo.mapType, firstSubRegion);
                        mapDataProvider.MarkMapAsRead(regionInfo.mapType, firstSubRegion);
                    }
                }
            }

            if (true == regionInfo.bCanAccess)
            {
                if (true == isUnlockStateSyncPending && !regionInfo.isUnlocked)
                {
                    mapDataProvider.MarkMapUnlocked(regionInfo.mapType);
                    mapDataProvider.MarkMapUnlockAnimationPlayed(regionInfo.mapType);
                    mapDataProvider.MarkMapLevelAsRead(regionInfo.mapType);
                }
            }

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];

                    if (true == subInfo.bCanAccess)
                    {
                        if (true == isUnlockStateSyncPending && !subInfo.isUnlocked)
                        {
                            mapDataProvider.MarkUnlocked(regionInfo.mapType, subInfo.forestType);
                            mapDataProvider.MarkUnlockAnimationPlayed(regionInfo.mapType, subInfo.forestType);
                            mapDataProvider.MarkMapAsRead(regionInfo.mapType, subInfo.forestType);
                        }
                    }
                }
            }
        }

        isUnlockStateSyncPending = false;
    }

    private bool CheckPendingRegionUnlocks()
    {
        if (null == mapDataProvider)
        {
            return false;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return false;
        }

        InitUnlockStates();

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (MapType.Town == regionInfo.mapType)
            {
                continue;
            }

            bool isUnlockPending = regionInfo.bCanAccess && !regionInfo.isUnlocked;

            if (true == isUnlockPending)
            {
                return true;
            }
        }

        return false;
    }

    private MapType FindPendingSubRegionUnlockMapType()
    {
        if (true == regionUnlockOccurred)
        {
            return MapType.None;
        }

        if (null == mapDataProvider)
        {
            return MapType.None;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return MapType.None;
        }

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (MapType.Town == regionInfo.mapType)
            {
                continue;
            }

            if (!regionInfo.isUnlocked)
            {
                continue;
            }

            if (regionInfo.isNew)
            {
                continue;
            }

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    if (true == subInfo.bCanAccess && !subInfo.isUnlocked)
                    {
                        return regionInfo.mapType;
                    }
                }
            }
        }

        return MapType.None;
    }

    private void BuildRegionUnlockQueue()
    {
        unlockQueue.Clear();
        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        bool hasRegionUnlock = false;

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (MapType.Town == regionInfo.mapType)
            {
                continue;
            }

            if (true == regionInfo.bCanAccess && !regionInfo.isUnlocked)
            {
                UnlockQueueItem item;
                item.isRegion = true;
                item.mapType = regionInfo.mapType;
                item.forestType = ForestType.None;
                unlockQueue.Enqueue(item);

                hasRegionUnlock = true;
            }
        }

        if (true == hasRegionUnlock)
        {
            regionUnlockOccurred = true;
        }
    }

    private void BuildSubRegionUnlockQueue(MapType _mapType)
    {
        unlockQueue.Clear();
        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        InitUnlockStates();

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType != _mapType)
            {
                continue;
            }

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    bool isSubUnlockPending = subInfo.bCanAccess && !subInfo.isUnlocked;

                    if (true == isSubUnlockPending)
                    {
                        UnlockQueueItem item;
                        item.isRegion = false;
                        item.mapType = _mapType;
                        item.forestType = subInfo.forestType;
                        unlockQueue.Enqueue(item);
                    }
                }
            }
        }
    }

    private void ProcessNextUnlock()
    {
        if (0 == unlockQueue.Count)
        {
            isUnlockingProductionActive = false;
            return;
        }

        isUnlockingProductionActive = true;

        UnlockQueueItem peekItem = unlockQueue.Peek();
        if (true == peekItem.isRegion && NavigationState.Region != currentState)
        {
            RestoreToHome();
            DOVirtual.DelayedCall(delayedCallDuration, triggerAllUnlocksDirectlyCallback).SetEase(Ease.Linear);
        }
        else
        {
            TriggerAllUnlocksDirectly();
        }
    }

    private void TriggerAllUnlocksDirectly()
    {
        activeUnlockCount = unlockQueue.Count;
        int count = unlockQueue.Count;

        for (int i = 0; i < count; i++)
        {
            UnlockQueueItem item = unlockQueue.Dequeue();
            PreSaveUnlockState(item);

            if (true == item.isRegion)
            {
                StartRegionUnlockProduction(item.mapType);
            }
            else
            {
                StartSubRegionUnlockProduction(item.forestType);
            }
        }
    }

    private void PreSaveUnlockState(UnlockQueueItem _item)
    {
        if (true == _item.isRegion)
        {
            mapDataProvider.MarkMapUnlocked(_item.mapType);
            mapDataProvider.MarkMapUnlockAnimationPlayed(_item.mapType);
        }
        else
        {
            mapDataProvider.MarkUnlocked(_item.mapType, _item.forestType);
            mapDataProvider.MarkUnlockAnimationPlayed(_item.mapType, _item.forestType);
        }
    }

    private void StartRegionUnlockProduction(MapType _mapType)
    {
        if (null == navigation)
        {
            OnUnlockProductionComplete();
            return;
        }

        HUD_NavigationRegion targetRegion = navigation.GetRegionInstance(_mapType);
        if (null != targetRegion)
        {
            targetRegion.PlayUnlockProduction(onUnlockProductionCompleteCallback);
        }
        else
        {
            OnUnlockProductionComplete();
        }
    }

    private void StartSubRegionUnlockProduction(ForestType _forestType)
    {
        if (null == subField)
        {
            OnUnlockProductionComplete();
            return;
        }

        HUD_NavigationSubRegion targetSubRegion = subField.GetSubRegionInstance(_forestType);
        if (null != targetSubRegion)
        {
            targetSubRegion.PlayUnlockProduction(onUnlockProductionCompleteCallback);
        }
        else
        {
            OnUnlockProductionComplete();
        }
    }

    private void OnUnlockProductionComplete()
    {
        activeUnlockCount--;

        if (0 == activeUnlockCount)
        {
            isUnlockingProductionActive = false;
        }
    }

    private void HandleClose()
    {
        if (null != navigation)
        {
            navigation.ResetSelection();
        }

        if (null != subField)
        {
            subField.ResetSelection();
        }

        if (null != treeField)
        {
            treeField.ResetSelection();
        }

        if (null != prevButton)
        {
            prevButton.ResetAnimation();
        }

        if (null != homeButton)
        {
            homeButton.ResetAnimation();
        }

        if (null != cancelButton)
        {
            cancelButton.ResetAnimation();
        }

        if (null != selectButton)
        {
            selectButton.ResetAnimation();
        }

        if (null != omp)
        {
            omp.ResetAllMotions();
        }

        gameObject.SetActive(false);
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        if (true == isUnlockingProductionActive)
        {
            return;
        }

        if (null == mapDataProvider || null == subField || null == navigation)
        {
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        MapEnvironmentDataInfo targetInfo = default;
        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            if (db.mapDatas[i].mapType == _mapType)
            {
                targetInfo = db.mapDatas[i];
                break;
            }
        }

        if (null != targetInfo.forestDatas)
        {
            lastSelectedMapType = _mapType;
            pendingMapType = _mapType;
            pendingForestDatas = targetInfo.forestDatas;

            if (null != navigation)
            {
                navigation.ClearAllNewIndicators();
            }

            navigation.PlayDisappearAnimations(onDisappearCompleteCallback);
        }
    }

    private void OnDisappearComplete()
    {
        if (null == subField)
        {
            return;
        }

        currentState = NavigationState.SubRegion;

        mapDataProvider.MarkMapLevelAsRead(pendingMapType);

        subField.SetSubRegions(pendingMapType, pendingForestDatas);

        if (null != prevButton)
        {
            prevButton.PlayAppearAnimation(prevButtonAppearDelay);
        }

        if (null != homeButton)
        {
            homeButton.PlayAppearAnimation(homeButtonAppearDelay);
        }

        BuildSubRegionUnlockQueue(pendingMapType);
        if (unlockQueue.Count > 0)
        {
            ProcessNextUnlock();
        }
    }

    private void RestoreToHome()
    {
        currentState = NavigationState.Region;
        lastSelectedMapType = MapType.None;

        if (null != subField)
        {
            subField.ClearAllNewIndicators();
            subField.ResetSelection();
        }

        if (null != treeField)
        {
            treeField.ResetSelection();
        }

        if (null != prevButton)
        {
            prevButton.ResetAnimation();
        }

        if (null != homeButton)
        {
            homeButton.ResetAnimation();
        }

        if (null != selectButton)
        {
            selectButton.ResetAnimation();
        }

        if (null != navigation)
        {
            navigation.ResetSelection();
            navigation.PlayAppearAnimations();
        }
    }

    private void RestoreToSelectedRegion(MapType _mapType)
    {
        if (null == mapDataProvider || null == subField || null == navigation)
        {
            return;
        }

        currentState = NavigationState.SubRegion;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        MapEnvironmentDataInfo targetInfo = default;
        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            if (db.mapDatas[i].mapType == _mapType)
            {
                targetInfo = db.mapDatas[i];
                break;
            }
        }

        navigation.ResetSelection(false);
        navigation.SetSelectedMapTypeWithoutAnimation(_mapType);

        mapDataProvider.MarkMapLevelAsRead(_mapType);

        if (null != targetInfo.forestDatas)
        {
            pendingMapType = _mapType;
            pendingForestDatas = targetInfo.forestDatas;
            subField.SetSubRegions(_mapType, targetInfo.forestDatas);
        }

        if (null != prevButton)
        {
            prevButton.PlayAppearAnimation(prevButtonAppearDelay);
        }

        if (null != homeButton)
        {
            homeButton.PlayAppearAnimation(homeButtonAppearDelay);
        }

        if (null != selectButton)
        {
            selectButton.ResetAnimation();
        }

        BuildSubRegionUnlockQueue(_mapType);
        if (unlockQueue.Count > 0)
        {
            ProcessNextUnlock();
        }
    }

    private void HandlePrevClicked()
    {
        if (true == isUnlockingProductionActive)
        {
            return;
        }

        if (NavigationState.TreeField == currentState)
        {
            if (null != selectButton)
            {
                selectButton.ResetAnimation();
            }

            if (null != treeField)
            {
                treeField.PlayDisappearAnimations(onTreeFieldPrevDisappearCompleteCallback);
            }
        }
        else
        {
            RestoreToHome();
            onPrevCallback?.Invoke();
        }
    }

    private void HandleHomeClicked()
    {
        if (true == isUnlockingProductionActive)
        {
            return;
        }

        if (NavigationState.TreeField == currentState)
        {
            if (null != selectButton)
            {
                selectButton.ResetAnimation();
            }

            if (null != treeField)
            {
                treeField.PlayDisappearAnimations(onTreeFieldHomeDisappearCompleteCallback);
            }
        }
        else
        {
            RestoreToHome();
            onHomeCallback?.Invoke();
        }
    }

    private void HandleSubRegionSelected()
    {
        if (true == isUnlockingProductionActive)
        {
            return;
        }

        if (null == navigation || null == subField || null == treeField)
        {
            return;
        }

        ForestEnvironmentInfo forestInfo = subField.GetSelectedForestInfo();
        if (ForestType.None == forestInfo.forestType)
        {
            return;
        }

        if (null != subField)
        {
            subField.ClearAllNewIndicators();
        }

        subField.PlayDisappearAnimations(onSubRegionDisappearCompleteCallback);
    }

    private void HandleTreeSelected(TreeType _treeType)
    {
    }

    private void HandleSelectButtonClicked()
    {
        if (true == isUnlockingProductionActive)
        {
            return;
        }

        if (null == navigation || null == subField)
        {
            return;
        }

        MapType mapType = navigation.GetSelectedMapType();
        ForestType forestType = subField.GetSelectedForestType();

        if (MapType.None != mapType && ForestType.None != forestType)
        {
            mapSelectedEvent?.Invoke(mapType, forestType);
        }
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDestroy()
    {
        if (null != navigation)
        {
            navigation.regionSelectedEvent -= HandleRegionSelected;
        }

        if (null != subField)
        {
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
        }

        if (null != treeField)
        {
            treeField.treeSelectedEvent -= HandleTreeSelected;
        }
    }

    private void OnDisable()
    {
        if (null != omp)
        {
            omp.ResetAllMotions();
        }

        if (null != navigation)
        {
            navigation.ClearAllNewIndicators();
        }

        if (null != subField)
        {
            subField.ClearAllNewIndicators();
        }

        if (null != navigation)
        {
            navigation.ResetSelection();
        }

        if (null != subField)
        {
            subField.ResetSelection();
        }

        if (null != treeField)
        {
            treeField.ResetSelection();
        }

        if (null != prevButton)
        {
            prevButton.ResetAnimation();
        }

        if (null != homeButton)
        {
            homeButton.ResetAnimation();
        }

        if (null != cancelButton)
        {
            cancelButton.ResetAnimation();
        }

        if (null != selectButton)
        {
            selectButton.ResetAnimation();
        }
    }
}
