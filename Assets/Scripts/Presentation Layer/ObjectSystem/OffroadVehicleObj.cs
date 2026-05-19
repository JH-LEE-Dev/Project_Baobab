using System;
using UnityEngine;

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

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type, IEnvironmentProvider _environmentProvider, InputManager _inputManager,
    IInventory _characterInventory, Transform _charTransform)
    {
        if (_characterInventory != null)
            characterInventory = _characterInventory;

        environmentProvider = _environmentProvider;
        inputManager = _inputManager;
        type = _type;
        characterLayer = LayerMask.NameToLayer("Character");

        lastActivatedTime = Time.time;

        if (baseShadow != null)
            baseShadow.Initialize();

        offroadContainer = GetComponentInChildren<OffroadContainer>();

        if (characterInventory != null)
        {
            offroadContainer.Initialize(characterInventory, _charTransform);

            offroadContainer.gameObject.SetActive(true);
        }
        else
            offroadContainer.gameObject.SetActive(false);

        BindEvents();
    }

    private void Update()
    {
        UpdateShadow(baseShadow);
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
}
