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

    [SerializeField] private OffsetShadow baseShadow;

    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject baseObject;
    [SerializeField] private GameObject wheelObject;
    [SerializeField] private GameObject containerObject;
    [SerializeField] private GameObject visualObject;

    [Header("Drive Settings")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float shakeIntensity = 0.008f; // 떨림 세기 감소 (0.05 -> 0.02)
    [SerializeField] private float ignitionDelay = 1.0f; // 시동 후 출발 전 대기 시간
    [SerializeField] private float reachThreshold = 0.1f;

    private Animator wheelAnimator;

    private CustomSortable customSortable;
    private CustomSortable customSortable_outline;

    private Coroutine driveCoroutine;

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

        if (baseShadow != null)
            baseShadow.Initialize();

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
            customSortable.SetSortingGroup(visualObject.GetComponentInChildren<SortingGroup>());
        }

        customSortable_outline = outLineObject.GetComponent<CustomSortable>();
        if (customSortable_outline != null)
        {
            customSortable_outline.Initialize(transform);
            customSortable_outline.SetSortingGroup(outLineObject.GetComponentInChildren<SortingGroup>());
        }

        if(wheelObject != null)
        {
            wheelAnimator = wheelObject.GetComponentInChildren<Animator>();
            wheelAnimator.speed = 0;
        }

        BindEvents();
    }

    private void Update()
    {
        UpdateShadow(baseShadow);
        customSortable.SetHeight(0);
    }

    private void UpdateShadow(OffsetShadow shadow)
    {
        if (shadow == null)
        {
            return;
        }

        shadow.ManualUpdate(
             environmentProvider.shadowDataProvider.CurrentShadowAngle,
             environmentProvider.shadowDataProvider.CurrentShadowScaleY,
             environmentProvider.shadowDataProvider.IsShadowActive
         );
    }

    public void ResetPortal()
    {
        lastActivatedTime = Time.time;
        bOverlapped = false;
        offroadContainer.transform.position = containerObject.transform.position;
        offroadContainer.SetVisualTransform(containerObject.transform);
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
    }

    public void StartDrive(Transform _endPoint)
    {
        if (driveCoroutine != null)
        {
            StopCoroutine(driveCoroutine);
        }

        driveCoroutine = StartCoroutine(DriveRoutine(_endPoint));
    }

    private IEnumerator DriveRoutine(Transform _endPoint)
    {
        float currentSpeed = 0f;
        Vector3 targetPosition = _endPoint.position;
        Vector3 baseObjectInitialLocalPos = baseObject.transform.localPosition;

        // 1. 시동 (약간의 대기 시간 동안 떨림 연출)
        float elapsed = 0f;
        while (elapsed < ignitionDelay)
        {
            float shakeX = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
            float shakeY = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
            baseObject.transform.localPosition = baseObjectInitialLocalPos + new Vector3(shakeX, shakeY, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 가속 및 이동
        while (Vector3.Distance(transform.position, targetPosition) > reachThreshold)
        {
            // 가속 로직
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            
            // 루트 이동
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);

            // 이동 중 떨림 효과 유지
            float shakeX = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
            float shakeY = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
            baseObject.transform.localPosition = baseObjectInitialLocalPos + new Vector3(shakeX, shakeY, 0);

            // 컨테이너 위치 동기화 (자식 객체가 아닌 경우 대응)
            if (offroadContainer != null && containerObject != null)
            {
                offroadContainer.transform.position = containerObject.transform.position;
            }

            // 바퀴 애니메이션 속도 조절 (속도에 비례)
            if (wheelAnimator != null)
            {
                wheelAnimator.speed = currentSpeed * 0.2f; // 주행 속도에 맞게 계수 조정
            }

            yield return null;
        }

        // 3. 목적지 도착 및 상태 초기화
        transform.position = targetPosition;
        baseObject.transform.localPosition = baseObjectInitialLocalPos;
        
        if (offroadContainer != null && containerObject != null)
        {
            offroadContainer.transform.position = containerObject.transform.position;
        }

        if (wheelAnimator != null)
        {
            wheelAnimator.speed = 0;
        }

        driveCoroutine = null;
        OffroadDriveEndEvent?.Invoke();
    }
}
