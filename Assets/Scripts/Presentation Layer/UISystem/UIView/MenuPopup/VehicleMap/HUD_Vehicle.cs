using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_Vehicle : MonoBehaviour
{
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

    [Header("Blink Config")]
    [SerializeField] private float blinkDuration = 0.5f;
    [SerializeField] private Ease blinkEase = Ease.InOutSine;

    [Header("Appear Delays")]
    [SerializeField] private float prevButtonAppearDelay = 0.1f;
    [SerializeField] private float homeButtonAppearDelay = 0.2f;

    // 내부 의존성
    private struct UnlockQueueItem
    {
        public bool isRegion;
        public MapType mapType;
        public ForestType forestType;
    }

    private readonly Queue<UnlockQueueItem> unlockQueue = new Queue<UnlockQueueItem>();
    private bool isUnlockingProductionActive = false;
    private ForestType pendingUnlockSubRegionForestType = ForestType.None;

    public bool IsUnlockingProductionActive => isUnlockingProductionActive;

    private IMapDataProvider mapDataProvider;
    private Tweener blinkTween;
    private TweenCallback onDisappearCompleteCallback;
    private UnityEngine.Events.UnityAction onNavTopCompleteCallback;
    private UnityEngine.Events.UnityAction onControlPanelCompleteCallback;
    private UnityEngine.Events.UnityAction handleCloseCallback;
    private List<ForestEnvironmentInfo> pendingForestDatas;
    private MapType pendingMapType = MapType.None;
    private bool isBlinking = false;

    private UnlockQueueItem currentUnlockItem;
    private TweenCallback triggerPendingSubRegionUnlockCallback;
    private TweenCallback triggerRegionUnlockProductionCallback;
    private Action onUnlockProductionCompleteCallback;

    private readonly Dictionary<MapType, string> regionKeyMap = new Dictionary<MapType, string>();
    private readonly Dictionary<MapType, string> regionNewKeyMap = new Dictionary<MapType, string>();
    private readonly Dictionary<ForestType, string> subRegionKeyMap = new Dictionary<ForestType, string>();
    private readonly Dictionary<ForestType, string> subRegionNewKeyMap = new Dictionary<ForestType, string>();

    // 캐싱된 상수 및 리터럴 값
    private const float transparentAlpha = 0f;
    private const float blinkTargetAlpha = 1f;
    private const bool forceReset = true;

    private Action onPrevCallback;
    private Action onHomeCallback;
    private Action handlePrevClickedCallback;
    private Action handleHomeClickedCallback;
    private Action handleSelectButtonClickedCallback;
    private Action onSubRegionDisappearCompleteCallback;
    private Action onTreeFieldPrevDisappearCompleteCallback;
    private Action onTreeFieldHomeDisappearCompleteCallback;
    private enum NavigationState
    {
        Region,
        SubRegion,
        TreeField
    }

    private MapType lastSelectedMapType = MapType.None;
    private NavigationState currentState = NavigationState.Region;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider, Action _onPrev, Action _onHome, Action _onClose, LocalizationManager _localizeManager)
    {
        isBlinking = false;
        onDisappearCompleteCallback = OnDisappearComplete;
        onNavTopCompleteCallback = OnNavTopComplete;
        onControlPanelCompleteCallback = OnControlPanelComplete;
        handleCloseCallback = HandleClose;
        onPrevCallback = _onPrev;
        onHomeCallback = _onHome;
        handlePrevClickedCallback = HandlePrevClicked;
        handleHomeClickedCallback = HandleHomeClicked;
        handleSelectButtonClickedCallback = HandleSelectButtonClicked;
        onSubRegionDisappearCompleteCallback = OnSubRegionDisappearComplete;
        onTreeFieldPrevDisappearCompleteCallback = OnTreeFieldPrevDisappearComplete;
        onTreeFieldHomeDisappearCompleteCallback = OnTreeFieldHomeDisappearComplete;

        triggerPendingSubRegionUnlockCallback = TriggerPendingSubRegionUnlock;
        triggerRegionUnlockProductionCallback = TriggerRegionUnlockProduction;
        onUnlockProductionCompleteCallback = OnUnlockProductionComplete;

        mapDataProvider = _mapDataProvider;
        BuildKeyCaches();
        InitUnlockStates();

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);

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
            prevButton.Initialize(handlePrevClickedCallback);

        if (null != homeButton)
            homeButton.Initialize(handleHomeClickedCallback);

        if (null != cancelButton)
            cancelButton.Initialize(_onClose);

        if (null != selectButton)
            selectButton.Initialize(handleSelectButtonClickedCallback);

        if (null != omp)
            omp.Initialize();

        Close(true);
    }

    public void Open()
    {
        gameObject.SetActive(true);

        if (null != navigation)
            navigation.RefreshRegionLocks();

        omp.Play(navTopMotionTag, bReset: forceReset, _onComplete: onNavTopCompleteCallback);
        omp.Play(backgroundMotionTag, bReset: forceReset);
        omp.Play(controlBoardMotionTag, bReset: forceReset, _onComplete: onControlPanelCompleteCallback);
    }

    public void Close(bool _isSkip = false)
    {
        isBlinking = false;

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);

        if (null != navigation)
            navigation.ResetSelection();

        if (null != subField)
            subField.ResetSelection();

        if (null != treeField)
            treeField.ResetSelection();

        if (null != prevButton)
            prevButton.ResetAnimation();

        if (null != homeButton)
            homeButton.ResetAnimation();

        if (null != cancelButton)
            cancelButton.ResetAnimation();

        if (null != selectButton)
            selectButton.ResetAnimation();

        omp.PlayBackward(backgroundMotionTag, bReset: forceReset, _skip: _isSkip);
        
        omp.PlayBackward(controlBoardMotionTag, bReset: forceReset, 
            _skip: _isSkip, _isSkipCallback: true, _onComplete: handleCloseCallback);
    }

    private void OnSubRegionDisappearComplete()
    {
        currentState = NavigationState.TreeField;

        if (null != subField && null != treeField)
        {
            ForestEnvironmentInfo forestInfo = subField.GetSelectedForestInfo();
            treeField.SetTreeField(forestInfo);
        }

        if (null != navigation)
            navigation.SetMapNameTextToInformation();

        if (null != selectButton)
            selectButton.PlayAppearAnimation(selectorButtonAppearDelay);
    }

    private void OnTreeFieldPrevDisappearComplete()
    {
        currentState = NavigationState.SubRegion;
        if (null != subField)
            subField.SetSubRegions(pendingMapType, pendingForestDatas);

        if (null != navigation)
            navigation.SetSelectedMapTypeWithoutAnimation(pendingMapType);
    }

    private void OnTreeFieldHomeDisappearComplete()
    {
        RestoreToHome();
        onHomeCallback?.Invoke();
    }

    // 내부 로직
 
    private void OnNavTopComplete()
    {
        bool hasPendingRegionUnlock = CheckPendingRegionUnlocks();

        if (hasPendingRegionUnlock)
        {
            currentState = NavigationState.Region;
            lastSelectedMapType = MapType.None;

            if (null != navigation)
                navigation.PlayAppearAnimations();

            if (null != cancelButton)
                cancelButton.PlayAppearAnimation(selectorButtonAppearDelay);

            BuildRegionUnlockQueue();
            if (unlockQueue.Count > 0)
                ProcessNextUnlock();
        }
        else
        {
            if (MapType.None != lastSelectedMapType)
            {
                RestoreToSelectedRegion(lastSelectedMapType);

                if (null != cancelButton)
                    cancelButton.PlayAppearAnimation(homeButtonAppearDelay + 0.1f);
            }
            else
            {
                currentState = NavigationState.Region;

                if (null != navigation)
                    navigation.PlayAppearAnimations();

                if (null != cancelButton)
                    cancelButton.PlayAppearAnimation(selectorButtonAppearDelay);
            }
        }
    }
 
    private void OnControlPanelComplete()
    {
    }

    private void BuildKeyCaches()
    {
        regionKeyMap.Clear();
        regionNewKeyMap.Clear();
        subRegionKeyMap.Clear();
        subRegionNewKeyMap.Clear();

        if (null == mapDataProvider)
        {
            Debug.LogError("[HUD_Vehicle] BuildKeyCaches - mapDataProvider is NULL!");
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            Debug.LogError("[HUD_Vehicle] BuildKeyCaches - mapDatas is NULL!");
            return;
        }

        Debug.Log(string.Format("[HUD_Vehicle] BuildKeyCaches - db.mapDatas Count: {0}", db.mapDatas.Count));

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType == MapType.Town)
                continue;

            regionKeyMap[regionInfo.mapType] = string.Format("UnLock_Region_{0}", regionInfo.mapType);
            regionNewKeyMap[regionInfo.mapType] = string.Format("New_Region_{0}", regionInfo.mapType);
            Debug.Log(string.Format("[HUD_Vehicle] Cache added - Region: {0}, Key: {1}", regionInfo.mapType, regionKeyMap[regionInfo.mapType]));

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    subRegionKeyMap[subInfo.forestType] = string.Format("UnLock_SubRegion_{0}", subInfo.forestType);
                    subRegionNewKeyMap[subInfo.forestType] = string.Format("New_SubRegion_{0}", subInfo.forestType);
                    Debug.Log(string.Format("[HUD_Vehicle] Cache added - SubRegion: {0}, Key: {1}", subInfo.forestType, subRegionKeyMap[subInfo.forestType]));
                }
            }
        }

        Debug.Log(string.Format("[HUD_Vehicle] BuildKeyCaches Finished. regionKeyMap Count: {0}, subRegionKeyMap Count: {1}", 
            regionKeyMap.Count, subRegionKeyMap.Count));
    }



    private void InitUnlockStates()
    {
        if (null == mapDataProvider)
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

        bool isFirstInit = !PlayerPrefs.HasKey("Navigation_First_Init");

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType == MapType.Town)
                continue;

            string regionKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string rKey) ? rKey : string.Empty;
            string regionNewKey = regionNewKeyMap.TryGetValue(regionInfo.mapType, out string rnKey) ? rnKey : string.Empty;

            if (!string.IsNullOrEmpty(regionKey))
            {
                if (isFirstInit)
                {
                    if (regionInfo.bCanAccess)
                    {
                        PlayerPrefs.SetInt(regionKey, 1);
                        PlayerPrefs.SetInt(regionNewKey, 0);
                    }
                    else
                    {
                        PlayerPrefs.SetInt(regionKey, 0);
                        PlayerPrefs.SetInt(regionNewKey, 0);
                    }
                }
                else
                {
                    if (false == regionInfo.bCanAccess && PlayerPrefs.GetInt(regionKey, 0) == 1)
                    {
                        PlayerPrefs.SetInt(regionKey, 0);
                        PlayerPrefs.SetInt(regionNewKey, 0);
                        Debug.Log(string.Format("[HUD_Vehicle] Sync Rollback - Region {0} is locked in DB. Resetting PlayerPref UnLock key to 0.", regionInfo.mapType));
                    }
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
                        if (isFirstInit)
                        {
                            if (subInfo.bCanAccess)
                            {
                                PlayerPrefs.SetInt(subKey, 1);
                                PlayerPrefs.SetInt(subNewKey, 0);
                            }
                            else
                            {
                                PlayerPrefs.SetInt(subKey, 0);
                                PlayerPrefs.SetInt(subNewKey, 0);
                            }
                        }
                        else
                        {
                            if (false == subInfo.bCanAccess && PlayerPrefs.GetInt(subKey, 0) == 1)
                            {
                                PlayerPrefs.SetInt(subKey, 0);
                                PlayerPrefs.SetInt(subNewKey, 0);
                                Debug.Log(string.Format("[HUD_Vehicle] Sync Rollback - SubRegion {0} is locked in DB. Resetting PlayerPref UnLock key to 0.", subInfo.forestType));
                            }
                        }
                    }
                }
            }
        }

        if (isFirstInit)
        {
            PlayerPrefs.SetInt("Navigation_First_Init", 1);
            PlayerPrefs.Save();
        }
        else
        {
            PlayerPrefs.Save();
        }
    }

    private bool CheckPendingRegionUnlocks()
    {
        if (null == mapDataProvider)
        {
            Debug.LogError("[HUD_Vehicle] CheckPendingRegionUnlocks - mapDataProvider is NULL!");
            return false;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            Debug.LogError("[HUD_Vehicle] CheckPendingRegionUnlocks - mapDatas is NULL!");
            return false;
        }

        InitUnlockStates();

        Debug.Log("[HUD_Vehicle] CheckPendingRegionUnlocks - Starting Scan...");

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType == MapType.Town)
                continue;

            bool hasKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string regionKey);
            int regionPrefVal = hasKey ? PlayerPrefs.GetInt(regionKey, 0) : -999;
            bool isUnlockPending = regionInfo.bCanAccess && (regionPrefVal == 0);

            Debug.Log(string.Format("[HUD_Vehicle] Scan Region: {0} | bCanAccess: {1} | HasCacheKey: {2} | RegionKey: {3} | PlayerPrefVal: {4} | Pending?: {5}",
                regionInfo.mapType, regionInfo.bCanAccess, hasKey, regionKey ?? "NULL", regionPrefVal, isUnlockPending));

            if (isUnlockPending)
            {
                Debug.Log(string.Format("[HUD_Vehicle] Scan Region - Found pending unlock region: {0}", regionInfo.mapType));
                return true;
            }
        }

        Debug.Log("[HUD_Vehicle] CheckPendingRegionUnlocks - Scan Finished. No pending region unlocks found.");
        return false;
    }

    private void BuildRegionUnlockQueue()
    {
        unlockQueue.Clear();
        if (null == mapDataProvider)
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType == MapType.Town)
                continue;

            string regionKey = regionKeyMap.TryGetValue(regionInfo.mapType, out string rKey) ? rKey : string.Empty;
            if (string.IsNullOrEmpty(regionKey))
                continue;

            int regionPrefVal = PlayerPrefs.GetInt(regionKey, 0);
            if (regionInfo.bCanAccess && regionPrefVal == 0)
            {
                UnlockQueueItem item;
                item.isRegion = true;
                item.mapType = regionInfo.mapType;
                item.forestType = ForestType.None;
                unlockQueue.Enqueue(item);
                Debug.Log(string.Format("[HUD_Vehicle] Region {0} ENQUEUED to UnlockQueue.", regionInfo.mapType));

                if (null != regionInfo.forestDatas)
                {
                    for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                    {
                        ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                        string subKey = subRegionKeyMap.TryGetValue(subInfo.forestType, out string sKey) ? sKey : string.Empty;
                        string subNewKey = subRegionNewKeyMap.TryGetValue(subInfo.forestType, out string snKey) ? snKey : string.Empty;

                        if (!string.IsNullOrEmpty(subKey))
                        {
                            if (subInfo.bCanAccess)
                            {
                                PlayerPrefs.SetInt(subKey, 0);
                                PlayerPrefs.SetInt(subNewKey, 0);
                                Debug.Log(string.Format("[HUD_Vehicle] SubRegion {0} is kept locked (0) to trigger its own unlock animation on sub-region entry.", subInfo.forestType));
                            }
                            else
                            {
                                PlayerPrefs.SetInt(subKey, 0);
                                PlayerPrefs.SetInt(subNewKey, 0);
                            }
                        }
                    }
                    PlayerPrefs.Save();
                }
            }
        }
    }

    private void BuildSubRegionUnlockQueue(MapType _mapType)
    {
        unlockQueue.Clear();
        if (null == mapDataProvider)
        {
            Debug.LogError("[HUD_Vehicle] BuildSubRegionUnlockQueue - mapDataProvider is NULL!");
            return;
        }

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
        {
            Debug.LogError("[HUD_Vehicle] BuildSubRegionUnlockQueue - mapDatas is NULL!");
            return;
        }

        InitUnlockStates();

        Debug.Log(string.Format("[HUD_Vehicle] BuildSubRegionUnlockQueue - Starting Scan for Region {0}...", _mapType));

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo regionInfo = db.mapDatas[i];
            if (regionInfo.mapType != _mapType)
                continue;

            if (null != regionInfo.forestDatas)
            {
                for (int j = 0; j < regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo subInfo = regionInfo.forestDatas[j];
                    bool hasKey = subRegionKeyMap.TryGetValue(subInfo.forestType, out string subKey);
                    int subPrefVal = hasKey ? PlayerPrefs.GetInt(subKey, 0) : -999;
                    bool isSubUnlockPending = subInfo.bCanAccess && (subPrefVal == 0);

                    Debug.Log(string.Format("[HUD_Vehicle] Scan SubRegion: {0} | bCanAccess: {1} | HasCacheKey: {2} | SubKey: {3} | PlayerPrefVal: {4} | Pending?: {5}",
                        subInfo.forestType, subInfo.bCanAccess, hasKey, subKey ?? "NULL", subPrefVal, isSubUnlockPending));

                    if (isSubUnlockPending)
                    {
                        UnlockQueueItem item;
                        item.isRegion = false;
                        item.mapType = _mapType;
                        item.forestType = subInfo.forestType;
                        unlockQueue.Enqueue(item);
                        Debug.Log(string.Format("[HUD_Vehicle] SubRegion {0} ENQUEUED to UnlockQueue.", subInfo.forestType));
                    }
                }
            }
        }

        Debug.Log(string.Format("[HUD_Vehicle] BuildSubRegionUnlockQueue Finished. unlockQueue Count: {0}", unlockQueue.Count));
    }

    private void ProcessNextUnlock()
    {
        if (0 == unlockQueue.Count)
        {
            isUnlockingProductionActive = false;
            Debug.Log("[HUD_Vehicle] ProcessNextUnlock finished. No more unlocks in queue.");
            return;
        }

        isUnlockingProductionActive = true;
        currentUnlockItem = unlockQueue.Dequeue();

        Debug.Log(string.Format("[HUD_Vehicle] ProcessNextUnlock started. Dequeued item - IsRegion: {0}, MapType: {1}, ForestType: {2}", 
            currentUnlockItem.isRegion, currentUnlockItem.mapType, currentUnlockItem.forestType));

        if (currentUnlockItem.isRegion)
        {
            if (currentState != NavigationState.Region)
            {
                Debug.Log("[HUD_Vehicle] Current state is not Region. Forcing RestoreToHome().");
                RestoreToHome();
                DOVirtual.DelayedCall(0.1f, triggerRegionUnlockProductionCallback).SetEase(Ease.Linear);
            }
            else
            {
                StartRegionUnlockProduction(currentUnlockItem.mapType);
            }
        }
        else
        {
            StartSubRegionUnlockProduction(currentUnlockItem.forestType);
        }
    }

    private void StartRegionUnlockProduction(MapType _mapType)
    {
        if (null == navigation)
        {
            Debug.LogError("[HUD_Vehicle] StartRegionUnlockProduction - navigation is null!");
            ProcessNextUnlock();
            return;
        }

        HUD_NavigationRegion targetRegion = navigation.GetRegionInstance(_mapType);
        if (null != targetRegion)
        {
            Debug.Log(string.Format("[HUD_Vehicle] Playing unlock production for Region: {0}", _mapType));
            targetRegion.PlayUnlockProduction(onUnlockProductionCompleteCallback);
        }
        else
        {
            Debug.LogError(string.Format("[HUD_Vehicle] StartRegionUnlockProduction - Target HUD_NavigationRegion not found for: {0}!", _mapType));
            ProcessNextUnlock();
        }
    }

    private void StartSubRegionUnlockProduction(ForestType _forestType)
    {
        if (null == subField)
        {
            Debug.LogError("[HUD_Vehicle] StartSubRegionUnlockProduction - subField is null!");
            ProcessNextUnlock();
            return;
        }

        HUD_NavigationSubRegion targetSubRegion = subField.GetSubRegionInstance(_forestType);
        if (null != targetSubRegion)
        {
            Debug.Log(string.Format("[HUD_Vehicle] Playing unlock production for SubRegion: {0}", _forestType));
            targetSubRegion.PlayUnlockProduction(onUnlockProductionCompleteCallback);
        }
        else
        {
            Debug.LogError(string.Format("[HUD_Vehicle] StartSubRegionUnlockProduction - Target HUD_NavigationSubRegion not found for: {0}!", _forestType));
            ProcessNextUnlock();
        }
    }

    private void TriggerPendingSubRegionUnlock()
    {
        if (ForestType.None != pendingUnlockSubRegionForestType)
        {
            ForestType _targetForest = pendingUnlockSubRegionForestType;
            pendingUnlockSubRegionForestType = ForestType.None;
            StartSubRegionUnlockProduction(_targetForest);
        }
    }

    private void TriggerRegionUnlockProduction()
    {
        StartRegionUnlockProduction(currentUnlockItem.mapType);
    }

    private void OnUnlockProductionComplete()
    {
        Debug.Log(string.Format("[HUD_Vehicle] OnUnlockProductionComplete called for - IsRegion: {0}, MapType: {1}, ForestType: {2}", 
            currentUnlockItem.isRegion, currentUnlockItem.mapType, currentUnlockItem.forestType));

        if (currentUnlockItem.isRegion)
        {
            string _regionKey = regionKeyMap.TryGetValue(currentUnlockItem.mapType, out string rKey) ? rKey : string.Empty;
            string _regionNewKey = regionNewKeyMap.TryGetValue(currentUnlockItem.mapType, out string rnKey) ? rnKey : string.Empty;
            if (!string.IsNullOrEmpty(_regionKey))
            {
                PlayerPrefs.SetInt(_regionKey, 1);
                PlayerPrefs.SetInt(_regionNewKey, 1);
                PlayerPrefs.Save();
                Debug.Log(string.Format("[HUD_Vehicle] Saved UnLock_Region and New_Region key for {0}", currentUnlockItem.mapType));
            }

            if (null != navigation)
            {
                HUD_NavigationRegion _targetRegion = navigation.GetRegionInstance(currentUnlockItem.mapType);
                if (null != _targetRegion)
                    _targetRegion.SetNewIndicator(true);
            }
        }
        else
        {
            string _subKey = subRegionKeyMap.TryGetValue(currentUnlockItem.forestType, out string sKey) ? sKey : string.Empty;
            string _subNewKey = subRegionNewKeyMap.TryGetValue(currentUnlockItem.forestType, out string snKey) ? snKey : string.Empty;
            if (!string.IsNullOrEmpty(_subKey))
            {
                PlayerPrefs.SetInt(_subKey, 1);
                PlayerPrefs.SetInt(_subNewKey, 1);
                PlayerPrefs.Save();
                Debug.Log(string.Format("[HUD_Vehicle] Saved UnLock_SubRegion and New_SubRegion key for {0}", currentUnlockItem.forestType));
            }

            if (null != subField)
            {
                HUD_NavigationSubRegion _targetSubRegion = subField.GetSubRegionInstance(currentUnlockItem.forestType);
                if (null != _targetSubRegion)
                    _targetSubRegion.SetNewIndicator(true);
            }
        }

        ProcessNextUnlock();
    }
 
    private void HandleClose()
    {
        if (null != navigation)
            navigation.ResetSelection();
 
        if (null != subField)
            subField.ResetSelection();
 
        if (null != treeField)
            treeField.ResetSelection();
 
        if (null != prevButton)
            prevButton.ResetAnimation();
 
        if (null != homeButton)
            homeButton.ResetAnimation();
 
        if (null != cancelButton)
            cancelButton.ResetAnimation();
 
        if (null != selectButton)
            selectButton.ResetAnimation();
 
        if (null != omp)
            omp.ResetAllMotions();
 
        gameObject.SetActive(false);
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        if (true == isUnlockingProductionActive)
            return;

        if (null == mapDataProvider || null == subField || null == navigation)
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

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
            navigation.PlayDisappearAnimations(onDisappearCompleteCallback);
        }
    }

    private void OnDisappearComplete()
    {
        if (null == subField)
            return;

        currentState = NavigationState.SubRegion;

        subField.SetSubRegions(pendingMapType, pendingForestDatas);

        if (null != prevButton)
            prevButton.PlayAppearAnimation(prevButtonAppearDelay);

        if (null != homeButton)
            homeButton.PlayAppearAnimation(homeButtonAppearDelay);

        BuildSubRegionUnlockQueue(pendingMapType);
        if (unlockQueue.Count > 0)
            ProcessNextUnlock();
    }

    private void RestoreToHome()
    {
        currentState = NavigationState.Region;
        lastSelectedMapType = MapType.None;

        if (null != subField)
            subField.ResetSelection();

        if (null != treeField)
            treeField.ResetSelection();

        if (null != prevButton)
            prevButton.ResetAnimation();

        if (null != homeButton)
            homeButton.ResetAnimation();

        if (null != selectButton)
            selectButton.ResetAnimation();

        if (null != navigation)
        {
            navigation.ResetSelection();
            navigation.PlayAppearAnimations();
        }
    }

    private void RestoreToSelectedRegion(MapType _mapType)
    {
        if (null == mapDataProvider || null == subField || null == navigation)
            return;

        currentState = NavigationState.SubRegion;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

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

        if (null != targetInfo.forestDatas)
        {
            pendingMapType = _mapType;
            pendingForestDatas = targetInfo.forestDatas;
            subField.SetSubRegions(_mapType, targetInfo.forestDatas);
        }

        if (null != prevButton)
            prevButton.PlayAppearAnimation(prevButtonAppearDelay);

        if (null != homeButton)
            homeButton.PlayAppearAnimation(homeButtonAppearDelay);

        if (null != selectButton)
            selectButton.ResetAnimation();

        BuildSubRegionUnlockQueue(_mapType);
        if (unlockQueue.Count > 0)
            ProcessNextUnlock();
    }

    private void HandlePrevClicked()
    {
        if (true == isUnlockingProductionActive)
            return;

        if (NavigationState.TreeField == currentState)
        {
            if (null != selectButton)
                selectButton.ResetAnimation();
 
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
            return;

        if (NavigationState.TreeField == currentState)
        {
            if (null != selectButton)
                selectButton.ResetAnimation();
 
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
            return;

        if (null == navigation || null == subField || null == treeField)
            return;
 
        ForestEnvironmentInfo forestInfo = subField.GetSelectedForestInfo();
        if (ForestType.None == forestInfo.forestType)
            return;
 
        subField.PlayDisappearAnimations(onSubRegionDisappearCompleteCallback);
    }

    private void HandleTreeSelected(TreeType _treeType)
    {
    }

    private void HandleSelectButtonClicked()
    {
        if (true == isUnlockingProductionActive)
            return;

        if (null == navigation || null == subField)
            return;

        MapType mapType = navigation.GetSelectedMapType();
        ForestType forestType = subField.GetSelectedForestType();

        if (MapType.None != mapType && ForestType.None != forestType)
            mapSelectedEvent?.Invoke(mapType, forestType);
    }


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDestroy()
    {
        if (null != blinkTween && true == blinkTween.IsActive())
            blinkTween.Kill();

        if (null != navigation)
            navigation.regionSelectedEvent -= HandleRegionSelected;

        if (null != subField)
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;

        if (null != treeField)
            treeField.treeSelectedEvent -= HandleTreeSelected;
    }

    private void OnDisable()
    {
        isBlinking = false;

        if (null != blinkTween && true == blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);

        if (null != omp)
            omp.ResetAllMotions();

        if (null != navigation)
            navigation.ResetSelection();

        if (null != subField)
            subField.ResetSelection();

        if (null != treeField)
            treeField.ResetSelection();

        if (null != prevButton)
            prevButton.ResetAnimation();

        if (null != homeButton)
            homeButton.ResetAnimation();

        if (null != cancelButton)
            cancelButton.ResetAnimation();

        if (null != selectButton)
            selectButton.ResetAnimation();
    }
}
