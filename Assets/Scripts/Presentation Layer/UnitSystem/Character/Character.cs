using System;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour, ITeleportable, ICharacter, IStaticCollidable, IDamageable
{
    public event Action StaminaIsEmptyEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;
    public event Action TreeDetectedEvent;
    public event Action TreeDetectionClearedEvent;

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

    // 매 프레임 갱신되는 현재 셀 좌표 (외부 시스템이 재계산 없이 재사용)
    public Vector3Int CurrentCell { get; private set; }

    // Town/Dungeon 전환 시 TownSystem/InDungeonSystem이 발소리 등에 쓰일 실제 타일맵 조회 대상을 직접 갈아끼운다.
    public void SetTilemapDataProvider(ITilemapDataProvider _tilemapDataProvider)
    {
        characterVisualComponent?.SetTilemapDataProvider(_tilemapDataProvider);
    }

    public void TakeEnvironmentalStaminaDamage(float _amount) => healthComponent.DecreaseStaminaFlat(_amount);
    public void AddOverheatDuration(float _seconds) => overheatComponent?.AddOverheatDuration(_seconds);
    public void EndOverheatBuff() => overheatComponent?.ForceEnd();

    // "열기 회수" 특성 - 과열 상태에서 나무를 벌목하면 과열 지속시간이 회복된다.
    public void OnTreeFelled()
    {
        if (overheatComponent == null || !overheatComponent.IsActive) return;
        if (statComponent.heatRecoveryAmount <= 0f) return;

        overheatComponent.AddOverheatDurationRaw(statComponent.heatRecoveryAmount);
    }

    // 넉백/경직 중에는 나무 열기 발산의 영향(데미지+넉백)을 받지 않는다.
    public bool bTreeHeatImmune { get; private set; }
    public void SetTreeHeatImmune(bool _immune) => bTreeHeatImmune = _immune;

    public void SetArmRotationLocked(bool _locked) => armComponent.SetRotationLocked(_locked);
    public void SetAttackIndicatorLocked(bool _locked) => attackComponent.SetRotationLocked(_locked);

    // 넉백 중에는 캐릭터 스프라이트가 조준 방향을 따라 좌우로 도는 것도 막아야 한다.
    private bool bFacingLocked = false;
    public void SetFacingLocked(bool _locked) => bFacingLocked = _locked;

    public void PlayStunVisual() => stunVisualComponent?.Play();
    public void StopStunVisual() => stunVisualComponent?.Stop();

    public void ApplyTreeHeatKnockback(Vector3Int _cellOffset)
    {
        if (bDead) return;

        stateMachine.GetState<KnockBackState>().SetKnockbackDirection(_cellOffset);
        stateMachine.ChangeState<KnockBackState>();
    }

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
    private bool bBoomerangSystemPaused = false; // WarningUI가 떠 있거나 마을로 돌아가는 동안 새로 발사되지 않도록 막는다

    [Header("Drone Settings")]
    [SerializeField] private DroneCreator droneCreator;
    [SerializeField] private float droneFollowRadius = 0.8f; // 타원 궤도의 장축 반지름(드론이 배치되는 타원의 크기)
    [SerializeField] private float droneWingAngleStep = 30f; // 양옆 드론이 중심(0번) 드론으로부터 벌어지는 각도(도). 캐릭터가 아래를 볼 때 0번=0°, 1번=-30°, 2번=+30°
    [SerializeField] private float droneRowDistanceStep = 0.32f; // 첫 3대(row 0~1)는 같은 타원, 4대째(row 2)부터 타원 반지름이 이 값씩 늘어난다
    [SerializeField] private float droneArrivalTolerance = 0.15f; // 슬롯과 이 거리 이내면 도착한 것으로 본다
    [SerializeField] private float droneCenterHoverHeight = 0.3f; // 대형 꼭짓점(0번, 캐릭터 바로 뒤) 드론만 이만큼 더 높이 띄운다
    [SerializeField] private float droneSeparationDistance = 0.4f; // 드론끼리 이 거리보다 가까워지면 서로 밀어낸다(안전망)
    [SerializeField] private float droneSeparationSpeed = 3f; // 밀어내는 속도
    private const float DroneFormationMinorAxisRatio = 0.5f; // 타원 궤도의 단축/장축 비율(아이소메트릭 2:1 압축). 1.0이면 원, 0.5면 위아래가 절반으로 눌린 타원

    private Vector2 droneBehindDir = Vector2.down; // 조준 방향의 반대("뒤"). 조준 지점을 못 구하면 마지막 방향을 유지한다.

    private readonly List<Drone> activeDrones = new List<Drone>(4);
    private readonly List<IStaticCollidable> droneScanResults = new List<IStaticCollidable>(16);
    private readonly List<ITreeObj> droneClaimedTargets = new List<ITreeObj>(4); // 한 번의 Activate/재타겟팅 호출 안에서 드론끼리 서로 다른 나무를 고르도록
    private readonly List<ITreeObj> droneChainHitTrees = new List<ITreeObj>(8); // 한 번의 연쇄공격 전이 동안 이미 맞은 나무(중복 전이 방지)
    private readonly List<Vector3> droneChainZapPoints = new List<Vector3>(8); // muzzle -> 각 나무 top 위치. 연쇄공격 VFX(LightningZap) 재생용
    private float droneRetargetTimer = 0f;
    private const float DroneRetargetInterval = 0.15f; // 타겟을 잃은 드론에게 새 나무를 물 흐르듯 이어서 배정하는 주기

    private CharacterVisualComponent characterVisualComponent;
    private StunVisualComponent stunVisualComponent;
    private OverheatComponent overheatComponent;

    public Transform centerTransform;

    private CustomSortable customSortable;

    public bool bDead { get; private set; } = false;

    bool ICharacter.bRide => bRide;

    public bool bRide = false;

    private float visualHeight = 0f;

    private bool bWhileReset = false;

    private bool bCanAcquiredItem = false;

    [SerializeField] private GameObject characterVisualObjects;
    private Vector3 characterVisualObjectsOriginalScale = Vector3.one;
    private float itemAcquireBounceTime = 1f;
    private const float ITEM_ACQUIRE_BOUNCE_DURATION = 0.2f;

    #region Public Methods (Initialization & Control)

    public void Initialize(InputManager _inputManager, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;

        if (characterVisualObjects != null)
        {
            characterVisualObjectsOriginalScale = characterVisualObjects.transform.localScale;
        }

        itemDetector = new ItemDetector(transform, itemLayer);

        // 컴포넌트 할당
        characterVisualComponent = animatorObject.GetComponent<CharacterVisualComponent>();
        // StunVisual은 animatorObject(Animator)의 형제(Visuals의 자식)라 그 아래에서는 못 찾으므로,
        // 계층 전체를 아우르는 Character 루트에서 직접 찾는다. 평소 꺼져있으므로 비활성 자식도 포함.
        stunVisualComponent = GetComponentInChildren<StunVisualComponent>(true);
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        attackComponent = GetComponentInChildren<AttackComponent>();
        healthComponent = GetComponentInChildren<PHealthComponent>();
        armComponent = GetComponentInChildren<ArmComponent>();
        statComponent = GetComponentInChildren<StatComponent>();
        overheatComponent = GetComponentInChildren<OverheatComponent>();
        customSortable = GetComponent<CustomSortable>();

        boomerangCreator?.Initialize(statComponent);
        droneCreator?.Initialize(statComponent);

        if (customSortable != null)
        {
            customSortable.Initialize(transform);
        }

        stateMachine = new StateMachine();
        ctx = new ComponentCtx();
        ctx.Initialize(inputManager, statComponent, environmentProvider.pathfindGridProvider, environmentProvider.tilemapDataProvider);
        ctx.overheatComponent = overheatComponent;

        // 컴포넌트 초기화
        characterVisualComponent.Initialize(environmentProvider, onWaterAnimatorObject, shadowObject, customSortable);
        attackComponent.Initialize(ctx);
        healthComponent.Initialize(ctx);
        armComponent.Initialize(ctx);
        statComponent.Initialize(ctx);
        overheatComponent?.Initialize(ctx);

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
        bBoomerangSystemPaused = false; // 다음 던전 입장 때는 다시 발사 가능해야 하므로 여기서 해제

        ClearActiveDrones();

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
            EndOverheatBuff(); // 던전을 나가면 과열 버프도 강제 종료 (MagmaForest 전용 효과가 다른 곳까지 이어지지 않도록)
        }
        else
        {
            bCanAcquiredItem = true;
            armComponent.ResetWeaponStatus();

            // 튜토리얼이 걸어둔 스태미나 바닥값이 다음 원정까지 남지 않도록 던전에 들어설 때마다 해제한다.
            // 타운 분기는 위의 StaminaReset()이 이미 해제하지만, 재도전(결과창 → 재도전)은 타운을 거치지
            // 않고 던전 → 던전으로 직행하므로 여기서 풀어주지 않으면 바닥값이 그대로 이어져 탈진이 막힌다.
            // 튜토리얼은 이 시점보다 한참 뒤(퀘스트 시작)에 바닥값을 걸므로 서로 간섭하지 않는다.
            healthComponent.SetMinStaminaPercent(0f);
        }

        bDead = false;

        bInDungeon = _bInDungeon;
        characterVisualComponent.SetHubState(!bInDungeon);
        characterVisualComponent.CharacterIsDead(false);
        armComponent.SetActivate(bInDungeon);
        SetFacingDirection(Vector2.down);

        statComponent.Reset();

        if (bInDungeon)
        {
            SpawnDrones();
            overheatComponent?.TryActivatePermanent(); // "화신" 특성이 있으면 던전 입장과 동시에 과열 상태로 진입
        }
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

    // OffroadContainer에서 아이템이 캐릭터에게 도착했을 때(OffroadContainerVComponent의 뽀잉 연출과
    // 동일한 감쇠 진동 곡선) characterVisualObjects에도 같은 연출을 재생한다.
    public void PlayItemAcquireBounce()
    {
        itemAcquireBounceTime = 0f;
    }

    // 흡입(Suck)/포물선(OffroadContainer) 두 아이템 획득 경로 모두에서 도착 시점에 호출한다.
    // 사망 연출(PlayDeathFlash/ShakeCamera)보다 훨씬 옅고 약하게 잡아 긍정적인 획득 피드백으로 느껴지게 한다.
    public void PlayItemAcquireFlash()
    {
        CameraMoveController.Instance?.ShakeCamera(1f, 0.08f);
    }

    // 아이템 획득 시의 옅은 하얀 스프라이트 플래시(셰이더의 _FlashAmount 연출, CharacterVisualComponent.PlayItemAcquireFlash)만
    // 필요한 곳(카메라 흔들림 없이)에서 사용한다. PlayDeathFlash가 characterVisualComponent에 직접 위임하는 것과 동일한 방식.
    public void PlayItemAcquireSpriteFlash()
    {
        characterVisualComponent.PlayItemAcquireFlash(GetArmFlashRenderer());
    }

    // 무기(Arm) 스프라이트는 던전에 있을 때만 반짝인다 - 마을에서는 무기가 꺼져 있어 의미가 없다.
    private SpriteRenderer GetArmFlashRenderer()
    {
        return bInDungeon ? armComponent?.currentWeapon?.spriteRenderer : null;
    }

    #endregion

    #region Private Methods

    private void SetupStateMachine()
    {
        AddState(new IdleState());
        AddState(new RunState());
        AddState(new DeadState());
        AddState(new KnockBackState());
        stateMachine.ChangeState<IdleState>();
    }

    private void AddState(CharacterState _state)
    {
        _state.Initialize(stateMachine, this, ctx);
        stateMachine.AddState(_state);
    }

    private void BindEvents()
    {
        attackComponent.TreeDetectedEvent -= TreeDetected;
        attackComponent.TreeDetectedEvent += TreeDetected;

        attackComponent.TreeDetectionClearedEvent -= TreeDetectionCleared;
        attackComponent.TreeDetectionClearedEvent += TreeDetectionCleared;

        if (armComponent.axeComponent != null)
        {
            armComponent.axeComponent.DeclareAttackStateEvent -= SetbCanAction;
            armComponent.axeComponent.DeclareAttackStateEvent += SetbCanAction;

            armComponent.axeComponent.AttackEvent -= attackComponent.Attack;
            armComponent.axeComponent.AttackEvent += attackComponent.Attack;

            attackComponent.AttackSuccessEvent -= armComponent.axeComponent.DecreaseDurability;
            attackComponent.AttackSuccessEvent += armComponent.axeComponent.DecreaseDurability;

            attackComponent.ShockWaveMissEvent -= armComponent.axeComponent.DecreaseDurabilityWithoutCombo;
            attackComponent.ShockWaveMissEvent += armComponent.axeComponent.DecreaseDurabilityWithoutCombo;

            healthComponent.StaminaIsEmptyEvent -= StaminaIsEmpty;
            healthComponent.StaminaIsEmptyEvent += StaminaIsEmpty;

            armComponent.axeComponent.DeclareCanSwapEvent -= SetbCanRotate;
            armComponent.axeComponent.DeclareCanSwapEvent += SetbCanRotate;
        }
    }

    private void ReleaseEvents()
    {
        if (attackComponent != null)
        {
            attackComponent.TreeDetectedEvent -= TreeDetected;
            attackComponent.TreeDetectionClearedEvent -= TreeDetectionCleared;
        }

        if (armComponent != null && armComponent.axeComponent != null)
        {
            armComponent.axeComponent.DeclareAttackStateEvent -= SetbCanAction;
            attackComponent.AttackSuccessEvent -= armComponent.axeComponent.DecreaseDurability;
            attackComponent.ShockWaveMissEvent -= armComponent.axeComponent.DecreaseDurabilityWithoutCombo;
            armComponent.axeComponent.AttackEvent -= attackComponent.Attack;
            healthComponent.StaminaIsEmptyEvent -= StaminaIsEmpty;
        }
    }

    private void TreeDetected()
    {
        TreeDetectedEvent?.Invoke();
    }

    private void TreeDetectionCleared()
    {
        TreeDetectionClearedEvent?.Invoke();
    }

    private void UpdateFacingByAttackPoint()
    {
        if (true == bFacingLocked || 0f == Time.timeScale) return;
        if (null == attackComponent || false == bInDungeon || false == attackComponent.IsCursorEnabled) return;

        Transform attackTarget = attackComponent.GetAttackPointTransform();
        if (null == attackTarget) return;

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

        // 공격 키(도끼 스윙 시작)를 감지한 시점에 드론을 활성화한다. 스윙이 연속되면 그때마다
        // 지속시간이 다시 갱신된다.
        if (_isAttacking)
        {
            ActivateDrones();
        }
    }

    private void SetbCanRotate(bool _bCanRotate)
    {
        bCanRotate = _bCanRotate;
        attackComponent.SetbCanRotate(_bCanRotate);
    }

    private void StaminaIsEmpty()
    {
        if (bDead) return; // MagmaForest 용암 지형에서 DecreaseStamina/ApplyEnvironmentalStaminaDrain가 같은 프레임에 중복 발화하는 것을 방지

        stateMachine.ChangeState<DeadState>();
        armComponent.SetActivate(false);
        attackComponent.SetEnable(false);
        characterVisualComponent.CharacterIsDead(true);
        characterVisualComponent.PlayDeathFlash(GetArmFlashRenderer());
        CameraMoveController.Instance?.ShakeCamera(5f, 0.3f);
        CameraMoveController.Instance?.ZoomCamera(1.05f, 0.08f, 0.05f, 0.15f);
        PostProcessSettingsApplier.Instance?.PlayDeathChromaticAberrationPulse();

        // 스태미너가 다 닳아 쓰러지는 순간
        Rumble.Play(EHapticEvent.StaminaDeath);

        bDead = true;
        healthComponent.SetStaminaDecrease(false);
        inputManager.PauseMove(true);

        // 여기서부터 결과창까지는 되돌릴 수 없는 구간이므로 ESC를 막는다.
        //
        // 차량을 타고 귀환하는 경로는 InDungeonProductionManager.CharacterRideRoutine()이 ESC를
        // 잠근 채 결과창까지 이어지는데, 사망 경로는 그 코루틴을 타지 않아 잠금이 아예 없었다.
        // 그래서 사망 결과창(UIView_Result는 bCloseableByESC = false라 뎁스 스택에도 없다) 위로
        // 일시정지 메뉴가 그대로 열렸고, 그 메뉴가 닫히면서 SetInputMode(Gameplay)/PauseMove(false)가
        // 결과창이 걸어둔 UI 모드와 이동 잠금을 덮어썼다.
        //
        // 해제 시점은 차량 경로와 동일하다. 결과창의 귀가/재도전 어느 쪽을 눌러도
        // InDungeonProductionManager.CameraDownIsEnd()가 풀어준다.
        inputManager.PauseESCKey(true);

        StartCoroutine(StaminaIsEmptyRoutine());
    }

    private System.Collections.IEnumerator StaminaIsEmptyRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        StaminaIsEmptyEvent?.Invoke();
    }

    private void UpdateItemAcquireBounce(float _deltaTime)
    {
        if (characterVisualObjects == null) return;

        if (itemAcquireBounceTime >= ITEM_ACQUIRE_BOUNCE_DURATION)
        {
            if (characterVisualObjects.transform.localScale != characterVisualObjectsOriginalScale)
                characterVisualObjects.transform.localScale = characterVisualObjectsOriginalScale;
            return;
        }

        itemAcquireBounceTime += _deltaTime;
        float t = itemAcquireBounceTime / ITEM_ACQUIRE_BOUNCE_DURATION;

        // 쫀득함(Squash & Stretch) 연출: 감쇠 진동 곡선(Damped Sine Wave). OffroadContainer.UpdateBounce와 동일한 방식.
        float curve = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 0.3f;

        characterVisualObjects.transform.localScale = new Vector3(
            characterVisualObjectsOriginalScale.x * (1f + curve),
            characterVisualObjectsOriginalScale.y * (1f - curve),
            characterVisualObjectsOriginalScale.z);
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
        // bBoomerangSystemPaused는 WarningUI가 떠 있는 동안이나 마을로 돌아가는 도중에 새로 발사되지
        // 않도록 막는다(PauseBoomerangs/DismissBoomerangsWithShrink에서 켜짐).
        if (bInDungeon == false || bDead || bWhileReset || bBoomerangSystemPaused || boomerangCreator == null) return;

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
        Vector3 dir = GetTreeTopPosition(_tree) - origin;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        // "부메랑 사정거리" 스킬(boomerangMajorAxisRatio)을 타원 장축 비율로 그대로 전달한다.
        float maxDistance = CameraBoundsUtil.GetMaxDistanceToEdge(dir, boomerangEdgePadding, statComponent.boomerangMajorAxisRatio);
        if (maxDistance <= 0.1f) return;

        activeBoomerangTargets.Add(_tree);

        Boomerang thrownBoomerang = null;
        // "화염 부메랑" 특성을 찍어야만 과열 상태의 부메랑 강화 효과가 적용된다.
        bool bIsOverheat = overheatComponent != null && overheatComponent.IsActive && statComponent.bBoomerangOverheatBoost;

        Action onFinished = () =>
        {
            activeBoomerangTargets.Remove(_tree);
            activeBoomerangs.Remove(thrownBoomerang);
            // 과열 상태에서 발사되었다면 쿨타임 50% 감소
            boomerangCooldownTimer = bIsOverheat ? statComponent.boomerangCooldown * 0.5f : statComponent.boomerangCooldown;
        };

        thrownBoomerang = boomerangCreator.ThrowBoomerang(origin, dir, maxDistance, transform, onFinished, bIsOverheat);

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

    // WarningUI가 뜨는 동안 NPC/FlyingItem과 동일하게 부메랑도 그 자리에서 멈춘다. 새로 발사되는 것도
    // bBoomerangSystemPaused로 함께 막는다(InDungeonObjectManager.GameEnd에서 호출).
    public void PauseBoomerangs()
    {
        bBoomerangSystemPaused = true;

        for (int i = 0; i < activeBoomerangs.Count; i++)
        {
            activeBoomerangs[i]?.Pause();
        }
    }

    // WarningUI를 취소했을 때(계속 진행) 멈춰있던 부메랑을 그 자리에서 다시 이어서 움직이게 한다
    // (InDungeonObjectManager.AbortGameEnd에서 _bAbort == true일 때 호출).
    public void ResumeBoomerangs()
    {
        bBoomerangSystemPaused = false;

        for (int i = 0; i < activeBoomerangs.Count; i++)
        {
            activeBoomerangs[i]?.Resume();
        }
    }

    // 마을로 돌아가기가 확정됐을 때(InDungeonObjectManager.HandleGameEnd) 호출된다. 날아가던 부메랑들을
    // 즉시 없애는 대신 허공에서 스케일을 줄이며 사라지게 한다. bBoomerangSystemPaused는 계속 true로
    // 남겨서, 씬이 바뀌기 전까지 다시 발사되지 않게 막는다(다음 던전 입장 시 SetWhereIsCharacter(true)에서 해제).
    public void DismissBoomerangsWithShrink()
    {
        bBoomerangSystemPaused = true;

        for (int i = 0; i < activeBoomerangs.Count; i++)
        {
            activeBoomerangs[i]?.DismissWithShrink();
        }
    }

    // 나무 밑동(GetTransform)이 아니라 TreeVisualComponent의 topRoot(나무 윗부분, 잎이 있는 쪽) 방향의
    // 좌표를 구한다. 부메랑 조준과 드론 연쇄공격 LightningZap 경유점 계산이 함께 사용한다. topRoot가
    // 없는 나무(비주얼 컴포넌트 누락 등)는 기존처럼 트리 오브젝트 위치로 폴백한다.
    private Vector3 GetTreeTopPosition(ITreeObj _tree)
    {
        if (_tree is TreeObj treeObj && treeObj.treeVisualComponent != null)
        {
            return treeObj.treeVisualComponent.GetTopRootPosition();
        }

        return _tree.GetTransform().position;
    }

    // 던전에 입장할 때(또는 던전 내 리셋 시) statComponent.droneCount만큼 드론을 소환해 캐릭터 뒤에
    // 타원 대형으로 배치한다. "드론" 스킬을 찍지 않아 droneCount가 0이면 아무것도 소환되지 않는다.
    private void SpawnDrones()
    {
        if (droneCreator == null || statComponent == null) return;

        droneBehindDir = Vector2.down;

        for (int i = 0; i < statComponent.droneCount; i++)
        {
            Vector3 spawnOffset = GetDroneWedgeOffset(i, droneBehindDir);
            Transform droneCenter = centerTransform != null ? centerTransform : transform;
            Drone drone = droneCreator.SpawnDrone(droneCenter.position + spawnOffset, droneCenter);
            if (drone == null) continue;

            drone.SetFollowOffset(spawnOffset);
            drone.SetArrivalTolerance(droneArrivalTolerance);
            drone.SetHoverHeight(i == 0 ? droneCenterHoverHeight : 0f); // 꼭짓점(캐릭터 바로 뒤) 슬롯만 더 높이 띄운다
            drone.SetRetargetCallback(RequestDroneRetarget);
            drone.SetChainAttackCallback(OnDroneChainAttack);
            activeDrones.Add(drone);
        }
    }

    // 캐릭터의 조준 방향(attackComponent의 공격 지점 - UpdateFacingByAttackPoint가 스프라이트 방향을
    // 갱신할 때 쓰는 것과 동일한 기준)을 추적해서 그 반대쪽("뒤")을 기준으로, 각 드론에게 배정된
    // 쐐기형 대형 슬롯을 매 프레임 다시 계산해 넘겨준다 - 그래서 캐릭터가 조준 방향을 바꾸면(이동
    // 여부와 무관하게) 대형 전체가 그 방향에 맞춰 자연스럽게 회전한다(Drone 쪽 SmoothDamp 가감속은
    // 그대로 유지). 조준 지점을 구할 수 없는 순간에는 마지막으로 유효했던 방향을 그대로 유지한다.
    private void UpdateDroneFormation()
    {
        if (activeDrones.Count == 0) return;

        Transform aimTarget = attackComponent != null ? attackComponent.GetAttackPointTransform() : null;
        if (aimTarget != null)
        {
            Vector2 centerPos = centerTransform != null ? (Vector2)centerTransform.position : (Vector2)transform.position;
            Vector2 rawAimDir = (Vector2)aimTarget.position - centerPos;
            if (rawAimDir.sqrMagnitude > 0.0001f)
            {
                droneBehindDir = -rawAimDir.normalized;
            }
        }

        Vector2 aimDir = -droneBehindDir; // 대형은 "뒤" 기준, 드론 개별 Idle 방향은 "조준" 기준이라 부호가 반대다

        // OnDroneChainAttack의 bIsOverheat 판정과 동일한 조건: 과열 버프 + "드론 과부하" 특성을
        // 둘 다 갖췄을 때만 스프라이트가 6~10행(Overheat 세트)으로 바뀐다.
        bool bIsOverheat = overheatComponent != null && overheatComponent.IsActive && statComponent.bDroneOverheatBoost;

        for (int i = 0; i < activeDrones.Count; i++)
        {
            activeDrones[i].SetFollowOffset(GetDroneWedgeOffset(i, droneBehindDir));
            activeDrones[i].SetCharacterAimDir(aimDir);
            activeDrones[i].SetOverheatState(bIsOverheat);
        }
    }

    // N번째 드론의 타원 대형 슬롯을 계산한다. 캐릭터를 중심으로 축 정렬된 타원(가로=장축,
    // 세로=단축=장축×MinorAxisRatio)이 고정되어 있고, 각 드론은 이 타원 곡선 위에 놓인다.
    // behindDir(조준 반대 방향)의 월드 각도에 드론별 고정 오프셋(0°, ±30°...)을 더한 각도로
    // 타원을 평가해서 좌표를 구한다 - 조준 방향이 바뀌면 드론들이 타원 곡선 위를 따라
    // 미끄러지듯 이동하므로 항상 타원 위에 머문다.
    private Vector3 GetDroneWedgeOffset(int _index, Vector2 _behindDir)
    {
        int row = (_index + 1) / 2;
        int side = _index == 0 ? 0 : (_index % 2 == 1 ? -1 : 1);

        float angleOffsetDeg = side * row * droneWingAngleStep;
        float ellipseRadius = droneFollowRadius + Mathf.Max(0, row - 1) * droneRowDistanceStep;

        // behindDir의 월드 각도 + 드론별 오프셋 = 타원 위에서의 최종 각도
        float behindAngleDeg = Mathf.Atan2(_behindDir.y, _behindDir.x) * Mathf.Rad2Deg;
        float finalAngleRad = (behindAngleDeg + angleOffsetDeg) * Mathf.Deg2Rad;

        // 축 정렬 타원 위의 좌표를 직접 평가한다 (타원은 회전하지 않는다)
        float x = Mathf.Cos(finalAngleRad) * ellipseRadius;
        float y = Mathf.Sin(finalAngleRad) * ellipseRadius * DroneFormationMinorAxisRatio;

        return new Vector3(x, y, 0f);
    }

    private static Vector2 RotateVector(Vector2 _v, float _degrees)
    {
        float rad = _degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(_v.x * cos - _v.y * sin, _v.x * sin + _v.y * cos);
    }

    // 대형 슬롯이 있어도, 캐릭터가 급하게 방향을 바꾸는 전환 구간 등에서 드론끼리 일시적으로 너무
    // 가까워질 수 있다. leash 로직과는 별개로, 드론끼리 너무 가까워지면 살짝 밀어내는 보정을
    // 안전망으로 매 프레임 추가 적용한다.
    private void UpdateDroneSeparation(float _deltaTime)
    {
        if (activeDrones.Count < 2) return;

        for (int i = 0; i < activeDrones.Count; i++)
        {
            for (int j = i + 1; j < activeDrones.Count; j++)
            {
                Transform a = activeDrones[i].transform;
                Transform b = activeDrones[j].transform;

                Vector2 diff = (Vector2)a.position - (Vector2)b.position;
                float dist = diff.magnitude;
                if (dist >= droneSeparationDistance) continue;

                Vector2 pushDir = dist > 0.0001f ? diff / dist : Vector2.right;
                float overlap = droneSeparationDistance - dist;
                Vector2 push = pushDir * overlap * 0.5f * droneSeparationSpeed * _deltaTime;

                a.position += (Vector3)push;
                b.position -= (Vector3)push;
            }
        }
    }

    // 던전을 나가거나 리셋할 때 소환되어 있던 드론을 전부 풀로 되돌린다.
    private void ClearActiveDrones()
    {
        for (int i = 0; i < activeDrones.Count; i++)
        {
            activeDrones[i]?.Despawn();
            droneCreator?.DespawnDrone(activeDrones[i]);
        }

        activeDrones.Clear();
    }

    // 공격 키를 누른 순간(SetbCanAction에서 호출) 현재 소환된 드론을 전부 지속시간만큼 활성화한다.
    // 이미 살아있는 나무를 물고 있는 드론은 그 나무를 그대로 유지한다(스윙 도중 더 가까운 나무가
    // 나타나도 타겟을 바꾸지 않는다) - 첫 타겟이 죽어야 비로소 다음 활성화 때 새 나무를 고른다.
    // 아직 타겟이 없는 드론끼리만 서로 다른 나무를 새로 나눠 갖도록, 이번 호출 안에서 이미
    // 배정(또는 유지)된 나무는 다른 드론이 고르지 못하게 막는다.
    private void ActivateDrones()
    {
        if (bInDungeon == false || bDead || bWhileReset || activeDrones.Count == 0) return;

        droneClaimedTargets.Clear();

        // Drone.CurrentTarget은 조회 시점에 즉시 유효성(죽음/묘목 리셋 여부)을 다시 확인하므로,
        // 여기서 별도로 bDead를 다시 검사할 필요가 없다 - 킬 직후 바로 공격 버튼을 다시 눌러도
        // 이미 무효해진 타겟을 "아직 살아있다"고 넘겨받는 일이 없다.
        for (int i = 0; i < activeDrones.Count; i++)
        {
            ITreeObj existingTarget = activeDrones[i].CurrentTarget;
            if (existingTarget != null)
            {
                droneClaimedTargets.Add(existingTarget);
            }
        }

        for (int i = 0; i < activeDrones.Count; i++)
        {
            ITreeObj existingTarget = activeDrones[i].CurrentTarget;
            ITreeObj target = existingTarget ?? FindNearestTreeForDrone(activeDrones[i].transform.position);

            if (target != null && target != existingTarget)
            {
                droneClaimedTargets.Add(target);
            }

            activeDrones[i].Activate(statComponent.droneDamage, statComponent.droneDamageInterval, statComponent.droneActiveDuration, statComponent.droneAttackRange, target);
        }
    }

    // Drone이 스윙 도중 타겟을 잃는 그 프레임에 Drone.SetRetargetCallback으로 등록해둔 콜백을 통해
    // 동기적으로 호출된다. 다른 활성 드론이 이미 물고 있는 나무는 제외하고 가장 가까운 나무를 그
    // 자리에서 즉시 돌려준다 - 응답이 같은 프레임에 오므로 Drone은 애니메이션을 끊지 않고 방향만
    // 새 나무 쪽으로 돌려 공격을 이어간다. 대체할 나무가 없으면 null을 반환하고, 그때만 Drone이
    // Idle로 취소한다.
    private ITreeObj RequestDroneRetarget(Drone _drone)
    {
        droneClaimedTargets.Clear();
        for (int i = 0; i < activeDrones.Count; i++)
        {
            if (activeDrones[i] == _drone) continue;

            ITreeObj existingTarget = activeDrones[i].CurrentTarget;
            if (existingTarget != null)
            {
                droneClaimedTargets.Add(existingTarget);
            }
        }

        return FindNearestTreeForDrone(_drone.transform.position);
    }

    // 스윙 도중 타겟이 범위를 벗어나거나 죽어서 Drone이 스스로 타겟을 비웠을 때, 공격 키를 다시
    // 누르지 않아도 짧은 주기로 주변에 다른 나무가 있는지 확인해 있으면 바로 이어서 공격하게 한다
    // (공격이 물 흐르듯 끊기지 않도록). RequestDroneRetarget(즉시 콜백)이 대체 타겟을 못 찾았을 때를
    // 대비한 안전망으로, 이후에 주변에 새 나무가 생기면(캐릭터/드론 이동 등) 여기서 뒤늦게라도
    // 이어 붙여준다. Activate가 아니라 AssignTarget을 쓰므로 지속시간(activeDuration)은 갱신되지
    // 않는다 - 원래 정해진 지속시간이 끝나면 여전히 다음 공격 키 입력을 기다려야 한다. 원래 타겟이
    // 아직 범위 안에 살아있는 드론은 CurrentTarget이 비어있지 않으므로 여기서 건드릴 일이 없다.
    private void UpdateDroneRetargeting()
    {
        if (bInDungeon == false || bDead || bWhileReset || activeDrones.Count == 0) return;

        droneRetargetTimer += Time.fixedDeltaTime;
        if (droneRetargetTimer < DroneRetargetInterval) return;
        droneRetargetTimer = 0f;

        bool anyDroneNeedsTarget = false;
        for (int i = 0; i < activeDrones.Count; i++)
        {
            if (activeDrones[i].IsActive && activeDrones[i].CurrentTarget == null)
            {
                anyDroneNeedsTarget = true;
                break;
            }
        }
        if (!anyDroneNeedsTarget) return;

        droneClaimedTargets.Clear();
        for (int i = 0; i < activeDrones.Count; i++)
        {
            ITreeObj existingTarget = activeDrones[i].CurrentTarget;
            if (existingTarget != null)
            {
                droneClaimedTargets.Add(existingTarget);
            }
        }

        for (int i = 0; i < activeDrones.Count; i++)
        {
            if (activeDrones[i].IsActive == false || activeDrones[i].CurrentTarget != null) continue;

            ITreeObj target = FindNearestTreeForDrone(activeDrones[i].transform.position);
            if (target == null) continue;

            droneClaimedTargets.Add(target);
            activeDrones[i].AssignTarget(target);
        }
    }

    private ITreeObj FindNearestTreeForDrone(Vector3 _origin)
    {
        if (CollisionSystem.Instance == null) return null;

        CollisionSystem.Instance.GetCollidablesInRadius(_origin, statComponent.droneAttackRange, treeLayer.value, droneScanResults);

        ITreeObj nearest = null;
        float nearestIsoSqr = float.MaxValue;
        Vector2 originPos = _origin;

        for (int i = 0; i < droneScanResults.Count; i++)
        {
            if (droneScanResults[i] is not ITreeObj treeObj || treeObj.bDead || droneClaimedTargets.Contains(treeObj))
            {
                continue;
            }

            // 죽었다가 그루터기->묘목으로 리셋된 나무(TreeObj.ResetTree/SetIsSapling)는 bDead가 다시
            // false로 돌아가지만 아직 공격할 수 없으므로, IDamageable.bCanApplyDamage로 한 번 더 거른다.
            bool canApplyDamage = (treeObj as IDamageable)?.bCanApplyDamage ?? true;

            // 방금 죽어서 오브젝트 풀로 반환된 나무(InDungeonObjectManager.OnTreeDead -> OnReleaseTree)는
            // bDead/bIsSapling이 둘 다 다시 false로 리셋되지만 GameObject는 비활성화된다. CollisionSystem은
            // 이때 Unregister되지 않아 여전히 검색에 걸리므로, 비활성 오브젝트는 여기서 반드시 걸러야 한다.
            Transform treeTransform = treeObj.GetTransform();
            bool isActiveInScene = treeTransform != null && treeTransform.gameObject.activeInHierarchy;

            if (canApplyDamage && isActiveInScene)
            {
                float isoSqr = GetIsometricDistSq(droneScanResults[i].Position, originPos);
                if (isoSqr < nearestIsoSqr)
                {
                    nearestIsoSqr = isoSqr;
                    nearest = treeObj;
                }
            }
        }

        return nearest;
    }

    // Drone이 주 타겟에 데미지를 입히는 순간마다(Drone.SetChainAttackCallback으로 등록) 호출된다.
    // 레이저 이펙트(muzzle -> 주 타겟 top)는 연쇄공격 스킬 해금 여부와 무관하게 항상 나간다.
    // droneChainCount가 0(스킬 미해금)이면 그 뒤의 실제 "전이"(추가 대상 탐색/데미지)만 일어나지 않는다.
    // 해금된 경우 방금 맞은 나무를 기점으로 droneChainRange 반경 안에서 아직 맞지 않은 가장 가까운
    // 나무를 찾아 데미지를 입히고, 그 나무를 다시 기점 삼아 최대 droneChainCount번까지 반복한다
    // (중간에 대상을 못 찾으면 중단).
    private void OnDroneChainAttack(Drone _drone, ITreeObj _primaryTarget)
    {
        if (statComponent == null || _primaryTarget == null) return;

        Transform primaryTransform = _primaryTarget.GetTransform();
        if (primaryTransform == null) return;

        // "드론 과부하" 특성을 찍어야만 과열 상태의 드론 강화 효과가 적용된다.
        bool bIsOverheat = overheatComponent != null && overheatComponent.IsActive && statComponent.bDroneOverheatBoost;

        if (bIsOverheat && _primaryTarget is TreeObj primaryTree)
        {
            primaryTree.ApplyDroneOverheatDot(10000f, 6, 0.5f);
        }

        Vector3 primaryTopPos = GetTreeTopPosition(_primaryTarget);

        droneChainZapPoints.Clear();
        droneChainZapPoints.Add(_drone.GetMuzzlePosition());
        droneChainZapPoints.Add(primaryTopPos);
        _drone.PlayAtkHitVfx(primaryTopPos);

        int finalChainCount = statComponent.droneChainCount;
        float finalChainRange = statComponent.droneChainRange;

        if (bIsOverheat)
        {
            finalChainCount *= 2;
            finalChainRange *= 5f;
        }

        if (finalChainCount > 0)
        {
            droneChainHitTrees.Clear();
            droneChainHitTrees.Add(_primaryTarget);

            Vector3 origin = primaryTransform.position;

            for (int i = 0; i < finalChainCount; i++)
            {
                ITreeObj next = FindNearestChainTarget(origin, finalChainRange);
                if (next == null) break;

                (next as IDamageable)?.TakeDamage(statComponent.droneDamage);

                if (bIsOverheat && next is TreeObj nextTree)
                {
                    nextTree.ApplyDroneOverheatDot(10000f, 6, 0.5f);
                }

                Vector3 nextTopPos = GetTreeTopPosition(next);
                droneChainHitTrees.Add(next);
                droneChainZapPoints.Add(nextTopPos);
                _drone.PlayAtkHitVfx(nextTopPos);
                origin = next.GetTransform().position;
            }
        }

        _drone.PlayChainZap(droneChainZapPoints, droneChainZapPoints.Count);
    }

    // droneChainHitTrees(이번 전이 동안 이미 맞은 나무)를 제외하고, origin 기준 지정된 반경(_chainRange)
    // 안에서 가장 가까운 살아있는 나무를 찾는다. FindNearestTreeForDrone과 동일한 유효성
    // 판정(bCanApplyDamage, activeInHierarchy)을 사용한다.
    private ITreeObj FindNearestChainTarget(Vector3 _origin, float _chainRange)
    {
        if (CollisionSystem.Instance == null) return null;

        CollisionSystem.Instance.GetCollidablesInRadius(_origin, _chainRange, treeLayer.value, droneScanResults);

        ITreeObj nearest = null;
        float nearestIsoSqr = float.MaxValue;
        Vector2 originPos = _origin;

        for (int i = 0; i < droneScanResults.Count; i++)
        {
            if (droneScanResults[i] is not ITreeObj treeObj || treeObj.bDead || droneChainHitTrees.Contains(treeObj))
            {
                continue;
            }

            bool canApplyDamage = (treeObj as IDamageable)?.bCanApplyDamage ?? true;

            Transform treeTransform = treeObj.GetTransform();
            bool isActiveInScene = treeTransform != null && treeTransform.gameObject.activeInHierarchy;

            if (canApplyDamage && isActiveInScene)
            {
                float isoSqr = GetIsometricDistSq(droneScanResults[i].Position, originPos);
                if (isoSqr < nearestIsoSqr)
                {
                    nearestIsoSqr = isoSqr;
                    nearest = treeObj;
                }
            }
        }

        return nearest;
    }

    #endregion

    #region Unity Event Functions

    private void Update()
    {
        stateMachine?.Update();

        UpdateDroneFormation(); // 캐릭터 이동 방향에 맞춰 드론 대형 슬롯을 매 프레임 회전/갱신
        UpdateDroneSeparation(Time.deltaTime); // 드론끼리 겹치지 않도록 매 프레임 살짝 밀어냄(안전망)

        // 조준점 및 무기 방향을 먼저 갱신한 뒤 비주얼을 렌더링하여 1프레임 지연을 제거
        UpdateFacingByAttackPoint();
        ConnectAttackToArm();

        // 비주얼 업데이트
        characterVisualComponent.UpdateVisuals(bMoving, !bInDungeon, bDead);
        UpdateItemAcquireBounce(Time.deltaTime);

        // 스태미나 로직
        UpdateStaminaAmounts(); // 실시간 소모량 갱신 반영
        if (true == bStaminaUpDown) healthComponent.IncreaseStamina();
        else healthComponent.DecreaseStamina();

        float staminaRatio = healthComponent.GetCurrentStamina() / Mathf.Max(1f, healthComponent.GetMaxStamina());

        // 던전에서 피로도(스태미나)가 낮아질수록 BGM/SFX가 서서히 먹먹해지는 긴박감 연출.
        // 타운에서는 적용하지 않으므로 던전을 벗어나면 매 프레임 1f(먹먹함 없음)로 되돌린다.
        // 스태미나 고갈로 사망한 뒤에도 꺼준다 - 그대로 두면 사망/결과창이 거는 별도의 덕킹과
        // 겹쳐 지나치게 먹먹해진다(ApplyCombinedCutoff가 둘 중 더 낮은 값을 취하기 때문).
        Sound.SetFatigueRatio(bInDungeon && !bDead ? staminaRatio : 1f);

        // 피로도가 낮을수록(임계 비율 이하 구간에서) 색수차가 연속적으로 짙어진다. Sound.SetFatigueRatio와
        // 같은 이유로 던전 밖/사망 후에는 1f(임계 비율보다 항상 커서 목표 세기가 0)를 넘겨 꺼둔다.
        // 사망 순간의 강한 펄스는 여기가 아니라 StaminaIsEmpty()의 PlayDeathChromaticAberrationPulse가 담당한다.
        PostProcessSettingsApplier.Instance?.UpdateLowStaminaChromaticAberration(bInDungeon && !bDead ? staminaRatio : 1f);

        // 용암 등 위험 지형 인접 시 추가 소모. 단, 일반 스태미나 소모(DecreaseStamina)와 마찬가지로
        // "실제 플레이가 시작된 이후"에만 적용해야 한다. bWhileReset(입장 카메라 연출 중, 조작 불가) 또는
        // bDead 상태에서까지 깎으면, 스태미나가 매우 빠르게 닳는 스테이지(예: 최대치가 낮은 상태의 MagmaForest)에서
        // 입장 연출 도중에 사망이 발생해 사망 시퀀스가 아직 끝나지 않은 입장 시퀀스와 충돌하고,
        // 공유 카메라(isMoved 토글)·입력 잠금 플래그가 꼬여 마을에서 캐릭터가 죽은 채로 남고 입력이 먹통이 된다.
        CurrentCell = environmentProvider.tilemapDataProvider.WorldToCell(transform.position);
        if (true == bInDungeon && false == bWhileReset && false == bDead)
        {
            float hazardDrain = environmentProvider.tilemapDataProvider.GetHazardStaminaDrainPerSecond(CurrentCell);
            if (0f < hazardDrain)
            {
                healthComponent.ApplyEnvironmentalStaminaDrain(hazardDrain);
                overheatComponent?.AddOverheatDuration(2f * Time.deltaTime); // 용암 열기 노출 1초당 2초 비율로 연속 적립
            }
        }
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
        UpdateDroneRetargeting(); // 내부적으로 DroneRetargetInterval마다만 실제 재타겟팅을 수행함

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

    /// <summary>
    /// 조준(마우스 추적)만 먼저 켠다. AttackIndicator 노출과 공격 허용은 포함하지 않으므로,
    /// 연출상 인디케이터를 나중에 띄우면서도 조작이 풀리는 즉시 캐릭터가 마우스를 바라보게 할 수 있다.
    /// </summary>
    public void EnableAim()
    {
        attackComponent.SetCursorEnable(true);
        UpdateFacingByAttackPoint();
    }

    public void ActivateCharacter()
    {
        // 조준이 아직 꺼져 있을 때만 초기 자세로 리셋한다. EnableAim()으로 이미 조준이 켜져
        // 마우스를 따라가는 중인데 리셋하면, 팔이 정면 아래로 튕겼다가 다시 마우스 쪽으로
        // 돌아가는 움직임이 눈에 보인다.
        if (attackComponent.IsCursorEnabled == false)
        {
            armComponent.ResetRotation();
            attackComponent.ResetAttackTransform();
            attackComponent.SetCursorEnable(true);
        }

        attackComponent.SetEnable(true);
        attackComponent.SetbCanAttack(true);
        armComponent.SetbCanAttack(true);
        bWhileReset = false;
    }

    public void StartDecreaseStamina()
    {
        healthComponent.SetStaminaDecrease(true);
    }

    public void SetMinStaminaPercent(float _percent)
    {
        healthComponent.SetMinStaminaPercent(_percent);
    }

    public void ResetStatus()
    {
        ClearActiveBoomerangs();
        treeScanTimer = 0f;
        boomerangCooldownTimer = 0f;
        bBoomerangSystemPaused = false;

        ClearActiveDrones();
        SpawnDrones(); // 던전 안에서의 리셋(사망 등)이므로 드론은 그대로 다시 소환해 계속 따라다니게 한다

        attackComponent.ResetAttackTransform();
        armComponent.ResetRotation();
        bWhileReset = true;
        armComponent.ResetWeaponStatus();
        bWhileSwing = false;
        healthComponent.StaminaReset();
        statComponent.ResetSpeed();
        bCanRotate = true;
        attackComponent.ResetAttackComponent();
        characterVisualComponent?.ResetFlash();
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
