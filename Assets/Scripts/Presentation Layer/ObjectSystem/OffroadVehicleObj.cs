using System;
using UnityEngine;
using UnityEngine.Rendering;

public class OffroadVehicleObj : MonoBehaviour
{
    //이벤트
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
    [SerializeField] private GameObject containerObject;

    private CustomSortable customSortable;
    private CustomSortable customSortable_outline;

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

        customSortable = baseObject.GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.SetSortingGroup(baseObject.GetComponentInChildren<SortingGroup>());
        }

        customSortable_outline = outLineObject.GetComponent<CustomSortable>();
        if (customSortable_outline != null)
        {
            customSortable_outline.Initialize(transform);
            customSortable_outline.SetSortingGroup(outLineObject.GetComponentInChildren<SortingGroup>());
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
}
