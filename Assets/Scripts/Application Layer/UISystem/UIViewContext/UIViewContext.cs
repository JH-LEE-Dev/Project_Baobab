using UnityEngine;

public class UIViewContext
{
    public InputManager inputManager { get; private set; }
    public LocalizationManager localizationManager { get; private set; }

    public void Initialize(InputManager _inputManager, LocalizationManager _localizationManager)
    {
        inputManager = _inputManager;
        localizationManager = _localizationManager;
    }

    public void Initialize_Gameplay()
    {

    }

    public void ReleaseDependency()
    {

    }
}
