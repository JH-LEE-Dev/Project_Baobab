using UnityEngine;
using System.Collections.Generic;

public class LumberjackNPC : MonoBehaviour
{
    // 외부 데이터 제공자
    public ITilemapDataProvider tilemapDataProvider { get; private set; }
    public IPathfindTreeProvider pathfindTreeProvider { get; private set; }
    private IEnvironmentProvider environmentProvider;

    // 내부 컴포넌트
    [SerializeField] private CharacterVisualComponent visualComponent;
    [SerializeField] private LumberjackArmComponent armComponent;
    [SerializeField] private LumberjackInventoryComponent inventoryComponent;
    public LumberjackInventoryComponent inventory => inventoryComponent;
    private PathFindComponent pathFindComponent;
    public OffroadContainer offroadContainer { get; private set; }

    [Header("Item Pickup Settings")]
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private float pickupRadius = 2.5f;
    [SerializeField] private float itemDetectionInterval = 0.2f;
    private ItemDetector itemDetector;
    
    // 상태 머신
    public LumberjackStateMachine stateMachine { get; private set; }

    // 이동 관련 설정
    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    
    [Header("Chop Settings")]
    public float chopDamage = 10f;
    public float chopInterval = 1.0f;

    [Header("Spawn Settings")]
    public float initialMoveDelay = 5f;
    private float spawnDelayEndTime = 0f;
    public bool IsSpawnDelayFinished => Time.time >= spawnDelayEndTime;

    // 현재 공유 데이터
    public ITreeObj targetTree;

    // 상태들이 매 프레임 읽기만 하고 직접 수정할 수 없도록 리스트 자체는 감추고 읽기 전용 뷰만 공개한다.
    // 실제로 채우는 곳은 TryFindTree()/FindPathTo() 뿐이다.
    private readonly List<Vector3> pathBuffer = new List<Vector3>();
    public IReadOnlyList<Vector3> currentPath => pathBuffer;

    private bool isMoving = false;
    private bool isPaused = false;
    private Vector2 currentFacingDir = Vector2.down;

    private CustomSortable customSortable;

    // 오브젝트 풀 재사용 시(던전 재입장마다) Initialize()가 반복 호출되는데, 아래 컴포넌트/자식 오브젝트
    // 참조들은 같은 인스턴스인 한 절대 바뀌지 않으므로 GetComponent 계열 탐색은 최초 1회만 수행한다.
    private bool bComponentsCached = false;
    private Shadow cachedShadow;
    private GameObject cachedWaterGo;

    public void Initialize(IEnvironmentProvider _envProvider, IPathfindTreeProvider _pathfindTreeProvider, OffroadContainer _offroadContainer = null)
    {
        environmentProvider = _envProvider;
        offroadContainer = _offroadContainer;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;
        pathfindTreeProvider = _pathfindTreeProvider;

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

        // 길찾기 컴포넌트는 던전마다 그리드/타겟 제공자가 바뀌므로 매번 다시 초기화해야 한다.
        if (pathFindComponent != null)
        {
            pathFindComponent.Initialize(tilemapDataProvider, _pathfindTreeProvider);
        }

        if (visualComponent != null)
        {
            visualComponent.Initialize(environmentProvider, cachedWaterGo, cachedShadow, customSortable);
        }

        if (itemDetector == null) itemDetector = new ItemDetector(transform, itemLayer);

        // 상태 머신 초기화 (오브젝트 풀 재사용 시 불필요한 재생성을 피하기 위해 최초 1회만 생성)
        if (stateMachine == null)
        {
            stateMachine = new LumberjackStateMachine();
            AddState(new LJState_Idle());
            AddState(new LJState_Move());
            AddState(new LJState_Chop());
            AddState(new LJState_Deliver());
        }

        // NPC는 던전 안에서만 유효한 존재이므로, 재사용(오브젝트 풀에서 다시 꺼내질 때) 시
        // 이전 생애의 인벤토리/타겟/경로/방향이 전혀 남아있지 않은 완전히 클린한 상태로 시작한다.
        ResetToCleanState();

        // 스폰 직후 일정 시간 동안은 움직이지 않도록 지연시간 설정
        spawnDelayEndTime = Time.time + initialMoveDelay;

        stateMachine.ChangeState<LJState_Idle>();
    }

    /// <summary>
    /// 인벤토리, 타겟 나무, 경로, 자세(방향)를 전부 초기 상태로 되돌립니다.
    /// 던전에서 마을로 돌아가 풀에 반환될 때(OnReleaseNPC)와, 다음 던전에서 다시 꺼내질 때(Initialize) 둘 다 호출됩니다.
    /// </summary>
    public void ResetToCleanState()
    {
        isPaused = false;
        if (armComponent != null) armComponent.Initialize();
        if (inventoryComponent != null) inventoryComponent.Initialize();

        ReleaseTargetTree();
        pathBuffer.Clear();

        SetVisualMoving(false);
        SetVisualFacing(Vector2.down);
        SetArmDirection(Vector2.down);
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

    private void AddState(LumberjackState _state)
    {
        _state.Initialize(stateMachine, this);
        stateMachine.AddState(_state);
    }

    private void OnEnable()
    {
        if (inventoryComponent != null)
        {
            inventoryComponent.InventoryIsFullEvent -= HandleInventoryFull;
            inventoryComponent.InventoryIsFullEvent += HandleInventoryFull;
        }
    }

    private void OnDisable()
    {
        if (inventoryComponent != null)
        {
            inventoryComponent.InventoryIsFullEvent -= HandleInventoryFull;
        }
    }

    private void HandleInventoryFull()
    {
        if (stateMachine != null && !(stateMachine.CurrentState is LJState_Deliver) && offroadContainer != null)
        {
            stateMachine.ChangeState<LJState_Deliver>();
        }
    }

    private void Update()
    {
        if (isPaused) return;

        stateMachine?.Update();

        if (visualComponent != null)
        {
            visualComponent.UpdateVisuals(isMoving, false, false);
        }

        // 납품(LJState_Deliver) 중에는 새 아이템을 줍지 않는다. 그렇지 않으면 가지고 있던 로그를
        // 전부 성공적으로 던진 직후(또는 던지는 도중) 근처의 로그를 하나 더 주워버려서, 납품이
        // 끝났을 때 인벤토리가 다시 비어있지 않은 것으로 보여 영구 정지로 오인되는 문제가 있었다.
        if (inventoryComponent != null && (stateMachine == null || !(stateMachine.CurrentState is LJState_Deliver)))
        {
            itemDetector.Tick(Time.deltaTime, itemDetectionInterval, pickupRadius, OnItemDetected);
        }
    }

    /// <summary>
    /// 반경 내에서 발견된 로그를 자신의 인벤토리로 흡입 시도한다. (Character.OnItemDetected와 동일한 역할,
    /// 흡입 대상/체커만 자신의 LumberjackInventoryComponent로 다르게 지정한다)
    /// </summary>
    private void OnItemDetected(IStaticCollidable _collidable)
    {
        if (_collidable is LogItem logItem)
        {
            if (inventoryComponent != null && !inventoryComponent.CanAcquired(logItem))
            {
                HandleInventoryFull();
                return;
            }
            logItem.SetSuckTarget(transform, inventoryComponent, inventoryComponent);
        }
    }

    private void FixedUpdate()
    {
        if (isPaused) return;

        stateMachine?.FixedUpdate();
    }

    private void LateUpdate()
    {
        customSortable?.ManualLateUpdate();
        armComponent?.UpdateSortingOrder();
    }

    private void OnDestroy()
    {
        ReleaseTargetTree();
        stateMachine?.ReleaseAllState();
    }

    // --- State 머신에서 호출할 유틸리티 메서드들 ---

    public bool TryFindTree()
    {
        if (pathFindComponent == null) return false;

        bool found = pathFindComponent.FindNearestTreePath(transform.position, out targetTree, pathBuffer);
        if (found && targetTree != null)
        {
            // 다른 NPC가 같은 나무를 동시에 타겟팅하지 못하도록 예약
            targetTree.bReserved = true;
        }

        return found;
    }

    /// <summary>
    /// 현재 타겟 나무의 예약을 해제하고 참조를 비웁니다. (나무를 포기하거나 다 벤 경우 호출)
    /// </summary>
    public void ReleaseTargetTree()
    {
        if (targetTree != null)
        {
            targetTree.bReserved = false;
        }

        targetTree = null;
    }

    /// <summary>
    /// 나무가 아닌 임의의 월드 좌표(오프로드 컨테이너 등)까지의 경로를 currentPath에 채웁니다.
    /// </summary>
    public bool FindPathTo(Vector3 _targetWorldPos)
    {
        if (pathFindComponent == null) return false;

        return pathFindComponent.FindPath(transform.position, _targetWorldPos, pathBuffer);
    }

    /// <summary>
    /// _targetWorldPos 자체가 이동 불가 타일이어도(오브젝트가 자기 발밑을 막아둔 경우 등)
    /// 그 주변에서 가장 가까운 이동 가능한 타일까지의 경로를 currentPath에 채웁니다.
    /// </summary>
    public bool FindPathNear(Vector3 _targetWorldPos)
    {
        if (pathFindComponent == null) return false;

        return pathFindComponent.FindPathNear(transform.position, _targetWorldPos, pathBuffer);
    }

    /// <summary>
    /// 인벤토리에 있는 로그를 전부 오프로드 컨테이너에 납품 시도합니다. 캐릭터가 컨테이너와 상호작용할 때와
    /// 동일하게 로그가 날아가는 연출을 거쳐 도착 시점에 실제로 컨테이너 슬롯에 더해집니다.
    /// 컨테이너가 가득 차서 일부만 들어가면 나머지는 인벤토리에 그대로 남습니다(유실 없음).
    /// </summary>
    public void DepositInventoryToOffroad(System.Action<bool> _onComplete)
    {
        if (offroadContainer == null || inventoryComponent == null)
        {
            _onComplete?.Invoke(false);
            return;
        }

        offroadContainer.TransferFromNPC(inventoryComponent, transform.position, _onComplete);
    }

    public void SetVisualMoving(bool _isMoving)
    {
        isMoving = _isMoving;
    }

    public void SetVisualFacing(Vector2 _direction)
    {
        currentFacingDir = _direction;
        if (visualComponent != null)
        {
            visualComponent.SetFacingDirection(_direction);
        }
    }

    public void SetArmDirection(Vector2 _direction)
    {
        if (armComponent != null)
        {
            armComponent.SetTargetDirection(_direction);
        }
    }

    // OnAxeImpact 메서드 그룹을 델리게이트로 변환할 때마다(=매 SwingAxe 호출마다) 새 힙 할당이
    // 발생하므로, 한 번만 만들어 재사용한다 (chopInterval마다 반복 호출되는 핫 패스).
    private System.Action cachedOnAxeImpact;

    public void SwingAxe()
    {
        if (armComponent != null)
        {
            cachedOnAxeImpact ??= OnAxeImpact;
            armComponent.SwingAxe(cachedOnAxeImpact);
        }
    }

    private void OnAxeImpact()
    {
        if (targetTree != null)
        {
            // 데미지 전달 로직 (나무 객체에 맞게 수정 가능)
            if (targetTree is TreeObj treeObj)
            {
                // 실제 나무 오브젝트인 경우 데미지 적용
                // (죽으면서 스폰되는 로그는 주기적으로 도는 itemDetector가 착지 후 자연스럽게 주워간다)
                treeObj.bLastHitByPlayer = false;
                treeObj.TakeDamage(chopDamage);
            }
        }
    }
}
