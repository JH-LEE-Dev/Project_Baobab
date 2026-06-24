using UnityEngine;

public class UIViewContext
{
    public InputManager inputManager { get; private set; }
    public LocalizationManager localizationManager { get; private set; }
    public UIDepthController depthController { get; private set; }
    public Canvas screenSpaceCanvas { get; private set; }
    public Canvas overlayCanvas { get; private set; }

    public void Initialize(InputManager _inputManager, LocalizationManager _localizationManager, UIDepthController _depthController)
    {
        inputManager = _inputManager;
        localizationManager = _localizationManager;
        depthController = _depthController;
    }

    public void DI(Canvas _screenSpaceCanvas,Canvas _overlayCanvas)
    {
        overlayCanvas = _overlayCanvas;
        screenSpaceCanvas = _screenSpaceCanvas;
    }

    public void Initialize_Gameplay()
    {

    }

    public void ReleaseDependency()
    {

    }
}
