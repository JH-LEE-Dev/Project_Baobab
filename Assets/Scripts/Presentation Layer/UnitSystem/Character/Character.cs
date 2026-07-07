using System;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour, ITeleportable, ICharacter, IStaticCollidable, IDamageable
{
    public event Action StaminaIsEmptyEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;

    // 외부 의존성
    public InputManager inputManager { get; private set; }
    private IEnvironmentProvider environmentProvider;
    private ComponentCtx ctx;

    // 내부 의존성 (컴포넌트 및 오브젝트)
    [Header("Internal Components")]
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private GameObject onWaterAnimatorObject;

    [Header("Collision Settings")]
    [SerializeField] private float collisionRadius = 0.15f;
    [SerializeField] private Vector2 collisionOffset = new Vector2(0f, 0.12f);

    private AttackComponent attackComponent;
    private PHealthComponent healthComponent;
    private ArmComponent armComponent;
    public StatComponent statComponent { get; private set; }

    public StateMachine stateMachine { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public CircleCollider2D col { get; private set; }

    // 상태 및 데이터
    [Header("Character Stats & States")]
    public GroundPhysicsData currentGroundData { get; private set; }
    public bool bInDungeon { get; private set; } = true;
    public bool bWhileSwing { get; private set; } = false;
    public bool bCanRotate { get; private set; } = true;

    private float staminaDecAmount = 0f;
    private float staminaIncAmount = 0f;
    private bool bStaminaUpDown = false;

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;
    public int EntityIndex { get; set; } = -1;
    public void TakeDamage(float _damage) => healthComponent.DecreaseHealth(_damage);
    public void KnockBack(Vector2 _knockBackDir, float _knockBackForce) { }

    // 캐싱된 해시 및 프로퍼티 (성능 최적화)
    public IPHealthComponent pHealthComponent => healthComponent;
    IBaseHealthComponent IDamageable.health => healthComponent;

    IStatComponent ICharacter.statComponent => statComponent;

    IArmComponent ICharacter.armComponent => armComponent;

    public void RepairWeapon(float _amount)
    {
        if (armComponent != null && armComponent.axeComponent != null)
        {
            armComponent.axeComponent.RepairDurability(_amount);
        }
    }

    public bool bCanApplyDamage => true;

    public bool bMoving = false;
    [SerializeField] private float itemSensorRadius = 0.35f;
    private float itemDetectionInterval = 0.2f; // 최적화: 0.2초 간격 (5Hz)
    private ItemDetector itemDetector;

    [SerializeField] private LayerMask itemLayer; // 아이템 레이어

    [Header("Boomerang Settings")]
    [SerializeField] private BoomerangCreator boomerangCreator;
    [SerializeField] private LayerMask treeLayer;
    [SerializeField] private float treeSensorRadius = 4f;
    [SerializeField] private float treeDetectionInterval = 0.5f; // 나무는 정적이라 아이템보다 낮은 주기로 충분함
    [SerializeField] private float boomerangEdgePadding = 0.5f; // 화면 경계에서 안쪽으로 두는 여유

    private readonly List<IStaticCollidable> treeScanResults = new List<IStaticCollidable>(16);
    private readonly List<ITreeObj> activeBoomerangTargets = new List<ITreeObj>(4); // 동시에 날아가는 부메랑들이 각자 다른 나무를 노리도록 이미 타겟팅된 나무를 추적
    private readonly List<Boomerang> activeBoomerangs = new List<Boomerang>(4);
    private float treeScanTimer = 0f;
    private float boomerangCooldownTimer = 0f;

    private CharacterVisualComponent characterVisualComponent;

    public Transform centerTransform;

    private CustomSortable customSortable;

    public bool bDead { get; private set; } = false;

    bool ICharacter.bRide => bRide;

    public bool bRide = false;

    private float visualHeight = 0f;

    private bool bWhileReset = false;

    private bool bCanAcquiredItem = false;

    #region Public Methods (Initialization & Control)

    public void Initialize(InputManager _inputManager, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;

        itemDetector = new ItemDetector(transform, itemLayer);

        // 컴포넌트 할당
        characterVisualComponent = animatorObject.GetComponent<CharacterVisualComponent>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        attackComponent = GetComponentInChildren<AttackComponent>();
        healthComponent = GetComponentInChildren<PHealthComponent>();
        armComponent = GetComponentInChildren<ArmComponent>();
        statComponent = GetComponentInChildren<StatComponent>();
        customSortable = GetComponent<CustomSortable>();

        boomerangCreator?.Initialize(statComponent);

        if (customSortable != null)
        {
            customSortable.Initialize(transform);
        }

        stateMachine = new StateMachine();
        ctx = new ComponentCtx();
        ctx.Initialize(inputManager, statComponent, environmentProvider.pathfindGridProvider, environmentProvider.tilemapDataProvider);

        // 컴포넌트 초기화
        characterVisualComponent.Initialize(environmentProvider, onWaterAnimatorObject, shadowObject, customSortable);
        attackComponent.Initialize(ctx);
        healthComponent.Initialize(ctx);
        armComponent.Initialize(ctx);
        statComponent.Initialize(ctx);

        attackComponent.SetCursorEnable(false);

        SetupStateMachine();
        BindEvents();
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (bDead == false)
            characterVisualComponent.SetFacingDirection(_input);
    }

    public void StaminaReset()
    {
        healthComponent.StaminaReset();
    }

    public void SetStaminaUpDownState(bool _bStaminaUpDown, float _staminaDecAmount, float _staminaIncAmount)
    {
        bStaminaUpDown = _bStaminaUpDown;
        staminaDecAmount = _staminaDecAmount;
        staminaIncAmount = _staminaIncAmount;

        UpdateStaminaAmounts();
    }

    private void UpdateStaminaAmounts()
    {
        // 최대 스태미나 동기화
        healthComponent.SetMaxStamina(statComponent.maxStamina);

        // staminaDecreaseAlpha는 소모량 감소 비율 (예: 10.0f면 10% 감소)
        float reductionMultiplier = 1.0f - (statComponent.staminaDecreaseAlpha / 100.0f);
        float finalDecAmount = staminaDecAmount * Mathf.Max(0, reductionMultiplier);

        // staminaIncreaseAlpha는 회복량 증가 비율 (예: 10.0f면 10% 증가)
        float boostMultiplier = 1.0f + (statComponent.staminaIncreaseAlpha / 100.0f);
        float finalIncAmount = staminaIncAmount * boostMultiplier;

        healthComponent.SetStaminaDecreaseAmount(finalDecAmount);
        healthComponent.SetStaminaIncreaseAmount(finalIncAmount);
    }

    public void SetWhereIsCharacter(bool _bInDungeon)
    {
        CollisionSystem.Instance?.Register(this, false);

        ClearActiveBoomerangs();
        treeScanTimer = 0f;
        boomerangCooldownTimer = 0f;

        if (_bInDungeon == false)
        {
            armComponent.ResetWeaponStatus();
            bWhileSwing = false;
            healthComponent.StaminaReset();
            statComponent.ResetSpeed();
            bCanRotate = true;
            attackComponent.ResetAttackComponent();
            stateMachine.ChangeState<IdleState>();
            attackComponent.SetEnable(false);
            attackComponent.SetCursorEnable(false);
        }
        else
        {
            bCanAcquiredItem = true;
            armComponent.ResetWeaponStatus();
        }
    
        bDead = false;

        bInDungeon = _bInDungeon;
        characterVisualComponent.SetHubState(!bInDungeon);
        characterVisualComponent.CharacterIsDead(false);
        armComponent.SetActivate(bInDungeon);
        SetFacingDirection(Vector2.down);

        statComponent.Reset();
    }

    public Transform GetTransform() => transform;

    public void SetInShadow(bool _isInShadow, float _duration)
    {
        characterVisualComponent.SetInShadow(_isInShadow, _duration);
    }

    public void SetHeight(float _height)
    {
        visualHeight = _height;
    }

    #endregion

    #region Private Methods

    private void SetupStateMachine()
    {
        AddState(new IdleState());
        AddState(new RunState());
        AddState(new DeadState());
        stateMachine.ChangeState<IdleState>();
    }

    private void AddState(CharacterState _state)
    {
        _state.Initialize(stateMachine, this, ctx);
        stateMachine.AddState(_state);
    }

    private void BindEvents()
    {
        if (armComponent.axeComponent != null)
        {
            armComponent.axeComponent.DeclareAttackStateEvent -= SetbCanAction;
            armComponent.axeComponent.DeclareAttackStateEvent += SetbCanAction;

            armComponent.axeComponent.AttackEvent -= attackComponent.Attack;
            armComponent.axeComponent.AttackEvent += attackComponent.Attack;

            attackComponent.AttackSuccessEvent -= armComponent.axeComponent.DecreaseDurability;
            attackComponent.AttackSuccessEvent += armComponent.axeComponent.DecreaseDurability;

            healthComponent.StaminaIsEmptyEvent -= StaminaIsEmpty;
            healthComponent.StaminaIsEmptyEvent += StaminaIsEmpty;

            armComponent.axeComponent.DeclareCanSwapEvent -= SetbCanRotate;
            armComponent.axeComponent.DeclareCanSwapEvent += SetbCanRotate;
        }
    }

    private void ReleaseEvents()
    {
        if (armComponent != null && armComponent.axeComponent != null)
        {
            armComponent.axeComponent.DeclareAttackStateEvent -= SetbCanAction;
            attackComponent.AttackSuccessEvent -= armComponent.axeComponent.DecreaseDurability;
            armComponent.axeComponent.AttackEvent -= attackComponent.Attack;
            healthComponent.StaminaIsEmptyEvent -= StaminaIsEmpty;
        }
    }

    private void UpdateFacingByAttackPoint()
    {
        if (attackComponent == null || bInDungeon == false || bWhileReset == true) return;

        Transform attackTarget = attackComponent.GetAttackPointTransform();
        if (attackTarget == null) return;

        Vector2 dir = (Vector2)attackTarget.position - (Vector2)transform.position;
        SetFacingDirection(dir);
    }

    private void ConnectAttackToArm()
    {
        armComponent.SetAttackTransform(attackComponent.GetAttackPointTransform());
        armComponent.SetMouseTransform(attackComponent.mouseTransform);
    }

    private void WeaponModeChanged(WeaponMode _currentMode)
    {
        WeaponModeChangedEvent?.Invoke(_currentMode);
        armComponent.WeaponModeChanged(_currentMode);
        bWhileSwing = false;
        attackComponent.SetbAttack(false);
        armComponent.axeComponent.SetbAttack(false);
        armComponent.rifleComponent.SetbAttack(false);
    }

    private void SetbCanAction(bool _isAttacking)
    {
        bWhileSwing = _isAttacking; // 도끼질 등 액션 중일 때 true
        attackComponent.SetbAttack(_isAttacking);
        UpdateFacingByAttackPoint();
    }

    private void SetbCanRotate(bool _bCanRotate)
    {
        bCanRotate = _bCanRotate;
        attackComponent.SetbCanRotate(_bCanRotate);
    }

    private void StaminaIsEmpty()
    {
        stateMachine.ChangeState<DeadState>();
        armComponent.SetActivate(false);
        attackComponent.SetEnable(false);
        characterVisualComponent.CharacterIsDead(true);
        bDead = true;
        healthComponent.SetStaminaDecrease(false);
        inputManager.PauseMove(true);
        StartCoroutine(StaminaIsEmptyRoutine());
    }

    private System.Collections.IEnumerator StaminaIsEmptyRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        StaminaIsEmptyEvent?.Invoke();
    }

    private void UpdateItemDetection()
    {
        if (bCanAcquiredItem == false) return;

        float finalRadius = itemSensorRadius * statComponent.pickupRangeMultiplier;
        itemDetector.Tick(Time.fixedDeltaTime, itemDetectionInterval, finalRadius, OnItemDetected);
    }

    private void OnItemDetected(IStaticCollidable _collidable)
    {
        if (_collidable is Item item)
        {
            item.SetSuckTarget(transform);
        }
    }

    private void UpdateTreeBoomerang()
    {
        // bWhileReset은 던전 입장 직후(ResetStatus)부터 카메라 연출이 끝나 실제로 조작 가능해지는
        // 시점(ActivateCharacter)까지 true로 유지된다. 이 동안엔 아직 캐릭터를 움직일 수 없는데도
        // 부메랑이 먼저 발사되고 있었으므로, 같은 조건으로 막는다.
        if (bInDungeon == false || bDead || bWhileReset || boomerangCreator == null) return;

        // "부메랑" 스킬을 찍어 boomerangCount(동시에 존재 가능한 부메랑 개수)가 1 이상이 되기 전에는
        // 아예 발사되지 않는다.
        if (statComponent.boomerangCount <= 0) return;

        boomerangCooldownTimer -= Time.fixedDeltaTime;

        treeScanTimer += Time.fixedDeltaTime;
        if (treeScanTimer < treeDetectionInterval) return;
        treeScanTimer = 0f;

        if (boomerangCooldownTimer > 0f) return;
        if (activeBoomerangTargets.Count >= statComponent.boomerangCount) return;

        ITreeObj nearestTree = FindNearestTree();
        if (nearestTree == null) return;

        ThrowBoomerangAt(nearestTree);
        boomerangCooldownTimer = statComponent.boomerangCooldown;
    }

    private ITreeObj FindNearestTree()
    {
        if (CollisionSystem.Instance == null) return null;

        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, treeSensorRadius, treeLayer.value, treeScanResults);

        ITreeObj nearest = null;
        float nearestIsoSqr = float.MaxValue;
        Vector2 myPos = transform.position;

        for (int i = 0; i < treeScanResults.Count; i++)
        {
            // 이미 다른 부메랑이 향하고 있는 나무는 제외해서, 동시에 여러 개가 날아갈 때 서로
            // 다른 나무를 노리도록 한다.
            if (treeScanResults[i] is ITreeObj treeObj && !treeObj.bDead && !activeBoomerangTargets.Contains(treeObj))
            {
                // 순수 유클리드 거리로 고르면, 아이소메트릭 시점에서는 세로로 떨어진 나무가
                // 실제로 화면상 더 가까운 나무보다 먼저 뽑히는 경우가 있었다(세로 이동이 화면에서
                // 절반만큼만 보이므로). ShockWave.GetIsometricDistSq와 동일하게 세로 차이를
                // 2배로 가중해서 "실제로 가장 가까워 보이는" 나무를 고르도록 맞춘다.
                float isoSqr = GetIsometricDistSq(treeScanResults[i].Position, myPos);
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

        // "부메랑 사정거리" 스킬(boomerangMajorAxisRatio)을 타원 장축 비율로 그대로 전달한다.
        float maxDistance = CameraBoundsUtil.GetMaxDistanceToEdge(dir, boomerangEdgePadding, statComponent.boomerangMajorAxisRatio);
        if (maxDistance <= 0.1f) return;

        activeBoomerangTargets.Add(_tree);

        Boomerang thrownBoomerang = null;
        Action onFinished = () =>
        {
            activeBoomerangTargets.Remove(_tree);
            activeBoomerangs.Remove(thrownBoomerang);
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

    // 캐릭터가 죽거나 던전을 나가는 등 왕복이 끝나기 전에 상태를 리셋해야 할 때, 날아가고 있던
    // 부메랑을 전부 강제로 회수하고 추적 목록을 비운다.
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

    // 나무 밑동(GetTransform)이 아니라 TreeVisualComponent의 topRoot(나무 윗부분, 잎이 있는 쪽) 방향으로
    // 부메랑이 날아가도록 목표 지점을 구한다. topRoot가 없는 나무(비주얼 컴포넌트 누락 등)는 기존처럼
    // 트리 오브젝트 위치로 폴백한다.
    private Vector3 GetBoomerangTargetPosition(ITreeObj _tree)
    {
        if (_tree is TreeObj treeObj && treeObj.treeVisualComponent != null)
        {
            return treeObj.treeVisualComponent.GetTopRootPosition();
        }

        return _tree.GetTransform().position;
    }

    #endregion

    #region Unity Event Functions

    private void Update()
    {
        stateMachine?.Update();

        // 비주얼 업데이트
        characterVisualComponent.UpdateVisuals(bMoving, !bInDungeon, bDead);

        // 스태미나 로직
        UpdateStaminaAmounts(); // 실시간 소모량 갱신 반영
        if (bStaminaUpDown) healthComponent.IncreaseStamina();
        else healthComponent.DecreaseStamina();

        UpdateFacingByAttackPoint();
        ConnectAttackToArm();
    }

    private void LateUpdate()
    {
        characterVisualComponent.SetOnWaterSROrder(transform.position);

        customSortable.ManualLateUpdate();
        armComponent.SortArmCompOrder();
    }

    private void FixedUpdate()
    {
        UpdateItemDetection(); // 내부적으로 itemDetectionInterval마다만 실제 스캔을 수행함
        UpdateTreeBoomerang(); // 내부적으로 treeDetectionInterval마다만 실제 스캔을 수행함

        // 커스텀 충돌 시스템 격자 정보 갱신
        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);
        stateMachine?.FixedUpdate();

        customSortable.SetHeight(visualHeight);
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();

        ReleaseEvents();
        CollisionSystem.Instance?.Unregister(this);
    }

    #endregion

    public void RefreshCharacterStat()
    {
        armComponent.Refresh();
    }

    public void DisableShadow()
    {
        shadowObject.gameObject.SetActive(false);
    }

    public void EnableShadow()
    {
        shadowObject.gameObject.SetActive(true);
    }

    public void DisableAttackComponent()
    {
        healthComponent.SetStaminaDecrease(false);
        attackComponent.SetCursorEnable(false);
        attackComponent.SetEnable(false);
        attackComponent.SetbCanAttack(false);
        armComponent.SetbCanAttack(false);
        bCanAcquiredItem = false;
    }

    public void SetStaminaDecrease(bool _boolean)
    {
        healthComponent.SetStaminaDecrease(_boolean);
    }

    public void ActivateCharacter()
    {
        armComponent.ResetRotation();
        attackComponent.ResetAttackTransform();
        attackComponent.SetCursorEnable(true);
        attackComponent.SetEnable(true);
        attackComponent.SetbCanAttack(true);
        armComponent.SetbCanAttack(true);
        bWhileReset = false;
    }

    public void StartDecreaseStamina()
    {
        healthComponent.SetStaminaDecrease(true);
    }

    public void ResetStatus()
    {
        ClearActiveBoomerangs();
        treeScanTimer = 0f;
        boomerangCooldownTimer = 0f;

        attackComponent.ResetAttackTransform();
        armComponent.ResetRotation();
        bWhileReset = true;
        armComponent.ResetWeaponStatus();
        bWhileSwing = false;
        healthComponent.StaminaReset();
        statComponent.ResetSpeed();
        bCanRotate = true;
        attackComponent.ResetAttackComponent();
        stateMachine.ChangeState<IdleState>();
        attackComponent.SetEnable(false);
        attackComponent.SetCursorEnable(false);
    }

    public bool IsAxeDurabilityZero()
    {
        return armComponent.axeComponent.IsDurabilityZero();
    }

    public void PauseCharacter(bool _boolean)
    {
        if (_boolean == true)
        {
            attackComponent.SetCursorEnable(false);
            attackComponent.SetbCanAttack(false);
            armComponent.SetbCanAttack(false);
        }
        else
        {
            attackComponent.SetCursorEnable(true);
            attackComponent.SetbCanAttack(true);
            armComponent.SetbCanAttack(true);
        }
    }

    public void StaminaRecover()
    {
        healthComponent.StaminaRecover(statComponent.sourceOfStaminaRecoverAmount);
        statComponent.ActivateSourceOfSpeed();
    }
}
