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

    // 이동 관련 설정
    [Header("Movement Settings")]
    public float moveSpeed = 3f;

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
        OffroadContainer _offroadContainer, LogContainer _logContainer)
    {
        tilemapDataProvider = _tilemapDataProvider;
        offroadContainer = _offroadContainer;
        logContainer = _logContainer;

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

        if (inventoryComponent != null) inventoryComponent.Initialize();

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
    /// 던전에 다녀와 마을로 복귀했을 때 호출한다. 원래 생성 위치로 되돌리고, 진행 중이던 경로/상태를
    /// 전부 초기화한 뒤 Idle로 되돌리고, 스폰 직후와 동일하게 initialMoveDelay만큼 재차 대기시킨다.
    /// </summary>
    public void ResetToSpawnPosition(Vector3 _spawnPos)
    {
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
        if (isPaused) return;

        stateMachine?.Update();

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

    public void WithdrawFromOffroad(Action _onComplete)
    {
        if (offroadContainer == null)
        {
            _onComplete?.Invoke();
            return;
        }

        offroadContainer.WithdrawToCarrier(inventoryComponent, _onComplete);
    }

    public void DepositToLogContainer(Action _onComplete)
    {
        if (logContainer == null)
        {
            _onComplete?.Invoke();
            return;
        }

        logContainer.TransferFromNPC(inventoryComponent, transform.position, _onComplete);
    }
}
