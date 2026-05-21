using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class OffroadVehicleObj : MonoBehaviour
{
    //이벤트
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
    [SerializeField] private GameObject wheelObject;
    [SerializeField] private GameObject containerObject;
    [SerializeField] private GameObject visualObject;

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
    private CustomSortable customSortable_outline;
    private CustomSortable customSortable_wheel;

    private Coroutine driveCoroutine;

    [SerializeField] private Transform containerCarryPoint;
    [SerializeField] private Transform containerDropPoint;
    public Transform CharacterRidePoint;

    private OffroadContainerVComponent offroadContainerVComponent;

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type, IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer)
    {
        offroadContainer = _offroadContainer;

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

        customSortable_outline = outLineObject.GetComponent<CustomSortable>();
        if (customSortable_outline != null)
        {
            customSortable_outline.Initialize(transform);
            customSortable_outline.SetSortingGroup(outLineObject.GetComponentInChildren<SortingGroup>());
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

        offroadContainerVComponent = containerObject.GetComponent<OffroadContainerVComponent>();

        BindEvents();
    }

    private void Update()
    {
        customSortable.SetHeight(0);
    }

    public void ResetPortal()
    {
        lastActivatedTime = Time.time;
        bOverlapped = false;
        offroadContainer.transform.position = containerObject.transform.position;
        offroadContainer.SetVisualTransform(containerObject.transform);
        containerObject.transform.position = containerDropPoint.position;
        offroadContainer.EnableCollision();
    }

    //유니티 이벤트 함수
    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (bCanJump == false)
            return;

        baseObject.SetActive(false);
        outLineObject.SetActive(true);

        bOverlapped = true;
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (bCanJump == false)
            return;

        baseObject.SetActive(true);
        outLineObject.SetActive(false);

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
    }

    public void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
    }

    public void OnDestroy()
    {
        ReleaseEvents();
    }

    private void InteractionKeyPressed()
    {
        if (bCanJump == false)
            return;

        if (bUIActivated)
        {
            PortalDeActivatedEvent?.Invoke();
            bUIActivated = false;
            return;
        }

        if (bOverlapped == true)
        {
            bUIActivated = true;
            PortalActivated?.Invoke();
        }
    }

    public void SetUIActivated(bool _boolean)
    {
        bUIActivated = _boolean;
    }

    private void LateUpdate()
    {
        customSortable.ManualLateUpdate();
        customSortable_outline.ManualLateUpdate();
        customSortable_wheel.ManualLateUpdate();
    }

    public void StartDrive(Transform _endPoint)
    {
        offroadContainer.DisableCollision();
        baseObject.SetActive(true);
        outLineObject.SetActive(false);
        
        if (driveCoroutine != null)
        {
            StopCoroutine(driveCoroutine);
        }

        driveCoroutine = StartCoroutine(DriveRoutine(_endPoint));
    }

    private IEnumerator DriveRoutine(Transform _endPoint)
    {
        Vector3 visualObjectInitialLocalPos = visualObject.transform.localPosition;
        Vector3 visualObjectInitialScale = visualObject.transform.localScale;

        // 0. 컨테이너 점프 시퀀스
        yield return ContainerJumpSequence();

        // 1.5초 대기 후 시동
        yield return new WaitForSeconds(1.5f);

        // 1. 시동 임팩트 시퀀스 (스프링 댐퍼)
        yield return IgnitionImpactSequence(visualObjectInitialLocalPos, visualObjectInitialScale);

        // 2. 시동 유지 시퀀스 (공회전)
        yield return IgnitionIdleSequence(visualObjectInitialLocalPos);

        // 3. 주행 시퀀스
        yield return TravelSequence(_endPoint.position, visualObjectInitialLocalPos);

        // 4. 주행 종료 처리
        FinishDrive(_endPoint.position, visualObjectInitialLocalPos);
    }

    private IEnumerator ContainerJumpSequence()
    {
        if (offroadContainerVComponent == null || containerCarryPoint == null) yield break;

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
        visualObject.transform.localPosition = _initialPos + new Vector3(shakeX, shakeY, 0);
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
        visualObject.transform.localPosition = _initialLocalPos;

        if (wheelAnimator != null) wheelAnimator.speed = 0;
        
        driveCoroutine = null;
        OffroadDriveEndEvent?.Invoke();
    }
}
