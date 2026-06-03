using System;
using UnityEngine;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType, ForestType> DungeonSelectedEvent;
    public event Action TeleportUIClosedEvent;

    // //외부 의존성
    private IMapDataProvider mapDataProvider;
    private IWeatherProvider weatherProvider;
    private ITimeDataProvider timeDataProvider;

    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject vehiclePrefab;

    // //내부 의존성
    private HUD_Vehicle vehicle;


    // //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null == vehicle && null != vehiclePrefab)
            vehicle = Instantiate(vehiclePrefab, this.transform).GetComponent<HUD_Vehicle>();

        CloseTeleportUI();
    }

    public void DependencyInjection(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider)
    {
        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        mapDataProvider = _mapDataProvider;

        if (null != vehicle)
        {
            vehicle.Initialize(mapDataProvider);
            vehicle.MapSelectedEvent -= HandleEnterDungeon;
            vehicle.MapSelectedEvent += HandleEnterDungeon;
        }
    }

    public void TeleportUIOpen()
    {
        if (null != vehicle)
            vehicle.Open();
    }

    public void CloseTeleportUI()
    {
        if (null != vehicle)
        {
            vehicle.Close();
            TeleportUIClosedEvent?.Invoke();
        }
    }


    // //내부 로직

    private void HandleEnterDungeon(MapType _type, ForestType _forestType)
    {
        if (MapType.None == _type)
            return;

        // 통신 및 던전 진입 로직 배치
        DungeonSelectedEvent?.Invoke(_type, _forestType);
        CloseTeleportUI();
    }


    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    private void OnDestroy()
    {
        if (null != vehicle)
            vehicle.MapSelectedEvent -= HandleEnterDungeon;
    }
}
