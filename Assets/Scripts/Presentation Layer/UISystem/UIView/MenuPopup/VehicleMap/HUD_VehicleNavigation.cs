using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class HUD_VehicleNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // //이벤트
    public event Action<MapType, ForestType> MapSelectedEvent;
    public event Action<MapType> regionSelectedEvent;

    // //외부 의존성
    [SerializeField] private GameObject regionContainer;
    [SerializeField] private GameObject regionPrefab;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private RectTransform dragAreaRect;
    [SerializeField] private RectTransform containerRect;

    // //내부 의존성
    private readonly List<HUD_NavigationRegion> spawnedRegions = new List<HUD_NavigationRegion>(8);
    private IMapDataProvider mapDataProvider;
    private MapType currentSelectedMapType = MapType.None;
    private bool isDragging;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider)
    {
        isDragging = false;
        mapDataProvider = _mapDataProvider;

        if (null != containerRect)
        {
            Vector2 initialPos = containerRect.anchoredPosition;
            initialPos.y = 0f;
            containerRect.anchoredPosition = initialPos;
        }

        SetupRegionsFromData();
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (null == dragAreaRect)
            return;

        isDragging = RectTransformUtility.RectangleContainsScreenPoint(
            dragAreaRect,
            _eventData.position,
            _eventData.pressEventCamera
        );
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

        float localDeltaY = currentLocalPoint.y - prevLocalPoint.y;

        float contentHeight = containerRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        float maxScrollY = Mathf.Max(0f, contentHeight - viewportHeight);

        Vector2 currentPosition = containerRect.anchoredPosition;
        currentPosition.y += localDeltaY;
        currentPosition.y = Mathf.Clamp(currentPosition.y, 0f, maxScrollY);

        containerRect.anchoredPosition = currentPosition;
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        isDragging = false;
    }

    public MapType GetSelectedMapType()
    {
        return currentSelectedMapType;
    }


    // //내부 로직

    private void SetupRegionsFromData()
    {
        if (null == mapDataProvider || null == regionContainer || null == regionPrefab)
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

            GameObject obj = Instantiate(regionPrefab, regionContainer.transform);
            if (null == obj)
                continue;

            HUD_NavigationRegion region = obj.GetComponent<HUD_NavigationRegion>();
            if (null != region)
            {
                region.Initialize(info.mapType, HandleRegionSelected);
                spawnedRegions.Add(region);
            }
        }
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        currentSelectedMapType = _mapType;
        regionSelectedEvent?.Invoke(_mapType);
    }
}
