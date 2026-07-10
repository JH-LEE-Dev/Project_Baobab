using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RepairBox : MonoBehaviour
{
    public event Action<bool> RepairBoxInteractStateChangedEvent;

    public int repairBoxCount = 0;
    public float repairAmount = 0.25f;
    public CircleCollider2D col;

    private bool bCanReach = false;
    public bool bCanInteract { get; private set; }

    private int characterLayer;
    private bool bPhysicalOverlapped = false;
    private bool bLastInteractState = false;

    // RepairBox가 Active일 때만 길찾기 상 이동 불가 타일로 등록되는 발밑 ColliderTilemap
    private TilemapFootprintCollider footprintCollider;

    [Header("Visual Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CustomSortable customSortable;
    [SerializeField] private GameObject outlineStencilObj;
    [SerializeField] private GameObject outlineObj;

    private SpriteRenderer outlineStencilSR;
    private SpriteRenderer outlineSR;
    private Sprite currentSprite;

    private InputManager inputManager;
    private Character character;

    private void Awake()
    {
        characterLayer = LayerMask.NameToLayer("Character");

        footprintCollider = new TilemapFootprintCollider(GetComponentInChildren<Tilemap>(true));

        if (customSortable != null && spriteRenderer != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(spriteRenderer);
        }

        if (outlineStencilObj != null) outlineStencilSR = outlineStencilObj.GetComponent<SpriteRenderer>();
        if (outlineObj != null) outlineSR = outlineObj.GetComponentInChildren<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (customSortable != null)
        {
            customSortable.SetHeight(0f);
            customSortable.ManualLateUpdate();
        }

        if (spriteRenderer != null)
        {
            currentSprite = spriteRenderer.sprite;
            if (outlineStencilSR != null) outlineStencilSR.sprite = currentSprite;
            if (outlineSR != null)
            {
                outlineSR.sprite = currentSprite;
                outlineSR.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }
    }

    public void SetOutlineMaterial()
    {
        if (outlineStencilObj != null) outlineStencilObj.SetActive(true);
    }

    public void ResetMaterial()
    {
        if (outlineStencilObj != null) outlineStencilObj.SetActive(false);
    }

    public void SetCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    public void SetRepairBoxCount(float _amount)
    {
        repairBoxCount = (int)_amount;
    }

    public void SetRepairAmount(float _amount)
    {
        repairAmount = _amount;
    }

    private void UpdateInteractState()
    {
        bool currentState = bCanReach && bPhysicalOverlapped && repairBoxCount > 0;
        if (currentState != bLastInteractState)
        {
            bLastInteractState = currentState;
            bCanInteract = currentState;

            RepairBoxInteractStateChangedEvent?.Invoke(currentState);
        }
    }

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

    public void Initialize(InputManager _inputManager, Transform _characterTransform, IEnvironmentProvider _environmentProvider)
    {
        inputManager = _inputManager;
        footprintCollider.SetEnvironmentProvider(_environmentProvider);
        if (_characterTransform != null && _characterTransform.parent != null)
        {
            character = _characterTransform.parent.GetComponent<Character>();
        }

        BindEvents();

        // OnEnable이 environmentProvider가 세팅되기 전(활성 상태로 인스턴스화된 시점)에 먼저 실행됐을 수 있으므로
        // 여기서 다시 한번 등록을 시도한다. Register()는 매번 먼저 Clear()하고 다시 채우므로 중복 호출해도 안전하다.
        if (gameObject.activeInHierarchy)
        {
            footprintCollider.Register();
        }
    }

    private void OnEnable()
    {
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

    public void BindEvents()
    {
        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
            inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
        }
    }

    public void ReleaseEvents()
    {
        if (inputManager != null && inputManager.inputReader != null)
        {
            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        }
    }

    private void InteractionKeyPressed()
    {
        if (!gameObject.activeInHierarchy) return;

        if (!bCanInteract) return;

        if (repairBoxCount <= 0) return;

        if (character != null)
        {
            character.RepairWeapon(repairAmount);
            repairBoxCount--;
            UpdateInteractState();
        }
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }
}
