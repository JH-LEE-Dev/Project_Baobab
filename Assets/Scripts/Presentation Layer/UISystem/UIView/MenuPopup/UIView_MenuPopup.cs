using System;
using PresentationLayer.UISystem.UIView.MenuPopup.Map;
using UnityEngine;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType, ForestType> DungeonSelectedEvent;

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

        CloseTeleportUI();
    }

    public void DependencyInjection(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider)
    {
        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        mapDataProvider = _mapDataProvider;

        if (null != mapSelector)
            mapSelector.Initialize(mapDataProvider, weatherProvider, timeDataProvider, HandleEnterDungeon, CloseTeleportUI);
    }

    private void HandleEnterDungeon(MapType _type, ForestType _forestType)
    {
        if (MapType.None == _type)
            return;

        Debug.Log($"[UIView_MenuPopup] Entering Dungeon: {_type} / {_forestType}");
        // 통신 및 던전 진입 로직 배치
        DungeonSelectedEvent?.Invoke(_type, _forestType);
        CloseTeleportUI();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public void TeleportUIOpen()
    {
        if (null != mapSelector)
            mapSelector.MapSelectorOpen();
    }

    public void CloseTeleportUI()
    {
        if (null != mapSelector)
            mapSelector.MapSelectorClose();
    }
}
