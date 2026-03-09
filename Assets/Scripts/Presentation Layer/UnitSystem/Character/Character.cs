using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Character : MonoBehaviour, ITeleportable
{
    //외부 의존성
    public InputManager inputManager { get; private set; }
    private IEnvironmentProvider environmentProvider;

    //내부 의존성 (컴포넌트)
    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private Light2D spotLight;
    [SerializeField] private float maxLightIntensity = 1.0f; // 밤일 때의 최대 밝기

    private AttackComponent attackComponent;
    private HealthComponent healthComponent;

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

    private float staminaDecAmount =0f;
    private float staminaIncAmount =0f;
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
        healthComponent = GetComponentInChildren<HealthComponent>();

        sr = animatorObject.GetComponent<SpriteRenderer>();
        shadowSR = shadowObject.GetComponent<SpriteRenderer>();

        shadowObject.Initialize(environmentProvider.shadowDataProvider);
        attackComponent.Initialize(componentCtx);
        healthComponent.Initialize(componentCtx);

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

    private void AddState(State _state)
    {
        _state.Initialize(stateMachine, this);
        stateMachine.AddState(_state);
    }

    private void Update()
    {
        stateMachine?.Update();
        shadowSR.sprite = sr.sprite;

        if(bStaminaUpDown == true)
        {
            IncreaseStamina();
        }
        else
        {
            DecreaseStamina();
        }

        UpdateSpotLight();
    }

    private void FixedUpdate()
    {
        // 매 틱마다 현재 위치의 지형 정보를 갱신 (마찰력 적용을 위함)
        currentGroundData = environmentProvider.groundDataProvider.GetGroundPhysicsData(transform.position);

        stateMachine?.FixedUpdate();
        
        if (spotLight != null)
        {
            spotLight.transform.position = transform.position;
        }
    }

    private void UpdateSpotLight()
    {
        if (spotLight == null || environmentProvider?.shadowDataProvider == null) return;

        float timePercent = environmentProvider.shadowDataProvider.currentTimePercent;

        // 밤에 켜지고 낮에 꺼지는 로직
        // 0.20 ~ 0.30 (일출): 불이 서서히 꺼짐
        // 0.70 ~ 0.80 (일몰): 불이 서서히 켜짐
        float morningFade = 1f - Mathf.InverseLerp(0.20f, 0.30f, timePercent);
        float eveningFade = Mathf.InverseLerp(0.70f, 0.80f, timePercent);
        
        // 최종 강도는 오전 페이드나 오후 페이드 중 더 강한 쪽 (밤 시간대 커버)
        // 0.80 ~ 0.20(다음날) 사이는 항상 1에 가깝게 유지됨
        float finalIntensityMultiplier = Mathf.Max(morningFade, eveningFade);
        
        spotLight.intensity = maxLightIntensity * finalIntensityMultiplier;
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();
    }

    private void DecreaseStamina()
    {
        healthComponent.DecreaseStamina(staminaDecAmount);
    }

    private void IncreaseStamina()
    {
        healthComponent.IncreaseStamina(staminaIncAmount);
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
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.LowerRight;

        string debugText = $"Stamina: {healthComponent.CurrentStamina:F1} / {healthComponent.MaxStamina:F1}";
        GUI.Label(new Rect(posX, posY, width, height), debugText, style);
    }
}
