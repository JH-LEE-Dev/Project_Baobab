using UnityEngine;

public class ComponentCtx
{
    public InputManager inputManager;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
    }
}
