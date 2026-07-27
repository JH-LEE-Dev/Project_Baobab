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
    public event Action ReloadButtonPressedEvent;
    public event Action AimCorrectionKeyPressedEvent;
    public event Action AimCorrectionKeyCanceledEvent;
    public event Action PotionKeyPressedEvent;

    //내부 의존성
    private InputActionSystem actions;

    private bool bPauseMove = false;

    private Vector2 keyboardMoveInput;

    // 단순 bool이 아닌 카운터인 이유: 던전 진입 연출(TownProductionManager.StartSkyProduction 등)과
    // 내비게이션 팝업 해금 연출(GameplayUICoordinator) 등 서로 다른 시스템이 겹치는 타이밍에 각자
    // Pause(true/false)를 걸 수 있는데, bool이면 한쪽이 먼저 false를 걸어 다른 쪽이 아직 막아야 하는
    // 구간에서 조기 해제되어 버린다.
    private int pauseInteractCount = 0;

    private bool bPauseESC = false;

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
            actions.Normal.PotionKey.performed += PotionKeyPressed;
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
        actions.Normal.PotionKey.performed -= PotionKeyPressed;
    }

    public void PauseMove(bool _bPause)
    {
        bPauseMove = _bPause;

        if (_bPause == true)
        {
            MoveEvent?.Invoke(Vector2.zero);
        }
        else
        {
            MoveEvent?.Invoke(keyboardMoveInput);
        }
    }

    public void ClearPrevKeyboardInput()
    {
        keyboardMoveInput = Vector2.zero;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        keyboardMoveInput = context.ReadValue<Vector2>();

        if (bPauseMove)
        {
            MoveEvent?.Invoke(Vector2.zero);
            return;
        }

        MoveTriggerEvent?.Invoke();
        MoveEvent?.Invoke(keyboardMoveInput);
    }

    private void OnESCButtonPressed(InputAction.CallbackContext context)
    {
        if (LoadingManager.Instance != null && LoadingManager.Instance.IsLoading)
            return;

        if (bPauseESC)
            return;

        ESCButtonPressedEvent?.Invoke();
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
        if (0 >= pauseInteractCount)
            InteractionKeyPressedEvent?.Invoke();
    }

    private void InteractionKeyCanceled(InputAction.CallbackContext context)
    {
        InteractionKeyCanceledEvent?.Invoke();
    }

    private void ReloadButtonPressed(InputAction.CallbackContext context)
    {
        ReloadButtonPressedEvent?.Invoke();
    }

    private void AimCorrectionKeyPressed(InputAction.CallbackContext context)
    {
        AimCorrectionKeyPressedEvent?.Invoke();
    }

    private void AimCorrectionKeyCanceled(InputAction.CallbackContext context)
    {
        AimCorrectionKeyCanceledEvent?.Invoke();
    }

    public void PauseMouse(bool _boolean)
    {
        if (_boolean == true)
        {
            actions.Normal.Mouse.performed -= OnMouseMove;
            actions.Normal.Click.performed -= OnMouseClick;
            actions.Normal.Click.canceled -= OnMouseReleased;
        }
        else
        {
            actions.Normal.Mouse.performed -= OnMouseMove;
            actions.Normal.Click.performed -= OnMouseClick;
            actions.Normal.Click.canceled -= OnMouseReleased;

            actions.Normal.Mouse.performed += OnMouseMove;
            actions.Normal.Click.performed += OnMouseClick;
            actions.Normal.Click.canceled += OnMouseReleased;
        }
    }

    public void PauseInteractKey(bool _boolean)
    {
        if (true == _boolean)
        {
            pauseInteractCount++;
        }
        else if (0 < pauseInteractCount)
        {
            pauseInteractCount--;
        }
    }

    public void PauseESCKey(bool _boolean)
    {
        bPauseESC = _boolean;
    }

    public void PotionKeyPressed(InputAction.CallbackContext context)
    {
        PotionKeyPressedEvent?.Invoke();
    }
}
