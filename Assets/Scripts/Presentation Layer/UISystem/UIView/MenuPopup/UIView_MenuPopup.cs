using System;
using UnityEngine;
using DG.Tweening;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType, ForestType> DungeonSelectedEvent;
    public event Action TeleportUIClosedEvent;
    public event Action PrevButtonClickedEvent;
    public event Action HomeButtonClickedEvent;

    //외부 의존성
    private IMapDataProvider mapDataProvider;
    private IWeatherProvider weatherProvider;
    private ITimeDataProvider timeDataProvider;

    //내부 의존성
    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject vehiclePrefab;
    private HUD_Vehicle vehicle;
    private bool isInitialOpen = false;

    [Header("Open Delay Settings")]
    [SerializeField] private float vehicleOpenDelay = 0.5f;

    private Tween vehicleOpenTween;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null == vehicle && null != vehiclePrefab)
           vehicle = Instantiate(vehiclePrefab, _ctx.screenSpaceCanvas.transform).GetComponent<HUD_Vehicle>();
    }

    public void DependencyInjection(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider)
    {
        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        mapDataProvider = _mapDataProvider;

        if (null != vehicle)
        {
            vehicle.Initialize(mapDataProvider, HandlePrev, HandleHome, OnHide, viewCtx.localizationManager);
            vehicle.mapSelectedEvent -= HandleEnterDungeon;
            vehicle.mapSelectedEvent += HandleEnterDungeon;
            vehicle.Close(true);
        }
    }

    private void HandlePrev()
    {
        PrevButtonClickedEvent?.Invoke();
    }

    private void HandleHome()
    {
        HomeButtonClickedEvent?.Invoke();
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

        if (false == isInitialOpen)
        {
            isInitialOpen = true;
            return;
        }

        if (null != vehicleOpenTween && vehicleOpenTween.IsActive())
            vehicleOpenTween.Kill();

        if (null != vehicle)
        {
            if (vehicleOpenDelay > 0f)
            {
                vehicleOpenTween = DOVirtual.DelayedCall(vehicleOpenDelay, () =>
                {
                    if (null != vehicle)
                        vehicle.Open();
                });
            }
            else
            {
                vehicle.Open();
            }
        }
    }

    public override void Hide()
    {
        if (null != vehicle && vehicle.IsUnlockingProductionActive)
            return;

        base.Hide();
    }

    protected override void OnHide()
    {
        base.OnHide();

        if (null != vehicleOpenTween && vehicleOpenTween.IsActive())
            vehicleOpenTween.Kill();

        if (null != vehicle)
        {
            vehicle.Close();
            TeleportUIClosedEvent?.Invoke();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (null != vehicleOpenTween && vehicleOpenTween.IsActive())
            vehicleOpenTween.Kill();

        if (null != vehicle)
            vehicle.mapSelectedEvent -= HandleEnterDungeon;
    }

    public override void Refresh()
    {
        base.Refresh();

        if (null != vehicle)
            vehicle.SyncUnlockStates();
    }
}
