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
    [SerializeField] private HUD_VehicleMapSelectorButton okButton;
    [SerializeField] private HUD_VehicleMapSelectorButton cancelButton;

    [SerializeField] private string blinkMotionTag = "Blink";
    [SerializeField] private string activeMotionTag = "Active";
    [SerializeField] private string deactiveMotionTag = "Deactive";

    // //내부 의존성
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
        {
            subField.Initialize();
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
            subField.subRegionSelectedEvent += HandleSubRegionSelected;
        }

        if (null != okButton)
        {
            okButton.Initialize(HandleConfirm);
            okButton.SetButtonActive(false, false);
        }

        if (null != cancelButton)
            cancelButton.Initialize(Close);

        if (null != omp)
            omp.Initialize();
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        isBlinking = false;

        if (null != omp)
        {
            omp.Stop(blinkMotionTag);
            omp.ResetAllMotions();
        }

        if (null != navigation)
            navigation.ResetSelection();

        if (null != subField)
            subField.ResetSelection();

        if (null != okButton)
            okButton.SetButtonActive(false, false);

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
            if (_mapType == db.mapDatas[i].mapType)
            {
                targetInfo = db.mapDatas[i];
                isFound = true;
                break;
            }
        }

        if (true == isFound && null != targetInfo.forestDatas)
            subField.SetSubRegions(_mapType, targetInfo.forestDatas);

        UpdateOkButtonState();
    }

    private void HandleSubRegionSelected()
    {
        UpdateOkButtonState();
    }

    private void UpdateOkButtonState()
    {
        if (null == okButton || null == navigation || null == subField)
            return;

        bool isRegionSelected = (MapType.None != navigation.GetSelectedMapType());
        bool isSubRegionSelected = (ForestType.None != subField.GetSelectedForestType());

        okButton.SetButtonActive(isRegionSelected && isSubRegionSelected, true);
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

        if (null != subField)
            subField.subRegionSelectedEvent -= HandleSubRegionSelected;
    }

    private void OnDisable()
    {
        isBlinking = false;

        if (null != omp)
        {
            omp.Stop(blinkMotionTag);
            omp.ResetAllMotions();
        }

        if (null != navigation)
            navigation.ResetSelection();

        if (null != subField)
            subField.ResetSelection();

        if (null != okButton)
            okButton.SetButtonActive(false, false);
    }
}
