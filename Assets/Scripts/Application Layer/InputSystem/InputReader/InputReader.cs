using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader
{
    //이벤트
    public event Action<Vector2> MoveEvent;
    public event Action MoveTriggerEvent;
    public event Action<Vector2> MouseMoveEvent;
    public event Action InventoryKeyEvent;

    public event Action MouseClickEvent;
    public event Action MouseReleaseEvent;
    public event Action ESCButtonPressedEvent;
    public event Action InteractionKeyPressedEvent;
    public event Action InteractionKeyCanceledEvent;
    public event Action SwitchModeKeyPressedEvent;
    public event Action GoToAxeModeEvent;
    public event Action GoToRifleModeEvent;
    public event Action ReloadButtonPressedEvent;

    //내부 의존성
    private InputActionSystem actions;

    private bool bPause = false;

    public void Initialize()
    {
        if (actions == null)
        {
            actions = new InputActionSystem();

            actions.Normal.ESC.performed += OnESCButtonPressed;

            // Move 액션 바인딩 추가
            actions.Normal.Move.performed += OnMove;
            actions.Normal.Move.canceled += OnMove;
            actions.Normal.Mouse.performed += OnMouseMove;
            actions.Normal.Click.performed += OnMouseClick;
            actions.Normal.Click.canceled += OnMouseReleased;
            actions.Normal.Inventory.performed += OnInventoryKeyPressed;
            actions.Normal.Interaction.performed += InteractionKeyPressed;
            actions.Normal.Interaction.canceled += InteractionKeyCanceled;
            actions.Normal.SwitchMode.performed += SwitchModeKeyPressed;
            actions.Normal.AxeMode.performed += GoToAxeModeKeyPressed;
            actions.Normal.RifleMode.performed += GoToRifleModeKeyPressed;
            actions.Normal.Reload.performed += ReloadButtonPressed;
        }

        actions.Normal.Enable();
    }

    public void Release()
    {
        actions.Normal.Disable();

        actions.Normal.ESC.performed -= OnESCButtonPressed;

        // Move 액션 바인딩 해제
        actions.Normal.Move.performed -= OnMove;
        actions.Normal.Move.canceled -= OnMove;
        actions.Normal.Mouse.performed -= OnMouseMove;
        actions.Normal.Click.performed -= OnMouseClick;
        actions.Normal.Click.canceled -= OnMouseReleased;
        actions.Normal.Inventory.performed -= OnInventoryKeyPressed;
        actions.Normal.Interaction.performed -= InteractionKeyPressed;
        actions.Normal.Interaction.canceled -= InteractionKeyCanceled;
        actions.Normal.SwitchMode.performed -= SwitchModeKeyPressed;
        actions.Normal.AxeMode.performed -= GoToAxeModeKeyPressed;
        actions.Normal.RifleMode.performed -= GoToRifleModeKeyPressed;
        actions.Normal.Reload.performed -= ReloadButtonPressed;
    }

    public void Pause(bool _bPause)
    {
        bPause = _bPause;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (bPause)
            return;

        Vector2 move = context.ReadValue<Vector2>();

        MoveTriggerEvent?.Invoke();
        MoveEvent?.Invoke(move);
    }

    private void OnESCButtonPressed(InputAction.CallbackContext context)
    {
        ESCButtonPressedEvent?.Invoke();

        ClearAllEvent();
    }

    private void OnMouseMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();

        MouseMoveEvent?.Invoke(move);
    }

    private void OnMouseClick(InputAction.CallbackContext context)
    {
        MouseClickEvent?.Invoke();
    }

    private void OnMouseReleased(InputAction.CallbackContext context)
    {
        MouseReleaseEvent?.Invoke();
    }

    private void ClearAllEvent()
    {
        MoveEvent = null;
    }

    private void OnInventoryKeyPressed(InputAction.CallbackContext context)
    {
        InventoryKeyEvent?.Invoke();
    }

    private void InteractionKeyPressed(InputAction.CallbackContext context)
    {
        InteractionKeyPressedEvent?.Invoke();
    }

    private void InteractionKeyCanceled(InputAction.CallbackContext context)
    {
        InteractionKeyCanceledEvent?.Invoke();
    }

    private void SwitchModeKeyPressed(InputAction.CallbackContext context)
    {
        SwitchModeKeyPressedEvent?.Invoke();
    }

    private void GoToAxeModeKeyPressed(InputAction.CallbackContext context)
    {
        GoToAxeModeEvent?.Invoke();
    }

    private void GoToRifleModeKeyPressed(InputAction.CallbackContext context)
    {
        GoToRifleModeEvent?.Invoke();
    }

    private void ReloadButtonPressed(InputAction.CallbackContext context)
    {
        ReloadButtonPressedEvent?.Invoke();
    }
}
