using UnityEngine;
using System;
using UnityEngine.Rendering;

public class Tent : MonoBehaviour
{
    public event Action<bool> TentInteractEvent;
    public event Action<bool> TentInteractStateChangedEvent;

    private const string PLAYER_TAG = "Player";

    private InputManager inputManager;

    private bool bCanInteract = false;
    private bool bInteract = false;

    private SpriteRenderer sr;

    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject basicObject;

    private CustomSortable customSortable;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        sr = basicObject.GetComponent<SpriteRenderer>();

        customSortable = GetComponentInChildren<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(GetComponentInChildren<SortingGroup>());

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
    }

    private void InteractionKeyPressed()
    {
        if (!bCanInteract) return;

        if (bInteract == true)
        {
            bInteract = false;
            TentInteractEvent?.Invoke(false);
        }
        else
        {
            bInteract = true;
            TentInteractEvent?.Invoke(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            outLineObject.SetActive(true);

            bCanInteract = true;
            TentInteractStateChangedEvent?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            outLineObject.SetActive(false);

            if (bInteract == false)
                bCanInteract = false;
            
            TentInteractStateChangedEvent?.Invoke(false);
        }
    }

    public void ResetTent()
    {
        bCanInteract = false;
        bInteract = false;
    }

    private void Update()
    {
        if (customSortable != null)
            customSortable.SetHeight(0f);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }
}
