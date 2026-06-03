using System;
using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_Vehicle : MonoBehaviour
{
    // //이벤트
    public event Action<MapType, ForestType> MapSelectedEvent;

    // //외부 의존성
    [SerializeField] private Image lightImage;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private HUD_VehicleNavigation navigation;
    [SerializeField] private HUD_NavigationSubField subField;

    // //내부 의존성
    [SerializeField] private string blinkMotionTag = "Blink";
    private IMapDataProvider mapDataProvider;
    private bool isBlinking = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(IMapDataProvider _mapDataProvider)
    {
        isBlinking = false;
        mapDataProvider = _mapDataProvider;

        if (null != navigation)
        {
            navigation.Initialize(mapDataProvider);
            navigation.regionSelectedEvent -= HandleRegionSelected;
            navigation.regionSelectedEvent += HandleRegionSelected;
        }

        if (null != subField)
            subField.Initialize();

        if (null != omp)
            omp.Initialize();
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }


    // //내부 로직

    private void HandleInteractButtonClicked()
    {
        isBlinking = !isBlinking;

        if (null == omp)
            return;

        if (true == isBlinking)
            omp.Play(blinkMotionTag, bReset: true);
        else
        {
            omp.Stop(blinkMotionTag);
            omp.ResetAllMotions();
        }
    }

    private void HandleRegionSelected(MapType _mapType)
    {
        if (null == mapDataProvider || null == subField)
            return;

        MapEnvironmentDatabase db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == db.mapDatas)
            return;

        MapEnvironmentDataInfo targetInfo = default;
        bool isFound = false;
        for (int i = 0; i < db.mapDatas.Count; i++)
        {
            if (db.mapDatas[i].mapType == _mapType)
            {
                targetInfo = db.mapDatas[i];
                isFound = true;
                break;
            }
        }

        if (true == isFound && null != targetInfo.forestDatas)
            subField.SetSubRegions(targetInfo.forestDatas);
    }

    private void HandleConfirm()
    {
        if (null == navigation || null == subField)
            return;

        MapType mapType = navigation.GetSelectedMapType();
        ForestType forestType = subField.GetSelectedForestType();

        if (MapType.None != mapType && ForestType.None != forestType)
            MapSelectedEvent?.Invoke(mapType, forestType);
    }

    private void HandleMapSelected(MapType _mapType, ForestType _forestType)
    {
        MapSelectedEvent?.Invoke(_mapType, _forestType);
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void OnDestroy()
    {
        if (null != navigation)
            navigation.regionSelectedEvent -= HandleRegionSelected;
    }
}
