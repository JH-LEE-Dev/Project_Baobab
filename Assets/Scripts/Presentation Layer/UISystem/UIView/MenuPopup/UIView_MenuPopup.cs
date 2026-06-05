using System;
using PresentationLayer.UISystem.UIView.MenuPopup.Map;
using UnityEngine;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType, ForestType> DungeonSelectedEvent;
    public event Action TeleportUIClosedEvent;

    //외부 의존성
    private IMapDataProvider mapDataProvider;
    private IWeatherProvider weatherProvider;
    private ITimeDataProvider timeDataProvider;

    //내부 의존성

    // //외부 의존성
    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject mapSelectorPrefab;

    // //내부 의존성
    private HUD_MapSelector mapSelector;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null == mapSelector && null != mapSelectorPrefab)
            mapSelector = Instantiate(mapSelectorPrefab, this.transform).GetComponent<HUD_MapSelector>();

        OnHide();
    }

    public void DependencyInjection(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider)
    {
        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        mapDataProvider = _mapDataProvider;

        if (null != mapSelector)
            mapSelector.Initialize(mapDataProvider, weatherProvider, timeDataProvider, HandleEnterDungeon, OnHide);
    }

    private void HandleEnterDungeon(MapType _type, ForestType _forestType)
    {
        if (MapType.None == _type)
            return;

        // 통신 및 던전 진입 로직 배치
        DungeonSelectedEvent?.Invoke(_type, _forestType);
        OnHide();
    }

    protected override void OnShow()
    {
        base.OnShow();

        if (null != mapSelector)
            mapSelector.MapSelectorOpen();
    }

    protected override void OnHide()
    {
        base.OnHide();

        if (null != mapSelector)
        {
            mapSelector.MapSelectorClose();
            TeleportUIClosedEvent?.Invoke();
        }
    }
}
