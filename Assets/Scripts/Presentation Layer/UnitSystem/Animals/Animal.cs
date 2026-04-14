using System;
using UnityEngine;

public class Animal : MonoBehaviour, IDamageable
{
    public event Action<Animal> AnimalIsDeadEvent;
    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성 (컴포넌트)
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private TriggerProxy shadowSensor; // 특정 콜라이더 감지용 센서
    [SerializeField] private TriggerProxy characterSensorProxy;

    public StateMachine stateMachine { get; private set; }
    private SpriteRenderer sr;
    private SpriteRenderer shadowSR;
    private int shadowOverlapCount = 0;
    private Color normalColor = Color.white;
    private Color shadowTint = new Color(0.6f, 0.6f, 0.7f, 1f);

    private static readonly int baseColorHash = Shader.PropertyToID("_BaseColor");

    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public Collider2D col { get; private set; }
    public Collider2D characterSensor { get; private set; }

    //현재 지형 물리 데이터 (캐싱)
    public GroundPhysicsData currentGroundData { get; private set; }

    // 캐싱된 해시값 (GC 방지 및 성능 최적화)
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");

    private PathFindComponent pathFindComponent;

    //군중 제어 코드
    public Vector3 centerPos;
    public Vector3 targetPos;
    public float scatterRadius;

    //도망 코드
    public bool bRunAway = false;
    public Vector3 FleeDirection { get; private set; }
    private Transform playerTransform;

    public AnimalAnimValueHandler animalAnimValueHandler { get; private set; }

    private EHealthComponent healthComponent;

    public bool bDead { get; private set; } = false;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        stateMachine = new StateMachine();
        animalAnimValueHandler = new AnimalAnimValueHandler();

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();
        pathFindComponent = GetComponentInChildren<PathFindComponent>();
        healthComponent = GetComponentInChildren<EHealthComponent>();
        healthComponent.Initialize();

        if (characterSensorProxy != null)
        {
            characterSensor = characterSensorProxy.GetComponent<Collider2D>();
        }

        shadowObject.Initialize();
        pathFindComponent.Initialize(environmentProvider.tilemapDataProvider, environmentProvider.pathfindGridProvider);

        if (shadowSensor != null)
        {
            shadowSensor.OnTriggerEnterEvent += HandleShadowEnter;
            shadowSensor.OnTriggerExitEvent += HandleShadowExit;
        }

        if (characterSensorProxy != null)
        {
            characterSensorProxy.OnTriggerEnterEvent += HandleCharacterEnter;
            characterSensorProxy.OnTriggerExitEvent += HandleCharacterExit;
        }

        SetupStateMachine();

        animalAnimValueHandler.Initialize(anim);
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        // 각도 계산 및 3방향 매핑 (0: 우/좌, 1: 상, 2: 하)
        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = 0;
        bool shouldFlip = false;

        if (angle > 45 && angle <= 135)
        {
            dirIndex = 1; // Up
        }
        else if (angle > 135 && angle <= 225)
        {
            dirIndex = 0; // Right (Flipped to Left)
            shouldFlip = true;
        }
        else if (angle > 225 && angle <= 315)
        {
            dirIndex = 2; // Down
        }
        else
        {
            dirIndex = 0; // Right
            shouldFlip = false;
        }

        anim.SetFloat(facingDirHash, dirIndex);

        // 좌측 방향일 경우 transform 반전 처리
        Vector3 localScale = animatorObject.transform.localScale;
        localScale.x = shouldFlip ? -1f : 1f;
        animatorObject.transform.localScale = localScale;
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
        stateMachine?.Update();

        if (bRunAway && playerTransform != null)
        {
            FleeDirection = (transform.position - playerTransform.position).normalized;
        }

        shadowSR.sprite = sr.sprite;
        UpdateAnimalColor();

        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(environmentProvider.shadowDataProvider.CurrentShadowRotation, environmentProvider.shadowDataProvider.CurrentShadowScaleY, false);
        }
    }

    private void FixedUpdate()
    {
        stateMachine?.FixedUpdate();

        // 매 틱마다 현재 위치의 지형 정보를 갱신 (마찰력 적용을 위함)
        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();

        if (shadowSensor != null)
        {
            shadowSensor.OnTriggerEnterEvent -= HandleShadowEnter;
            shadowSensor.OnTriggerExitEvent -= HandleShadowExit;
        }

        if (characterSensorProxy != null)
        {
            characterSensorProxy.OnTriggerEnterEvent -= HandleCharacterEnter;
            characterSensorProxy.OnTriggerExitEvent -= HandleCharacterExit;
        }
    }

    private void UpdateAnimalColor()
    {
        Color targetColor = (shadowOverlapCount > 0) ? shadowTint : normalColor;

        sr.color = targetColor;
    }

    private void HandleShadowEnter(Collider2D _other)
    {
        if (_other.CompareTag("TreeShadow"))
        {
            shadowOverlapCount++;
        }
    }

    private void HandleShadowExit(Collider2D _other)
    {
        if (_other.CompareTag("TreeShadow"))
        {
            shadowOverlapCount = Mathf.Max(0, shadowOverlapCount - 1);
        }
    }

    private void HandleCharacterEnter(Collider2D _other)
    {
        // "Player" 태그를 가진 객체가 감지되면 도망 상태 활성화
        if (_other.CompareTag("Player"))
        {
            playerTransform = _other.transform;
            bRunAway = true;
        }
    }

    private void HandleCharacterExit(Collider2D _other)
    {
        // 플레이어가 감지 범위를 벗어나면 도망 상태 해제
        if (_other.CompareTag("Player"))
        {
            playerTransform = null;
            bRunAway = false;
        }
    }

    public void TakeDamage(float _damage)
    {
        healthComponent.DecreaseHealth(_damage);

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

        if (healthComponent != null)
            healthComponent.Reset();

        if (stateMachine != null)
            stateMachine.ChangeState<AS_IdleState>();
    }
}
