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
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<InventoryUpdatedSignal>(InventoryUpdated);
        signalHub.UnSubscribe<TreeGetHitSignal>(TreeGetHit);
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
    }

    private void BindEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        inputManager.inputReader.InventoryKeyEvent += OnInventoryKeyPressed;

        popUpUI.goHomeButtonClickedEvent -= GoHomeButtonClicked;
        popUpUI.goHomeButtonClickedEvent += GoHomeButtonClicked;

        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        popUpUI.sendDeleteItemEvent += SendDeleteItem;

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

        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        menuPopupUI.TeleportUIClosedEvent += TeleportUIClosed;

        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        popUpUI.InventoryUIOpendEvent += InventoryUIOpened;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        popUpUI.goHomeButtonClickedEvent -= GoHomeButtonClicked;
        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.SaveGameButtonClickedEvent -= SaveGame;
        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
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
        tentUI.TentInteract(tentInteractSignal.bInteract);
    }

    private void PortalActivated(PortalActivatedSignal _portalActivatedSignal)
    {
        menuPopupUI.TeleportUIOpen();
    }

    private void PortalDeActivated(PortalDeActivatedSignal _portalDeActivatedSignal)
    {
        menuPopupUI.CloseTeleportUI();
    }

    private void DungeonSelected(MapType _type, ForestType _forestType)
    {
        signalHub.Publish(new DungeonSelectedSignal(_type, _forestType));
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
        unitUI.Refresh();
        hudUI.SetCurrentMapType(MapType.Town, ForestType.InTown);
        popUpUI.SetCurrentMapType(MapType.Town, ForestType.InTown);

        bInventoryOpened = false;
        popUpUI.Hide();
    }

    private void DungeonStarted(DecalreDungeonTypeSignal decareDungeonTypeSignal)
    {
        hudUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        popUpUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);

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
}
