

public class MainMenuUIManager : UIManager
{
    private IMainMenuSaveSystem saveSystem;
    private bool cameFromEscMenu;

    public void Initialize(InputManager _inputManager, LocalizationManager _localizeManager, UIDepthController _depthController, IMainMenuSaveSystem _saveSystem, bool _cameFromEscMenu)
    {
        base.Initialize(_inputManager, _localizeManager, _depthController);
        saveSystem = _saveSystem;
        cameFromEscMenu = _cameFromEscMenu;
    }

    protected override void DataInjection(UIView view)
    {
        if (view is UIView_MainMenu mainMenuUI)
            mainMenuUI.DependencyInjection(saveSystem, cameFromEscMenu);
    }
}
