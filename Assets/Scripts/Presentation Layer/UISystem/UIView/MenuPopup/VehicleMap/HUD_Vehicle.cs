using System;
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
    private IMapDataProvider mapDataProvider;
    private Tweener blinkTween;
    private TweenCallback onDisappearCompleteCallback;
    private UnityEngine.Events.UnityAction onNavTopCompleteCallback;
    private UnityEngine.Events.UnityAction onControlPanelCompleteCallback;
    private UnityEngine.Events.UnityAction handleCloseCallback;
    private System.Collections.Generic.List<ForestEnvironmentInfo> pendingForestDatas;
    private MapType pendingMapType = MapType.None;
    private bool isBlinking = false;

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

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (null != lightImage)
            lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);

        mapDataProvider = _mapDataProvider;

        if (null != navigation)
        {
            navigation.Initialize(mapDataProvider, _localizeManager);
            navigation.regionSelectedEvent -= HandleRegionSelected;
            navigation.regionSelectedEvent += HandleRegionSelected;
        }

        if (null != subField)
        {
            subField.Initialize();
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
            subField.subRegionSelectedEvent += HandleSubRegionSelected;
        }

        if (null != treeField)
        {
            treeField.Initialize();
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
        omp.PlayBackward(controlBoardMotionTag, bReset: forceReset, _skip: _isSkip, _onComplete: handleCloseCallback);
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
 
    private void OnControlPanelComplete()
    {
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

    private void HandleInteractButtonClicked()
    {
        isBlinking = !isBlinking;

        if (null != blinkTween && blinkTween.IsActive())
            blinkTween.Kill();

        if (true == isBlinking)
        {
            if (null != lightImage)
            {
                lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);
                blinkTween = lightImage.DOColor(new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, blinkTargetAlpha), blinkDuration)
                                       .SetLoops(-1, LoopType.Yoyo)
                                       .SetEase(blinkEase);
            }
        }
        else
        {
            if (null != lightImage)
                lightImage.color = new Color(lightImage.color.r, lightImage.color.g, lightImage.color.b, transparentAlpha);
        }
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        if (null == mapDataProvider || null == subField || null == navigation)
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

        MapEnvironmentDataInfo targetInfo = default;
        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            if (_mapType == db.mapDatas[i].mapType)
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
            if (_mapType == db.mapDatas[i].mapType)
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
    }

    private void HandlePrevClicked()
    {
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

        if (null != blinkTween && blinkTween.IsActive())
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
