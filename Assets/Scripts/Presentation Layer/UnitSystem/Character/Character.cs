using UnityEngine;

public class Character : MonoBehaviour, ITeleportable, ICharacter
{
    //외부 의존성
    public InputManager inputManager { get; private set; }
    private IEnvironmentProvider environmentProvider;

    //내부 의존성 (컴포넌트)
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private TriggerProxy shadowSensor; // 특정 콜라이더 감지용 센서
    [SerializeField] private GameObject itemSensor;

    private Rigidbody2D itemSensorRB;

    private AttackComponent attackComponent;
    private PHealthComponent healthComponent;

    private SpriteRenderer sr;
    private SpriteRenderer shadowSR;
    private Material characterMaterial;

    private int shadowOverlapCount = 0;
    private Color normalColor = Color.white;
    private Color shadowTint = new Color(0.6f, 0.6f, 0.7f, 1f);

    private static readonly int baseColorHash = Shader.PropertyToID("_BaseColor");

    public StateMachine stateMachine { get; private set; }
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public Collider2D col { get; private set; }

    //현재 지형 물리 데이터 (캐싱)
    public GroundPhysicsData currentGroundData { get; private set; }

    public IPHealthComponent pHealthComponent => healthComponent;

    // 캐싱된 해시값 (GC 방지 및 성능 최적화)
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public readonly int isMovingHash = Animator.StringToHash("IsMoving");

    private float staminaDecAmount = 0f;
    private float staminaIncAmount = 0f;
    private bool bStaminaUpDown = false;

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
        healthComponent = GetComponentInChildren<PHealthComponent>();
        itemSensorRB = itemSensor.GetComponent<Rigidbody2D>();


        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();
        characterMaterial = sr.material;

        shadowObject.Initialize();
        attackComponent.Initialize(componentCtx);
        healthComponent.Initialize(componentCtx);

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

    public void SetStaminaUpDownState(bool _bStaminaUpDown, float _staminaDecAmount, float _staminaIncAmount)
    {
        bStaminaUpDown = _bStaminaUpDown;

        staminaDecAmount = _staminaDecAmount;
        staminaIncAmount = _staminaIncAmount;
    }

    private void SetupStateMachine()
    {
        AddState(new IdleState());
        AddState(new RunState());

        // 초기 상태 설정
        stateMachine.ChangeState<IdleState>();
    }

    private void AddState(CharacterState _state)
    {
        _state.Initialize(stateMachine, this);
        stateMachine.AddState(_state);
    }

    private void Update()
    {
        stateMachine?.Update();
        shadowSR.sprite = sr.sprite;
        UpdateCharacterColor();

        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(environmentProvider.shadowDataProvider.CurrentShadowRotation, environmentProvider.shadowDataProvider.CurrentShadowScaleY, false);
        }

        if (bStaminaUpDown == true)
        {
            IncreaseStamina();
        }
        else
        {
            DecreaseStamina();
        }
    }

    private void FixedUpdate()
    {
        SetItemSensorPos();

        // 매 틱마다 현재 위치의 지형 정보를 갱신 (마찰력 적용을 위함)
        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);

        stateMachine?.FixedUpdate();
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

    private void DecreaseStamina()
    {
        healthComponent.DecreaseStamina(staminaDecAmount);
    }

    private void IncreaseStamina()
    {
        healthComponent.IncreaseStamina(staminaIncAmount);
    }

    private void UpdateCharacterColor()
    {
        if (characterMaterial == null) return;

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

    private void OnGUI()
    {
        if (healthComponent == null) return;

        // 화면 우측 하단 좌표 계산
        float width = 200f;
        float height = 50f;
        float posX = Screen.width - width - 10f;
        float posY = Screen.height - height - 10f;

        GUIStyle style = new GUIStyle();
        style.fontSize = 12;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.LowerRight;

        string debugText = $"Stamina: {healthComponent.CurrentStamina:F1} / {healthComponent.MaxStamina:F1}";
        GUI.Label(new Rect(posX, posY, width, height), debugText, style);
    }

    private void SetItemSensorPos()
    {
        itemSensorRB.MovePosition(transform.position);
    }

    public Transform GetTransform()
    {
        return transform;
    }
}
