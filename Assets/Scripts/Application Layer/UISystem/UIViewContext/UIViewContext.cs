using UnityEngine;

public class UIViewContext
{
    public InputManager inputManager { get; private set; }
    public LocalizationManager localizationManager { get; private set; }
    public UIDepthController depthController { get; private set; }
    public Canvas ppCanvas { get; private set; }

    public void Initialize(InputManager _inputManager, LocalizationManager _localizationManager, UIDepthController _depthController,
     Canvas _ppCanvas)
    {
        inputManager = _inputManager;
        localizationManager = _localizationManager;
        depthController = _depthController;
        ppCanvas = _ppCanvas;
    }

    public void Initialize_Gameplay()
    {

    }

    public void ReleaseDependency()
    {

    }
}
