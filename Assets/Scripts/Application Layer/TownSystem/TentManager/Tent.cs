using UnityEngine;
using System;
public class Tent : MonoBehaviour
{
    public event Action<bool> TentInteractEvent;

    private const string PLAYER_TAG = "Player";

    private InputManager inputManager;

    private bool bCanInteract = false;
    private bool bInteract = false;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

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
            bCanInteract = true;
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            bCanInteract = false;
        }

        if (bInteract == true)
            TentInteractEvent?.Invoke(false);
    }
}
