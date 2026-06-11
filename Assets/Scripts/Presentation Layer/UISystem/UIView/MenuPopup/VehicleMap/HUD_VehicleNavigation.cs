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

    // 내부 의존성
    private readonly List<HUD_NavigationRegion> spawnedRegions = new List<HUD_NavigationRegion>(8);
    private IMapDataProvider mapDataProvider;
    private LocalizationManager localizationManager;
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


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider, LocalizationManager _localizeManager)
    {
        isDragging = false;
        mapDataProvider = _mapDataProvider;
        localizationManager = _localizeManager;

        if (null != localizationManager)
        {
            if (null != localizationMapping)
            {
                localizationManager.LoadMappingData(localizationMapping);
            }
        }

        if (null != mapNameOmp)
            mapNameOmp.Initialize();

        isYPositionCached = false;
        onRegionDisappearCallback = OnRegionDisappearComplete;
        onUpButtonPressStateChangedCallback = OnUpButtonPressStateChanged;
        onDownButtonPressStateChangedCallback = OnDownButtonPressStateChanged;
        onRegionSelectedCallback = HandleRegionSelected;
        playMapNameChangeMotionCallback = PlayMapNameChangeMotion;

        if (null != mapNameText)
            defaultMapNameText = mapNameText.text;

        UpdateMapNameText(MapType.None);

        SetupRegionsFromData();

        if (null != upButton)
            upButton.Initialize(onUpButtonPressStateChangedCallback);

        if (null != downButton)
            downButton.Initialize(onDownButtonPressStateChangedCallback);
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        // 런타임 Overlay 캔버스 모드 오작동 방지를 위해 Graphic 레이캐스트가 감지한 OnBeginDrag 이벤트를 즉각 신뢰하여 드래그 개시
        isDragging = true;
        dragVelocityY = 0f;

        if (null != containerRect && null != viewportRect)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, _eventData.position, _eventData.pressEventCamera, out Vector2 localPoint))
            {
                startMousePos = localPoint;
                startAnchoredPos = containerRect.anchoredPosition;
                targetY = startAnchoredPos.y;
            }
        }
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (false == isDragging)
            return;

        if (null == containerRect || null == viewportRect)
            return;

        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewportRect,
            _eventData.position,
            _eventData.pressEventCamera,
            out Vector2 currentLocalPoint))
            return;

        if (false == RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewportRect,
            _eventData.position - _eventData.delta,
            _eventData.pressEventCamera,
            out Vector2 prevLocalPoint))
            return;

        // 드래그 시작 위치 대비 마우스의 이동량(Y) 연산 및 감도 적용
        float deltaY = (currentLocalPoint.y - startMousePos.y) * scrollSensitivity;

        // Content 높이와 Viewport 높이 기준 스크롤 가능 한계선(Range) 계산
        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        
        // 화면을 벗어날 만큼의 개수가 없으면 스크롤을 막아 상단 고정
        float scrollRange = 0f;
        if (viewportHeight < contentHeight)
            scrollRange = contentHeight - viewportHeight + extraBottomPadding;

        // 에디터에 디자인된 초기 위치(initialY)를 기준으로 스크롤 범위 클램핑 적용
        float minScrollY = initialY;
        float maxScrollY = initialY + scrollRange;

        float nextTargetY = startAnchoredPos.y + deltaY;
        targetY = Mathf.Clamp(nextTargetY, minScrollY, maxScrollY);

        // 물리 마우스 이동량에 기반하여 관성용 프레임 속도 연산 (SmoothDamp의 추종 오차가 아닌 순수 마우스 물리 속도 반영)
        if (0f < Time.deltaTime)
        {
            float localDeltaY = currentLocalPoint.y - prevLocalPoint.y;
            float rawVelocity = (localDeltaY * scrollSensitivity) / Time.deltaTime;
            
            // 과도한 속도로 끝까지 날아가는 스냅 현상 방지를 위해 최대속도 제한(Clamp) 처리
            dragVelocityY = Mathf.Clamp(rawVelocity, -maxDragVelocity, maxDragVelocity);
        }
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        isDragging = false;
    }

    public void OnScroll(PointerEventData _eventData)
    {
        if (null == containerRect || null == viewportRect)
            return;

        // 마우스 휠을 굴릴 때도 즉각적으로 관성 속도를 리셋하여 휠 제어력을 높임
        dragVelocityY = 0f;

        // 휠을 위로 굴리면 Y 감소(내용 내림), 아래로 굴리면 Y 증가(내용 올림)
        float scrollAmount = -_eventData.scrollDelta.y * wheelSensitivity;

        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        
        // 화면을 벗어날 만큼의 개수가 없으면 스크롤을 막아 상단 고정
        float scrollRange = 0f;
        if (viewportHeight < contentHeight)
            scrollRange = contentHeight - viewportHeight + extraBottomPadding;

        float minScrollY = initialY;
        float maxScrollY = initialY + scrollRange;

        targetY = Mathf.Clamp(targetY + scrollAmount, minScrollY, maxScrollY);
    }

    public void SetSelectedMapTypeWithoutAnimation(MapType _mapType)
    {
        currentSelectedMapType = _mapType;
        UpdateMapNameText(_mapType);

        for (int i = 0; i < spawnedRegions.Count; i++)
            if (null != spawnedRegions[i])
                spawnedRegions[i].SetSelect(_mapType == spawnedRegions[i].GetMapType());
    }

    public void ResetSelection(bool _updateText = true)
    {
        isTransitioning = false;
        currentSelectedMapType = MapType.None;

        if (true == _updateText)
            UpdateMapNameText(MapType.None);

        for (int i = 0; i < spawnedRegions.Count; i++)
            if (null != spawnedRegions[i])
            {
                spawnedRegions[i].gameObject.SetActive(true);
                spawnedRegions[i].SetSelect(false);
                spawnedRegions[i].ClearEntry();
                spawnedRegions[i].ResetAnimation();
            }

        if (null != upButton)
            upButton.ResetAnimation();

        if (null != downButton)
            downButton.ResetAnimation();

        // 지연 캐싱이 성공적으로 완료되었을 때에만 Y축 정렬 위치 복구 수행
        if (true == isYPositionCached)
        {
            targetY = initialY;
            if (null != containerRect)
                containerRect.anchoredPosition = new Vector2(containerRect.anchoredPosition.x, initialY);
        }
    }

    public void PlayAppearAnimations()
    {
        if (null != upButton)
            upButton.PlayAppearAnimation();

        if (null != downButton)
            downButton.PlayAppearAnimation();

        for (int i = 0; i < spawnedRegions.Count; i++)
            if (null != spawnedRegions[i])
            {
                spawnedRegions[i].gameObject.SetActive(true);
                spawnedRegions[i].PlayAppearAnimation(i * appearDelayGap);
            }
    }

    public void PlayDisappearAnimations(TweenCallback _onComplete)
    {
        if (true == isTransitioning)
            return;

        isTransitioning = true;

        int activeCount = 0;
        for (int i = 0; i < spawnedRegions.Count; i++)
            if (null != spawnedRegions[i])
                activeCount++;

        if (0 == activeCount)
        {
            if (null != upButton)
                upButton.gameObject.SetActive(false);
            if (null != downButton)
                downButton.gameObject.SetActive(false);
            _onComplete?.Invoke();
            return;
        }

        onAllDisappearComplete = _onComplete;
        disappearCompletedCount = 0;
        disappearActiveCount = activeCount;

        if (null != upButton)
            upButton.PlayDisappearAnimation(null);
        if (null != downButton)
            downButton.PlayDisappearAnimation(null);

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
            return;
 
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
                    mapNameChangeTween.Kill();
                mapNameChangeTween = DOVirtual.DelayedCall(0.05f, playMapNameChangeMotionCallback).SetEase(Ease.Linear);
            }
        }
    }

    private void PlayMapNameChangeMotion()
    {
        if (null != mapNameOmp)
            mapNameOmp.Play(mapNameChangeMotionTag, bReset: true);
    }

    // 내부 로직
 
    private void OnRegionDisappearComplete()
    {
        disappearCompletedCount++;
        if (disappearActiveCount == disappearCompletedCount)
        {
            for (int j = 0; j < spawnedRegions.Count; j++)
                if (null != spawnedRegions[j])
                    spawnedRegions[j].gameObject.SetActive(false);

            isTransitioning = false;
            onAllDisappearComplete?.Invoke();
            onAllDisappearComplete = null;
        }
    }

    private void ScrollByStep(bool _isUp)
    {
        if (null == containerRect || null == viewportRect)
            return;

        // 버튼 클릭 시에도 기존 관성 속도를 강제로 정지
        dragVelocityY = 0f;

        float stepSize = GetScrollStepSize();
        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;

        float scrollRange = 0f;
        if (viewportHeight < contentHeight)
            scrollRange = contentHeight - viewportHeight + extraBottomPadding;

        float minScrollY = initialY;
        float maxScrollY = initialY + scrollRange;

        // Up 버튼 클릭 시 Y 감소(목록 위로 올림), Down 버튼 클릭 시 Y 증가(목록 아래로 내림)
        float direction = true == _isUp ? -1f : 1f;
        targetY = Mathf.Clamp(targetY + direction * stepSize, minScrollY, maxScrollY);
    }

    private float GetScrollStepSize()
    {
        if (null == spawnedRegions || 0 == spawnedRegions.Count)
            return defaultScrollStepSize;

        float elementHeight = defaultElementHeight;
        HUD_NavigationRegion firstRegion = spawnedRegions[0];
        if (null != firstRegion)
        {
            RectTransform rectTrans = firstRegion.GetComponent<RectTransform>();
            if (null != rectTrans)
                elementHeight = rectTrans.rect.height;
        }

        float spacing = 0f;
        if (null != containerRect)
        {
            VerticalLayoutGroup layoutGroup = containerRect.GetComponent<VerticalLayoutGroup>();
            if (null != layoutGroup)
                spacing = layoutGroup.spacing;
        }

        return elementHeight + spacing;
    }

    private void HandleButtonPressStateChanged(bool _isUp, bool _isPressed)
    {
        if (true == _isUp)
            isUpButtonPressed = _isPressed;
        else
            isDownButtonPressed = _isPressed;

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
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

        for (int i = 0; i < spawnedRegions.Count; i++)
            if (null != spawnedRegions[i])
                Destroy(spawnedRegions[i].gameObject);
        spawnedRegions.Clear();

        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo info = db.mapDatas[i];

            if (MapType.Town == info.mapType)
                continue;

            GameObject obj = Instantiate(regionPrefab, containerRect);
            if (null == obj)
                continue;

            HUD_NavigationRegion region = obj.GetComponent<HUD_NavigationRegion>();
            if (null != region)
            {
                region.Initialize(info.mapType, onRegionSelectedCallback, localizationManager);

                // 각 맵의 서브 리전들 중 접근 가능한 지역(bCanAccess == true)이 단 하나라도 없다면 락 처리
                bool isMapLocked = true;
                if (null != info.forestDatas)
                {
                    for (int j = 0; info.forestDatas.Count > j; j++)
                    {
                        if (true == info.forestDatas[j].bCanAccess)
                        {
                            isMapLocked = false;
                            break;
                        }
                    }
                }

                region.SetLock(isMapLocked);
                spawnedRegions.Add(region);
            }
        }

        // 레이아웃이 즉각 갱신되어 드래그 스크롤 범위를 오차 없이 실시간 연산하도록 강제 빌드
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
    }

    public MapType GetSelectedMapType()
    {
        return currentSelectedMapType;
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        currentSelectedMapType = _mapType;
        regionSelectedEvent?.Invoke(_mapType);
        UpdateMapNameText(_mapType);

        // 생성된 모든 리전 버튼을 순회하며 포커스 상태(SetSelect)를 동적으로 업데이트
        for (int i = 0; i < spawnedRegions.Count; i++)
            if (null != spawnedRegions[i])
                spawnedRegions[i].SetSelect(_mapType == spawnedRegions[i].GetMapType());
    }

    private void UpdateMapNameText(MapType _mapType)
    {
        if (null == mapNameText)
            return;

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
                _newText = defaultMapNameText;
            else
                _newText = GetMapTypeString(_mapType);
        }

        if (_newText != mapNameText.text)
        {
            mapNameText.text = _newText;

            if (null != mapNameOmp)
            {
                mapNameOmp.ResetAllMotions();
                if (null != mapNameChangeTween && true == mapNameChangeTween.IsActive())
                    mapNameChangeTween.Kill();
                mapNameChangeTween = DOVirtual.DelayedCall(0.05f, playMapNameChangeMotionCallback).SetEase(Ease.Linear);
            }
        }
    }

    private string GetMapTypeString(MapType _type) => _type switch
    {
        MapType.Town => townString,
        MapType.VegetatedForest => plainsString,
        MapType.MongleSporeForest => forestString,
        _ => noneString
    };


    // 유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Update()
    {
        if (null == containerRect)
            return;

        // 첫 번째 활성 렌더 갱신 프레임에 단 1회 지연 계측하여 프리팹 원본 오프셋 Y 보존
        if (false == isYPositionCached)
        {
            initialY = containerRect.anchoredPosition.y;
            targetY = initialY;
            isYPositionCached = true;
        }

        // 꾹 누르기(Long Press) 스크롤 상태 처리
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

        // 드래그가 종료되었을 때, 남은 속도가 있다면 관성 미끄러짐 적용
        if (false == isDragging && true == inertiaEnabled && inertiaStopThreshold < Mathf.Abs(dragVelocityY))
        {
            // 프레임 타임 기준 감속율 적용
            dragVelocityY *= Mathf.Pow(decelerationRate, Time.deltaTime * targetFrameRate);

            float contentHeight = containerRect.rect.height;
            float viewportHeight = viewportRect.rect.height;
            
            // 화면을 벗어날 만큼의 개수가 없으면 스크롤을 막아 상단 고정
            float scrollRange = 0f;
            if (viewportHeight < contentHeight)
                scrollRange = contentHeight - viewportHeight + extraBottomPadding;

            float minScrollY = initialY;
            float maxScrollY = initialY + scrollRange;

            targetY += dragVelocityY * Time.deltaTime;
            targetY = Mathf.Clamp(targetY, minScrollY, maxScrollY);

            // 속도가 임계값 이하로 줄어들면 완전히 관성 중지
            if (velocityStopThreshold > Mathf.Abs(dragVelocityY))
                dragVelocityY = 0f;
        }

        // 보간 처리가 필요한 조건 감지 (드래그 중이거나, 관성이 돌거나, 타겟 위치와 현재 위치의 오차가 존재할 때)
        // 미세 오차 시에는 대입 연산을 멈추어 유니티 레이아웃 시스템을 보존
        float diffY = Mathf.Abs(containerRect.anchoredPosition.y - targetY);
        if (true == isDragging || inertiaStopThreshold < Mathf.Abs(dragVelocityY) || positionTolerance < diffY)
        {
            float newY = Mathf.SmoothDamp(containerRect.anchoredPosition.y, targetY, ref currentYVelocity, smoothTime);
            containerRect.anchoredPosition = new Vector2(containerRect.anchoredPosition.x, newY);
        }
        else
        {
            // 움직임이 완료되었을 때는 속도를 완전히 0으로 고정
            currentYVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        if (null != mapNameChangeTween && true == mapNameChangeTween.IsActive())
            mapNameChangeTween.Kill();
    }
}
