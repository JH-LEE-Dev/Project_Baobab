using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType, ForestType> DungeonSelectedEvent;
    public event Action TeleportUIClosedEvent;
    public event Action PrevButtonClickedEvent;
    public event Action HomeButtonClickedEvent;
    public event Action CancelButtonClickedEvent;

    // popupNavMain.OnUnlockProductionStarted/Ended를 그대로 상위로 릴레이하는 이벤트.
    // (popupNavMain이 private 필드라 외부에서 직접 구독할 방법이 없어 최소한의 릴레이만 추가)
    public event Action UnlockProductionStartedEvent;
    public event Action UnlockProductionEndedEvent;

    //외부 의존성
    private IMapDataProvider mapDataProvider;
    private IWeatherProvider weatherProvider;
    private ITimeDataProvider timeDataProvider;

    //내부 의존성
    [Header("Sub UI Prefabs")]
    // [기존 3-Depth Vehicle UI 주석 처리]
    // [SerializeField] private GameObject vehiclePrefab;
    // private HUD_Vehicle vehicle;

    [Tooltip("신규 1-Depth 내비게이션 팝업 프리팹")]
    [SerializeField] private GameObject popupNavPrefab;
    private HUD_PopupNav_Main popupNavMain;

    private bool isInitialOpen = false;

    [Header("Open Delay Settings")]
    [SerializeField] private float vehicleOpenDelay = 0f;

    private Coroutine vehicleOpenCoroutine;
    private readonly Dictionary<float, WaitForSeconds> waitCache = new Dictionary<float, WaitForSeconds>(4);

    private WaitForSeconds GetWaitForSeconds(float _seconds)
    {
        if (false == waitCache.TryGetValue(_seconds, out WaitForSeconds _w))
        {
            _w = new WaitForSeconds(_seconds);
            waitCache.Add(_seconds, _w);
        }
        return _w;
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        // [기존 시스템 주석 처리]
        // if (null == vehicle && null != vehiclePrefab)
        //    vehicle = Instantiate(vehiclePrefab, _ctx.screenSpaceCanvas.transform).GetComponent<HUD_Vehicle>();

        // [신규 1-Depth 내비게이션 동기화]
        if (null == popupNavMain && null != popupNavPrefab)
        {
            GameObject _obj = Instantiate(popupNavPrefab, _ctx.screenSpaceCanvas.transform);
            if (null != _obj)
            {
                popupNavMain = _obj.GetComponent<HUD_PopupNav_Main>();
            }
        }
    }

    public void DependencyInjection(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider)
    {
        weatherProvider = _weatherProvider;
        timeDataProvider = _timeDataProvider;
        mapDataProvider = _mapDataProvider;

        // [기존 시스템 주석 처리]
        /*
        if (null != vehicle)
        {
            vehicle.Initialize(mapDataProvider, HandlePrev, HandleHome, HandleCancelClicked, viewCtx.localizationManager);
            vehicle.mapSelectedEvent -= HandleEnterDungeon;
            vehicle.mapSelectedEvent += HandleEnterDungeon;
            vehicle.Close(true);
        }
        */

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain)
        {
            popupNavMain.Initialize(mapDataProvider, viewCtx.localizationManager, HandlePopupNavClosed, HandleEnterDungeon);

            popupNavMain.OnUnlockProductionStarted -= HandleUnlockProductionStarted;
            popupNavMain.OnUnlockProductionStarted += HandleUnlockProductionStarted;
            popupNavMain.OnUnlockProductionEnded -= HandleUnlockProductionEnded;
            popupNavMain.OnUnlockProductionEnded += HandleUnlockProductionEnded;

            popupNavMain.Close(true);
        }
    }

    private void HandleUnlockProductionStarted()
    {
        UnlockProductionStartedEvent?.Invoke();
    }

    private void HandleUnlockProductionEnded()
    {
        UnlockProductionEndedEvent?.Invoke();
    }

    private void HandlePrev()
    {
        PrevButtonClickedEvent?.Invoke();
    }

    private void HandleHome()
    {
        HomeButtonClickedEvent?.Invoke();
    }

    private void HandleCancelClicked()
    {
        CancelButtonClickedEvent?.Invoke();
    }

    private void HandlePopupNavClosed()
    {
        TeleportUIClosedEvent?.Invoke();
        ForceHide();
    }

    private void HandleEnterDungeon(MapType _type, ForestType _forestType)
    {
        if (MapType.None == _type)
        {
            return;
        }

        // 실제로 닫는 시점은 이 뷰가 스스로 정하지 않고, 이벤트를 구독하는 GameplayUICoordinator가
        // 게임 상태 처리(신호 발행 등)와 함께 ForceHide()를 호출해 결정한다.
        DungeonSelectedEvent?.Invoke(_type, _forestType);
    }

    // 취소 버튼/던전 선택은 언락 연출 중에도 항상 닫혀야 하므로 Hide()의 IsUnlockingProductionActive
    // 가드를 우회한다. 다만 OnHide()를 직접 호출하면 bVisible/depthController 등록 해제가 되지 않아
    // TeleportUIClosedEvent가 재진입 시 중복 발행되므로, base.Hide()를 통해 상태 정리는 항상 거치게 한다.
    public void ForceHide()
    {
        base.Hide();
    }

    protected override void OnShow()
    {
        base.OnShow();

        if (false == isInitialOpen)
        {
            isInitialOpen = true;
            return;
        }

        if (null != vehicleOpenCoroutine)
        {
            StopCoroutine(vehicleOpenCoroutine);
            vehicleOpenCoroutine = null;
        }

        // [기존 시스템 주석 처리]
        /*
        if (null != vehicle)
        {
            if (vehicleOpenDelay > 0f)
            {
                vehicleOpenCoroutine = StartCoroutine(CoOpenVehicle());
            }
            else
            {
                vehicle.Open();
            }
        }
        */

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain)
        {
            if (0f < vehicleOpenDelay)
            {
                vehicleOpenCoroutine = StartCoroutine(CoOpenPopupNav());
            }
            else
            {
                popupNavMain.Open();
            }
        }
    }

    private IEnumerator CoOpenPopupNav()
    {
        yield return GetWaitForSeconds(vehicleOpenDelay);
        if (null != popupNavMain)
        {
            popupNavMain.Open();
        }
        vehicleOpenCoroutine = null;
    }

    public override void Hide()
    {
        // [기존 시스템 주석 처리]
        // if (null != vehicle && vehicle.IsUnlockingProductionActive)
        //     return;

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain && true == popupNavMain.IsUnlockingProductionActive)
        {
            return;
        }

        base.Hide();
    }

    protected override void OnHide()
    {
        base.OnHide();

        if (null != vehicleOpenCoroutine)
        {
            StopCoroutine(vehicleOpenCoroutine);
            vehicleOpenCoroutine = null;
        }

        // [기존 시스템 주석 처리]
        /*
        if (null != vehicle)
        {
            vehicle.Close();
            TeleportUIClosedEvent?.Invoke();
        }
        */

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain)
        {
            popupNavMain.Close();
            TeleportUIClosedEvent?.Invoke();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (null != vehicleOpenCoroutine)
        {
            StopCoroutine(vehicleOpenCoroutine);
            vehicleOpenCoroutine = null;
        }

        if (null != popupNavMain)
        {
            popupNavMain.OnUnlockProductionStarted -= HandleUnlockProductionStarted;
            popupNavMain.OnUnlockProductionEnded -= HandleUnlockProductionEnded;
        }

        // [기존 시스템 주석 처리]
        // if (null != vehicle)
        //     vehicle.mapSelectedEvent -= HandleEnterDungeon;
    }

    public override void Refresh()
    {
        base.Refresh();

        // [기존 시스템 주석 처리]
        // if (null != vehicle)
        //     vehicle.SyncUnlockStates();
    }
}
