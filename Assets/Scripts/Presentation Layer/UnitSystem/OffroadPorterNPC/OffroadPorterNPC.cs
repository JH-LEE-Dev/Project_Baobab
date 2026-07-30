using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마을(Town)에서 OffroadContainer의 로그를 LogContainer(상점 보관함)로 자동으로 옮겨주는 운반 NPC.
/// LumberjackNPC와 동일한 구조(상태머신 + PathFindComponent + LumberjackInventoryComponent)를 쓰지만,
/// 나무를 찾아 벌목하는 대신 두 컨테이너 사이를 오간다.
/// </summary>
public class OffroadPorterNPC : MonoBehaviour
{
    // 외부 데이터 제공자
    public ITilemapDataProvider tilemapDataProvider { get; private set; }

    // 내부 컴포넌트
    [SerializeField] private CharacterVisualComponent visualComponent;
    [SerializeField] private LumberjackInventoryComponent inventoryComponent;
    public LumberjackInventoryComponent inventory => inventoryComponent;
    private PathFindComponent pathFindComponent;

    public OffroadContainer offroadContainer { get; private set; }
    public LogContainer logContainer { get; private set; }

    // 모든 오프로드 포터 NPC가 공용으로 참조하는 스탯. TownUnitSpawner가 들고 있다가 Initialize()에서 주입해준다.
    private OffroadPorterStatComponent statComponent;
    public OffroadPorterStatComponent stat => statComponent;

    [Header("Spawn Settings")]
    public float initialMoveDelay = 3f;
    private float spawnDelayEndTime = 0f;
    public bool IsSpawnDelayFinished => Time.time >= spawnDelayEndTime;

    private readonly List<Vector3> pathBuffer = new List<Vector3>();
    public IReadOnlyList<Vector3> currentPath => pathBuffer;

    private bool isMoving = false;
    private bool isPaused = false;

    private CustomSortable customSortable;

    private PorterStateMachine stateMachine;

    // 오브젝트 재사용 대비: 같은 인스턴스인 한 바뀌지 않는 참조들은 최초 1회만 캐싱한다.
    private bool bComponentsCached = false;
    private Shadow cachedShadow;
    private GameObject cachedWaterGo;

    public void Initialize(ITilemapDataProvider _tilemapDataProvider, IEnvironmentProvider _envProvider,
        OffroadContainer _offroadContainer, LogContainer _logContainer, OffroadPorterStatComponent _statComponent = null)
    {
        tilemapDataProvider = _tilemapDataProvider;
        offroadContainer = _offroadContainer;
        logContainer = _logContainer;

        if (_statComponent != null) statComponent = _statComponent;

        if (!bComponentsCached)
        {
            pathFindComponent = GetComponent<PathFindComponent>();

            if (visualComponent != null)
            {
                cachedShadow = GetComponentInChildren<Shadow>(true);
                customSortable = GetComponentInChildren<CustomSortable>(true);
                if (customSortable != null)
                {
                    customSortable.Initialize(transform);
                }
                Transform waterObj = transform.Find("Visual/OnWaterAnimatorObject");
                cachedWaterGo = waterObj != null ? waterObj.gameObject : null;
            }

            bComponentsCached = true;
        }

        if (pathFindComponent != null)
        {
            // 이 NPC는 나무를 찾지 않으므로 IPathfindTreeProvider가 필요 없다.
            pathFindComponent.Initialize(tilemapDataProvider, null);
        }

        if (visualComponent != null)
        {
            visualComponent.Initialize(_envProvider, cachedWaterGo, cachedShadow, customSortable);
            // _envProvider.tilemapDataProvider는 던전 전용 TileMapGenerator라, 발소리 등에는
            // 이미 Town용으로 주입받은 tilemapDataProvider(TownTilemapDataProvider)로 덮어써야 한다.
            visualComponent.SetTilemapDataProvider(tilemapDataProvider);
        }

        if (inventoryComponent != null)
        {
            inventoryComponent.Initialize();
            SyncInventorySlotCapacity();
        }

        if (stateMachine == null)
        {
            stateMachine = new PorterStateMachine();
            AddState(new PorterState_Idle());
            AddState(new PorterState_MoveToOffroad());
            AddState(new PorterState_MoveToLogContainer());
        }

        pathBuffer.Clear();
        SetVisualMoving(false);
        SetVisualFacing(Vector2.down); // 생성됐을 때는 항상 아래를 보게 한다

        spawnDelayEndTime = Time.time + initialMoveDelay;

        stateMachine.ChangeState<PorterState_Idle>();
    }

    private void AddState(PorterState _state)
    {
        _state.Initialize(stateMachine, this);
        stateMachine.AddState(_state);
    }

    // 기존 로직과는 무관한, 아이템 획득 뽀잉 연출 전용 구독/핸들러.
    private void OnEnable()
    {
        if (inventoryComponent != null)
        {
            inventoryComponent.ItemAddedEvent -= HandleItemAddedForVisualBounce;
            inventoryComponent.ItemAddedEvent += HandleItemAddedForVisualBounce;
        }
    }

    private void OnDisable()
    {
        if (inventoryComponent != null)
        {
            inventoryComponent.ItemAddedEvent -= HandleItemAddedForVisualBounce;
        }
    }

    private void HandleItemAddedForVisualBounce()
    {
        visualComponent?.PlayItemAcquireBounce();
    }

    public void PauseNPC()
    {
        isPaused = true;
        SetVisualMoving(false);
    }

    public void ResumeNPC()
    {
        isPaused = false;
    }

    /// <summary>
    /// 던전으로 카메라가 완전히 올라간 뒤(CameraUpIsEnd) 호출한다. 이 NPC는 GameInstaller 하위의
    /// DontDestroyOnLoad 계층에 있어 던전 씬으로 넘어가도 파괴되지 않으므로, 여기서 직접 꺼주지
    /// 않으면 마을에서 멈췄던 위치 그대로 던전 화면에 계속 살아있게 된다(트리 컬링 버그와 동일한 원인).
    /// </summary>
    public void Deactivate()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 던전에 다녀와 마을로 복귀했을 때 호출한다. 원래 생성 위치로 되돌리고, 진행 중이던 경로/상태를
    /// 전부 초기화한 뒤 Idle로 되돌리고, 스폰 직후와 동일하게 initialMoveDelay만큼 재차 대기시킨다.
    /// </summary>
    public void ResetToSpawnPosition(Vector3 _spawnPos)
    {
        gameObject.SetActive(true);
        transform.position = _spawnPos;
        pathBuffer.Clear();
        SetVisualMoving(false);
        SetVisualFacing(Vector2.down);

        spawnDelayEndTime = Time.time + initialMoveDelay;

        stateMachine?.ChangeState<PorterState_Idle>();

        isPaused = false;
    }

    /// <summary>
    /// 공용 statComponent의 슬롯 용량을 인벤토리에 반영한다. 이동 속도(stat.moveSpeed)는 PorterState가
    /// 매 프레임 직접 읽어가므로 스킬 효과가 즉시 반영되지만, 슬롯 용량은 인벤토리 쪽에 복사해줘야만
    /// 적용되는 값이라 한 번만 복사해두면 이후에 오른 스킬 수치가 영영 반영되지 않는다. 이 NPC는
    /// 마을 최초 진입 시 딱 한 번만 Initialize()되는 반면(TownUnitSpawner.SpawnNPCsIfNeeded) 슬롯 용량
    /// 스킬은 던전에서 획득되므로, 매 프레임 최신 값으로 동기화해줘야 한다.
    /// (던전에 있는 동안엔 이 NPC가 비활성화되어 Update가 돌지 않으므로, 스탯 변경 이벤트를
    /// 구독하는 방식으로는 그 사이에 오른 수치를 놓치게 된다.)
    /// </summary>
    private void SyncInventorySlotCapacity()
    {
        if (inventoryComponent == null || statComponent == null) return;

        inventoryComponent.SetSlotCount(statComponent.slotCapacity);
    }

    private void Update()
    {
        SyncInventorySlotCapacity();

        // 일시정지 중에는 상태 로직(이동/작업)만 멈추고, 시각 갱신은 계속 돌려서 멈춘 프레임의
        // 움직이는 포즈가 그대로 굳어있지 않고 Idle 포즈로 정상적으로 보이게 한다.
        if (!isPaused)
        {
            stateMachine?.Update();
        }

        if (visualComponent != null)
        {
            // bInHub = true 고정: 이 NPC는 마을에서만 존재하므로 InDungeon_base_* 스프라이트가 아니라
            // base_*(마을용) 스프라이트 세트를 사용해야 한다. (LumberjackNPC는 반대로 항상 false)
            visualComponent.UpdateVisuals(isMoving, true, false);
        }
    }

    private void LateUpdate()
    {
        customSortable?.ManualLateUpdate();
    }

    public void SetVisualMoving(bool _isMoving)
    {
        isMoving = _isMoving;
    }

    public void SetVisualFacing(Vector2 _direction)
    {
        if (visualComponent != null)
        {
            visualComponent.SetFacingDirection(_direction);
        }
    }

    public void SetInShadow(bool _isInShadow, float _duration)
    {
        if (visualComponent != null)
        {
            visualComponent.SetInShadow(_isInShadow, _duration);
        }
    }

    public bool FindPathTo(Vector3 _targetWorldPos)
    {
        if (pathFindComponent == null) return false;
        return pathFindComponent.FindPath(transform.position, _targetWorldPos, pathBuffer);
    }

    /// <summary>
    /// _targetWorldPos 자체가 이동 불가 타일이어도(컨테이너가 자기 발밑을 막아둔 경우 등) 그 주변에서
    /// 가장 가까운 이동 가능한 타일까지의 경로를 currentPath에 채웁니다.
    /// </summary>
    public bool FindPathNear(Vector3 _targetWorldPos)
    {
        if (pathFindComponent == null) return false;
        return pathFindComponent.FindPathNear(transform.position, _targetWorldPos, pathBuffer);
    }

    public Coroutine WithdrawFromOffroad(Action<bool> _onComplete)
    {
        if (offroadContainer == null)
        {
            _onComplete?.Invoke(false);
            return null;
        }

        return offroadContainer.WithdrawToCarrier(inventoryComponent, _onComplete);
    }

    /// <summary>
    /// 텔레포트 UI가 닫히는 시점 등, Pause와는 별개로 지금 하던 작업을 중단시켜야 할 때 호출한다.
    /// 현재 상태에 따라 알맞게 취소를 위임한다.
    /// </summary>
    public void CancelCurrentTaskForTeleport()
    {
        if (stateMachine == null) return;

        if (stateMachine.CurrentState is PorterState_MoveToOffroad moveToOffroad)
        {
            moveToOffroad.CancelForTeleport();
        }
        else if (stateMachine.CurrentState is PorterState_MoveToLogContainer moveToLogContainer)
        {
            moveToLogContainer.CancelForTeleport();
        }
    }

    public void DepositToLogContainer(Action _onComplete)
    {
        if (logContainer == null)
        {
            _onComplete?.Invoke();
            return;
        }

        float jackpotChance = statComponent != null ? statComponent.jackpotChance : 0f;
        logContainer.TransferFromNPC(inventoryComponent, transform.position, _onComplete, jackpotChance);
    }
}
