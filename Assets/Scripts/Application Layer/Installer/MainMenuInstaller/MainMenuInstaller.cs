using System;
using UnityEngine;

public class MainMenuInstaller : MonoBehaviour
{
    private InputManager inputManager;
    private MainMenuUIInstaller uiInstaller;
    private IBootStrapProvider bootStrapProvider;
    private LocalizationManager localizationManager;

    public void Initialize(IBootStrapProvider _bootStrapProvider, InputManager _inputManager, LocalizationManager _localizeManager, IMainMenuSaveSystem _saveSystem)
    {
        // Town 카메라 인트로 연출과 동시에 걷히도록, Town 씬 로드 이후까지 살아있어야 한다 (GameInstaller.Initialize()와 동일 패턴).
        DontDestroyOnLoad(gameObject);

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

    public void PlayExitAnimation(Action _onComplete)
    {
        uiInstaller.PlayExitAnimation(_onComplete);
    }

    public void StartMainMenuScene()
    {
        inputManager.PauseMove(false);
        uiInstaller.MainMenuLevelStarted();
    }
}
