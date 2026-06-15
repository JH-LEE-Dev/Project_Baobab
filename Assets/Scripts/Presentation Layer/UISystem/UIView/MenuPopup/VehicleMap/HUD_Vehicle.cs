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

    // 내부 의존성
    private readonly Queue<UnlockQueueItem> unlockQueue = new Queue<UnlockQueueItem>();
    private bool isUnlockingProductionActive = false;

    private TweenCallback onDisappearCompleteCallback;
    private UnityEngine.Events.UnityAction onNavTopCompleteCallback;
    private UnityEngine.Events.UnityAction handleCloseCallback;
    private List<ForestEnvironmentInfo> pendingForestDatas;
    private MapType pendingMapType = MapType.None;
    private bool isUnlockStateSyncPending = true;

    private int activeUnlockCount = 0;
    private TweenCallback triggerAllUnlocksDirectlyCallback;
    private Action onUnlockProductionCompleteCallback;

    private readonly Dictionary<MapType, string> regionKeyMap = new Dictionary<MapType, string>();
    private readonly Dictionary<MapType, string> regionNewKeyMap = new Dictionary<MapType, string>();
    private readonly Dictionary<ForestType, string> subRegionKeyMap = new Dictionary<ForestType, string>();
    private readonly Dictionary<ForestType, string> subRegionNewKeyMap = new Dictionary<ForestType, string>();

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
    private const string unlockRegionFormat = "UnLock_Region_{0}";
    private const string newRegionFormat = "New_Region_{0}";
    private const string unlockSubRegionFormat = "UnLock_SubRegion_{0}";
    private const string newSubRegionFormat = "New_SubRegion_{0}";
    private const string visitedSubRegionFormat = "Visited_SubRegion_{0}";
    private const string regionUnlockOccurredKey = "RegionUnlockOccurred";
    private const float delayedCallDuration = 0.1f;
    private const int prefValueActive = 1;
    private const int prefValueInactive = 0;

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
        BuildKeyCaches();
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

        Close(true);
    }

    public void SyncUnlockStates()
    {
        isUnlockStateSyncPending = true;
        PlayerPrefs.SetInt(regionUnlockOccurredKey, prefValueInactive);
        PlayerPrefs.Save();
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

    private void BuildKeyCaches()
    {
        regionKeyMap.Clear();
        regionNewKeyMap.Clear();
        subRegionKeyMap.Clear();
        subRegionNewKeyMap.Clear();

        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            return;
        }

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType == MapType.Town)
            {
                continue;
            }

            regionKeyMap[regionInfo.mapType] = string.Format(unlockRegionFormat, regionInfo.mapType);
            regionNewKeyMap[regionInfo.mapType] = string.Format(newRegionFormat, regionInfo.mapType);

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    subRegionKeyMap[subInfo.forestType] = string.Format(unlockSubRegionFormat, subInfo.forestType);
                    subRegionNewKeyMap[subInfo.forestType] = string.Format(newSubRegionFormat, subInfo.forestType);
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

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType == MapType.Town)
            {
                continue;
            }

            string regionKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string rKey) ? rKey : string.Empty;
            string regionNewKey = regionNewKeyMap.TryGetValue(regionInfo.mapType, out string rnKey) ? rnKey : string.Empty;

            if (!string.IsNullOrEmpty(regionKey))
            {
                if (true == regionInfo.bCanAccess)
                {
                    if (true == isUnlockStateSyncPending)
                    {
                        PlayerPrefs.SetInt(regionKey, prefValueActive);
                    }
                }
                else
                {
                    PlayerPrefs.SetInt(regionKey, prefValueInactive);
                    PlayerPrefs.SetInt(regionNewKey, prefValueInactive);
                }
            }

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    string subKey = subRegionKeyMap.TryGetValue(subInfo.forestType, out string sKey) ? sKey : string.Empty;
                    string subNewKey = subRegionNewKeyMap.TryGetValue(subInfo.forestType, out string snKey) ? snKey : string.Empty;

                    if (!string.IsNullOrEmpty(subKey))
                    {
                        if (true == subInfo.bCanAccess)
                        {
                            if (true == isUnlockStateSyncPending)
                            {
                                PlayerPrefs.SetInt(subKey, prefValueActive);
                            }
                        }
                        else
                        {
                            PlayerPrefs.SetInt(subKey, prefValueInactive);
                            PlayerPrefs.SetInt(subNewKey, prefValueInactive);
                        }
                    }
                }
            }
        }

        isUnlockStateSyncPending = false;
        PlayerPrefs.Save();
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
            if (regionInfo.mapType == MapType.Town)
            {
                continue;
            }

            bool hasKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string regionKey);
            int regionPrefVal = true == hasKey ? PlayerPrefs.GetInt(regionKey, 0) : -999;
            bool isUnlockPending = regionInfo.bCanAccess && (regionPrefVal == 0);

            if (true == isUnlockPending)
            {
                return true;
            }
        }

        return false;
    }

    private MapType FindPendingSubRegionUnlockMapType()
    {
        if (prefValueActive == PlayerPrefs.GetInt(regionUnlockOccurredKey, prefValueInactive))
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

            string regionKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string rKey) ? rKey : string.Empty;
            if (string.IsNullOrEmpty(regionKey) || prefValueInactive == PlayerPrefs.GetInt(regionKey, prefValueInactive))
            {
                continue;
            }

            string visitedKey = string.Format(visitedSubRegionFormat, regionInfo.mapType);
            if (prefValueInactive == PlayerPrefs.GetInt(visitedKey, prefValueInactive))
            {
                continue;
            }

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    string subKey = subRegionKeyMap.TryGetValue(subInfo.forestType, out string sKey) ? sKey : string.Empty;
                    if (!string.IsNullOrEmpty(subKey))
                    {
                        if (true == subInfo.bCanAccess && prefValueInactive == PlayerPrefs.GetInt(subKey, prefValueInactive))
                        {
                            return regionInfo.mapType;
                        }
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
            if (regionInfo.mapType == MapType.Town)
            {
                continue;
            }

            string regionKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string rKey) ? rKey : string.Empty;
            if (string.IsNullOrEmpty(regionKey))
            {
                continue;
            }

            int regionPrefVal = PlayerPrefs.GetInt(regionKey, prefValueInactive);
            if (true == regionInfo.bCanAccess && prefValueInactive == regionPrefVal)
            {
                UnlockQueueItem item;
                item.isRegion = true;
                item.mapType = regionInfo.mapType;
                item.forestType = ForestType.None;
                unlockQueue.Enqueue(item);

                hasRegionUnlock = true;

                if (null != regionInfo.forestDatas)
                {
                    for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                    {
                        ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                        string subKey = subRegionKeyMap.TryGetValue(subInfo.forestType, out string sKey) ? sKey : string.Empty;
                        string subNewKey = subRegionNewKeyMap.TryGetValue(subInfo.forestType, out string snKey) ? snKey : string.Empty;

                        if (!string.IsNullOrEmpty(subKey))
                        {
                            if (true == subInfo.bCanAccess)
                            {
                                PlayerPrefs.SetInt(subKey, prefValueInactive);
                                PlayerPrefs.SetInt(subNewKey, prefValueInactive);
                            }
                            else
                            {
                                PlayerPrefs.SetInt(subKey, prefValueInactive);
                                PlayerPrefs.SetInt(subNewKey, prefValueInactive);
                            }
                        }
                    }
                    PlayerPrefs.Save();
                }
            }
        }

        if (true == hasRegionUnlock)
        {
            PlayerPrefs.SetInt(regionUnlockOccurredKey, prefValueActive);
            PlayerPrefs.Save();
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
                    bool hasKey = subRegionKeyMap.TryGetValue(subInfo.forestType, out string subKey);
                    int subPrefVal = true == hasKey ? PlayerPrefs.GetInt(subKey, 0) : -999;
                    bool isSubUnlockPending = subInfo.bCanAccess && (0 == subPrefVal);

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
        if (true == peekItem.isRegion && currentState != NavigationState.Region)
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
            string _regionKey = regionKeyMap.TryGetValue(_item.mapType, out string rKey) ? rKey : string.Empty;
            string _regionNewKey = regionNewKeyMap.TryGetValue(_item.mapType, out string rnKey) ? rnKey : string.Empty;
            if (!string.IsNullOrEmpty(_regionKey))
            {
                PlayerPrefs.SetInt(_regionKey, prefValueActive);
                PlayerPrefs.SetInt(_regionNewKey, prefValueActive);
                PlayerPrefs.Save();
            }
        }
        else
        {
            string _subKey = subRegionKeyMap.TryGetValue(_item.forestType, out string sKey) ? sKey : string.Empty;
            string _subNewKey = subRegionNewKeyMap.TryGetValue(_item.forestType, out string snKey) ? snKey : string.Empty;
            if (!string.IsNullOrEmpty(_subKey))
            {
                PlayerPrefs.SetInt(_subKey, prefValueActive);
                PlayerPrefs.SetInt(_subNewKey, prefValueActive);
                PlayerPrefs.Save();
            }
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

        string visitedKey = string.Format(visitedSubRegionFormat, pendingMapType);
        PlayerPrefs.SetInt(visitedKey, prefValueActive);
        PlayerPrefs.Save();

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

        string visitedKey = string.Format(visitedSubRegionFormat, _mapType);
        PlayerPrefs.SetInt(visitedKey, prefValueActive);
        PlayerPrefs.Save();

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
