using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class OffroadVehicleObj : MonoBehaviour, IOffroadProvider
{
    //이벤트
    public event Action GoToTownEvent;
    public event Action OffroadDriveEndEvent;
    public event Action PortalActivated;
    public event Action PortalDeActivatedEvent;

    private IEnvironmentProvider environmentProvider;

    private InputManager inputManager;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;

    //내부 의존성
    private int characterLayer;
    [SerializeField] private PortalType type;
    //[SerializeField] private float cooldownTime = 2.0f; // 쿨타임 설정
    private float lastActivatedTime = -10.0f; // 마지막 활성화 시간 (초기값은 충분히 과거로 설정)

    private bool bCanJump = false;

    private bool bOverlapped = false;

    private bool bUIActivated = false;
    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject baseObject;
    [SerializeField] private GameObject wheelObjectForStencil;
    [SerializeField] private GameObject wheelObject;
    [SerializeField] private GameObject containerObject;
    [SerializeField] private GameObject visualObject;
    [SerializeField] private GameObject jitterVisualObject;

    [Header("Container Jump Settings")]
    [SerializeField] private float containerJumpDuration = 0.5f;
    [SerializeField] private float containerJumpHeight = 1.5f;
    [SerializeField] private float containerSpringFrequency = 20f;
    [SerializeField] private float containerSpringDamping = 5f;

    [Header("Drive Settings")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float shakeIntensity = 0.008f; // 떨림 세기 감소 (0.05 -> 0.02)
    [SerializeField] private float ignitionShakeMultiplier = 5.0f; // 시동 시 떨림 배율
    [SerializeField] private float ignitionScaleIntensity = 0.3f; // 시동 시 최대 스케일 변화량
    [SerializeField] private float ignitionSpringFrequency = 18f; // 스프링 진동 주파수 (높을수록 많이 뜀)
    [SerializeField] private float ignitionSpringDamping = 6f; // 스프링 감쇄율 (높을수록 빨리 멈춤)
    [SerializeField] private float ignitionSquashDuration = 0.4f; // 스프링 연출이 일어나는 전체 시간
    [SerializeField] private float ignitionDelay = 0.1f; // 연출 후 대기 시간
    [SerializeField] private float reachThreshold = 0.1f;

    private Animator wheelAnimator;

    private CustomSortable customSortable;
    private CustomSortable customSortable_wheel;

    private Coroutine driveCoroutine;
    private Coroutine interactionHoldCoroutine;

    [SerializeField] private Transform containerCarryPoint;
    [SerializeField] private Transform containerDropPoint;
    public Transform CharacterRidePoint;

    private OffroadContainerVComponent offroadContainerVComponent;

    private CircleCollider2D col;
    private float colRadius;
    private bool bCanReach = true;
    private bool bCanInteract = false;

    private Transform charTransform;

    public Material originalMaterial;
    public Material stencilMaterial;

    public SpriteRenderer baseSR;
    public SpriteRenderer wheelStencilSR;

    [SerializeField] private GameObject containerShadowObj;

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type, IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer, Transform _characterTransform)
    {
        offroadContainer = _offroadContainer;
        charTransform = _characterTransform;

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
            customSortable_wheel = wheelObject.GetComponent<CustomSortable>();
            if (customSortable_wheel != null)
            {
                customSortable_wheel.Initialize(transform);
                customSortable_wheel.AddSpriteRenderer(wheelAnimator.GetComponent<SpriteRenderer>());
            }
        }

        offroadContainerVComponent = containerObject.GetComponentInChildren<OffroadContainerVComponent>();

        BindEvents();
    }

    private void Update()
    {
        customSortable.SetHeight(0);

        CalcDistForCanReach();
    }

    public void ResetPortal()
    {
        lastActivatedTime = Time.time;
        bOverlapped = false;
        var conPos = containerObject.transform.position;
        conPos.y += 0.25f;
        offroadContainer.transform.position = conPos;
        offroadContainer.SetVisualTransform(containerObject.transform);
        containerObject.transform.position = containerDropPoint.position;
        offroadContainer.EnableCollision();

        containerShadowObj.SetActive(true);
    }

    public void SetVisualActive(bool _boolean)
    {
        offroadContainerVComponent.bActive = _boolean;
    }

    //유니티 이벤트 함수
    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (bCanJump == false)
            return;

        if (bCanReach == false)
            return;

        outLineObject.SetActive(true);

        baseSR.material = stencilMaterial;
        wheelStencilSR.material = stencilMaterial;

        bOverlapped = true;
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        if (bCanJump == false)
            return;

        if (bCanReach == false)
            return;

        bCanInteract = true;

        baseSR.material = stencilMaterial;
        wheelStencilSR.material = stencilMaterial;
        outLineObject.SetActive(true);

        bOverlapped = true;
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (bCanJump == false || bCanInteract == false)
            return;

        outLineObject.SetActive(false);

        baseSR.material = originalMaterial;
        wheelStencilSR.material = originalMaterial;

        bCanInteract = false;

        bOverlapped = false;
        PortalDeActivatedEvent?.Invoke();
    }

    public void SetCanTravel(bool _canJump)
    {
        bCanJump = _canJump;
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

        if (type == PortalType.ToDungeonPortal)
        {
            offroadContainerVComponent.ContainerOpenedEvent -= ContainerVisualOpened;
            offroadContainerVComponent.ContainerClosedEvent -= ContainerVisualClosed;
        }
    }

    public void OnDestroy()
    {
        ReleaseEvents();
    }

    private void InteractionKeyPressed()
    {
        if (gameObject.activeSelf == false) return;

        if (interactionHoldCoroutine != null) StopCoroutine(interactionHoldCoroutine);
        interactionHoldCoroutine = StartCoroutine(InteractionHoldRoutine());
    }

    private void InteractionKeyCanceled()
    {
        if (gameObject.activeSelf == false) return;

        if (interactionHoldCoroutine != null)
        {
            StopCoroutine(interactionHoldCoroutine);
            interactionHoldCoroutine = null;
        }
    }

    private IEnumerator InteractionHoldRoutine()
    {
        yield return new WaitForSeconds(1.0f);

        if (bCanJump == false)
        {
            interactionHoldCoroutine = null;
            yield break;
        }

        if (bUIActivated)
        {
            if (type == PortalType.ToDungeonPortal)
            {
                PortalDeActivatedEvent?.Invoke();
                bUIActivated = false;
            }
        }
        else if (bOverlapped == true)
        {
            if (type == PortalType.ToDungeonPortal)
            {
                bUIActivated = true;
                PortalActivated?.Invoke();
            }
            else
                GoToTownEvent?.Invoke();
        }

        interactionHoldCoroutine = null;
    }

    public void SetUIActivated(bool _boolean)
    {
        bUIActivated = _boolean;
    }

    private void LateUpdate()
    {
        customSortable.ManualLateUpdate();
        customSortable_wheel.ManualLateUpdate();
    }

    public void StartDrive(Transform _endPoint)
    {
        offroadContainer.DisableCollision();
        outLineObject.SetActive(false);
        wheelObjectForStencil.SetActive(false);

        baseSR.material = originalMaterial;
        wheelStencilSR.material = originalMaterial;

        if (driveCoroutine != null)
        {
            StopCoroutine(driveCoroutine);
        }

        driveCoroutine = StartCoroutine(DriveRoutine(_endPoint));
    }

    private IEnumerator DriveRoutine(Transform _endPoint)
    {
        Vector3 jitterVisualObjectInitialLocalPos = jitterVisualObject.transform.localPosition;
        Vector3 visualObjectInitialScale = visualObject.transform.localScale;

        // 0. 컨테이너 점프 시퀀스
        yield return ContainerJumpSequence();

        // 1.5초 대기 후 시동
        yield return new WaitForSeconds(1.5f);

        // 1. 시동 임팩트 시퀀스 (스프링 댐퍼)
        yield return IgnitionImpactSequence(jitterVisualObjectInitialLocalPos, visualObjectInitialScale);

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

    private IEnumerator IgnitionImpactSequence(Vector3 _initialPos, Vector3 _initialScale)
    {
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
        while (Vector3.Distance(transform.position, _targetPos) > reachThreshold)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, _targetPos, currentSpeed * Time.deltaTime);

            ApplyShake(_initialLocalPos, shakeIntensity);
            UpdateWheelAnimation(currentSpeed);

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
            wheelAnimator.speed = _speed * 0.2f;
        }
    }

    private void FinishDrive(Vector3 _targetPos, Vector3 _initialLocalPos)
    {
        transform.position = _targetPos;
        jitterVisualObject.transform.localPosition = _initialLocalPos;

        wheelObjectForStencil.SetActive(true);

        if (wheelAnimator != null) wheelAnimator.speed = 0;

        driveCoroutine = null;
        OffroadDriveEndEvent?.Invoke();
    }

    public void CalcDistForCanReach()
    {
        if (charTransform == null || offroadContainer == null) return;

        if (offroadContainer.gameObject.activeSelf == false)
        {
            SetbCanReach(true);
            return;
        }

        float distToVehicleSq = (col.bounds.center - charTransform.position).sqrMagnitude;
        float distToContainerSq = (offroadContainer.transform.position - charTransform.position).sqrMagnitude;

        if (distToVehicleSq <= distToContainerSq)
        {
            SetbCanReach(true);
            offroadContainer.SetCanReach(false);
        }
        else
        {
            SetbCanReach(false);
            offroadContainer.SetCanReach(true);
        }
    }

    public void SetbCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;

        if (bCanReach == false && bCanInteract == true)
        {
            bCanInteract = false;

            outLineObject.SetActive(false);

            bOverlapped = false;
            PortalDeActivatedEvent?.Invoke();
        }
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

    }

    private void ContainerVisualClosed()
    {

    }
}
