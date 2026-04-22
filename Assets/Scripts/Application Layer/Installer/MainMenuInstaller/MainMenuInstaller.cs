using UnityEngine;

public class MainMenuInstaller : MonoBehaviour
{
    private InputManager inputManager;
    private MainMenuUIInstaller uiInstaller;
    private IBootStrapProvider bootStrapProvider;
    private LocalizationManager localizationManager;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager)
    {
        localizationManager = _localizeManager;
        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

        uiInstaller = GetComponentInChildren<MainMenuUIInstaller>();
        uiInstaller.Initialize(bootStrapProvider, inputManager, localizationManager);
    }

    public void Release()
    {
        uiInstaller.Release();
    }

    public void StartMainMenuScene()
    {
        uiInstaller.MainMenuLevelStarted();
    }
}
