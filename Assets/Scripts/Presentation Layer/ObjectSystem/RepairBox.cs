using System;
using UnityEngine;

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
    private float lastInteractTime = 0f;

    private void Awake()
    {
        characterLayer = LayerMask.NameToLayer("Character");

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

    public void Initialize(InputManager _inputManager, Transform _characterTransform)
    {
        inputManager = _inputManager;
        if (_characterTransform != null && _characterTransform.parent != null)
        {
            character = _characterTransform.parent.GetComponent<Character>();
        }
        
        BindEvents();
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

        if (Time.time - lastInteractTime < 1f) return;

        if (character != null)
        {
            character.RepairWeapon(repairAmount);
            repairBoxCount--;
            UpdateInteractState();
        }

        lastInteractTime = Time.time;
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }
}
