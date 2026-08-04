using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public class OffroadVehicleObj : MonoBehaviour, IOffroadProvider
{
    // 이벤트
    public event Action GameEndEvent;
    public event Action OffroadDriveEndEvent;
    public event Action PortalActivated;
    public event Action PortalDeActivatedEvent;
    public event Action<bool> OffroadInteractStateChangedEvent;
    public event Action<bool> RepairBoxInteractStateChangedEvent;

    [Header("Portal Settings")]
    [SerializeField] private PortalType type;

    [Space(10)]
    [Header("Visual Object References")]
    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject baseObject;
    [SerializeField] private GameObject wheelObjectForStencil;
    [SerializeField] private GameObject wheelObject;
    [SerializeField] private GameObject containerObject;
    [SerializeField] private GameObject visualObject;
    [SerializeField] private GameObject jitterVisualObject;
    [SerializeField] private GameObject containerShadowObj;

    [Space(10)]
    [Header("Container Jump Settings")]
    [SerializeField] private float containerJumpDuration = 0.5f;
    [SerializeField] private float containerJumpHeight = 1.5f;
    [SerializeField] private float containerSpringFrequency = 20f;
    [SerializeField] private float containerSpringDamping = 5f;

    [Space(10)]
    [Header("Drive Settings")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float shakeIntensity = 0.008f; // 떨림 세기 감소
    [SerializeField] private float ignitionShakeMultiplier = 5.0f; // 시동 시 떨림 배율
    [SerializeField] private float ignitionScaleIntensity = 0.3f; // 시동 시 최대 스케일 변화량
    [SerializeField] private float ignitionSpringFrequency = 18f; // 스프링 진동 주파수
    [SerializeField] private float ignitionSpringDamping = 6f; // 스프링 감쇄율
    [SerializeField] private float ignitionSquashDuration = 0.4f; // 스프링 연출이 일어나는 전체 시간
    [SerializeField] private float ignitionDelay = 0.1f; // 연출 후 대기 시간
    [SerializeField] private float reachThreshold = 0.1f;
    [Tooltip("Offroad_RunStart 클립에서 엔진이 걸리는 지점(클립 길이 대비 비율). 이 지점까지 먼저 사운드를 재생한 뒤 시동 임팩트 연출(먼지 파티클)이 나온다.")]
    [SerializeField] private float engineIgnitionCatchRatio = 0.1f;
    [Tooltip("시동이 걸린 뒤(=출발하는 구간) 남은 재생 시간 동안 서서히 도달하는 최대 피치")]
    [SerializeField] private float engineRunPitchTarget = 1.4f;

    [Space(10)]
    [Header("Point Transforms")]
    [SerializeField] private Transform containerCarryPoint;
    [SerializeField] private Transform containerDropPoint;
    public Transform CharacterRidePoint;
    public Transform getOffTransform;

    [Space(10)]
    [Header("Sprites Settings")]
    [SerializeField] private Sprite darkBaseSprite;
    [SerializeField] private Sprite darkWheelSprite;
    [SerializeField] private Sprite cinderBaseSprite;
    [SerializeField] private Sprite cinderWheelSprite;

    [Space(10)]
    [Header("VFX Settings")]
    [SerializeField] private Transform startUpEffectPoint;
    [SerializeField] private Transform goEffectPoint;
    [SerializeField] private Transform shinyEffectPoint;
    [SerializeField] private ParticleSystem.MinMaxGradient effectColor;
    [SerializeField] private float goEffectInterval = 0.2f;

    // 외부 의존성 및 컴포넌트
    private IEnvironmentProvider environmentProvider;
    private InputManager inputManager;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;
    private VFXComponent vfxComponent;
    private Animator wheelAnimator;
    private CustomSortable customSortable;
    private CustomSortable customSortable_wheel;
    private OffroadContainerVComponent offroadContainerVComponent;

    [Space(10)]
    [Header("Physics & Renderers")]
    public CircleCollider2D col;

    // Sprite Renderers
    public SpriteRenderer baseSR;
    public SpriteRenderer wheelSR;
    public SpriteRenderer containerSR;
    public SpriteRenderer wheelStencilSR;
    public SpriteRenderer baseOutlineStencilSR;
    public SpriteRenderer wheelOutlineStencilSR;
    public SpriteRenderer innerSR;

    // 내부 상태 변수들
    private int characterLayer;
    private float lastActivatedTime = -10.0f;
    private bool bCanJump = false;
    private bool bOverlapped = false;
    private bool bUIActivated = false;
    private Coroutine driveCoroutine;
    private AudioHandle engineStartHandle;
    private float colRadius;
    private bool bCanReach = true;
    private bool bCanInteract = false;
    private bool bPhysicalOverlapped = false;
    private bool bLastInteractState = false;
    private Transform charTransform;
    private Sprite originalBaseSprite;
    private Sprite originalWheelSprite;
    private Color originalContainerColor;
    private bool bOriginalSpritesSaved = false;

    [Space(10)]
    [Header("Shiny Effect Settings")]
    [SerializeField] private float shinyDuration = 0.15f;
    [SerializeField] private AnimationCurve shinyCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock _flashMPB;

    private StaminaRecoverCircle staminaRecoverCircle;

    private RepairBox repairBox;

    [Header("Repair Box Settings")]
    public float repairBoxCount = 0f;
    public float repairAmount = 0.25f;

    // 차량+컨테이너 발밑에 깔린(RepairBox 소유가 아닌) ColliderTilemap - 길찾기 상 이동 불가 타일로 등록하기 위함
    private TilemapFootprintCollider footprintCollider;
    private Tilemap footprintColliderTilemap;

    /// <summary>
    /// 차량 발밑 ColliderTilemap 원본 참조. TownTilemapDataProvider 등 외부 길찾기 시스템이
    /// 이 타일맵이 덮는 영역을 이동 불가 타일로 등록할 때 사용한다.
    /// </summary>
    public Tilemap FootprintColliderTilemap => footprintColliderTilemap;

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type, IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer, Transform _characterTransform)
    {
        offroadContainer = _offroadContainer;
        charTransform = _characterTransform;

        vfxComponent = GetComponent<VFXComponent>();

        col = GetComponent<CircleCollider2D>();
        colRadius = col.radius;

        if (_characterInventory != null)
            characterInventory = _characterInventory;

        environmentProvider = _environmentProvider;
        inputManager = _inputManager;
        type = _type;
        characterLayer = LayerMask.NameToLayer("Character");

        lastActivatedTime = Time.time;

        if (characterInventory != null)
        {
            offroadContainer.gameObject.SetActive(true);
        }
        else
            offroadContainer.gameObject.SetActive(false);

        customSortable = visualObject.GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.SetSortingGroup(visualObject.GetComponent<SortingGroup>());
        }

        if (wheelObject != null)
        {
            wheelAnimator = wheelObject.GetComponentInChildren<Animator>();
            wheelAnimator.speed = 0;
            wheelAnimator.enabled = false;
            customSortable_wheel = wheelObject.GetComponent<CustomSortable>();
            if (customSortable_wheel != null)
            {
                customSortable_wheel.Initialize(transform);
                customSortable_wheel.AddSpriteRenderer(wheelAnimator.GetComponent<SpriteRenderer>());
            }
        }

        offroadContainerVComponent = containerObject.GetComponentInChildren<OffroadContainerVComponent>();

        staminaRecoverCircle = GetComponent<StaminaRecoverCircle>();
        staminaRecoverCircle.Initialize(charTransform);

        repairBox = GetComponentInChildren<RepairBox>();
        if (repairBox != null)
        {
            repairBox.Initialize(_inputManager, charTransform, environmentProvider);
        }

        // 차량/컨테이너 자체의 ColliderTilemap을 찾는다. RepairBox 밑에 있는 것과 이름이 같아서(둘 다 "ColliderTilemap")
        // RepairBox의 하위 트리에 속하지 않는 것만 골라야 한다.
        footprintColliderTilemap = null;
        Tilemap[] allTilemaps = GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < allTilemaps.Length; i++)
        {
            if (repairBox != null && allTilemaps[i].transform.IsChildOf(repairBox.transform)) continue;
            footprintColliderTilemap = allTilemaps[i];
            break;
        }
        footprintCollider = new TilemapFootprintCollider(footprintColliderTilemap);
        footprintCollider.SetEnvironmentProvider(environmentProvider);

        BindEvents();
    }

    private void Update()
    {
        customSortable.SetHeight(0);

        CalcDistForCanReach();
    }

    public void ResetObject()
    {
        SetActiveWheelForStencil(true);
        lastActivatedTime = Time.time;
        bPhysicalOverlapped = false;
        UpdateInteractState();
        var conPos = containerObject.transform.position;
        conPos.y += 0.25f;
        offroadContainer.transform.position = conPos;
        offroadContainer.SetVisualTransform(containerObject.transform);
        containerObject.transform.position = containerDropPoint.position;
        offroadContainer.EnableCollision();

        containerShadowObj.SetActive(true);

        offroadContainer.ResetState();
        offroadContainerVComponent.Reset();
        ResetRepairBox();

        // 차량이 배치/재배치될 때마다 발밑 타일을 길찾기 불가 타일로 등록 (이전 위치의 등록은 먼저 해제)
        footprintCollider?.Register();

        // 던전 재입장 시 타일맵이 이후 프레임에 걸쳐 다시 초기화/갱신되면서 방금 한 등록이
        // 뒤늦게 걷어차이는 경우를 대비해, 몇 프레임 뒤 한 번 더 재등록해서 안전하게 확정한다.
        StopCoroutine(nameof(ReregisterFootprintDelayed));
        StartCoroutine(nameof(ReregisterFootprintDelayed));
    }

    private IEnumerator ReregisterFootprintDelayed()
    {
        yield return null;
        yield return null;
        footprintCollider?.Register();

        yield return new WaitForSeconds(0.2f);
        footprintCollider?.Register();
    }

    private void OnDisable()
    {
        footprintCollider?.Clear();
    }

    public void ResetRepairBox()
    {
        if (repairBoxCount > 0)
        {
            repairBox.gameObject.SetActive(true);
            repairBox.SetRepairBoxCount(repairBoxCount);
            repairBox.SetRepairAmount(repairAmount);
        }
        else
        {
            repairBox.gameObject.SetActive(false);
        }
    }

    public void DeActivateRepairBox()
    {
        repairBox.gameObject.SetActive(false);
    }

    public void SetVisualActive(bool _boolean)
    {
        offroadContainerVComponent.bActive = _boolean;
    }

    private void UpdateInteractState()
    {
        bool currentState = bCanJump && bCanReach && bPhysicalOverlapped;
        if (currentState != bLastInteractState)
        {
            bLastInteractState = currentState;
            bCanInteract = currentState;
            bOverlapped = currentState;

            OffroadInteractStateChangedEvent?.Invoke(currentState);
            outLineObject.SetActive(currentState);

            if (!currentState)
            {
                PortalDeActivatedEvent?.Invoke();
            }
        }
    }

    //유니티 이벤트 함수
    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            bPhysicalOverlapped = true;
            UpdateInteractState();
        }
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            if (bPhysicalOverlapped == false)
            {
                bPhysicalOverlapped = true;
                UpdateInteractState();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            bPhysicalOverlapped = false;
            UpdateInteractState();
        }
    }

    public void SetCanTravel(bool _canJump)
    {
        bCanJump = _canJump;
        UpdateInteractState();
    }

    public void BindEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
        inputManager.inputReader.InteractionKeyCanceledEvent += InteractionKeyCanceled;

        offroadContainer.ContainerOpenedEvent -= ContainerOpend;
        offroadContainer.ContainerOpenedEvent += ContainerOpend;
        offroadContainer.ContainerClosedEvent -= ContainerClosed;
        offroadContainer.ContainerClosedEvent += ContainerClosed;

        if (repairBox != null)
        {
            repairBox.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
            repairBox.RepairBoxInteractStateChangedEvent += RepairBoxInteractStateChanged;
        }

        if (type == PortalType.ToDungeonPortal)
        {
            offroadContainerVComponent.ContainerOpenedEvent -= ContainerVisualOpened;
            offroadContainerVComponent.ContainerOpenedEvent += ContainerVisualOpened;

            offroadContainerVComponent.ContainerClosedEvent -= ContainerVisualClosed;
            offroadContainerVComponent.ContainerClosedEvent += ContainerVisualClosed;
        }
    }

    public void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyCanceledEvent -= InteractionKeyCanceled;
        offroadContainer.ContainerOpenedEvent -= ContainerOpend;
        offroadContainer.ContainerClosedEvent -= ContainerClosed;

        if (repairBox != null)
        {
            repairBox.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        }

        if (type == PortalType.ToDungeonPortal)
        {
            offroadContainerVComponent.ContainerOpenedEvent -= ContainerVisualOpened;
            offroadContainerVComponent.ContainerClosedEvent -= ContainerVisualClosed;
        }
    }

    private void RepairBoxInteractStateChanged(bool _state)
    {
        RepairBoxInteractStateChangedEvent?.Invoke(_state);
    }

    public void OnDestroy()
    {
        ReleaseEvents();
    }

    private void InteractionKeyPressed()
    {
        if (gameObject.activeSelf == false) return;

        if (bCanJump == false)
        {
            return;
        }

        // 상호작용 연타 방지 (쿨다운 0.5초)
        if (Time.time - lastActivatedTime < 0.5f)
        {
            return;
        }

        if (bUIActivated)
        {
            if (type == PortalType.ToDungeonPortal)
            {
                lastActivatedTime = Time.time;
                PortalDeActivatedEvent?.Invoke();
                bUIActivated = false;
            }
        }
        else if (bOverlapped == true)
        {
            if (type == PortalType.ToDungeonPortal)
            {
                lastActivatedTime = Time.time;
                bUIActivated = true;
                PortalActivated?.Invoke();
            }
            else
            {
                lastActivatedTime = Time.time;
                GameEndEvent?.Invoke();
            }
        }
    }

    private void InteractionKeyCanceled()
    {
    }

    public void SetUIActivated(bool _boolean)
    {
        bUIActivated = _boolean;
    }

    private void LateUpdate()
    {
        customSortable.ManualLateUpdate();
        customSortable_wheel.ManualLateUpdate();

        if (innerSR != null)
        {
            innerSR.sortingOrder = innerSR.sortingOrder - 1;
        }
    }

    public void SetActiveWheelForStencil(bool _boolean)
    {
        wheelObjectForStencil.SetActive(_boolean);
    }

    public void StartDrive(Transform _endPoint)
    {
        offroadContainer.DisableCollision();
        outLineObject.SetActive(false);
        wheelObjectForStencil.SetActive(false);


        if (driveCoroutine != null)
        {
            StopCoroutine(driveCoroutine);
        }

        Sound.StopTracked(engineStartHandle);

        driveCoroutine = StartCoroutine(DriveRoutine(_endPoint));
    }

    private IEnumerator DriveRoutine(Transform _endPoint)
    {
        Vector3 jitterVisualObjectInitialLocalPos = jitterVisualObject.transform.localPosition;
        Vector3 visualObjectInitialScale = visualObject.transform.localScale;

        // 0. 컨테이너 점프 시퀀스
        yield return ContainerJumpSequence();

        // 시동 임팩트(먼지 파티클)보다 엔진 캐치 지점만큼 앞서 엔진 사운드를 재생한다.
        engineStartHandle = Sound.PlayTracked(SoundID.OffroadNonEdit, transform.position);
        float runStartClipLength = Sound.GetClipLength(SoundID.OffroadNonEdit);
        float ignitionCatchTime = runStartClipLength * engineIgnitionCatchRatio;
        yield return new WaitForSeconds(ignitionCatchTime);

        // 1. 시동 임팩트 시퀀스 (스프링 댐퍼) - 사운드의 엔진 캐치 지점과 동시에 재생된다.
        yield return IgnitionImpactSequence(jitterVisualObjectInitialLocalPos, visualObjectInitialScale);

        // 시동이 걸린 뒤(=이후 출발하는 구간) 남은 재생 시간 동안 피치를 서서히 올린다.
        float remainingClipTime = runStartClipLength - ignitionCatchTime;
        if (remainingClipTime > 0f)
        {
            Sound.RampTrackedPitch(engineStartHandle, engineRunPitchTarget, remainingClipTime);
        }

        // 2. 시동 유지 시퀀스 (공회전)
        yield return IgnitionIdleSequence(jitterVisualObjectInitialLocalPos);

        // 3. 주행 시퀀스
        yield return TravelSequence(_endPoint.position, jitterVisualObjectInitialLocalPos);

        // 4. 주행 종료 처리
        FinishDrive(_endPoint.position, jitterVisualObjectInitialLocalPos);
    }

    private IEnumerator ContainerJumpSequence()
    {
        if (offroadContainerVComponent == null || containerCarryPoint == null) yield break;


        containerShadowObj.SetActive(false);

        yield return offroadContainerVComponent.JumpSequence(
            containerCarryPoint.position,
            containerJumpHeight,
            containerJumpDuration,
            containerSpringFrequency,
            containerSpringDamping
        );

        // 상자 안착 후 차량(VisualObject) 쫀득한 연출
        yield return VehicleLandingImpactSequence();
    }

    public IEnumerator VehicleLandingImpactSequence()
    {
        if (visualObject == null) yield break;

        Vector3 initialScale = visualObject.transform.localScale;
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float spring = Mathf.Exp(-containerSpringDamping * t) * Mathf.Sin(containerSpringFrequency * t);

            // 아래로 눌리면서 양옆으로 퍼지는 쫀득한 연출
            visualObject.transform.localScale = initialScale + new Vector3(spring * 0.15f, -spring * 0.15f, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        visualObject.transform.localScale = initialScale;
    }

    public IEnumerator CharacterRideLandingImpactSequence(Action _onHalfway = null)
    {
        if (visualObject == null) yield break;

        if (_onHalfway != null)
        {
            _onHalfway.Invoke();
        }

        Vector3 initialScale = visualObject.transform.localScale;
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            float spring = Mathf.Exp(-containerSpringDamping * t) * Mathf.Sin(containerSpringFrequency * t);

            // 아래로 눌리면서 양옆으로 퍼지는 쫀득한 연출
            visualObject.transform.localScale = initialScale + new Vector3(spring * 0.15f, -spring * 0.15f, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        visualObject.transform.localScale = initialScale;
    }

    private IEnumerator IgnitionImpactSequence(Vector3 _initialPos, Vector3 _initialScale)
    {
        PlayStartUpEffect();

        float elapsed = 0f;
        while (elapsed < ignitionSquashDuration)
        {
            float progress = elapsed / ignitionSquashDuration;
            float spring = Mathf.Exp(-ignitionSpringDamping * progress) * Mathf.Sin(ignitionSpringFrequency * progress);
            float pulse = spring * ignitionScaleIntensity;

            visualObject.transform.localScale = _initialScale + new Vector3(pulse, -pulse * 0.7f, 0);

            float shakeProgress = Mathf.Exp(-ignitionSpringDamping * progress);
            float currentShakeIntensity = shakeIntensity * ignitionShakeMultiplier * shakeProgress;
            ApplyShake(_initialPos, currentShakeIntensity);

            elapsed += Time.deltaTime;
            yield return null;
        }
        visualObject.transform.localScale = _initialScale;
    }

    private IEnumerator IgnitionIdleSequence(Vector3 _initialPos)
    {
        float elapsed = 0f;
        while (elapsed < ignitionDelay)
        {
            ApplyShake(_initialPos, shakeIntensity);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator TravelSequence(Vector3 _targetPos, Vector3 _initialLocalPos)
    {
        float currentSpeed = 0f;
        float goEffectTimer = 0f;
        while (Vector3.Distance(transform.position, _targetPos) > reachThreshold)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, _targetPos, currentSpeed * Time.deltaTime);

            ApplyShake(_initialLocalPos, shakeIntensity);
            UpdateWheelAnimation(currentSpeed);

            goEffectTimer += Time.deltaTime;
            if (goEffectTimer >= goEffectInterval)
            {
                goEffectTimer = 0f;
                PlayGoEffect();
            }

            yield return null;
        }
    }

    private void ApplyShake(Vector3 _initialPos, float _intensity)
    {
        float shakeX = UnityEngine.Random.Range(-_intensity, _intensity);
        float shakeY = UnityEngine.Random.Range(-_intensity, _intensity);
        jitterVisualObject.transform.localPosition = _initialPos + new Vector3(shakeX, shakeY, 0);
    }

    private void UpdateWheelAnimation(float _speed)
    {
        if (wheelAnimator != null)
        {
            if (_speed > 0f)
            {
                wheelAnimator.enabled = true;
                wheelAnimator.speed = _speed * 0.2f;
            }
            else
            {
                wheelAnimator.speed = 0;
                wheelAnimator.enabled = false;
            }
        }
    }

    private void PlayStartUpEffect()
    {
        if (vfxComponent != null && startUpEffectPoint != null)
        {
            OffroadPlayEffect("StartUp", startUpEffectPoint);
        }
    }

    private void PlayGoEffect()
    {
        if (vfxComponent != null && goEffectPoint != null)
        {
            OffroadPlayEffect("Go", goEffectPoint);
        }
    }

    private void OffroadPlayEffect(string _effectName, Transform _effectPoint)
    {
        if (vfxComponent != null && _effectPoint != null)
        {
            VFXPlaySettings settings = new VFXPlaySettings(_effectName, _effectPoint.position, _effectPoint.rotation, effectColor);
            settings.OverrideSorting = true;
            settings.SortingLayerName = "Objects";
            settings.SortingOrder = baseSR.sortingOrder;

            ParticleSystem effect = vfxComponent.Play(settings);
        }
    }

    private void FinishDrive(Vector3 _targetPos, Vector3 _initialLocalPos)
    {
        transform.position = _targetPos;
        jitterVisualObject.transform.localPosition = _initialLocalPos;

        wheelObjectForStencil.SetActive(true);

        if (wheelAnimator != null)
        {
            wheelAnimator.speed = 0;
            wheelAnimator.enabled = false;
        }

        driveCoroutine = null;
        OffroadDriveEndEvent?.Invoke();
    }

    public void CalcDistForCanReach()
    {
        if (charTransform == null || offroadContainer == null) return;

        if (offroadContainer.gameObject.activeSelf == false)
        {
            SetbCanReach(true);
            if (repairBox != null) repairBox.SetCanReach(false);
            return;
        }

        Vector3 playerPos = charTransform.position;
        float distToVehicleSq = (col.ClosestPoint(playerPos) - (Vector2)playerPos).sqrMagnitude;
        float distToContainerSq = (offroadContainer.col.ClosestPoint(playerPos) - (Vector2)playerPos).sqrMagnitude;
        float distToRepairBoxSq = float.MaxValue;

        if (repairBox != null && repairBox.gameObject.activeSelf)
        {
            distToRepairBoxSq = (repairBox.col.ClosestPoint(playerPos) - (Vector2)playerPos).sqrMagnitude;
        }

        // 플레이어가 여러 콜라이더 교집합 영역(거리 0)에 있을 경우 중심점 거리를 비교하여 가장 가까운 쪽을 활성화
        float minDist = Mathf.Min(distToVehicleSq, Mathf.Min(distToContainerSq, distToRepairBoxSq));
        if (minDist == 0f)
        {
            float centerDistV = distToVehicleSq == 0f ? (col.bounds.center - playerPos).sqrMagnitude : float.MaxValue;
            float centerDistC = distToContainerSq == 0f ? (offroadContainer.transform.position - playerPos).sqrMagnitude : float.MaxValue;
            float centerDistR = distToRepairBoxSq == 0f ? (repairBox.transform.position - playerPos).sqrMagnitude : float.MaxValue;

            float minCenterDist = Mathf.Min(centerDistV, Mathf.Min(centerDistC, centerDistR));

            if (minCenterDist == centerDistV) distToVehicleSq = -1f;
            else if (minCenterDist == centerDistC) distToContainerSq = -1f;
            else if (minCenterDist == centerDistR) distToRepairBoxSq = -1f;

            minDist = Mathf.Min(distToVehicleSq, Mathf.Min(distToContainerSq, distToRepairBoxSq));
        }

        bool vehicleCanReach = false;
        bool containerCanReach = false;
        bool repairBoxCanReach = false;

        if (minDist == distToVehicleSq)
        {
            vehicleCanReach = true;
        }
        else if (minDist == distToContainerSq)
        {
            containerCanReach = true;
        }
        else if (repairBox != null && minDist == distToRepairBoxSq)
        {
            repairBoxCanReach = true;
        }

        SetbCanReach(vehicleCanReach);
        offroadContainer.SetCanReach(containerCanReach);
        if (repairBox != null) repairBox.SetCanReach(repairBoxCanReach);

        if (offroadContainer.bCanInteract)
            offroadContainerVComponent.SetOutlineMaterial();
        else
            offroadContainerVComponent.ResetMaterial();

        if (repairBox != null)
        {
            if (repairBox.bCanInteract)
                repairBox.SetOutlineMaterial();
            else
                repairBox.ResetMaterial();
        }
    }

    public void SetbCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    private void ContainerOpend()
    {
        offroadContainerVComponent.Open();
    }

    private void ContainerClosed()
    {
        offroadContainerVComponent.Close();
    }

    private void ContainerVisualOpened()
    {
        offroadContainer.SetContainerVisualOpened(true);
    }

    private void ContainerVisualClosed()
    {
        offroadContainer.SetContainerVisualOpened(false);
    }

    public void ChangeSprite(MapType _mapType)
    {
        if (!bOriginalSpritesSaved)
        {
            if (baseSR != null) originalBaseSprite = baseSR.sprite;
            if (wheelSR != null) originalWheelSprite = wheelSR.sprite;
            if (containerSR != null)
            {
                originalContainerColor = containerSR.color;
            }
            bOriginalSpritesSaved = true;
        }

        if (_mapType == MapType.StarrootForest)
        {
            if (baseSR != null && darkBaseSprite != null) baseSR.sprite = darkBaseSprite;
            if (wheelSR != null && darkWheelSprite != null) wheelSR.sprite = darkWheelSprite;

            if (containerSR != null)
            {
                containerSR.color = new Color32(144, 157, 224, 255);
            }
        }
        else if (_mapType == MapType.MagmaForest)
        {
            if (baseSR != null && cinderBaseSprite != null) baseSR.sprite = cinderBaseSprite;
            if (wheelSR != null && cinderWheelSprite != null) wheelSR.sprite = cinderWheelSprite;
            if (containerSR != null)
            {
                containerSR.color = new Color32(255, 200, 200, 255);
            }
        }
    }

    public void ResetSprite()
    {
        if (bOriginalSpritesSaved)
        {
            if (baseSR != null) baseSR.sprite = originalBaseSprite;
            if (wheelSR != null) wheelSR.sprite = originalWheelSprite;
            if (containerSR != null)
            {
                containerSR.color = originalContainerColor;
            }
        }
    }

    public void PlayShinyEffect()
    {
        if (_flashMPB == null) _flashMPB = new MaterialPropertyBlock();

        if (vfxComponent != null && shinyEffectPoint != null)
        {
            OffroadPlayEffect("Shiny", shinyEffectPoint);
        }

        StartCoroutine(ShinyRoutine());
    }

    private IEnumerator ShinyRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shinyDuration)
        {
            float t = elapsed / shinyDuration;
            float flash = shinyCurve.Evaluate(t);

            _flashMPB.SetFloat(FlashAmountID, flash);
            baseSR?.SetPropertyBlock(_flashMPB);
            wheelSR?.SetPropertyBlock(_flashMPB);
            innerSR?.SetPropertyBlock(_flashMPB);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _flashMPB.SetFloat(FlashAmountID, 0f);
        baseSR?.SetPropertyBlock(_flashMPB);
        wheelSR?.SetPropertyBlock(_flashMPB);
        innerSR?.SetPropertyBlock(_flashMPB);
    }

    public void IncreaseRepairBoxCount(float _amount)
    {
        repairBoxCount = _amount;
    }

    public void IncreaseRepairAmount(float _amount)
    {
        repairAmount += (_amount / 100f);
    }
}
