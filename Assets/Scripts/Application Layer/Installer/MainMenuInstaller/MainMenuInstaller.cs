using UnityEngine;

public class MainMenuInstaller : MonoBehaviour
{
    private InputManager inputManager;
    private MainMenuUIInstaller uiInstaller;
    private IBootStrapProvider bootStrapProvider;
    private LocalizationManager localizationManager;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager, IMainMenuSaveSystem _saveSystem)
    {
        localizationManager = _localizeManager;
        inputManager = _inputManager;
        bootStrapProvider = _bootStrapProvider;

        uiInstaller = GetComponentInChildren<MainMenuUIInstaller>();
        uiInstaller.Initialize(bootStrapProvider, inputManager, localizationManager, _saveSystem);
    }

    public void Release()
    {
        uiInstaller.Release();
    }

    public void StartMainMenuScene()
    {
        inputManager.PauseMove(false);
        uiInstaller.MainMenuLevelStarted();
    }
}
