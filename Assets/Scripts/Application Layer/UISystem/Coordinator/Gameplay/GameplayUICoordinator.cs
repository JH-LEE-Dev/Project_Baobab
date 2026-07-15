using System;
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
    private UIView_Result resultUI;
    private UIView_SkyProduction skyProduction;
    private UIView_Warning warningUI;

    private UIDepthController uiDepthController;

    private bool bInventoryOpened = false;

    private MapType mapType;
    private ForestType forestType;

    public void Initialize(SignalHub _signalHub, InputManager _inputManager, UIView_Popup _popUpUI, UIView_HUD _hudUI,
     UIView_Unit _unitUI, UIView_WorldPopup _worldPopupUI, UIView_MenuPopup _menuPopupUI, UIView_Tent _tentUI, UIView_ESC _escUI,
     UIDepthController _uiDepthController, UIView_SkyProduction _skyProduction, UIView_Result _resultUI, UIView_Warning _warningUI)
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
        uiDepthController = _uiDepthController;
        skyProduction = _skyProduction;
        resultUI = _resultUI;
        warningUI = _warningUI;

        SubscribeSignals();
        BindEvents();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.Subscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.Subscribe<TreeShieldRecoveringSignal>(TreeShieldRecovering);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
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
        signalHub.Subscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.Subscribe<PortalDeActivatedSignal>(PortalDeActivated);
        signalHub.Subscribe<OffraodContainerSpecChangedSignal>(OffraodContainerSpecChanged);
        signalHub.Subscribe<OffroadContainerInteractStateChangedSignal>(OffroadContainerInteractStateChanged);
        signalHub.Subscribe<LoosAllInventoryItemSignal>(LoosAllInventoryItem);
        signalHub.Subscribe<OffroadContainerUpdatedSignal>(OffroadContainerUpdated);
        signalHub.Subscribe<InventoryIsFullSignal>(InventoryIsFull);
        signalHub.Subscribe<OffroadSpawnedSignal>(OffroadSpawned);
        signalHub.Subscribe<ItemAddedToInventorySignal>(ItemAddedToInventory);
        signalHub.Subscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.Subscribe<TentInteractStateChangedSignal>(TentInteractStateChanged);
        signalHub.Subscribe<OffroadInteractStateChangedSignal>(OffroadInteractStateChanged);
        signalHub.Subscribe<ShopInteractStateChangedSignal>(ShopInteractStateChanged);
        signalHub.Subscribe<LogItemProcessorActiveStateSignal>(LogItemProcessorIsActive);
        signalHub.Subscribe<ItemCantAcquiedSignal>(ItemCantAcquired_Inventory);
        signalHub.Subscribe<DeclareSkillAccumulatedValueSignal>(DeclareSkillAccumulativeValue);
        signalHub.Subscribe<StartSkyProductionSignal>(StartSkyProduction);
        signalHub.Subscribe<RollbackSkyProductionSignal>(RollbackSkyProduction);
        signalHub.Subscribe<PopupUIDownSignal>(PopupUIDown);
        signalHub.Subscribe<PopupUIUpSignal>(PopupUIUp);
        signalHub.Subscribe<ProvideSkillAccumulatedValueChangeSignal>(ProvideAccumulatedValueChangeEvent);
        signalHub.Subscribe<GameEndSignal>(GameEnd);
        signalHub.Subscribe<ActivateWarningUISignal>(ActivateWarningUI);
        signalHub.Subscribe<InventoryItemTransferToOffroadContainerSignal>(InventoryItemToOffroadContainer);
        signalHub.Subscribe<DeclareDungeonStateSignal>(DeclareDungeonState);
        signalHub.Subscribe<RepairBoxInteractStateChangedSignal>(RepairBoxInteractStateChanged);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.UnSubscribe<TreeGetHitSignal>(TreeGetHit);
        signalHub.UnSubscribe<TreeShieldRecoveringSignal>(TreeShieldRecovering);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
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
        signalHub.UnSubscribe<SkillDispatchedSignal>(SkillDispatched);
        signalHub.UnSubscribe<PortalDeActivatedSignal>(PortalDeActivated);
        signalHub.UnSubscribe<OffraodContainerSpecChangedSignal>(OffraodContainerSpecChanged);
        signalHub.UnSubscribe<OffroadContainerInteractStateChangedSignal>(OffroadContainerInteractStateChanged);
        signalHub.UnSubscribe<LoosAllInventoryItemSignal>(LoosAllInventoryItem);
        signalHub.UnSubscribe<OffroadContainerUpdatedSignal>(OffroadContainerUpdated);
        signalHub.UnSubscribe<InventoryIsFullSignal>(InventoryIsFull);
        signalHub.UnSubscribe<OffroadSpawnedSignal>(OffroadSpawned);
        signalHub.UnSubscribe<ItemAddedToInventorySignal>(ItemAddedToInventory);
        signalHub.UnSubscribe<ItemRemovedFromInventorySignal>(ItemRemovedFromInventory);
        signalHub.UnSubscribe<TentInteractStateChangedSignal>(TentInteractStateChanged);
        signalHub.UnSubscribe<OffroadInteractStateChangedSignal>(OffroadInteractStateChanged);
        signalHub.UnSubscribe<ShopInteractStateChangedSignal>(ShopInteractStateChanged);
        signalHub.UnSubscribe<LogItemProcessorActiveStateSignal>(LogItemProcessorIsActive);
        signalHub.UnSubscribe<ItemCantAcquiedSignal>(ItemCantAcquired_Inventory);
        signalHub.UnSubscribe<DeclareSkillAccumulatedValueSignal>(DeclareSkillAccumulativeValue);
        signalHub.UnSubscribe<StartSkyProductionSignal>(StartSkyProduction);
        signalHub.UnSubscribe<RollbackSkyProductionSignal>(RollbackSkyProduction);
        signalHub.UnSubscribe<PopupUIDownSignal>(PopupUIDown);
        signalHub.UnSubscribe<PopupUIUpSignal>(PopupUIUp);
        signalHub.UnSubscribe<ProvideSkillAccumulatedValueChangeSignal>(ProvideAccumulatedValueChangeEvent);
        signalHub.UnSubscribe<GameEndSignal>(GameEnd);
        signalHub.UnSubscribe<ActivateWarningUISignal>(ActivateWarningUI);
        signalHub.UnSubscribe<InventoryItemTransferToOffroadContainerSignal>(InventoryItemToOffroadContainer);
        signalHub.UnSubscribe<DeclareDungeonStateSignal>(DeclareDungeonState);
        signalHub.UnSubscribe<RepairBoxInteractStateChangedSignal>(RepairBoxInteractStateChanged);
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;

        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        popUpUI.sendDeleteItemEvent += SendDeleteItem;

        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        menuPopupUI.DungeonSelectedEvent += DungeonSelected;

        menuPopupUI.CancelButtonClickedEvent -= CancelMenuPopup;
        menuPopupUI.CancelButtonClickedEvent += CancelMenuPopup;

        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        inputManager.inputReader.ESCButtonPressedEvent += EscButtonPressed;

        escUI.SaveGameButtonClickedEvent -= SaveGame;
        escUI.SaveGameButtonClickedEvent += SaveGame;

        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.GoToMainMenuButtonClickedEvent += GoToMainMenu;

        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.ExitButtonClickedEvent += ExitGame;

        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        menuPopupUI.TeleportUIClosedEvent += TeleportUIClosed;

        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        popUpUI.InventoryUIOpendEvent += InventoryUIOpened;

        resultUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        resultUI.GoHomeButtonClickedEvent += GoHomeButtonClicked;

        resultUI.RetryButtonClickedEvent -= RetryGame;
        resultUI.RetryButtonClickedEvent += RetryGame;

        warningUI.DeActivateWarningUIEvent -= DeActivateWarningUI;
        warningUI.DeActivateWarningUIEvent += DeActivateWarningUI;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        menuPopupUI.CancelButtonClickedEvent -= CancelMenuPopup;
        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.SaveGameButtonClickedEvent -= SaveGame;
        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        resultUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        resultUI.RetryButtonClickedEvent -= RetryGame;
        warningUI.DeActivateWarningUIEvent -= DeActivateWarningUI;
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
        menuPopupUI.Refresh();
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

    private void TreeShieldRecovering(TreeShieldRecoveringSignal treeShieldRecoveringSignal)
    {
        unitUI.TreeShieldRecovering(treeShieldRecoveringSignal.treeObj);
    }

    private void CharacterSpawned(CharacterSpawnedSignal characterSpawendSignal)
    {
        hudUI.SetCharacter(characterSpawendSignal.character);
        unitUI.SetCharacter(characterSpawendSignal.character);
        worldPopupUI.SetCharacter(characterSpawendSignal.character);
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
        popUpUI.LogContainerCanInteract(containerInteractStateChangedSignal.state);
        unitUI.LogContainerInteractStateChanged(containerInteractStateChangedSignal.state);
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
        if (tentInteractSignal.bInteract == true)
        {
            tentUI.Show();
        }
        else
        {
            tentUI.Hide();
        }
    }

    private void PortalActivated(PortalActivatedSignal _portalActivatedSignal)
    {
        menuPopupUI.Show();
    }

    private void PortalDeActivated(PortalDeActivatedSignal _portalDeActivatedSignal)
    {
        menuPopupUI.Hide();
    }

    private void DungeonSelected(MapType _type, ForestType _forestType)
    {
        mapType = _type;
        forestType = _forestType;

        signalHub.Publish(new TeleportUIClosedWhileTeleportSignal());
        signalHub.Publish(new DungeonSelectedSignal(_type, _forestType));

        menuPopupUI.ForceHide();
    }

    private void CancelMenuPopup()
    {
        menuPopupUI.ForceHide();
    }

    private void InventorySpecChanged(InventorySpecChangedSignal _inventorySpecChangedSignal)
    {
        popUpUI.InventorySpecChanged();
    }

    private void LogContainerSpecChanged(LogContainerSpecChangedSignal _logContainerSpecChangedSignal)
    {
        worldPopupUI.LogContainerSpecChanged();
    }

    private void OffraodContainerSpecChanged(OffraodContainerSpecChangedSignal offraodContainerSpecChangedSignal)
    {
        worldPopupUI.OffraodContainerSpecChanged();
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
        // 카메라 상승 연출이 재생되는 동안 중복 클릭으로 재진입하지 못하도록 즉시 닫는다.
        escUI.Hide();

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
        if (uiDepthController != null && uiDepthController.TryCloseTopView())
        {
            return;
        }

        if (!escUI.IsVisible)
        {
            escUI.Show();
            Time.timeScale = 0f;
        }
        else
        {
            escUI.Hide();
            Time.timeScale = 1f;
        }
    }

    private void TownStarted(TownStartedSignal townStartedSignal)
    {
        unitUI.Refresh();
        unitUI.TownStarted();

        hudUI.SetCurrentMapType(MapType.Town, ForestType.InTown);
        popUpUI.SetCurrentMapType(MapType.Town, ForestType.InTown);
        worldPopupUI.SetCurrentMapType(MapType.Town, ForestType.InTown);

        bInventoryOpened = false;
        popUpUI.Hide();
    }

    private void DungeonStarted(DecalreDungeonTypeSignal decareDungeonTypeSignal)
    {
        hudUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        popUpUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        worldPopupUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);

        resultUI.DungeonStarted();

        bInventoryOpened = false;
        popUpUI.Hide();
    }

    private void AnimalHit(AnimalHitSignal animalHitSignal)
    {
        unitUI.AnimalGetHit(animalHitSignal.animal);
    }

    private void SkillDispatched(SkillDispatchedSignal skillDispatchedSignal)
    {
        hudUI.Refresh();
        popUpUI.Refresh();
        worldPopupUI.Refresh();
    }

    private void TeleportUIClosed()
    {
        // 뷰를 닫는 호출(ForceHide/Hide)은 항상 이 이벤트를 발행하는 쪽(ESC의 UIDepthController,
        // PortalDeActivated, DungeonSelected, CancelMenuPopup)에서 이미 끝낸 뒤이므로 여기서는
        // 후속 신호만 발행한다.
        signalHub.Publish(new TeleportUIClosedSignal());
    }

    private void OffroadContainerInteractStateChanged(OffroadContainerInteractStateChangedSignal _offroadContainerInteractStateChangedSignal)
    {
        worldPopupUI.OffroadContainerInteractStateChanged(_offroadContainerInteractStateChangedSignal.state);
        popUpUI.LogContainerCanInteract(_offroadContainerInteractStateChangedSignal.state);
        unitUI.OffroadContainerInteractStateChanged(_offroadContainerInteractStateChangedSignal.state);
    }

    private void LoosAllInventoryItem(LoosAllInventoryItemSignal _loosAllInventoryItemSignal)
    {
        popUpUI.LoosAllInventoryItems();
    }

    private void OffroadContainerUpdated(OffroadContainerUpdatedSignal offroadContainerUpdatedSignal)
    {
        worldPopupUI.OffroadContainerUpdated();
    }

    private void InventoryIsFull(InventoryIsFullSignal _inventoryIsFullSignal)
    {
        unitUI.InventoryIsFull();
        popUpUI.InventoryIsFull();
    }

    private void OffroadSpawned(OffroadSpawnedSignal _offroadSpawnedSignal)
    {
        hudUI.OffroadSpawned(_offroadSpawnedSignal.offroadVehicleObj);
    }

    private void ItemAddedToInventory(ItemAddedToInventorySignal _itemAddedToInventorySignal)
    {
        popUpUI.ItemAddedToInventory();
    }

    private void ItemRemovedFromInventory(ItemRemovedFromInventorySignal itemRemovedFromInventorySignal)
    {
        popUpUI.ItemRemovedFromInventory();
    }

    private void TentInteractStateChanged(TentInteractStateChangedSignal _tentInteractStateChangedSignal)
    {
        unitUI.TentInteractStateChanged(_tentInteractStateChangedSignal.state);
    }

    private void OffroadInteractStateChanged(OffroadInteractStateChangedSignal _offroadInteractStateChangedSignal)
    {
        unitUI.OffroadInteractStateChanged(_offroadInteractStateChangedSignal.state);
    }

    private void RepairBoxInteractStateChanged(RepairBoxInteractStateChangedSignal _repairBoxInteractStateChangedSignal)
    {
        unitUI.RepairBoxInteractStateChanged(_repairBoxInteractStateChangedSignal.state);
    }

    private void ShopInteractStateChanged(ShopInteractStateChangedSignal _shopInteractStateChangedSignal)
    {
        unitUI.ShopInteractStateChanged(_shopInteractStateChangedSignal.state);
    }

    private void LogItemProcessorIsActive(LogItemProcessorActiveStateSignal _logItemProcessorActiveStateSignal)
    {
        worldPopupUI.LogItemProcessorActiveStateChange(_logItemProcessorActiveStateSignal.state);
    }

    private void InventoryUIOpened(bool _boolean)
    {
        bInventoryOpened = _boolean;

        if (_boolean == true)
        {
            popUpUI.Show();
        }
        else
        {
            popUpUI.Hide();
        }
    }

    private void ItemCantAcquired_Inventory(ItemCantAcquiedSignal _itemCantAcquiedSignal)
    {
        unitUI.ItemCantAcquired_Inventory();
    }

    private void DeclareSkillAccumulativeValue(DeclareSkillAccumulatedValueSignal _declareSkillAccumulativeValueSignal)
    {

    }

    private void StartSkyProduction(StartSkyProductionSignal _startSkyProductionSignal)
    {
        skyProduction.SetMainMenuMode(_startSkyProductionSignal.isMainMenuRelated);
        skyProduction.StartSkyProduction();
    }

    private void RollbackSkyProduction(RollbackSkyProductionSignal _rollbackSkyProductionSignal)
    {
        skyProduction.StartSkyProduction();
    }

    private void PopupUIDown(PopupUIDownSignal _popupUIDownSignal)
    {
        hudUI.HUDGoDown();
        popUpUI.PopupGoDown();
        worldPopupUI.WorldPopupGoDown();
    }

    private void PopupUIUp(PopupUIUpSignal _popupUIUpSignal)
    {
        hudUI.HUDGoUp();
        popUpUI.PopupGoUp();
        worldPopupUI.WorldPopupGoUp();
    }

    private void ProvideAccumulatedValueChangeEvent(ProvideSkillAccumulatedValueChangeSignal _signal)
    {
        tentUI.SkillAccumulatedValuePreviewProvided(_signal.data);
    }

    private void GameEnd(GameEndSignal _gameEndSignal)
    {
        resultUI.OpenResultUI();
    }

    private void RetryGame()
    {
        signalHub.Publish(new RetryButtonClickedSignal());
    }

    private void ActivateWarningUI(ActivateWarningUISignal _activateWarningUISignal)
    {
        warningUI.Show();
    }

    private void DeActivateWarningUI()
    {
        if (warningUI.IsVisible == true)
        {
            warningUI.Hide();
            return;
        }

        signalHub.Publish(new WarningUIClosedSignal(warningUI.bApproved));
    }

    private void InventoryItemToOffroadContainer(InventoryItemTransferToOffroadContainerSignal _inventoryItemToOffroadContainerSignal)
    {
        unitUI.InventoryItemToOffroadContainer();
    }

    private void DeclareDungeonState(DeclareDungeonStateSignal _declareDungeonStateSignal)
    {
        hudUI.DungeonStateDeclared(mapType, forestType, _declareDungeonStateSignal.dungeonState);
    }
}
