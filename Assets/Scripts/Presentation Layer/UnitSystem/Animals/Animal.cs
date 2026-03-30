using UnityEngine;

public class Animal : MonoBehaviour
{
    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성 (컴포넌트)
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private TriggerProxy shadowSensor; // 특정 콜라이더 감지용 센서

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
    public bool bArrived = true;
    public Vector3 centerPos;
    public Vector3 targetPos;
    public float scatterRadius;

    //현재 지형 물리 데이터 (캐싱)
    public GroundPhysicsData currentGroundData { get; private set; }

    // 캐싱된 해시값 (GC 방지 및 성능 최적화)
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");

    private PathFindComponent pathFindComponent;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        stateMachine = new StateMachine();

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();
        pathFindComponent = GetComponentInChildren<PathFindComponent>();

        shadowObject.Initialize();
        pathFindComponent.Initialize(environmentProvider.tilemapDataProvider, environmentProvider.pathfindGridProvider);

        if (shadowSensor != null)
        {
            shadowSensor.OnTriggerEnterEvent += HandleShadowEnter;
            shadowSensor.OnTriggerExitEvent += HandleShadowExit;
        }

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

    public void MoveTo(Vector3 _endPos,Vector3 _centerPos,float _scatterRadius)
    {
        targetPos = _endPos;
        centerPos = _centerPos;
        scatterRadius = _scatterRadius;

        pathFindComponent.FindPath(transform.position, _endPos);
        bArrived = false;
        stateMachine.ChangeState<AS_RunState>();
    }

    private void SetupStateMachine()
    {
        AddState(new AS_IdleState());
        AddState(new AS_RunState());

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
}
