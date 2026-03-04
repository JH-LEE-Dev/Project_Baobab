using UnityEngine;

public class UIViewContext
{
    public InputManager inputManager { get; private set; }

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
    }

    public void Initialize_Gameplay()
    {

    }

    public void ReleaseDependency()
    {

    }
}
