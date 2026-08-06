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
    private UIView_OverUIPopup overUIPopupUI;

    private UIDepthController uiDepthController;

    private bool bInventoryOpened = false;
    private bool bIsTutorialQuestHiding = false;
    private bool bPendingGameEnd = false;

    // MainMenu → Dungeon 튜토리얼: 로고 연출이 끝난 뒤 처음으로 HUD가 다 올라오는 시점에만
    // 인트로 종료를 알리기 위한 예약 플래그(일반 던전/타운 전환의 HUD 복귀와 구분한다).
    private bool bWaitingIntroProductionEnd = false;

    // 튜토리얼 퀘스트 체인이 진행 중인 동안(첫 스텝 시작 ~ 마지막 스텝(UpgradeAxe) 완료)만 true.
    // ResultUI가 튜토리얼 중 Retry를 막는 등 자체 판단을 하도록 SetTutorialState()로 넘겨준다.
    private bool bIsTutorialActive = false;

    private MapType mapType;
    private ForestType forestType;

    public void Initialize(SignalHub _signalHub, InputManager _inputManager, UIView_Popup _popUpUI, UIView_HUD _hudUI,
     UIView_Unit _unitUI, UIView_WorldPopup _worldPopupUI, UIView_MenuPopup _menuPopupUI, UIView_Tent _tentUI, UIView_ESC _escUI,
     UIDepthController _uiDepthController, UIView_SkyProduction _skyProduction, UIView_Result _resultUI, UIView_Warning _warningUI,
     UIView_OverUIPopup _overUIPopupUI)
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
        overUIPopupUI = _overUIPopupUI;

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
        signalHub.Subscribe<StudioLogoRevealSignal>(StudioLogoReveal);
        signalHub.Subscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.Subscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
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
        signalHub.UnSubscribe<StudioLogoRevealSignal>(StudioLogoReveal);
        signalHub.UnSubscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.UnSubscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
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

        escUI.ResumeButtonClickedEvent -= ResumeGame;
        escUI.ResumeButtonClickedEvent += ResumeGame;

        escUI.SaveGameButtonClickedEvent -= SaveGame;
        escUI.SaveGameButtonClickedEvent += SaveGame;

        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.GoToMainMenuButtonClickedEvent += GoToMainMenu;

        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.ExitButtonClickedEvent += ExitGame;

        escUI.UIInputLockChangedEvent -= ESCUIInputLockChanged;
        escUI.UIInputLockChangedEvent += ESCUIInputLockChanged;

        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        menuPopupUI.TeleportUIClosedEvent += TeleportUIClosed;

        menuPopupUI.UnlockProductionStartedEvent -= MenuPopupUnlockProductionStarted;
        menuPopupUI.UnlockProductionStartedEvent += MenuPopupUnlockProductionStarted;

        menuPopupUI.UnlockProductionEndedEvent -= MenuPopupUnlockProductionEnded;
        menuPopupUI.UnlockProductionEndedEvent += MenuPopupUnlockProductionEnded;

        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        popUpUI.InventoryUIOpendEvent += InventoryUIOpened;

        resultUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        resultUI.GoHomeButtonClickedEvent += GoHomeButtonClicked;

        resultUI.RetryButtonClickedEvent -= RetryGame;
        resultUI.RetryButtonClickedEvent += RetryGame;

        warningUI.DeActivateWarningUIEvent -= DeActivateWarningUI;
        warningUI.DeActivateWarningUIEvent += DeActivateWarningUI;

        overUIPopupUI.CompanyLogoProductionCompletedEvent -= CompanyLogoProductionCompleted;
        overUIPopupUI.CompanyLogoProductionCompletedEvent += CompanyLogoProductionCompleted;

        overUIPopupUI.TutorialQuestHideCompletedEvent -= TutorialQuestHideCompleted;
        overUIPopupUI.TutorialQuestHideCompletedEvent += TutorialQuestHideCompleted;

        overUIPopupUI.TutorialQuestTransitionCompletedEvent -= TutorialQuestTransitionCompleted;
        overUIPopupUI.TutorialQuestTransitionCompletedEvent += TutorialQuestTransitionCompleted;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InventoryKeyEvent -= OnInventoryKeyPressed;
        popUpUI.sendDeleteItemEvent -= SendDeleteItem;
        menuPopupUI.DungeonSelectedEvent -= DungeonSelected;
        menuPopupUI.CancelButtonClickedEvent -= CancelMenuPopup;
        inputManager.inputReader.ESCButtonPressedEvent -= EscButtonPressed;
        escUI.ResumeButtonClickedEvent -= ResumeGame;
        escUI.ExitButtonClickedEvent -= ExitGame;
        escUI.GoToMainMenuButtonClickedEvent -= GoToMainMenu;
        escUI.SaveGameButtonClickedEvent -= SaveGame;
        escUI.UIInputLockChangedEvent -= ESCUIInputLockChanged;
        menuPopupUI.TeleportUIClosedEvent -= TeleportUIClosed;
        menuPopupUI.UnlockProductionStartedEvent -= MenuPopupUnlockProductionStarted;
        menuPopupUI.UnlockProductionEndedEvent -= MenuPopupUnlockProductionEnded;
        popUpUI.InventoryUIOpendEvent -= InventoryUIOpened;
        resultUI.GoHomeButtonClickedEvent -= GoHomeButtonClicked;
        resultUI.RetryButtonClickedEvent -= RetryGame;
        warningUI.DeActivateWarningUIEvent -= DeActivateWarningUI;
        overUIPopupUI.CompanyLogoProductionCompletedEvent -= CompanyLogoProductionCompleted;
        overUIPopupUI.TutorialQuestHideCompletedEvent -= TutorialQuestHideCompleted;
        overUIPopupUI.TutorialQuestTransitionCompletedEvent -= TutorialQuestTransitionCompleted;
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

    private void ResumeGame()
    {
        // escUI.OnResumeButtonClicked()가 Hide()를 이미 호출한 뒤 이 이벤트를 발행하므로
        // 여기서는 EscButtonPressed의 ESC 키 종료 경로와 동일하게 이동/시간만 복구한다.
        inputManager.PauseMove(false);
        Time.timeScale = 1f;
    }

    private void GoToMainMenu()
    {
        // 카메라 상승 연출이 재생되는 동안 중복 클릭으로 재진입하지 못하도록 즉시 닫는다.
        escUI.Hide();
        inputManager.PauseMove(false);

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
        if (null != uiDepthController && uiDepthController.TryCloseTopView())
        {
            return;
        }

        if (null != escUI && true == escUI.IsVisible)
        {
            if (true == escUI.IsOptionOpen)
            {
                escUI.CloseOption();
                return;
            }

            escUI.Hide();
            inputManager.PauseMove(false);
            Time.timeScale = 1f;
        }
        else if (null != escUI)
        {
            escUI.Show();
            inputManager.PauseMove(true);
            Time.timeScale = 0f;
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
        bIsTutorialQuestHiding = false;
        bPendingGameEnd = false;
        popUpUI.Hide();
    }

    private void DungeonStarted(DecalreDungeonTypeSignal decareDungeonTypeSignal)
    {
        unitUI.DungeonStarted();

        // MainMenu → Dungeon 직행 등, DungeonSelected(MapType,ForestType) UI 콜백을 거치지 않고 던전에
        // 들어온 경우에도 DeclareDungeonState()가 올바른 mapType/forestType을 참조하도록 여기서 동기화한다.
        mapType = decareDungeonTypeSignal.mapType;
        forestType = decareDungeonTypeSignal.forestType;

        hudUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        popUpUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);
        worldPopupUI.SetCurrentMapType(decareDungeonTypeSignal.mapType, decareDungeonTypeSignal.forestType);

        resultUI.DungeonStarted();

        bInventoryOpened = false;
        bIsTutorialQuestHiding = false;
        bPendingGameEnd = false;
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

    // 지역/서브지역 해금 연출(popupNavMain.OnUnlockProductionStarted/Ended) 재생 중에는 상호작용 키를
    // 잠가서, 그 사이 상호작용 키가 다시 눌려 OffroadVehicleObj의 토글 의도(bUIActivated)가 실제 UI
    // 상태와 어긋나는 것을 막는다.
    // 주의: 이건 해금 연출 구간만 방어한다. 기본 등장/퇴장 트윈(매번 여닫을 때마다 재생)까지 막으려면
    // UIView_MenuPopup/HUD_PopupNav_Main 쪽에 더 넓은 범위의 이벤트가 필요한데, 그 시스템은 건드리지
    // 않기로 했으므로 이번 방어는 해금 연출이 겹치는 케이스로 한정된다.
    private void MenuPopupUnlockProductionStarted()
    {
        inputManager.PauseInteractKey(true);
    }

    private void MenuPopupUnlockProductionEnded()
    {
        inputManager.PauseInteractKey(false);
    }

    private void ESCUIInputLockChanged(bool _isLocked)
    {
        inputManager.PauseESCKey(_isLocked);
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
        popUpUI.ItemCantAcquired_Inventory();
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
        // bWaitingIntroProductionEnd는 MainMenu → Dungeon 튜토리얼의 스튜디오 로고 연출이 끝난 뒤부터
        // 이번 HUDGoUp의 완료 콜백(HUDGoUpCompleted)에서 꺼질 때까지만 true이므로, 튜토리얼 최초 HUD
        // 노출인지 여기서 정확히 판별할 수 있다. 이 진입에서는 던전 상태 배너를 띄우지 않는다.
        hudUI.HUDGoUp(HUDGoUpCompleted, bWaitingIntroProductionEnd);
        popUpUI.PopupGoUp();
        worldPopupUI.WorldPopupGoUp();
    }

    // HUD가 완전히 다 올라온 시점. 튜토리얼 인트로 중이었다면 그 종료를 UI에 알린다.
    private void HUDGoUpCompleted()
    {
        if (bWaitingIntroProductionEnd == false)
            return;

        bWaitingIntroProductionEnd = false;
        overUIPopupUI.IntroProductionEnded();

        // 튜토리얼 로직(TutorialSystem)이 첫 스텝을 시작할 수 있도록 같은 시점에 알린다.
        signalHub.Publish(new TutorialIntroEndedSignal());
    }

    private void TutorialStepStarted(TutorialStepStartedSignal _signal)
    {
        bIsTutorialActive = true;

        overUIPopupUI.TutorialStepStarted(_signal.step);
    }

    private void TutorialStepCompleted(TutorialStepCompletedSignal _signal)
    {
        overUIPopupUI.TutorialStepCompleted(_signal.step);

        if (_signal.step == TutorialStep.GoHomeBeforeExhausted || _signal.step == TutorialStep.FillOffroadContainer)
        {
            bIsTutorialQuestHiding = true;
        }

        // 튜토리얼 마지막 스텝(StartNewLogging)이 끝나면 이후 결과창은 더 이상 튜토리얼 상태로 취급하지 않는다.
        if (_signal.step == TutorialStep.StartNewLogging)
        {
            bIsTutorialActive = false;
        }
    }

    private void TutorialQuestHideCompleted(TutorialStep _step)
    {
        bIsTutorialQuestHiding = false;

        if (_step == TutorialStep.GoHomeBeforeExhausted)
        {
            if (bPendingGameEnd)
            {
                bPendingGameEnd = false;
                resultUI.SetTutorialState(bIsTutorialActive);
                resultUI.OpenResultUI();
            }
        }
        else if (_step == TutorialStep.FillOffroadContainer || _step == TutorialStep.UpgradeAxe)
        {
            // FillOffroadContainer는 InDungeonSystem이 이 신호로 차량 상호작용 잠금을 풀고,
            // UpgradeAxe는 TutorialSystem이 이 신호로 다음(마지막) 스텝인 StartNewLogging을 시작한다.
            // 두 경우 모두 "완료 연출(안내 UI가 사라지는 애니메이션)이 실제로 끝난 뒤"에 다음 로직이
            // 이어져야 하므로 스텝 완료 시점이 아니라 이 콜백에서 발행한다.
            signalHub.Publish(new TutorialQuestHideCompletedSignal(_step));
        }
    }

    private void TutorialQuestTransitionCompleted(TutorialStep _step)
    {
        if (_step == TutorialStep.FillOffroadContainer)
        {
            signalHub.Publish(new TutorialQuestTransitionCompletedSignal(_step));
        }
    }

    private void ProvideAccumulatedValueChangeEvent(ProvideSkillAccumulatedValueChangeSignal _signal)
    {
        tentUI.SkillAccumulatedValuePreviewProvided(_signal.data);
    }

    private void GameEnd(GameEndSignal _gameEndSignal)
    {
        if (bIsTutorialQuestHiding)
        {
            bPendingGameEnd = true;
        }
        else
        {
            resultUI.SetTutorialState(bIsTutorialActive);
            resultUI.OpenResultUI();
        }
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

    private void StudioLogoReveal(StudioLogoRevealSignal _studioLogoRevealSignal)
    {
        overUIPopupUI.PlayCompanyLogo();
    }

    // 스튜디오 로고 UI 연출이 끝난 시점 - 이후 캐릭터 하차 연출(InDungeonSystem)이 이 신호를 받는다.
    // 하차 뒤 HUD가 다 올라오면 인트로 전체가 끝난 것이므로, 그때까지 종료 통보를 예약해둔다.
    private void CompanyLogoProductionCompleted()
    {
        bWaitingIntroProductionEnd = true;

        signalHub.Publish(new CompanyLogoProductionCompletedSignal());
    }
}
