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
    // InDungeonUnitSpawner가 들고 있는 공용 인스턴스를 Initialize()에서 주입받는다(NPC마다 따로 안 만듦).
    private LumberjackStatComponent statComponent;
    public LumberjackStatComponent stat => statComponent;

    // 셰이크웨이브 생성기와, 셰이크웨이브 계산에 필요한 캐릭터 스탯. 둘 다 InDungeonUnitSpawner가
    // 공용으로 들고 있다가 SetShockWaveDependencies()로 주입해준다.
    private IShockWaveCreator shockWaveCreator;
    private ICharacterStatForNPC playerStatForShockWave;

    public void SetShockWaveDependencies(IShockWaveCreator _shockWaveCreator, ICharacterStatForNPC _playerStat)
    {
        shockWaveCreator = _shockWaveCreator;
        playerStatForShockWave = _playerStat;
    }

    // 부메랑 발사기와, 발사 주기/사거리/개수 판단에 필요한 캐릭터 스탯. 둘 다 InDungeonUnitSpawner가
    // 공용으로 들고 있다가 SetBoomerangDependencies()로 주입해준다. BoomerangCreator 자체가 데미지 등을
    // 자신에게 주입된 StatComponent에서 직접 읽으므로, 이 NPC가 던지는 부메랑도 캐릭터와 완전히 동일한
    // 스탯(데미지/범위/사정거리/쿨타임/치명타)을 갖는다.
    private IBoomerangCreator boomerangCreator;
    private StatComponent playerStatForBoomerang;

    public void SetBoomerangDependencies(IBoomerangCreator _boomerangCreator, StatComponent _playerStat)
    {
        boomerangCreator = _boomerangCreator;
        playerStatForBoomerang = _playerStat;
    }

    [Header("Boomerang Settings")]
    [SerializeField] private LayerMask treeLayer;
    [SerializeField] private float boomerangTreeSensorRadius = 4f;
    [SerializeField] private float boomerangTreeDetectionInterval = 0.5f;
    [SerializeField] private float boomerangEdgePadding = 0.5f; // 화면 경계에서 안쪽으로 두는 여유 (Character와 동일)

    private readonly List<IStaticCollidable> boomerangTreeScanResults = new List<IStaticCollidable>(16);
    private readonly List<ITreeObj> activeBoomerangTargets = new List<ITreeObj>(4); // 동시에 날아가는 부메랑들이 서로 다른 나무를 노리도록
    private readonly List<Boomerang> activeBoomerangs = new List<Boomerang>(4);
    private float boomerangCooldownTimer = 0f;
    private float boomerangTreeScanTimer = 0f;

    private PathFindComponent pathFindComponent;
    public OffroadContainer offroadContainer { get; private set; }

    [Header("Item Pickup Settings")]
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private float pickupRadius = 2.5f;
    [SerializeField] private float itemDetectionInterval = 0.2f;
    private ItemDetector itemDetector;

    // 상태 머신
    public LumberjackStateMachine stateMachine { get; private set; }

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

    // 던전 시작 직후 대기(LJState_Idle의 스폰 딜레이) 중에는 부메랑도 쏘지 않아야 하므로, "한 번이라도
    // 움직이기 시작했는지"를 따로 기억해둔다. LJState_Move.Enter()가 SetVisualMoving(true)를 호출하는
    // 순간(=나무를 찾아 실제로 걷기 시작하는 순간) true가 되고, 이후엔 도끼질/납품 등으로 잠깐씩
    // 멈춰도 다시 false로 돌아가지 않는다(캐릭터의 부메랑처럼 계속 켜져 있어야 하므로).
    private bool hasStartedMoving = false;

    private CustomSortable customSortable;

    // 오브젝트 풀 재사용 시(던전 재입장마다) Initialize()가 반복 호출되는데, 아래 컴포넌트/자식 오브젝트
    // 참조들은 같은 인스턴스인 한 절대 바뀌지 않으므로 GetComponent 계열 탐색은 최초 1회만 수행한다.
    private bool bComponentsCached = false;
    private Shadow cachedShadow;
    private GameObject cachedWaterGo;

    public void Initialize(IEnvironmentProvider _envProvider, IPathfindTreeProvider _pathfindTreeProvider, OffroadContainer _offroadContainer = null, LumberjackStatComponent _statComponent = null)
    {
        environmentProvider = _envProvider;

        // offroadContainer는 던전마다 새로 주입되는 참조라, 이전 던전에서 구독했던 인스턴스가 남아있지
        // 않도록 매번 재구독한다. bPermanentlyStuck은 원래 "내 인벤토리가 바뀔 때"만 재확인했는데,
        // 다른 럼버잭이나 캐릭터의 납품이 착지(AddItemByData)해서 자리가 새로 생기는 경우는 이 NPC의
        // 인벤토리와 무관해서 절대 감지되지 않아, 실제로는 자리가 다시 났는데도 영구히 멈춰있는
        // 경우가 있었다. (던전에서는 컨테이너에서 아이템을 "빼가는" 경로가 없으므로 여기서
        // ContainerUpdatedEvent가 의미 있게 발생하는 건 사실상 납품 착지뿐이다.)
        if (offroadContainer != null)
        {
            offroadContainer.ContainerUpdatedEvent -= HandleOffroadContainerUpdated;
        }
        offroadContainer = _offroadContainer;
        if (offroadContainer != null)
        {
            offroadContainer.ContainerUpdatedEvent -= HandleOffroadContainerUpdated;
            offroadContainer.ContainerUpdatedEvent += HandleOffroadContainerUpdated;
        }

        if (_statComponent != null) statComponent = _statComponent;
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

        ClearActiveBoomerangs();
        boomerangCooldownTimer = 0f;
        boomerangTreeScanTimer = 0f;
        hasStartedMoving = false;
    }

    public void PauseNPC()
    {
        // TEMP DEBUG
        LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={name}({GetEntityId()}) PauseNPC() 호출됨. state={stateMachine?.CurrentState?.GetType().Name}");
        isPaused = true;
        SetVisualMoving(false);
        PauseBoomerangs();
    }

    public void ResumeNPC()
    {
        // TEMP DEBUG
        LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={name}({GetEntityId()}) ResumeNPC() 호출됨.");
        isPaused = false;
        ResumeBoomerangs();
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

            inventoryComponent.ItemAddedEvent -= HandleItemAdded;
            inventoryComponent.ItemAddedEvent += HandleItemAdded;

            inventoryComponent.ItemAddedEvent -= HandleItemAddedForVisualBounce;
            inventoryComponent.ItemAddedEvent += HandleItemAddedForVisualBounce;
        }
    }

    private void OnDisable()
    {
        if (inventoryComponent != null)
        {
            inventoryComponent.InventoryIsFullEvent -= HandleInventoryFull;
            inventoryComponent.ItemAddedEvent -= HandleItemAdded;
            inventoryComponent.ItemAddedEvent -= HandleItemAddedForVisualBounce;
        }
    }

    // 이미 "하나도 못 넣어서 영구 정지" 판정이 난 뒤에도, 그 전에 이미 흡입 중이던 로그가 뒤늦게
    // 인벤토리에 들어올 수 있다(문제 3). 그 순간 인벤토리 상황이 바뀐 것이므로, Deliver 상태에
    // 있다면 영구 정지 판정을 다시 풀어서 새로 들어온 걸 납품할 기회를 준다.
    private void HandleItemAdded()
    {
        if (stateMachine?.CurrentState is LJState_Deliver deliverState)
        {
            deliverState.ClearPermanentStuckIfNeeded();
        }
    }

    // 기존 HandleItemAdded()와는 완전히 분리된, 아이템 획득 뽀잉 연출 전용 구독/핸들러.
    private void HandleItemAddedForVisualBounce()
    {
        visualComponent?.PlayItemAcquireBounce();
    }

    // 영구 정지는 "내 인벤토리가 바뀔 때"만 재확인되던 HandleItemAdded와 별개로, 다른 럼버잭이나
    // 캐릭터의 납품이 착지해서 자리가 새로 생긴 경우도 감지해야 한다(던전에서 컨테이너 아이템이
    // 줄어드는 경로는 없으므로, 여기서 의미 있는 경우는 사실상 "다른 쪽 납품 착지"뿐이다). 이건 이
    // NPC의 인벤토리와 무관하므로 HandleItemAdded로는 절대 잡히지 않는다 - 실제로 자리가 다시
    // 났는데도 이 NPC만 영구히 멈춰있는 문제를 막기 위한 것이다.
    private void HandleOffroadContainerUpdated()
    {
        if (stateMachine?.CurrentState is LJState_Deliver deliverState)
        {
            deliverState.ClearPermanentStuckIfNeeded();
        }
    }

    private void HandleInventoryFull()
    {
        // TEMP DEBUG
        LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={name}({GetEntityId()}) HandleInventoryFull() 호출됨. currentState={stateMachine?.CurrentState?.GetType().Name}, offroadContainer={(offroadContainer != null)}");

        if (stateMachine != null && !(stateMachine.CurrentState is LJState_Deliver) && offroadContainer != null)
        {
            stateMachine.ChangeState<LJState_Deliver>();
        }
    }

    // TEMP DEBUG: 같은 상태에 너무 오래 머물면 자동으로 상세 정보를 찍는 감시용 타이머.
    private System.Type lastWatchedState;
    private float stateStuckTimer = 0f;
    private bool stuckWarningLogged = false;
    private const float STUCK_WARNING_THRESHOLD = 5f;

    private void Update()
    {
        // 일시정지 중에는 상태 로직(이동/벌목/납품/아이템 감지)만 멈추고, 시각 갱신은 계속 돌려서
        // 멈춘 프레임의 움직이는 포즈가 그대로 굳어있지 않고 Idle 포즈로 정상적으로 보이게 한다.
        if (!isPaused)
        {
            stateMachine?.Update();

            // TEMP DEBUG: 상태 정체 감시
            var currentStateType = stateMachine?.CurrentState?.GetType();
            if (currentStateType != lastWatchedState)
            {
                lastWatchedState = currentStateType;
                stateStuckTimer = 0f;
                stuckWarningLogged = false;
            }
            else
            {
                stateStuckTimer += Time.deltaTime;
                if (!stuckWarningLogged && stateStuckTimer > STUCK_WARNING_THRESHOLD)
                {
                    stuckWarningLogged = true;
                    int invCount = inventoryComponent != null ? inventoryComponent.GetTotalItemCount() : -1;
                    bool invFull = inventoryComponent != null && inventoryComponent.bInventoryIsFull;
                    LJDebugLog.LogWarning($"[LJDebug] t={Time.time:F2} npc={name}({GetEntityId()}) 상태 정체 감지! state={currentStateType?.Name}, {STUCK_WARNING_THRESHOLD}초 이상 유지, " +
                        $"pos={transform.position}, isPaused={isPaused}, 인벤토리={invCount}, bInventoryIsFull={invFull}, targetTree={(targetTree != null ? targetTree.GetTransform()?.name : "null")}");
                }
            }

            // 납품(LJState_Deliver) 중에는 새 아이템을 줍지 않는다. 그렇지 않으면 가지고 있던 로그를
            // 전부 성공적으로 던진 직후(또는 던지는 도중) 근처의 로그를 하나 더 주워버려서, 납품이
            // 끝났을 때 인벤토리가 다시 비어있지 않은 것으로 보여 영구 정지로 오인되는 문제가 있었다.
            if (inventoryComponent != null && (stateMachine == null || !(stateMachine.CurrentState is LJState_Deliver)))
            {
                itemDetector.Tick(Time.deltaTime, itemDetectionInterval, pickupRadius, OnItemDetected);
            }

            UpdateTreeBoomerang();
        }

        if (visualComponent != null)
        {
            visualComponent.UpdateVisuals(isMoving, false, false);
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

            // 같은 로그를 다른 NPC(또는 캐릭터)가 같은 프레임에 먼저 흡입 걸었다면 SetSuckTarget이
            // 조용히 무시된다(state != Dropped). 그러면 이 NPC의 CanAcquired 예약만 유령처럼 남아서,
            // 실제로는 나에게 오지 않을 아이템 때문에 다른 진짜 아이템을 잘못 거부하게 된다.
            // 그래서 내가 실제로 이 아이템의 소유자가 됐는지 확인하고, 아니라면 예약을 즉시 취소한다.
            if (!ReferenceEquals(logItem.CustomAcquirer, inventoryComponent))
            {
                inventoryComponent.CancelReservation(logItem);
            }
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
        if (offroadContainer != null)
        {
            offroadContainer.ContainerUpdatedEvent -= HandleOffroadContainerUpdated;
        }

        ReleaseTargetTree();
        stateMachine?.ReleaseAllState();
        ClearActiveBoomerangs();
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

            // TEMP DEBUG
            LJDebugLog.Log($"[LJDebug] t={Time.time:F2} npc={name}({GetEntityId()}) TryFindTree 성공. tree={targetTree.GetTransform().name}({targetTree.GetTransform().GetEntityId()}), treePos={targetTree.GetTransform().position}, npcPos={transform.position}, pathCount={pathBuffer.Count}");
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
        if (_isMoving) hasStartedMoving = true;
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

    public void SetInShadow(bool _isInShadow, float _duration)
    {
        if (visualComponent != null)
        {
            visualComponent.SetInShadow(_isInShadow, _duration);
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
                treeObj.TakeDamage(statComponent.attackDamage);

                TryTriggerShockWave();
            }
        }
    }

    // bCanUseShockWave가 켜져 있으면, 캐릭터의 StatComponent에 정의된 셰이크웨이브 스탯을 그대로 사용해
    // 나무를 벨 때 확률적으로 셰이크웨이브를 발생시킨다(캐릭터의 AttackComponent.ProcessAxeHit와 동일한 방식).
    private void TryTriggerShockWave()
    {
        if (!statComponent.bCanUseShockWave || shockWaveCreator == null || playerStatForShockWave == null) return;
        if (playerStatForShockWave.bShockWaveMastery) return;

        if (UnityEngine.Random.Range(0f, 100f) < playerStatForShockWave.shockWaveChance)
        {
            StartCoroutine(CreateShockWaveRoutine(transform.position, currentFacingDir));
        }
    }

    private System.Collections.IEnumerator CreateShockWaveRoutine(Vector3 _position, Vector3 _direction)
    {
        yield return new WaitForSeconds(playerStatForShockWave.shockWaveCreateDelay);

        if (shockWaveCreator == null) yield break;

        ShockWave sw = shockWaveCreator.CreateShockWave(_position);
        if (sw != null)
        {
            sw.SetDirection(_direction);
            shockWaveCreator.PlayShockWaveVisual(sw);
        }
    }

    // statComponent.bCanUseBoomerang가 켜져 있으면, 캐릭터의 Character.UpdateTreeBoomerang와 완전히 동일한
    // 방식(주기적 스캔 -> 쿨타임 -> 가장 가까운 나무에 발사, 최대 boomerangCount개 동시 유지)으로
    // 부메랑을 사용한다. 도끼질과는 무관하게 독립적으로 동작한다(캐릭터도 그렇다).
    private void UpdateTreeBoomerang()
    {
        // 던전이 시작되고 스폰 딜레이 동안 가만히 대기하는 구간(LJState_Idle)에는 부메랑도 쏘지 않고,
        // 나무를 찾아 실제로 걷기 시작한 뒤(LJState_Move.Enter -> SetVisualMoving(true)) 부터 켜진다.
        if (!hasStartedMoving || !statComponent.bCanUseBoomerang || boomerangCreator == null || playerStatForBoomerang == null) return;

        // "부메랑" 스킬을 찍어 boomerangCount(동시에 존재 가능한 부메랑 개수)가 1 이상이 되기 전에는
        // 아예 발사되지 않는다.
        if (playerStatForBoomerang.boomerangCount <= 0) return;

        boomerangCooldownTimer -= Time.deltaTime;

        boomerangTreeScanTimer += Time.deltaTime;
        if (boomerangTreeScanTimer < boomerangTreeDetectionInterval) return;
        boomerangTreeScanTimer = 0f;

        if (boomerangCooldownTimer > 0f) return;
        if (activeBoomerangTargets.Count >= playerStatForBoomerang.boomerangCount) return;

        ITreeObj nearestTree = FindNearestTreeForBoomerang();
        if (nearestTree == null) return;

        ThrowBoomerangAt(nearestTree);
    }

    private ITreeObj FindNearestTreeForBoomerang()
    {
        if (CollisionSystem.Instance == null) return null;

        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, boomerangTreeSensorRadius, treeLayer.value, boomerangTreeScanResults);

        ITreeObj nearest = null;
        float nearestIsoSqr = float.MaxValue;
        Vector2 myPos = transform.position;

        for (int i = 0; i < boomerangTreeScanResults.Count; i++)
        {
            // 이미 다른 부메랑이 향하고 있는 나무는 제외해서, 동시에 여러 개가 날아갈 때 서로
            // 다른 나무를 노리도록 한다(Character.FindNearestTree와 동일한 규칙).
            if (boomerangTreeScanResults[i] is ITreeObj treeObj && !treeObj.bDead && !activeBoomerangTargets.Contains(treeObj))
            {
                float isoSqr = GetIsometricDistSq(boomerangTreeScanResults[i].Position, myPos);
                if (isoSqr < nearestIsoSqr)
                {
                    nearestIsoSqr = isoSqr;
                    nearest = treeObj;
                }
            }
        }

        return nearest;
    }

    private static float GetIsometricDistSq(Vector2 _a, Vector2 _b)
    {
        float dx = _a.x - _b.x;
        float dy = (_a.y - _b.y) * 2f;
        return dx * dx + dy * dy;
    }

    private void ThrowBoomerangAt(ITreeObj _tree)
    {
        Vector3 origin = transform.position;
        Vector3 dir = GetBoomerangTargetPosition(_tree) - origin;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        // "부메랑 사정거리" 스킬(boomerangMajorAxisRatio)을 캐릭터와 동일하게 타원 장축 비율로 전달한다.
        float maxDistance = CameraBoundsUtil.GetMaxDistanceToEdge(dir, boomerangEdgePadding, playerStatForBoomerang.boomerangMajorAxisRatio);
        if (maxDistance <= 0.1f) return;

        activeBoomerangTargets.Add(_tree);

        Boomerang thrownBoomerang = null;
        System.Action onFinished = () =>
        {
            activeBoomerangTargets.Remove(_tree);
            activeBoomerangs.Remove(thrownBoomerang);
            boomerangCooldownTimer = playerStatForBoomerang.boomerangCooldown;
        };

        thrownBoomerang = boomerangCreator.ThrowBoomerang(origin, dir, maxDistance, transform, onFinished);

        if (thrownBoomerang == null)
        {
            // Initialize가 아직 안 됐거나 풀 생성에 실패한 경우: 예약해둔 타겟팅을 되돌린다.
            activeBoomerangTargets.Remove(_tree);
            return;
        }

        activeBoomerangs.Add(thrownBoomerang);
    }

    // 나무 밑동이 아니라 TreeVisualComponent의 topRoot(나무 윗부분) 방향으로 부메랑이 날아가도록 목표
    // 지점을 구한다. Character.GetBoomerangTargetPosition과 동일한 규칙이다.
    private Vector3 GetBoomerangTargetPosition(ITreeObj _tree)
    {
        if (_tree is TreeObj treeObj && treeObj.treeVisualComponent != null)
        {
            return treeObj.treeVisualComponent.GetTopRootPosition();
        }

        return _tree.GetTransform().position;
    }

    // 던전을 나가거나(ResetToCleanState) 풀로 반환될 때(OnDestroy), 날아가고 있던 부메랑을 전부 강제로
    // 회수하고 추적 목록을 비운다. Character.ClearActiveBoomerangs와 동일하다.
    private void ClearActiveBoomerangs()
    {
        if (activeBoomerangs.Count > 0)
        {
            foreach (Boomerang boomerang in activeBoomerangs.ToArray())
            {
                boomerang?.ForceStop();
            }
        }

        activeBoomerangs.Clear();
        activeBoomerangTargets.Clear();
    }

    // NPC가 일시정지되는 동안(WarningUI 등, PauseNPC/ResumeNPC과 같은 시점) 날아가던 부메랑도 캐릭터와
    // 동일하게 그 자리에서 멈췄다가 다시 이어서 움직이게 한다.
    private void PauseBoomerangs()
    {
        for (int i = 0; i < activeBoomerangs.Count; i++)
        {
            activeBoomerangs[i]?.Pause();
        }
    }

    private void ResumeBoomerangs()
    {
        for (int i = 0; i < activeBoomerangs.Count; i++)
        {
            activeBoomerangs[i]?.Resume();
        }
    }
}
