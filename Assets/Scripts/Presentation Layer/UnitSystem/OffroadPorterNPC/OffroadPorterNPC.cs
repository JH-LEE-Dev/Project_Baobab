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
        }

        if (inventoryComponent != null)
        {
            inventoryComponent.Initialize();
            if (statComponent != null) inventoryComponent.SetSlotCount(statComponent.slotCapacity);
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

    private void Update()
    {
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
