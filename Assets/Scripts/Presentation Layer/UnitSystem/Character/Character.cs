using System;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;

public class Character : MonoBehaviour, ITeleportable, ICharacter, IStaticCollidable
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

    [Header("Collision Settings")]
    [SerializeField] private float collisionRadius = 0.15f;
    [SerializeField] private Vector2 collisionOffset = new Vector2(0f, 0.12f);

    private AttackComponent attackComponent;
    private PHealthComponent healthComponent;
    private ArmComponent armComponent;
    public StatComponent statComponent { get; private set; }

    public StateMachine stateMachine { get; private set; }
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public CircleCollider2D col { get; private set; }
    private SpriteRenderer sr;
    private SpriteRenderer shadowSR;
    private Animator shadowAnim;

    // 상태 및 데이터
    [Header("Character Stats & States")]
    public GroundPhysicsData currentGroundData { get; private set; }
    public bool bInDungeon { get; private set; } = true;
    public bool bWhileSwing { get; private set; } = false;
    public bool bCanRotate { get; private set; } = true;

    private bool bIsUnderShadow = false;
    private float shadowLerp = 0f;
    private float currentFadeDuration = 0.3f;
    private Color normalColor = Color.white;
    private Color shadowTint = new Color(0.6f, 0.6f, 0.7f, 1f);

    private float staminaDecAmount = 0f;
    private float staminaIncAmount = 0f;
    private bool bStaminaUpDown = false;
    private float currentFacingAngle = 0f; // 캐릭터의 현재 바라보는 각도 저장

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;
    public int EntityIndex { get; set; } = -1;
    public void TakeDamage(float _damage) => healthComponent.DecreaseHealth(_damage);

    // 캐싱된 해시 및 프로퍼티 (성능 최적화)
    public IPHealthComponent pHealthComponent => healthComponent;

    IStatComponent ICharacter.statComponent => statComponent;

    IArmComponent ICharacter.armComponent => armComponent;

    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");
    public readonly int bInHubHash = Animator.StringToHash("bInHub");
    private float itemSensorRadius = 1.15f;
    private readonly List<IStaticCollidable> itemDetectionResults = new List<IStaticCollidable>(16);
    private float itemDetectionInterval = 0.2f; // 최적화: 0.2초 간격 (5Hz)
    private float itemDetectionTimer = 0f;

    [SerializeField] private LayerMask itemLayer; // 아이템 레이어

    #region Public Methods (Initialization & Control)

    public void Initialize(InputManager _inputManager, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;

        // 컴포넌트 할당
        anim = animatorObject.GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        attackComponent = GetComponentInChildren<AttackComponent>();
        healthComponent = GetComponentInChildren<PHealthComponent>();
        armComponent = GetComponentInChildren<ArmComponent>();
        statComponent = GetComponentInChildren<StatComponent>();

        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();
        shadowAnim = shadowObject.GetComponent<Animator>(); // 그림자 전용 애니메이터

        stateMachine = new StateMachine();
        ctx = new ComponentCtx();
        ctx.Initialize(inputManager, statComponent, environmentProvider.pathfindGridProvider, environmentProvider.tilemapDataProvider);

        // 컴포넌트 초기화
        shadowObject.Initialize();
        attackComponent.Initialize(ctx);
        healthComponent.Initialize(ctx);
        armComponent.Initialize(ctx);
        statComponent.Initialize(ctx);

        SetupStateMachine();
        BindEvents();
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f || bCanRotate == false || bWhileSwing == true) return;

        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        currentFacingAngle = angle; // 각도 저장
        SetAnimatorDirection(anim, sr, _input);
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

        if (_bInDungeon == false)
        {
            armComponent.ResetWeaponStatus();
            bWhileSwing = false;
            healthComponent.StaminaReset();
            statComponent.ResetSpeed();
            bCanRotate = true;
            attackComponent.ResetAttackComponent();
        }
        else
        {
            armComponent.ResetWeaponStatus();
            attackComponent.SetbCanSwap(true);
        }

        bInDungeon = _bInDungeon;
        anim.SetBool(bInHubHash, !bInDungeon);
        armComponent.SetActivate(bInDungeon);
    }

    public Transform GetTransform() => transform;

    public void SetInShadow(bool _isInShadow, float _duration)
    {
        bIsUnderShadow = _isInShadow;
        currentFadeDuration = _duration;
    }

    #endregion

    #region Private Methods

    private void SetupStateMachine()
    {
        AddState(new IdleState());
        AddState(new RunState());
        stateMachine.ChangeState<IdleState>();
    }

    private void AddState(CharacterState _state)
    {
        _state.Initialize(stateMachine, this, ctx);
        stateMachine.AddState(_state);
    }

    private void BindEvents()
    {
        attackComponent.WeaponModeChangedEvent -= WeaponModeChanged;
        attackComponent.WeaponModeChangedEvent += WeaponModeChanged;

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

            armComponent.rifleComponent.DeclareCanSwapEvent -= SetbCanRotate;
            armComponent.rifleComponent.DeclareCanSwapEvent += SetbCanRotate;
        }
    }

    private void ReleaseEvents()
    {
        if (attackComponent != null)
            attackComponent.WeaponModeChangedEvent -= WeaponModeChanged;

        if (armComponent != null && armComponent.axeComponent != null)
        {
            armComponent.axeComponent.DeclareAttackStateEvent -= SetbCanAction;
            attackComponent.AttackSuccessEvent -= armComponent.axeComponent.DecreaseDurability;
            armComponent.axeComponent.AttackEvent -= attackComponent.Attack;
            healthComponent.StaminaIsEmptyEvent -= StaminaIsEmpty;
        }
    }

    private void UpdateCharacterColor()
    {
        float target = bIsUnderShadow ? 1f : 0f;
        float speed = currentFadeDuration > 0 ? 1.0f / currentFadeDuration : 100f;
        shadowLerp = Mathf.MoveTowards(shadowLerp, target, Time.deltaTime * speed);
        sr.color = Color.Lerp(normalColor, shadowTint, shadowLerp);
    }

    private void UpdateShadowVisual()
    {
        if (shadowAnim == null) return;

        // 1. 애니메이션 파라미터 동기화
        shadowAnim.SetBool(isMovingHash, anim.GetBool(isMovingHash));
        shadowAnim.SetBool(bInHubHash, anim.GetBool(bInHubHash));

        float shadowAngle = environmentProvider.shadowDataProvider.CurrentShadowAngle;
        float normalizedAngle = shadowAngle % 360;
        if (normalizedAngle < 0) normalizedAngle += 360;

        // 2. 수평 방향 강제 보정 로직 (그림자가 좌/우로 길게 늘어질 때)
        // 오른쪽 방향 (337.5 ~ 22.5도)
        if (normalizedAngle <= 22.5f || normalizedAngle >= 337.5f)
        {
            SetAnimatorDirection(shadowAnim, shadowSR, Vector2.right);
        }
        // 왼쪽 방향 (157.5 ~ 202.5도)
        else if (normalizedAngle >= 157.5f && normalizedAngle <= 202.5f)
        {
            SetAnimatorDirection(shadowAnim, shadowSR, Vector2.left);
        }
        else
        {
            // 3. 수직 또는 대각선 각도는 광원 시점(Light Perspective) 로직 적용
            float lightPerspectiveAngle = currentFacingAngle - shadowAngle + 90f;
            Vector2 lightViewDir = new Vector2(
                Mathf.Cos(lightPerspectiveAngle * Mathf.Deg2Rad), 
                Mathf.Sin(lightPerspectiveAngle * Mathf.Deg2Rad)
            );
            SetAnimatorDirection(shadowAnim, shadowSR, lightViewDir);
        }
    }

    private void SetAnimatorDirection(Animator _targetAnim, SpriteRenderer _targetSR, Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        bool flipX = false;
        int animIndex = -1;

        switch (dirIndex)
        {
            case 0: animIndex = 0; break; // 우
            case 1: animIndex = 1; break; // 우상
            case 2: animIndex = 2; break; // 상
            case 3: animIndex = 1; flipX = true; break; // 좌상
            case 4: animIndex = 0; flipX = true; break; // 좌
            case 5: animIndex = 4; flipX = true; break; // 좌하
            case 6: animIndex = 3; break; // 하
            case 7: animIndex = 4; break; // 우하
        }

        if (animIndex != -1)
        {
            _targetSR.flipX = flipX;
            _targetAnim.SetFloat(facingDirHash, animIndex);
        }
    }

    private void UpdateFacingByAttackPoint()
    {
        if (attackComponent == null || bInDungeon == false) return;

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
        StaminaIsEmptyEvent?.Invoke();
    }

    private void UpdateItemDetection()
    {
        if (CollisionSystem.Instance == null) return;

        float finalRadius = itemSensorRadius * statComponent.pickupRangeMultiplier;
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, finalRadius, itemLayer.value, itemDetectionResults);
        for (int i = 0; i < itemDetectionResults.Count; i++)
        {
            if (itemDetectionResults[i] is Item item)
            {
                item.SetSuckTarget(transform);
            }
        }
    }

    #endregion

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }

    #region Unity Event Functions

    private void Update()
    {
        stateMachine?.Update();

        // 비주얼 업데이트
        UpdateCharacterColor();
        UpdateShadowVisual();

        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(
                environmentProvider.shadowDataProvider.CurrentShadowAngle,
                environmentProvider.shadowDataProvider.CurrentShadowScaleY,
                environmentProvider.shadowDataProvider.IsShadowActive);
        }

        // 스태미나 로직
        UpdateStaminaAmounts(); // 실시간 소모량 갱신 반영
        if (bStaminaUpDown) healthComponent.IncreaseStamina();
        else healthComponent.DecreaseStamina();

        UpdateFacingByAttackPoint();
        ConnectAttackToArm();
    }

    private void FixedUpdate()
    {
        itemDetectionTimer += Time.fixedDeltaTime;
        if (itemDetectionTimer >= itemDetectionInterval)
        {
            UpdateItemDetection();
            itemDetectionTimer = 0f;
        }

        // 커스텀 충돌 시스템 격자 정보 갱신
        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);
        stateMachine?.FixedUpdate();
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();

        ReleaseEvents();
        CollisionSystem.Instance?.Unregister(this);
    }

    private void OnGUI()
    {
        if (healthComponent == null) return;

        float width = 200f;
        float height = 50f;
        float posX = Screen.width - width - 10f;
        float posY = Screen.height - height - 10f;

        GUIStyle style = new GUIStyle { fontSize = 12, alignment = TextAnchor.LowerRight };
        style.normal.textColor = Color.white;

        string debugText = $"Stamina: {healthComponent.CurrentStamina:F1} / {healthComponent.MaxStamina:F1}";
        GUI.Label(new Rect(posX, posY, width, height), debugText, style);
    }

    #endregion

    public void RefreshCharacterStat()
    {
        armComponent.Refresh();
    }
}
