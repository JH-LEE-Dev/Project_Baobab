using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType, ForestType> DungeonSelectedEvent;
    public event Action TeleportUIClosedEvent;

    // popupNavMain.OnUnlockProductionStarted/Ended를 그대로 상위로 릴레이하는 이벤트.
    // (popupNavMain이 private 필드라 외부에서 직접 구독할 방법이 없어 최소한의 릴레이만 추가)
    public event Action UnlockProductionStartedEvent;
    public event Action UnlockProductionEndedEvent;

    // popupNavMain.DungeonConfirmStartedEvent를 그대로 상위로 릴레이하는 이벤트.
    // 던전 클릭으로 선택이 확정된 순간(닫힘 연출/딜레이가 끝나 DungeonSelectedEvent가 발행되기 전)을 알린다.
    public event Action DungeonConfirmStartedEvent;

    // 외부 의존성
    private IMapDataProvider mapDataProvider;

    // 내부 의존성
    [Header("Sub UI Prefabs")]
    [Tooltip("신규 1-Depth 내비게이션 팝업 프리팹")]
    [SerializeField] private GameObject popupNavPrefab;
    private HUD_PopupNav_Main popupNavMain;

    private bool isInitialOpen = false;

    // OnHide()가 던전 확정 대기(IsDungeonConfirmPending) 때문에 TeleportUIClosedEvent 발행을
    // 미뤘다는 표시. 이때만 뒤늦게 도착하는 HandlePopupNavClosed()가 대신 발행한다.
    private bool hasPendingCloseEvent = false;

    [Header("Open Delay Settings")]
    [FormerlySerializedAs("vehicleOpenDelay")]
    [SerializeField] private float popupNavOpenDelay = 0f;

    private Coroutine popupNavOpenCoroutine;
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

    public void DependencyInjection(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider = null, ITimeDataProvider _timeDataProvider = null)
    {
        mapDataProvider = _mapDataProvider;

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain)
        {
            popupNavMain.Initialize(mapDataProvider, viewCtx.localizationManager, viewCtx.cursorBoxUI, HandlePopupNavClosed, HandleEnterDungeon, viewCtx.depthController, viewCtx.inputManager);

            popupNavMain.OnUnlockProductionStarted -= HandleUnlockProductionStarted;
            popupNavMain.OnUnlockProductionStarted += HandleUnlockProductionStarted;
            popupNavMain.OnUnlockProductionEnded -= HandleUnlockProductionEnded;
            popupNavMain.OnUnlockProductionEnded += HandleUnlockProductionEnded;
            popupNavMain.DungeonConfirmStartedEvent -= HandleDungeonConfirmStarted;
            popupNavMain.DungeonConfirmStartedEvent += HandleDungeonConfirmStarted;

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

    private void HandleDungeonConfirmStarted()
    {
        DungeonConfirmStartedEvent?.Invoke();
    }

    private void HandlePopupNavClosed()
    {
        // 이 콜백은 내비가 닫힘 연출까지 끝낸 뒤 도착한다. 뷰가 아직 열려 있으면 아래 ForceHide()가
        // OnHide()를 태워 거기서 발행하므로 여기서 발행하면 안 되고, 뷰가 이미 닫혔더라도 그때
        // OnHide()가 이미 발행했으므로 마찬가지다. 유일한 예외가 던전 확정 대기로 발행을 건너뛴
        // 경우이고, 그건 hasPendingCloseEvent로만 구분한다.
        if (true == hasPendingCloseEvent)
        {
            hasPendingCloseEvent = false;
            TeleportUIClosedEvent?.Invoke();
        }

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

        // 확정 대기 중 내비가 파괴되는 등으로 콜백이 끝내 오지 않았을 때 플래그가 다음 세션까지
        // 남아 엉뚱한 시점에 발행되는 것을 막는다.
        hasPendingCloseEvent = false;

        viewCtx?.inputManager?.SetInputMode(EInputMode.UI);

        Sound.RequestAudioDuck();

        if (false == isInitialOpen)
        {
            isInitialOpen = true;
            return;
        }

        if (null != popupNavOpenCoroutine)
        {
            StopCoroutine(popupNavOpenCoroutine);
            popupNavOpenCoroutine = null;
        }

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain)
        {
            if (0f < popupNavOpenDelay)
            {
                popupNavOpenCoroutine = StartCoroutine(CoOpenPopupNav());
            }
            else
            {
                popupNavMain.Open();
            }
        }
    }

    private IEnumerator CoOpenPopupNav()
    {
        yield return GetWaitForSeconds(popupNavOpenDelay);
        if (null != popupNavMain)
        {
            popupNavMain.Open();
        }
        popupNavOpenCoroutine = null;
    }

    public override void Hide()
    {
        if (null != popupNavMain && true == popupNavMain.IsUnlockingProductionActive)
        {
            return;
        }

        base.Hide();
    }

    protected override void OnHide()
    {
        viewCtx?.inputManager?.SetInputMode(EInputMode.Gameplay);

        base.OnHide();

        Sound.ReleaseAudioDuck();

        if (null != popupNavOpenCoroutine)
        {
            StopCoroutine(popupNavOpenCoroutine);
            popupNavOpenCoroutine = null;
        }

        // [신규 1-Depth 내비게이션 동기화]
        if (null != popupNavMain)
        {
            if (true == popupNavMain.IsUnlockingProductionActive)
            {
                popupNavMain.AbortUnlockProduction();
            }

            bool _isDungeonConfirmPending = popupNavMain.IsDungeonConfirmPending;

            popupNavMain.Close();

            // 던전 확정 대기 구간(닫힘 연출 + dungeonConfirmDelay)에 외부 요인으로 이 뷰가 먼저 닫히면
            // TeleportUIClosedEvent가 확정 콜백보다 앞서 발행되는데, 그 시점엔 아직 bCanGetOff가 true라
            // TownSystem.TeleportUIClosed()의 GetOffFromTheVehicle()이 통과해 캐릭터가 차에서 잘못 내린다.
            // 이 구간에서는 발행을 건너뛰어도 확정 딜레이가 끝나면 HUD_PopupNav_Main.OnDungeonConfirmDelayComplete가
            // onNavigationClosedCallback(HandlePopupNavClosed)으로 같은 이벤트를 올바른 순서에 발행해준다.
            if (false == _isDungeonConfirmPending)
            {
                hasPendingCloseEvent = false;
                TeleportUIClosedEvent?.Invoke();
            }
            else
            {
                hasPendingCloseEvent = true;
            }
        }
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void OnDestroy()
    {
        if (null != popupNavOpenCoroutine)
        {
            StopCoroutine(popupNavOpenCoroutine);
            popupNavOpenCoroutine = null;
        }

        if (null != popupNavMain)
        {
            if (true == popupNavMain.IsUnlockingProductionActive)
            {
                popupNavMain.AbortUnlockProduction();
            }

            popupNavMain.OnUnlockProductionStarted -= HandleUnlockProductionStarted;
            popupNavMain.OnUnlockProductionEnded -= HandleUnlockProductionEnded;
            popupNavMain.DungeonConfirmStartedEvent -= HandleDungeonConfirmStarted;
        }

        base.OnDestroy();
    }
}
