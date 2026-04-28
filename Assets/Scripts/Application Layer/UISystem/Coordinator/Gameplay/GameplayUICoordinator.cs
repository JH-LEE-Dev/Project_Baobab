using System;
using System.Diagnostics;
using UnityEngine;

public class GameplayUICoordinator
{
    public event Action SaveGameEvent;
    public event Action GoToMainMenuEvent;
    private UIView_Popup popUpUI;
    private InputManager inputManager;
    private UIView_Unit unitUI;

    private SignalHub signalHub;
    private UIView_HUD hudUI;
    private UIView_WorldPopup worldPopupUI;
    private UIView_MenuPopup menuPopupUI;
    private UIView_Tent tentUI;
    private UIView_ESC escUI;


    private bool bInventoryOpened = false;
    private bool bESCMenuOpended = false;
    public void Initialize(SignalHub _signalHub, InputManager _inputManager, UIView_Popup _popUpUI, UIView_HUD _hudUI,
     UIView_Unit _unitUI, UIView_WorldPopup _worldPopupUI, UIView_MenuPopup _menuPopupUI, UIView_Tent _tentUI, UIView_ESC _escUI)
    {
        inputManager = _inputManager;
        popUpUI = _popUpUI;
        hudUI = _hudUI;
        signalHub = _signalHub;
        unitUI = _unitUI;
        worldPopupUI = _worldPopupUI;
        menuPopupUI = _menuPopupUI;
        tentUI = _tentUI;
        escUI = _escUI;


        SubscribeSignals();
        BindEvents();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.Subscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
        signalHub.Subscribe<ContainerUpdatedSignal>(ContainerUpdated);
        signalHub.Subscribe<ContainerInteractStateChangedSignal>(ContainerInteractStateChanged);
        signalHub.Subscribe<CharacterEarnMoneySignal>(CharacterEarnMoney);
        signalHub.Subscribe<WeaponModeChangedSignal>(WeaponModeChanged);
        signalHub.Subscribe<TentInteractSignal>(TentInteract);
        signalHub.Subscribe<PortalActivatedSignal>(PortalActivated);
        signalHub.Subscribe<InventorySpecChangedSignal>(InventorySpecChanged);
        signalHub.Subscribe<LogContainerSpecChangedSignal>(LogContainerSpecChanged);
        signalHub.Subscribe<SpendMoneySignal>(SpendMoney);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
        signalHub.Subscribe<DecalreDungeonTypeSignal>(DungeonStarted);
        signalHub.Subscribe<AnimalHitSignal>(AnimalHit);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.UnSubscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
        signalHub.UnSubscribe<ContainerUpdatedSignal>(ContainerUpdated);
        signalHub.UnSubscribe<ContainerInteractStateChangedSignal>(ContainerInteractStateChanged);
        signalHub.UnSubscribe<CharacterEarnMoneySignal>(CharacterEarnMoney);
        signalHub.UnSubscribe<WeaponModeChangedSignal>(WeaponModeChanged);
        signalHub.UnSubscribe<TentInteractSignal>(TentInteract);
        signalHub.UnSubscribe<PortalActivatedSignal>(PortalActivated);
        signalHub.UnSubscribe<InventorySpecChangedSignal>(InventorySpecChanged);
        signalHub.UnSubscribe<LogContainerSpecChangedSignal>(LogContainerSpecChanged);
        signalHub.UnSubscribe<SpendMoneySignal>(SpendMoney);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
        signalHub.UnSubscribe<DecalreDungeonTypeSignal>(DungeonStarted);
        signalHub.UnSubscribe<AnimalHitSignal>(AnimalHit);
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;

        popUpUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        popUpUI.GoHomeButtonClickedEvent += GoHomeButtonClicked;

        popUpUI.SendDeleteItemEvent -= SendDeleteItem;
        popUpUI.SendDeleteItemEvent += SendDeleteItem;

        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        menuPopupUI.DungeonSelectedEvent += DungeonSelected;

        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        inputManager.inputReader.ESCButtonPressedEvent += EscButtonPressed;

        escUI.SaveGameButtonClickedEvent -= SaveGame;
        escUI.SaveGameButtonClickedEvent += SaveGame;

        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.GoToMainMenuButtonClickedEvent += GoToMainMenu;

        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.ExitButtonClickedEvent += ExitGame;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        popUpUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        popUpUI.SendDeleteItemEvent -= SendDeleteItem;
        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.SaveGameButtonClickedEvent -= SaveGame;
    }

    public void Release()
    {
        UnSubscribeSignals();
        ReleaseEvents();
    }

    public void Refresh()
    {
        hudUI.Refresh();
        unitUI.Refresh();
        popUpUI.Refresh();
        worldPopupUI.Refresh();
        menuPopupUI.Refresh();
        tentUI.Refresh();
        escUI.Refresh();
    }

    private void OnInventoryKeyPressed()
    {
        if (bInventoryOpened == false)
        {
            bInventoryOpened = true;
            popUpUI.Show();
        }
        else
        {
            bInventoryOpened = false;
            popUpUI.Hide();
        }
    }

    private void InventoryUpdated(InventoryUpdatedSignal inventoryUpdatedSignal)
    {
        popUpUI.InventoryShowEvent();
    }

    private void TreeGetHit(TreeGetHitSignal treeGetHitSignal)
    {
        unitUI.TreeGetHit(treeGetHitSignal.treeObj);
    }

    private void CharacterSpawned(CharacterSpawendSignal characterSpawendSignal)
    {
        hudUI.SetCharacter(characterSpawendSignal.character);
        unitUI.SetCharacter(characterSpawendSignal.character);
    }

    private void GoHomeButtonClicked()
    {
        signalHub.Publish(new GoHomeButtonClickedSignal());
    }

    private void SendDeleteItem(IInventorySlot _inData)
    {
        signalHub.Publish(new DeleteItemSignal(_inData));
    }

    private void ContainerUpdated(ContainerUpdatedSignal containerUpdatedSignal)
    {
        worldPopupUI.ContainerUpdated();
    }

    private void ContainerInteractStateChanged(ContainerInteractStateChangedSignal containerInteractStateChangedSignal)
    {
        worldPopupUI.LogContainerInteractStateChanged(containerInteractStateChangedSignal.state);
    }

    private void CharacterEarnMoney(CharacterEarnMoneySignal characterEarnMoneySignal)
    {
        popUpUI.CharacterEarnMoney(characterEarnMoneySignal.moneyType);
        tentUI.CharacterEarnMoney(characterEarnMoneySignal.moneyType);
    }

    private void WeaponModeChanged(WeaponModeChangedSignal weaponModeChangedSignal)
    {
        hudUI.WeaponModeChanged(weaponModeChangedSignal.weaponMode);
        unitUI.WeaponModeChanged(weaponModeChangedSignal.weaponMode);
    }

    private void TentInteract(TentInteractSignal tentInteractSignal)
    {
        tentUI.TentInteract(tentInteractSignal.bInteract);
    }

    private void PortalActivated(PortalActivatedSignal portalActivatedSignal)
    {
        menuPopupUI.TeleportUIOpen();
    }

    private void DungeonSelected(DungeonType _type)
    {
        signalHub.Publish(new DungeonSelectedSignal(_type));
    }

    private void Sleep()
    {
        signalHub.Publish(new SleepSignal());
    }

    private void InventorySpecChanged(InventorySpecChangedSignal _inventorySpecChangedSignal)
    {
        popUpUI.InventorySpecChanged();
    }

    private void LogContainerSpecChanged(LogContainerSpecChangedSignal _logContainerSpecChangedSignal)
    {
        worldPopupUI.LogContainerSpecChanged();
    }

    private void SpendMoney(SpendMoneySignal spendMoneySignal)
    {
        popUpUI.CharactersMoneyChanged();
        tentUI.CharactersMoneyChanged();
    }

    private void SaveGame()
    {
        SaveGameEvent?.Invoke();
    }

    private void GoToMainMenu()
    {
        GoToMainMenuEvent?.Invoke();
        Time.timeScale = 1f;
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EscButtonPressed()
    {
        if (bESCMenuOpended == false)
        {
            bESCMenuOpended = true;
            escUI.Show();
            Time.timeScale = 0f;
        }
        else
        {
            bESCMenuOpended = false;
            escUI.Hide();
            Time.timeScale = 1f;
        }
    }

    private void TownStarted(TownStartedSignal townStartedSignal)
    {
        hudUI.SetCurrentMapType(MapType.Town);
    }

    private void DungeonStarted(DecalreDungeonTypeSignal decareDungeonTypeSignal)
    {
        hudUI.SetCurrentMapType(decareDungeonTypeSignal.mapType);
    }

    private void AnimalHit(AnimalHitSignal animalHitSignal)
    {
        unitUI.AnimalGetHit(animalHitSignal.animal);
    }
}
