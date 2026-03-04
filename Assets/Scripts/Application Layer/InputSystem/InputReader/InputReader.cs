using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader
{
    //이벤트
    public event Action<Vector2> MoveEvent;
    public event Action ESCButtonPressedEvent;

    private InputActionSystem actions;

    public void Initialize()
    {
        if (actions == null)
        {
            actions = new InputActionSystem();

            actions.Normal.ESC.performed += OnESCButtonPressed;
        }

        actions.Normal.Enable();
    }

    public void Release()
    {
        actions.Normal.Disable();

        actions.Normal.ESC.performed -= OnESCButtonPressed; 
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();

        MoveEvent?.Invoke(move);
    }

    private void OnESCButtonPressed(InputAction.CallbackContext context)
    {
        ESCButtonPressedEvent?.Invoke();

        ClearAllEvent();
    }

    private void ClearAllEvent()
    {
        MoveEvent = null;
    }
}
