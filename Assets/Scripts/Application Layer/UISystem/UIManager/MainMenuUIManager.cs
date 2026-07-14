

public class MainMenuUIManager : UIManager
{
    private IMainMenuSaveSystem saveSystem;

    public void Initialize(InputManager _inputManager, LocalizationManager _localizeManager, UIDepthController _depthController, IMainMenuSaveSystem _saveSystem)
    {
        base.Initialize(_inputManager, _localizeManager, _depthController);
        saveSystem = _saveSystem;
    }

    protected override void DataInjection(UIView view)
    {
        if (view is UIView_MainMenu mainMenuUI)
            mainMenuUI.DependencyInjection(saveSystem);
    }
}
