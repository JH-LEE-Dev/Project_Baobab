using UnityEngine;

public class Character : MonoBehaviour, ITeleportable
{
    //외부 의존성
    public InputManager inputManager { get; private set; }
    private IEnvironmentProvider environmentProvider;

    //내부 의존성 (컴포넌트)
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;

    private AttackComponent attackComponent;

    private SpriteRenderer sr;
    private SpriteRenderer shadowSR;

    public StateMachine stateMachine { get; private set; }
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public Collider2D col { get; private set; }

    //현재 지형 물리 데이터 (캐싱)
    public GroundPhysicsData currentGroundData { get; private set; }

    // 캐싱된 해시값 (GC 방지 및 성능 최적화)
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");

    public void Initialize(InputManager _inputManager, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;

        stateMachine = new StateMachine();
        ComponentCtx componentCtx = new ComponentCtx();
        componentCtx.Initialize(inputManager);

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        attackComponent = GetComponentInChildren<AttackComponent>();

        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();

        shadowObject.Initialize(environmentProvider.shadowDataProvider);
        attackComponent.Initialize(componentCtx);

        SetupStateMachine();
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        // 8방향 인덱스 계산 (0: 우, 1: 우상, 2: 상, 3: 좌상, 4: 좌, 5: 좌하, 6: 하, 7: 우하)
        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        anim.SetFloat(facingDirHash, dirIndex);
    }

    private void SetupStateMachine()
    {
        AddState(new IdleState());
        AddState(new RunState());

        // 초기 상태 설정
        stateMachine.ChangeState<IdleState>();
    }

    private void AddState(State _state)
    {
        _state.Initialize(stateMachine, this);
        stateMachine.AddState(_state);
    }

    private void Update()
    {
        stateMachine?.Update();
        shadowSR.sprite = sr.sprite;
    }

    private void FixedUpdate()
    {
        // 매 틱마다 현재 위치의 지형 정보를 갱신 (마찰력 적용을 위함)
        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);

        stateMachine?.FixedUpdate();
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();
    }
}
