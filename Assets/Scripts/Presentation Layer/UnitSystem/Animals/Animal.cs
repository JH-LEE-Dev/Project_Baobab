using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal : MonoBehaviour, IDamageable, IStaticCollidable, IAnimalObj
{
    public event Action<Animal> AnimalHitEvent;
    public event Action<Animal> AnimalIsDeadEvent;
    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성 (컴포넌트)
    [Header("Internal Components")]
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;

    [Header("Collision & Detection")]
    [SerializeField] private float collisionRadius = 0.14f;
    [SerializeField] private Vector2 collisionOffset = new Vector2(0.02f, 0.09f);
    [SerializeField] private float detectionRadius = 2.75f;
    [SerializeField] private LayerMask detectionLayerMask;

    public StateMachine stateMachine { get; private set; }
    private SpriteRenderer sr;
    private SpriteRenderer shadowSR;
    private Animator shadowAnim;

    private bool bIsUnderShadow = false;
    private float shadowLerp = 0f;
    private float currentFadeDuration = 0.3f;
    private Color normalColor = Color.white;
    private Color shadowTint = new Color(0.6f, 0.6f, 0.7f, 1f);

    private static readonly int baseColorHash = Shader.PropertyToID("_BaseColor");

    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public Collider2D col { get; private set; }

    //현재 지형 물리 데이터 (캐싱)
    public GroundPhysicsData currentGroundData { get; private set; }

    // 캐싱된 해시 및 결과 리스트 (GC 방지 및 성능 최적화)
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly List<IStaticCollidable> detectionResults = new List<IStaticCollidable>(4);

    private PathFindComponent pathFindComponent;

    //군중 제어 코드
    public Vector3 centerPos;
    public Vector3 targetPos;
    public float scatterRadius;

    //도망 코드
    public bool bRunAway = false;
    public Vector3 FleeDirection { get; private set; }
    private Vector2 detectedCharacterPos;
    private float currentFacingAngle = 0f;

    public AnimalAnimValueHandler animalAnimValueHandler { get; private set; }

    private EHealthComponent healthComponent;

    public bool bDead { get; private set; } = false;

    // 최적화: 감지 주기 관리
    private float detectionTimer = 0f;
    private const float DETECTION_INTERVAL = 0.2f; // 5Hz

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => collisionOffset; // 오프셋 반환
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;
    public int EntityIndex { get; set; } = -1;

    public IHealthComponent health => healthComponent;

    public bool bCanApplyDamage => true;

    public AnimalType animalType;

    public GameObject statusEffectObject;

    public GameObject feetShadowObject;

    public bool bActivated = true;

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        stateMachine = new StateMachine();
        animalAnimValueHandler = new AnimalAnimValueHandler();

        anim = animatorObject.GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // 메인 콜라이더 비활성화 (물리 엔진 부하 제거)
        if (col != null) col.enabled = false;

        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();
        shadowAnim = shadowObject.GetComponent<Animator>();
        pathFindComponent = GetComponent<PathFindComponent>();
        healthComponent = GetComponent<EHealthComponent>();
        healthComponent.Initialize();

        shadowObject.Initialize();
        pathFindComponent.Initialize(environmentProvider.tilemapDataProvider, environmentProvider.pathfindGridProvider);

        Hide();
        SetupStateMachine();

        animalAnimValueHandler.Initialize(anim, shadowAnim);

        if (statusEffectObject != null)
            statusEffectObject.SetActive(false);

        if (feetShadowObject != null)
            feetShadowObject.SetActive(false);
    }

    public void Hide()
    {
        shadowSR.enabled = false;
        sr.enabled = false;
        feetShadowObject.SetActive(false);
        shadowAnim.enabled = false;
        bActivated = false;

        // 물리 속도 및 애니메이션 초기화
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.SetBool(isMovingHash, false);

        // 동적 객체에서 제거 (위치 인자 없이 안전하게 제거)
        CollisionSystem.Instance?.Unregister(this);
    }

    public void Show()
    {
        shadowSR.enabled = true;
        sr.enabled = true;
        shadowAnim.enabled = true;
        bActivated = true;

        // 동적 객체(동물)로 등록
        CollisionSystem.Instance?.Register(this, false);
    }

    private void OnEnable()
    {

    }

    private void OnDisable()
    {
        // 동적 객체에서 제거
        CollisionSystem.Instance?.Unregister(this);
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;
        currentFacingAngle = angle;

        // 90도 단위로 명확하게 4방향 구분 (absX, absY 비교)
        float absX = Mathf.Abs(_input.x);
        float absY = Mathf.Abs(_input.y);

        int dirIndex = 0;
        bool shouldFlip = false;

        if (absX > absY)
        {
            dirIndex = 0; // Horizontal
            shouldFlip = _input.x < 0;
        }
        else
        {
            if (_input.y > 0) dirIndex = 1; // Up
            else dirIndex = 2; // Down
        }

        // 1. 본체 방향 및 반전 설정
        anim.SetFloat(facingDirHash, dirIndex);
        Vector3 bodyScale = animatorObject.transform.localScale;
        bodyScale.x = shouldFlip ? -1f : 1f;
        animatorObject.transform.localScale = bodyScale;
    }

    public void MoveTo(Vector3 _endPos, Vector3 _centerPos, float _scatterRadius)
    {
        targetPos = _endPos;
        centerPos = _centerPos;
        scatterRadius = _scatterRadius;

        pathFindComponent.FindPath(transform.position, _endPos);

        stateMachine.ChangeState<AS_RunState>();
    }

    private void SetupStateMachine()
    {
        AddState(new AS_IdleState());
        AddState(new AS_RunState());
        AddState(new AS_DeadState());
        AddState(new AS_KnockBackState());

        // 초기 상태 설정
        stateMachine.ChangeState<AS_IdleState>();
    }

    private void AddState(AnimalState _state)
    {
        _state.Initialize(stateMachine, this, pathFindComponent);
        stateMachine.AddState(_state);
    }

    private void Update()
    {
        // 죽었거나 숨겨진 상태에서는 상태 머신 업데이트 중단
        if (bDead || !bActivated) return;

        stateMachine?.Update();

        if (bRunAway)
        {
            FleeDirection = ((Vector2)transform.position - detectedCharacterPos).normalized;
        }

        // 본체가 숨겨진 상태(Hide)라면 시각적 업데이트를 중단하여 그림자가 다시 켜지는 것을 방지합니다.
        if (!sr.enabled) return;

        UpdateAnimalColor();

        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(
                environmentProvider.shadowDataProvider.CurrentShadowAngle,
                environmentProvider.shadowDataProvider.CurrentShadowScaleY,
                environmentProvider.shadowDataProvider.IsShadowActive);
        }

        UpdateShadowVisual();
    }

    private void FixedUpdate()
    {
        // 죽었거나 숨겨진 상태에서는 상태 머신 업데이트 및 로직 중단
        if (bDead || !bActivated) return;

        stateMachine?.FixedUpdate();

        // 커스텀 충돌 시스템 격자 정보 갱신 (위치 업데이트는 매번 수행)
        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        // 최적화: 플레이어 감지 로직 주기적 수행 (0.2초 간격)
        detectionTimer += Time.fixedDeltaTime;
        if (detectionTimer >= DETECTION_INTERVAL)
        {
            UpdateCharacterDetection();
            detectionTimer = 0f;
        }

        // 매 틱마다 현재 위치의 지형 정보를 갱신 (마찰력 적용을 위함)
        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);
    }

    private void UpdateCharacterDetection()
    {
        if (CollisionSystem.Instance == null) return;

        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, detectionRadius, detectionLayerMask, detectionResults);

        if (detectionResults.Count > 0)
        {
            detectedCharacterPos = detectionResults[0].Position;
            bRunAway = true;
        }
        else
        {
            bRunAway = false;
        }
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();

        // 등록 해제
        CollisionSystem.Instance?.Unregister(this);
    }

    private void UpdateAnimalColor()
    {
        float target = bIsUnderShadow ? 1f : 0f;
        float speed = currentFadeDuration > 0 ? 1.0f / currentFadeDuration : 100f;
        shadowLerp = Mathf.MoveTowards(shadowLerp, target, Time.deltaTime * speed);
        sr.color = Color.Lerp(normalColor, shadowTint, shadowLerp);
    }

    private void UpdateShadowVisual()
    {
        if (shadowAnim == null || shadowSR == null) return;

        bool isMoving = anim.GetBool(isMovingHash);
        shadowAnim.SetBool(isMovingHash, isMoving);

        // 광원 각도 동기화 및 방향 보정
        float shadowAngle = environmentProvider.shadowDataProvider.CurrentShadowAngle;

        // 캐릭터의 바라보는 방향을 4방향으로 스냅하여 본체 스프라이트 판정 로직과 동기화합니다.
        float snappedFacingAngle;
        float cos = Mathf.Cos(currentFacingAngle * Mathf.Deg2Rad);
        float sin = Mathf.Sin(currentFacingAngle * Mathf.Deg2Rad);

        if (Mathf.Abs(cos) > Mathf.Abs(sin))
        {
            snappedFacingAngle = (cos > 0) ? 0f : 180f;
        }
        else
        {
            snappedFacingAngle = (sin > 0) ? 90f : 270f;
        }

        // 상하 이동 중인지 확인 (90도: 위, 270도: 아래)
        bool isMovingVertical = (Mathf.Approximately(snappedFacingAngle, 90f) || Mathf.Approximately(snappedFacingAngle, 270f));

        // 2:1 아이소매트릭 보정을 위한 기본 가중치는 2.0입니다 (Y축이 0.5배이므로).
        // 상하 이동 중일 때는 좌우 판정 범위를 더 줄이기 위해 가중치를 높입니다 (예: 2.5).
        float thresholdMultiplier = isMovingVertical ? 2.5f : 1.25f;

        // 광원 시점(Light Perspective) 로직 적용
        // 2:1 아이소매트릭 비율을 반영하여 Y축 성분을 0.5배로 보정합니다.
        float rad = (snappedFacingAngle - shadowAngle + 90f) * Mathf.Deg2Rad;
        Vector2 lightViewDir = new Vector2(
            Mathf.Cos(rad),
            Mathf.Sin(rad) * 0.5f
        );
        SetAnimatorDirection(shadowAnim, shadowSR, lightViewDir, thresholdMultiplier);
    }

    private void SetAnimatorDirection(Animator _targetAnim, SpriteRenderer _targetSR, Vector2 _input, float _thresholdMultiplier = 1.0f)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        float absX = Mathf.Abs(_input.x);
        float absY = Mathf.Abs(_input.y);

        int dirIndex = 0;
        bool shouldFlip = false;

        // _thresholdMultiplier가 높을수록 수평(Horizontal) 판정이 일어나기 위해 더 큰 X값이 필요합니다.
        if (absX > absY * _thresholdMultiplier)
        {
            dirIndex = 0; // Horizontal
            shouldFlip = _input.x < 0;
        }
        else
        {
            if (_input.y > 0) dirIndex = 1; // Up
            else dirIndex = 2; // Down
        }

        // 애니메이터 파라미터 설정 및 반전 적용
        _targetAnim.SetFloat(facingDirHash, dirIndex);

        Vector3 scale = _targetSR.transform.localScale;
        scale.x = shouldFlip ? 1f : -1f;
        _targetSR.transform.localScale = scale;
    }

    public void TakeDamage(float _damage)
    {
        healthComponent.DecreaseHealth(_damage);
        AnimalHitEvent?.Invoke(this);

        if (healthComponent.GetCurrentHealth() == 0f)
        {
            stateMachine.ChangeState<AS_DeadState>();
            bDead = true;
            AnimalIsDeadEvent?.Invoke(this);
        }
    }

    public void Reset()
    {
        bDead = false;
        PoolIndex = -1;
        UpdateIndex = -1;

        if (healthComponent != null)
            healthComponent.Reset();

        if (stateMachine != null)
            stateMachine.ChangeState<AS_IdleState>();
    }

    public void DeActivate()
    {
        stateMachine.ChangeState<AS_DeadState>();
    }

    public void RunAway(Vector2 _characterPos)
    {
        StartCoroutine(RunAwayRoutine(_characterPos));
    }

    private IEnumerator RunAwayRoutine(Vector2 _characterPos)
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.25f, 0.5f));
        bRunAway = true;
        detectedCharacterPos = _characterPos;
    }

    public void KnockBack(Vector2 _knockBackDir, float _knockBackForce)
    {
        if (bDead) return;

        var state = stateMachine.GetState<AS_KnockBackState>();
        state.SetKnockBack(_knockBackDir, _knockBackForce);
        stateMachine.ChangeState<AS_KnockBackState>();
    }

    public void SetInShadow(bool _isInShadow, float _duration)
    {
        bIsUnderShadow = _isInShadow;
        currentFadeDuration = _duration;
    }

    public Transform GetTransform()
    {
        return gameObject.transform;
    }
}
